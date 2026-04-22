// Package fontpkg loads the Kreon font from the Slay the Spire 2 PCK for
// text rendering.
package fontpkg

import (
	"bytes"
	"fmt"
	"strings"
	"sync"

	"golang.org/x/image/font"
	"golang.org/x/image/font/opentype"

	"github.com/jphastings/slay-the-spire-ii-atproto/extract/fontdata"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/pck"
)

// Faces holds the parsed Kreon regular + bold OpenType fonts plus a cache
// of already-sized faces.
type Faces struct {
	Regular, Bold *opentype.Font

	mu    sync.Mutex
	cache map[faceKey]font.Face
}

type faceKey struct {
	size float64
	bold bool
}

// LoadKreon finds the two Kreon .fontdata entries in the pack, extracts
// the TTFs, and parses them.
func LoadKreon(p *pck.Pack) (*Faces, error) {
	reg, err := loadOne(p, "kreon_regular")
	if err != nil {
		return nil, err
	}
	bold, err := loadOne(p, "kreon_bold")
	if err != nil {
		return nil, err
	}
	return &Faces{Regular: reg, Bold: bold, cache: map[faceKey]font.Face{}}, nil
}

// Face returns a font.Face at the given size (px), using Regular or Bold.
func (f *Faces) Face(sizePx float64, bold bool) (font.Face, error) {
	f.mu.Lock()
	defer f.mu.Unlock()
	k := faceKey{size: sizePx, bold: bold}
	if face, ok := f.cache[k]; ok {
		return face, nil
	}
	src := f.Regular
	if bold {
		src = f.Bold
	}
	face, err := opentype.NewFace(src, &opentype.FaceOptions{
		Size:    sizePx,
		DPI:     72, // so Size is in pixels
		Hinting: font.HintingFull,
	})
	if err != nil {
		return nil, err
	}
	f.cache[k] = face
	return face, nil
}

// loadOne finds the .fontdata entry whose path contains `stem`, unwraps
// FileAccessCompressed if needed, and extracts the embedded TTF/OTF.
//
// FontFile's binary layout doesn't expose where the embedded font sits, so
// we scan for every sfnt magic and try-parse each: the first that the
// opentype parser accepts is the real font. False positives are common —
// 0x00010000 is a very plausible 32-bit integer.
func loadOne(p *pck.Pack, stem string) (*opentype.Font, error) {
	for _, e := range p.Files {
		if !strings.HasSuffix(e.Path, ".fontdata") {
			continue
		}
		if !strings.Contains(e.Path, stem+".ttf") {
			continue
		}
		raw, err := p.Read(e.Path)
		if err != nil {
			return nil, fmt.Errorf("read %s: %w", e.Path, err)
		}
		unwrapped, err := fontdata.Unwrap(raw)
		if err != nil {
			return nil, fmt.Errorf("unwrap %s: %w", e.Path, err)
		}
		parsed, err := findParseableFont(unwrapped)
		if err != nil {
			return nil, fmt.Errorf("scan %s: %w", e.Path, err)
		}
		return parsed, nil
	}
	return nil, fmt.Errorf("fontpkg: no .fontdata matching %q in pack", stem)
}

var sfntMagics = [][]byte{
	{0x00, 0x01, 0x00, 0x00}, // TrueType
	{'O', 'T', 'T', 'O'},     // CFF/OpenType
	{'t', 'r', 'u', 'e'},     // Apple TrueType
}

// findParseableFont scans data for any sfnt magic and returns the first
// candidate that opentype.Parse accepts.
func findParseableFont(data []byte) (*opentype.Font, error) {
	for _, m := range sfntMagics {
		for start := 0; ; {
			idx := bytes.Index(data[start:], m)
			if idx < 0 {
				break
			}
			off := start + idx
			if f, err := opentype.Parse(data[off:]); err == nil {
				return f, nil
			}
			start = off + 1
		}
	}
	return nil, fmt.Errorf("no parseable sfnt font found in %d bytes", len(data))
}
