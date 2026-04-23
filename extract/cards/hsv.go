package cards

import (
	"fmt"
	"image"
	"image/png"
	"math"
	"os"
	"path/filepath"
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

// bakeFrames reads each card_frame_<color>_mat.tres, applies its HSV
// parameters to the three base frame shapes (attack/skill/power), and
// writes out/parts/frame/<type>_<color>.png for every combination. This
// mirrors the in-game runtime where the ShaderMaterial is assigned at
// runtime to the shared card_frame_*_s texture.
func bakeFrames(p *pck.Pack, partsDir string) error {
	bases := map[string]*image.RGBA{}
	for _, t := range []string{"attack", "skill", "power"} {
		img, err := loadPNGFromParts(partsDir, "frame_base", t)
		if err != nil {
			return fmt.Errorf("base frame %s: %w", t, err)
		}
		bases[t] = img
	}

	for _, col := range frameColors {
		matPath := fmt.Sprintf("materials/cards/frames/card_frame_%s_mat.tres", col)
		params, err := readHSVMaterial(p, matPath)
		if err != nil {
			return fmt.Errorf("read %s: %w", matPath, err)
		}
		for cardType, base := range bases {
			out := tintImage(base, params)
			dst := filepath.Join(partsDir, "frame", cardType+"_"+col+".png")
			if err := writePNG(dst, out); err != nil {
				return err
			}
		}
	}
	return nil
}

// bakeBanners applies each banner rarity material to the shared banner
// sprite and writes out/parts/banner/<rarity>.png.
func bakeBanners(p *pck.Pack, partsDir string) error {
	base, err := loadPNGFromParts(partsDir, "banner_base", "shared")
	if err != nil {
		return fmt.Errorf("base banner: %w", err)
	}
	for _, r := range bannerRarities {
		params, err := readBannerMaterial(p, r)
		if err != nil {
			return err
		}
		out := tintImage(base, params)
		dst := filepath.Join(partsDir, "banner", r+".png")
		if err := writePNG(dst, out); err != nil {
			return err
		}
	}
	return nil
}

// bakePortraitBorders applies each banner rarity material to each card-type
// portrait border and writes out/parts/portrait_border/<type>_<rarity>.png.
// The scene (scenes/cards/card.tscn) assigns the banner material — not the
// frame material — to the PortraitBorder node.
func bakePortraitBorders(p *pck.Pack, partsDir string) error {
	bases := map[string]*image.RGBA{}
	for _, t := range []string{"attack", "skill", "power"} {
		img, err := loadPNGFromParts(partsDir, "portrait_border_base", t)
		if err != nil {
			return fmt.Errorf("base border %s: %w", t, err)
		}
		bases[t] = img
	}
	for _, r := range bannerRarities {
		params, err := readBannerMaterial(p, r)
		if err != nil {
			return err
		}
		for cardType, base := range bases {
			out := tintImage(base, params)
			dst := filepath.Join(partsDir, "portrait_border", cardType+"_"+r+".png")
			if err := writePNG(dst, out); err != nil {
				return err
			}
		}
	}
	return nil
}

// bakePlaque applies each banner rarity material to the title-plaque
// (card_portrait_border_plaque2.png) and writes
// out/parts/plaque/<rarity>.png. The plaque is the small "Attack/Skill/
// Power" pill under the portrait; the scene code assigns the card's
// BannerMaterial at runtime (see UpdateTypePlaque in sts2.dll).
func bakePlaque(p *pck.Pack, partsDir string) error {
	path := filepath.Join(partsDir, "plaque_base.png")
	f, err := os.Open(path)
	if err != nil {
		return err
	}
	defer f.Close()
	decoded, err := png.Decode(f)
	if err != nil {
		return err
	}
	b := decoded.Bounds()
	base := image.NewRGBA(image.Rect(0, 0, b.Dx(), b.Dy()))
	for y := 0; y < b.Dy(); y++ {
		for x := 0; x < b.Dx(); x++ {
			base.Set(x, y, decoded.At(b.Min.X+x, b.Min.Y+y))
		}
	}
	for _, r := range bannerRarities {
		params, err := readBannerMaterial(p, r)
		if err != nil {
			return err
		}
		out := tintImage(base, params)
		dst := filepath.Join(partsDir, "plaque", r+".png")
		if err := writePNG(dst, out); err != nil {
			return err
		}
	}
	return nil
}

func readBannerMaterial(p *pck.Pack, rarity string) (HSVParams, error) {
	matPath := fmt.Sprintf("materials/cards/banners/card_banner_%s_mat.tres", rarity)
	params, err := readHSVMaterial(p, matPath)
	if err != nil {
		return HSVParams{}, fmt.Errorf("read %s: %w", matPath, err)
	}
	return params, nil
}

// loadPNGFromParts reads a PNG we wrote earlier during atlasPart extraction.
func loadPNGFromParts(partsDir, kind, variant string) (*image.RGBA, error) {
	path := filepath.Join(partsDir, kind, variant+".png")
	f, err := os.Open(path)
	if err != nil {
		return nil, err
	}
	defer f.Close()
	img, err := png.Decode(f)
	if err != nil {
		return nil, err
	}
	b := img.Bounds()
	out := image.NewRGBA(image.Rect(0, 0, b.Dx(), b.Dy()))
	for y := 0; y < b.Dy(); y++ {
		for x := 0; x < b.Dx(); x++ {
			out.Set(x, y, img.At(b.Min.X+x, b.Min.Y+y))
		}
	}
	return out, nil
}

// HSVParams holds the three shader_parameter/{h,s,v} values from a
// ShaderMaterial .tres.
type HSVParams struct {
	H, S, V float64
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
// src and returns a new RGBA image. Alpha is preserved verbatim.
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

			// RGB -> YIQ (row = (c0.y, c1.y, c2.y) for each output).
			Y := yiqFwd[0][0]*r + yiqFwd[0][1]*g + yiqFwd[0][2]*bl
			I := yiqFwd[1][0]*r + yiqFwd[1][1]*g + yiqFwd[1][2]*bl
			Q := yiqFwd[2][0]*r + yiqFwd[2][1]*g + yiqFwd[2][2]*bl

			// Hue rotation in YZ plane, matching the shader's
			// `col *= hue_shift` (row × matrix) direction.
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

// yiqInv is the exact inverse of yiqFwd, computed once so our RGB round-trip
// matches GLSL's `inverse(RGB_to_YIQ) * col.rgb`.
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
