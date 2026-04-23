package cards

import (
	"encoding/json"
	"fmt"
	"image"
	"math"
	"os"
	"regexp"
	"strconv"

	"github.com/jphastings/slay-the-spire-ii-atproto/extract/pck"
)

// Frame color material paths, one per character-ish frame color.
// Names match shaders/hsv.gdshader parameters in the game's
// materials/cards/frames/*.tres files.
var frameColors = []string{
	"red", "blue", "green", "orange", "pink",
	"colorless", "curse", "quest",
}

// Banner rarity material names.
var bannerRarities = []string{
	"common", "uncommon", "rare",
	"curse", "status", "event", "quest", "ancient",
}

// TintsManifest is the JSON the web client reads to drive its CSS
// filters. Each entry mirrors one shader_parameter/{h,s,v} block from
// the game's ShaderMaterial .tres. The web converts these to
// `filter: hue-rotate(...) saturate(...) brightness(...)` at render
// time — see web/src/lib/utils/tints.ts.
type TintsManifest struct {
	FrameColors map[string]HSVParams `json:"frameColors"`
	Rarities    map[string]HSVParams `json:"rarities"`
	Enchant     HSVParams            `json:"enchant"`
}

func writeTints(p *pck.Pack, outPath string) error {
	out := TintsManifest{
		FrameColors: map[string]HSVParams{},
		Rarities:    map[string]HSVParams{},
		// Enchantment tab tint is hard-coded in scenes/cards/card.tscn —
		// keep it in sync with the value Extract() applies via tintImage.
		Enchant: HSVParams{H: 0.25, S: 0.4, V: 1.0},
	}
	for _, col := range frameColors {
		params, err := readHSVMaterial(p, fmt.Sprintf("materials/cards/frames/card_frame_%s_mat.tres", col))
		if err != nil {
			return fmt.Errorf("read frame %s: %w", col, err)
		}
		out.FrameColors[col] = params
	}
	for _, r := range bannerRarities {
		params, err := readBannerMaterial(p, r)
		if err != nil {
			return err
		}
		out.Rarities[r] = params
	}
	buf, err := json.MarshalIndent(out, "", "  ")
	if err != nil {
		return err
	}
	return os.WriteFile(outPath, buf, 0o644)
}

func readBannerMaterial(p *pck.Pack, rarity string) (HSVParams, error) {
	matPath := fmt.Sprintf("materials/cards/banners/card_banner_%s_mat.tres", rarity)
	params, err := readHSVMaterial(p, matPath)
	if err != nil {
		return HSVParams{}, fmt.Errorf("read %s: %w", matPath, err)
	}
	return params, nil
}

// HSVParams holds the three shader_parameter/{h,s,v} values from a
// ShaderMaterial .tres.
type HSVParams struct {
	H float64 `json:"h"`
	S float64 `json:"s"`
	V float64 `json:"v"`
}

var (
	hsvParamRE = regexp.MustCompile(`shader_parameter/([hsv])\s*=\s*(-?[\d.]+)`)
)

// readHSVMaterial parses shader_parameter/{h,s,v} from a .tres file.
func readHSVMaterial(p *pck.Pack, path string) (HSVParams, error) {
	data, err := p.Read(path)
	if err != nil {
		return HSVParams{}, err
	}
	out := HSVParams{H: 0, S: 1, V: 1}
	for _, m := range hsvParamRE.FindAllStringSubmatch(string(data), -1) {
		v, err := strconv.ParseFloat(m[2], 64)
		if err != nil {
			continue
		}
		switch m[1] {
		case "h":
			out.H = v
		case "s":
			out.S = v
		case "v":
			out.V = v
		}
	}
	return out, nil
}

// tintImage applies the shaders/hsv.gdshader algorithm to every pixel of
// src and returns a new RGBA image. Alpha is preserved verbatim. Used by
// Extract() to bake the fixed enchant-tab tint (the only frame asset
// whose HSV params don't vary at runtime).
func tintImage(src *image.RGBA, p HSVParams) *image.RGBA {
	b := src.Bounds()
	out := image.NewRGBA(b)
	hue := (1.0 - p.H) * 2.0 * math.Pi
	cosH := math.Cos(hue)
	sinH := math.Sin(hue)
	for y := b.Min.Y; y < b.Max.Y; y++ {
		for x := b.Min.X; x < b.Max.X; x++ {
			i := src.PixOffset(x, y)
			r := float64(src.Pix[i+0]) / 255
			g := float64(src.Pix[i+1]) / 255
			bl := float64(src.Pix[i+2]) / 255
			a := src.Pix[i+3]

			// RGB -> YIQ.
			Y := yiqFwd[0][0]*r + yiqFwd[0][1]*g + yiqFwd[0][2]*bl
			I := yiqFwd[1][0]*r + yiqFwd[1][1]*g + yiqFwd[1][2]*bl
			Q := yiqFwd[2][0]*r + yiqFwd[2][1]*g + yiqFwd[2][2]*bl

			// Hue rotation in IQ plane.
			Ih := I*cosH - Q*sinH
			Qh := I*sinH + Q*cosH

			// Saturation and value.
			Ih *= p.S
			Qh *= p.S
			Y *= p.V
			Ih *= p.V
			Qh *= p.V

			// YIQ -> RGB via the exact inverse of yiqFwd.
			rO := yiqInv[0][0]*Y + yiqInv[0][1]*Ih + yiqInv[0][2]*Qh
			gO := yiqInv[1][0]*Y + yiqInv[1][1]*Ih + yiqInv[1][2]*Qh
			bO := yiqInv[2][0]*Y + yiqInv[2][1]*Ih + yiqInv[2][2]*Qh

			out.Pix[i+0] = clampByte(rO)
			out.Pix[i+1] = clampByte(gO)
			out.Pix[i+2] = clampByte(bO)
			out.Pix[i+3] = a
		}
	}
	return out
}

// yiqFwd is the exact forward RGB→YIQ matrix used in the game's
// shaders/hsv.gdshader (stored row-major for readability).
var yiqFwd = [3][3]float64{
	{0.2989, 0.5870, 0.1140},
	{0.5959, -0.2774, -0.3216},
	{0.2115, -0.5229, 0.3114},
}

// yiqInv is the exact inverse of yiqFwd.
var yiqInv = invert3x3(yiqFwd)

func invert3x3(m [3][3]float64) [3][3]float64 {
	det := m[0][0]*(m[1][1]*m[2][2]-m[1][2]*m[2][1]) -
		m[0][1]*(m[1][0]*m[2][2]-m[1][2]*m[2][0]) +
		m[0][2]*(m[1][0]*m[2][1]-m[1][1]*m[2][0])
	inv := [3][3]float64{}
	inv[0][0] = (m[1][1]*m[2][2] - m[1][2]*m[2][1]) / det
	inv[0][1] = (m[0][2]*m[2][1] - m[0][1]*m[2][2]) / det
	inv[0][2] = (m[0][1]*m[1][2] - m[0][2]*m[1][1]) / det
	inv[1][0] = (m[1][2]*m[2][0] - m[1][0]*m[2][2]) / det
	inv[1][1] = (m[0][0]*m[2][2] - m[0][2]*m[2][0]) / det
	inv[1][2] = (m[0][2]*m[1][0] - m[0][0]*m[1][2]) / det
	inv[2][0] = (m[1][0]*m[2][1] - m[1][1]*m[2][0]) / det
	inv[2][1] = (m[0][1]*m[2][0] - m[0][0]*m[2][1]) / det
	inv[2][2] = (m[0][0]*m[1][1] - m[0][1]*m[1][0]) / det
	return inv
}

func clampByte(f float64) uint8 {
	if f <= 0 {
		return 0
	}
	if f >= 1 {
		return 255
	}
	return uint8(math.Round(f * 255))
}
