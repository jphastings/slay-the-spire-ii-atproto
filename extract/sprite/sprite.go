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
// are tileW×tileH pixels so the layout is fully determined by Columns,
// TileW, TileH.
type Manifest struct {
	Image   string   `json:"image"`
	TileW   int      `json:"tileW"`
	TileH   int      `json:"tileH"`
	Columns int      `json:"columns"`
	Items   []string `json:"items"` // id at grid position i
}

// Build reads every *.png in srcDir, scales each to tileW×tileH with
// Catmull-Rom (good quality for UI icons), packs them into a grid, and
// writes a composite PNG + JSON index. The grid has ceil(√n) columns by
// default so the sheet stays roughly tile-square.
func Build(srcDir, outPNG, outJSON string, tileW, tileH int) (int, error) {
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
	sheet := image.NewRGBA(image.Rect(0, 0, cols*tileW, rows*tileH))

	ids := make([]string, n)
	for i, name := range names {
		src, err := readPNG(filepath.Join(srcDir, name))
		if err != nil {
			return 0, fmt.Errorf("sprite %s: %w", name, err)
		}
		x := (i % cols) * tileW
		y := (i / cols) * tileH
		dst := image.Rect(x, y, x+tileW, y+tileH)
		xdraw.CatmullRom.Scale(sheet, dst, src, src.Bounds(), xdraw.Over, nil)
		ids[i] = strings.TrimSuffix(name, ".png")
	}

	if err := writePNG(outPNG, sheet); err != nil {
		return 0, err
	}

	manifest := Manifest{
		Image:   filepath.Base(outPNG),
		TileW:   tileW,
		TileH:   tileH,
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

// BuildUniform packs square tiles. Convenience wrapper around Build.
func BuildUniform(srcDir, outPNG, outJSON string, tile int) (int, error) {
	return Build(srcDir, outPNG, outJSON, tile, tile)
}

// PackedItem records one image's bounds inside a packed sheet.
type PackedItem struct {
	X int `json:"x"`
	Y int `json:"y"`
	W int `json:"w"`
	H int `json:"h"`
}

// PackedManifest mirrors the JSON emitted alongside a packed sprite PNG.
// Unlike the uniform Manifest, each item carries its own pixel rect so
// callers can mix differently-sized images in one sheet.
type PackedManifest struct {
	Image  string                `json:"image"`
	Width  int                   `json:"width"`
	Height int                   `json:"height"`
	Items  map[string]PackedItem `json:"items"`
}

// BuildPacked reads every *.png in srcDir, packs them at native size
// with a simple shelf packer (sort by decreasing height, wrap to next
// row when the current row overflows maxWidth), and writes a composite
// PNG + JSON manifest. Each item keeps its source resolution — useful
// when tiles in the same sheet have different sizes or aspect ratios.
func BuildPacked(srcDir, outPNG, outJSON string, maxWidth int) (int, error) {
	entries, err := os.ReadDir(srcDir)
	if err != nil {
		return 0, err
	}
	type loaded struct {
		id  string
		img image.Image
	}
	var imgs []loaded
	for _, e := range entries {
		if e.IsDir() || !strings.HasSuffix(e.Name(), ".png") {
			continue
		}
		img, err := readPNG(filepath.Join(srcDir, e.Name()))
		if err != nil {
			return 0, fmt.Errorf("sprite %s: %w", e.Name(), err)
		}
		imgs = append(imgs, loaded{strings.TrimSuffix(e.Name(), ".png"), img})
	}
	if len(imgs) == 0 {
		return 0, fmt.Errorf("sprite: no png files in %s", srcDir)
	}
	// Tallest first gives a reasonably tight shelf pack.
	sort.Slice(imgs, func(i, j int) bool {
		return imgs[i].img.Bounds().Dy() > imgs[j].img.Bounds().Dy()
	})

	items := make(map[string]PackedItem, len(imgs))
	x, y, rowH, sheetW := 0, 0, 0, 0
	for _, li := range imgs {
		b := li.img.Bounds()
		w, h := b.Dx(), b.Dy()
		if x+w > maxWidth && x > 0 {
			y += rowH
			x, rowH = 0, 0
		}
		items[li.id] = PackedItem{X: x, Y: y, W: w, H: h}
		if h > rowH {
			rowH = h
		}
		x += w
		if x > sheetW {
			sheetW = x
		}
	}
	sheetH := y + rowH

	sheet := image.NewRGBA(image.Rect(0, 0, sheetW, sheetH))
	for _, li := range imgs {
		r := items[li.id]
		dst := image.Rect(r.X, r.Y, r.X+r.W, r.Y+r.H)
		xdraw.Copy(sheet, dst.Min, li.img, li.img.Bounds(), xdraw.Over, nil)
	}

	if err := writePNG(outPNG, sheet); err != nil {
		return 0, err
	}
	manifest := PackedManifest{
		Image:  filepath.Base(outPNG),
		Width:  sheetW,
		Height: sheetH,
		Items:  items,
	}
	buf, err := json.MarshalIndent(manifest, "", "  ")
	if err != nil {
		return 0, err
	}
	if err := os.WriteFile(outJSON, buf, 0o644); err != nil {
		return 0, err
	}
	return len(imgs), nil
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
