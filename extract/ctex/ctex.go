// Package ctex decodes Godot 4 CompressedTexture2D files (magic "GST2").
//
// Supports the common case: PNG- or WebP-wrapped payloads holding RGBA8
// pixel data. GPU-compressed payloads (BPTC, S3TC, ETC2, ASTC) are
// signalled with ErrGPUCompressed — they require a block-format decoder
// that this package does not ship.
package ctex

import (
	"bytes"
	"encoding/binary"
	"errors"
	"fmt"
	"image"
	"image/png"
	"io"

	_ "image/png" // register PNG decoder

	"golang.org/x/image/webp"
)

type DataFormat uint32

const (
	DataFormatImage          DataFormat = 0
	DataFormatPNG            DataFormat = 1
	DataFormatWebP           DataFormat = 2
	DataFormatBasisUniversal DataFormat = 3
)

// Image::Format constants (only the ones we reference here). Full list in
// godot's core/io/image.h.
const (
	FormatRGB8     = 4
	FormatRGBA8    = 5
	FormatDXT1     = 17
	FormatDXT3     = 18
	FormatDXT5     = 19
	FormatBPTCRGBA = 22 // BC7
	FormatBPTCRGBF = 23 // BC6H signed
	FormatBPTCRGBU = 24 // BC6H unsigned
)

// isGPUCompressed reports whether a Godot Image::Format is a block-compressed
// GPU format we can't decode after WebP unwrapping.
func isGPUCompressed(f uint32) bool {
	switch f {
	case FormatRGB8, FormatRGBA8:
		return false
	}
	return true
}

const (
	magic      = "GST2"
	headerSize = 36 // magic(4) + version(4) + tw(4) + th(4) + flags(4) + mipmap_limit(4) + reserved(12)
)

// ErrGPUCompressed means the .ctex wraps GPU-format block-compressed pixel
// data (BC7, DXT, etc.) and can't be decoded by this package alone.
var ErrGPUCompressed = errors.New("ctex: payload is GPU block-compressed (BPTC/S3TC/ETC2/ASTC)")

// Decode parses a .ctex payload and returns the decoded main (mip 0) image.
// Mipmaps, if present, are ignored.
func Decode(buf []byte) (image.Image, error) {
	if len(buf) < headerSize+20 || string(buf[0:4]) != magic {
		return nil, fmt.Errorf("not a GST2 file")
	}
	r := bytes.NewReader(buf[headerSize:])

	var dataFormat uint32
	if err := binary.Read(r, binary.LittleEndian, &dataFormat); err != nil {
		return nil, err
	}

	switch DataFormat(dataFormat) {
	case DataFormatPNG, DataFormatWebP:
		// w(u16) h(u16) mipmaps(u32) format(u32)
		var w, h uint16
		var mipmaps, imgFormat uint32
		for _, dst := range []any{&w, &h, &mipmaps, &imgFormat} {
			if err := binary.Read(r, binary.LittleEndian, dst); err != nil {
				return nil, err
			}
		}
		// Reject GPU-compressed payloads — the WebP/PNG decode would
		// succeed but the bytes would still be BPTC/DXT blocks.
		if isGPUCompressed(imgFormat) {
			return nil, fmt.Errorf("%w (Image::Format=%d, data_format=%d)",
				ErrGPUCompressed, imgFormat, dataFormat)
		}
		// First mipmap: size u32, then size bytes.
		var size uint32
		if err := binary.Read(r, binary.LittleEndian, &size); err != nil {
			return nil, err
		}
		data := make([]byte, size)
		if _, err := io.ReadFull(r, data); err != nil {
			return nil, err
		}
		if DataFormat(dataFormat) == DataFormatPNG {
			return png.Decode(bytes.NewReader(data))
		}
		return webp.Decode(bytes.NewReader(data))

	case DataFormatImage:
		return nil, fmt.Errorf("DATA_FORMAT_IMAGE (raw pixel) decoding not implemented")
	case DataFormatBasisUniversal:
		return nil, fmt.Errorf("DATA_FORMAT_BASIS_UNIVERSAL not implemented")
	default:
		return nil, fmt.Errorf("unknown data format %d", dataFormat)
	}
}
