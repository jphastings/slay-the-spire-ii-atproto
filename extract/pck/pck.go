// Package pck parses Godot 4 .pck files.
//
// Supports pack_format_version 2 (directory immediately after header) and 3
// (directory at end of file, pointed to by a u64 at byte 0x20). Encrypted
// packs and per-file-encrypted entries are not supported.
package pck

import (
	"encoding/binary"
	"errors"
	"fmt"
	"io"
	"os"
	"strings"
)

const (
	magic = "GDPC"

	packDirEncrypted  uint32 = 1 << 0
	packFileEncrypted uint32 = 1 << 0
)

// File is one entry in the pack directory.
type File struct {
	Path   string
	Offset int64
	Size   int64
	MD5    [16]byte
	Flags  uint32
}

// Pack is an open .pck file. Callers must Close when done.
type Pack struct {
	f       *os.File
	Version uint32 // pack_format_version
	Files   []File
	byPath  map[string]int
}

// Open parses the pack header and directory.
func Open(path string) (*Pack, error) {
	f, err := os.Open(path)
	if err != nil {
		return nil, err
	}
	p := &Pack{f: f}
	if err := p.parse(); err != nil {
		f.Close()
		return nil, err
	}
	return p, nil
}

func (p *Pack) Close() error { return p.f.Close() }

func (p *Pack) parse() error {
	var hdr [0x28]byte
	if _, err := io.ReadFull(p.f, hdr[:]); err != nil {
		return fmt.Errorf("read header: %w", err)
	}
	if string(hdr[0:4]) != magic {
		return fmt.Errorf("not a Godot pack (bad magic %q)", hdr[0:4])
	}

	p.Version = binary.LittleEndian.Uint32(hdr[4:8])
	// hdr[8:20] = engine major/minor/patch (ignored)
	packFlags := binary.LittleEndian.Uint32(hdr[20:24])
	if packFlags&packDirEncrypted != 0 {
		return errors.New("encrypted pack directory is not supported")
	}
	fileBase := int64(binary.LittleEndian.Uint64(hdr[24:32]))

	var dirOff int64
	var fileCount uint32
	switch p.Version {
	case 2:
		// v2: 16 x u32 reserved then file_count, directory follows inline.
		var reserved [16 * 4]byte
		if _, err := io.ReadFull(p.f, reserved[:]); err != nil {
			return err
		}
		var fc [4]byte
		if _, err := io.ReadFull(p.f, fc[:]); err != nil {
			return err
		}
		fileCount = binary.LittleEndian.Uint32(fc[:])
		// directory starts here (current offset)
		off, _ := p.f.Seek(0, io.SeekCurrent)
		dirOff = off
	case 3:
		// v3: directory offset at 0x20, then padding to file_base.
		dirOff = int64(binary.LittleEndian.Uint64(hdr[32:40]))
		if _, err := p.f.Seek(dirOff, io.SeekStart); err != nil {
			return err
		}
		var fc [4]byte
		if _, err := io.ReadFull(p.f, fc[:]); err != nil {
			return err
		}
		fileCount = binary.LittleEndian.Uint32(fc[:])
	default:
		return fmt.Errorf("unsupported pack_format_version %d", p.Version)
	}
	_ = dirOff

	p.Files = make([]File, 0, fileCount)
	p.byPath = make(map[string]int, fileCount)

	for i := uint32(0); i < fileCount; i++ {
		entry, err := p.readEntry(fileBase)
		if err != nil {
			return fmt.Errorf("entry %d: %w", i, err)
		}
		p.byPath[entry.Path] = len(p.Files)
		p.Files = append(p.Files, entry)
	}
	return nil
}

func (p *Pack) readEntry(fileBase int64) (File, error) {
	var plBuf [4]byte
	if _, err := io.ReadFull(p.f, plBuf[:]); err != nil {
		return File{}, err
	}
	pathLen := binary.LittleEndian.Uint32(plBuf[:])
	if pathLen == 0 || pathLen > 1<<16 {
		return File{}, fmt.Errorf("implausible path length %d", pathLen)
	}

	pathBuf := make([]byte, pathLen)
	if _, err := io.ReadFull(p.f, pathBuf); err != nil {
		return File{}, err
	}
	// Path is NUL-padded to multiple of 4; strip trailing NULs.
	path := strings.TrimRight(string(pathBuf), "\x00")
	path = strings.TrimPrefix(path, "res://")

	var tail [8 + 8 + 16 + 4]byte
	if _, err := io.ReadFull(p.f, tail[:]); err != nil {
		return File{}, err
	}
	offset := int64(binary.LittleEndian.Uint64(tail[0:8])) + fileBase
	size := int64(binary.LittleEndian.Uint64(tail[8:16]))
	var md5 [16]byte
	copy(md5[:], tail[16:32])
	flags := binary.LittleEndian.Uint32(tail[32:36])
	if flags&packFileEncrypted != 0 {
		return File{}, fmt.Errorf("encrypted file %q not supported", path)
	}
	return File{Path: path, Offset: offset, Size: size, MD5: md5, Flags: flags}, nil
}

// Read returns the raw bytes of a file in the pack.
func (p *Pack) Read(path string) ([]byte, error) {
	path = strings.TrimPrefix(path, "res://")
	idx, ok := p.byPath[path]
	if !ok {
		return nil, fmt.Errorf("file %q not in pack", path)
	}
	e := p.Files[idx]
	buf := make([]byte, e.Size)
	if _, err := p.f.ReadAt(buf, e.Offset); err != nil {
		return nil, fmt.Errorf("read %q: %w", path, err)
	}
	return buf, nil
}

// Has reports whether the pack contains path.
func (p *Pack) Has(path string) bool {
	_, ok := p.byPath[strings.TrimPrefix(path, "res://")]
	return ok
}
