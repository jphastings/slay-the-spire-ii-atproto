package cards

import (
	"bytes"
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"golang.org/x/image/font/opentype"

	"github.com/jphastings/slay-the-spire-ii-atproto/extract/fontdata"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/pck"
)

// extractFonts writes Kreon regular + bold TTFs as raw font files the web
// client can load via @font-face. The sfnt scan here mirrors
// fontpkg.findParseableFont so we end up with the same bytes the Go
// compose path already validates.
func extractFonts(p *pck.Pack, fontsDir string) error {
	for _, weight := range []string{"kreon_regular", "kreon_bold"} {
		ttf, err := extractFontBytes(p, weight)
		if err != nil {
			return fmt.Errorf("%s: %w", weight, err)
		}
		out := filepath.Join(fontsDir, strings.ReplaceAll(weight, "_", "-")+".ttf")
		if err := os.MkdirAll(filepath.Dir(out), 0o755); err != nil {
			return err
		}
		if err := os.WriteFile(out, ttf, 0o644); err != nil {
			return err
		}
	}
	return nil
}

// extractFontBytes finds the .fontdata entry for `stem`, unwraps the RSCC
// wrapper, scans for an sfnt magic, and returns the validated font bytes
// from that offset onward.
func extractFontBytes(p *pck.Pack, stem string) ([]byte, error) {
	for _, e := range p.Files {
		if !strings.HasSuffix(e.Path, ".fontdata") {
			continue
		}
		if !strings.Contains(e.Path, stem+".ttf") {
			continue
		}
		raw, err := p.Read(e.Path)
		if err != nil {
			return nil, err
		}
		unwrapped, err := fontdata.Unwrap(raw)
		if err != nil {
			return nil, err
		}
		off, err := findSfntOffset(unwrapped)
		if err != nil {
			return nil, err
		}
		return unwrapped[off:], nil
	}
	return nil, fmt.Errorf("no .fontdata matching %q", stem)
}

var sfntMagics = [][]byte{
	{0x00, 0x01, 0x00, 0x00}, // TrueType
	{'O', 'T', 'T', 'O'},     // OpenType/CFF
	{'t', 'r', 'u', 'e'},     // Apple TrueType
}

// findSfntOffset scans data for the first magic that also parses cleanly
// as an opentype font. Matches fontpkg.findParseableFont's strategy so we
// pick the same byte offset.
func findSfntOffset(data []byte) (int, error) {
	for _, m := range sfntMagics {
		start := 0
		for {
			idx := bytes.Index(data[start:], m)
			if idx < 0 {
				break
			}
			off := start + idx
			if _, err := opentype.Parse(data[off:]); err == nil {
				return off, nil
			}
			start = off + 1
		}
	}
	return 0, fmt.Errorf("no parseable sfnt font in %d bytes", len(data))
}
