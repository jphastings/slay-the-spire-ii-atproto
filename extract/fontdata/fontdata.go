// Package fontdata unwraps Godot 4 .fontdata files.
//
// Godot saves imported fonts as a FontFile binary resource. With
// `compress=true` (the editor default) the resource is wrapped in
// FileAccessCompressed — magic "RSCC", block-based per-block compression.
// This package decompresses that wrapper and returns the raw resource
// bytes; the caller is responsible for locating the TTF/OTF payload
// within those bytes (see fontpkg).
package fontdata

import (
	"bytes"
	"compress/flate"
	"compress/gzip"
	"encoding/binary"
	"errors"
	"fmt"
	"io"

	"github.com/klauspost/compress/zstd"
)

// Godot Compression::Mode values.
const (
	modeFastLZ  uint32 = 0
	modeDeflate uint32 = 1
	modeZSTD    uint32 = 2
	modeGZIP    uint32 = 3
	modeBrotli  uint32 = 4
)

// Unwrap decompresses a FileAccessCompressed (magic "RSCC") blob if needed
// and returns the inner FontFile resource bytes. If the input is not
// RSCC-wrapped it's returned unchanged.
func Unwrap(data []byte) ([]byte, error) {
	if len(data) >= 4 && string(data[0:4]) == "RSCC" {
		return decompressRSCC(data)
	}
	return data, nil
}

// decompressRSCC — Godot's FileAccessCompressed format:
//
//	magic      [4]byte  ("RSCC" or other, set by caller)
//	mode       uint32   (Compression::Mode)
//	block_size uint32
//	read_total uint32   (uncompressed byte count)
//	cblock_size[numBlocks] uint32  (numBlocks = read_total/block_size + 1)
//	concatenated compressed blocks
func decompressRSCC(data []byte) ([]byte, error) {
	if len(data) < 16 {
		return nil, errors.New("RSCC: header truncated")
	}
	mode := binary.LittleEndian.Uint32(data[4:8])
	blockSize := binary.LittleEndian.Uint32(data[8:12])
	readTotal := binary.LittleEndian.Uint32(data[12:16])
	if blockSize == 0 {
		return nil, errors.New("RSCC: block_size is 0")
	}
	numBlocks := int(readTotal/blockSize) + 1
	if len(data) < 16+4*numBlocks {
		return nil, fmt.Errorf("RSCC: truncated block-size table (need %d bytes)",
			16+4*numBlocks)
	}
	sizes := make([]uint32, numBlocks)
	for i := 0; i < numBlocks; i++ {
		sizes[i] = binary.LittleEndian.Uint32(data[16+i*4:])
	}
	payload := data[16+4*numBlocks:]

	out := make([]byte, 0, readTotal)
	pos := 0
	for i := 0; i < numBlocks; i++ {
		csize := int(sizes[i])
		if csize == 0 {
			break
		}
		if pos+csize > len(payload) {
			return nil, fmt.Errorf("RSCC: block %d payload overflow", i)
		}
		chunk, err := decompressBlock(mode, payload[pos:pos+csize])
		if err != nil {
			return nil, fmt.Errorf("block %d: %w", i, err)
		}
		out = append(out, chunk...)
		pos += csize
	}
	if len(out) > int(readTotal) {
		out = out[:readTotal]
	}
	return out, nil
}

func decompressBlock(mode uint32, compressed []byte) ([]byte, error) {
	switch mode {
	case modeZSTD:
		d, err := zstd.NewReader(bytes.NewReader(compressed))
		if err != nil {
			return nil, err
		}
		defer d.Close()
		return io.ReadAll(d)
	case modeDeflate:
		r := flate.NewReader(bytes.NewReader(compressed))
		defer r.Close()
		return io.ReadAll(r)
	case modeGZIP:
		r, err := gzip.NewReader(bytes.NewReader(compressed))
		if err != nil {
			return nil, err
		}
		defer r.Close()
		return io.ReadAll(r)
	case modeFastLZ, modeBrotli:
		return nil, fmt.Errorf("fontdata: compression mode %d not implemented", mode)
	default:
		return nil, fmt.Errorf("fontdata: unknown compression mode %d", mode)
	}
}
