// Package sprite packs a directory of same-purpose PNGs (relic icons,
// potion icons, …) into one PNG + JSON index so the web client can load
// hundreds of assets as a single sprite sheet — keeping the committed
// static asset count below the deploy platform's file limit.
package sprite

import (
	"encoding/json"
	"fmt"
	"image"
	"image/png"
	"math"
	"os"
	"path/filepath"
	"sort"
	"strings"

	xdraw "golang.org/x/image/draw"
)

// Manifest mirrors the JSON we emit alongside the sprite PNG. All tiles
// are the same size (Tile pixels square) so the layout is fully
// determined by Columns + Tile.
type Manifest struct {
	Image   string   `json:"image"`
	Tile    int      `json:"tile"`
	Columns int      `json:"columns"`
	Items   []string `json:"items"` // id at grid position i
}

// BuildUniform reads every *.png in srcDir, scales each to tile×tile with
// Catmull-Rom (good quality for UI icons), packs them into a grid, and
// writes a composite PNG + JSON index. The grid has ceil(√n) columns by
// default so the sheet stays roughly square.
func BuildUniform(srcDir, outPNG, outJSON string, tile int) (int, error) {
	entries, err := os.ReadDir(srcDir)
	if err != nil {
		return 0, err
	}
	var names []string
	for _, e := range entries {
		if e.IsDir() || !strings.HasSuffix(e.Name(), ".png") {
			continue
		}
		names = append(names, e.Name())
	}
	sort.Strings(names)
	n := len(names)
	if n == 0 {
		return 0, fmt.Errorf("sprite: no png files in %s", srcDir)
	}

	cols := int(math.Ceil(math.Sqrt(float64(n))))
	rows := (n + cols - 1) / cols
	sheet := image.NewRGBA(image.Rect(0, 0, cols*tile, rows*tile))

	ids := make([]string, n)
	for i, name := range names {
		src, err := readPNG(filepath.Join(srcDir, name))
		if err != nil {
			return 0, fmt.Errorf("sprite %s: %w", name, err)
		}
		x := (i % cols) * tile
		y := (i / cols) * tile
		dst := image.Rect(x, y, x+tile, y+tile)
		xdraw.CatmullRom.Scale(sheet, dst, src, src.Bounds(), xdraw.Over, nil)
		ids[i] = strings.TrimSuffix(name, ".png")
	}

	if err := writePNG(outPNG, sheet); err != nil {
		return 0, err
	}

	manifest := Manifest{
		Image:   filepath.Base(outPNG),
		Tile:    tile,
		Columns: cols,
		Items:   ids,
	}
	buf, err := json.MarshalIndent(manifest, "", "  ")
	if err != nil {
		return 0, err
	}
	if err := os.WriteFile(outJSON, buf, 0o644); err != nil {
		return 0, err
	}
	return n, nil
}

func readPNG(path string) (image.Image, error) {
	f, err := os.Open(path)
	if err != nil {
		return nil, err
	}
	defer f.Close()
	return png.Decode(f)
}

func writePNG(path string, img image.Image) error {
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return err
	}
	f, err := os.Create(path)
	if err != nil {
		return err
	}
	defer f.Close()
	return png.Encode(f, img)
}
