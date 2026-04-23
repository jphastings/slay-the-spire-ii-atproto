// Package cards extracts Slay the Spire 2 card render assets — individual
// atlas parts, HSV-tinted frame/banner variants, Kreon TTFs, and per-card
// metadata — for the web client to compose cards in-browser.
package cards

import (
	"bytes"
	"fmt"
	"image"
	"image/draw"
	"image/png"
	"os"
	"path/filepath"
	"strings"

	"github.com/jphastings/slay-the-spire-ii-atproto/extract/ctex"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/godotimport"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/pck"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/tres"
)

// Extract runs every card-asset extraction phase: atlas parts, HSV-baked
// frame colors and banner rarities, Kreon TTFs, and card metadata (if a
// decompiled C# source path is provided).
func Extract(p *pck.Pack, outDir, decompiledCS string) error {
	partsDir := filepath.Join(outDir, "parts")

	// Atlas-based parts — one PNG per (kind, variant).
	for _, spec := range atlasParts {
		if err := extractAtlasPart(p, spec, partsDir); err != nil {
			return fmt.Errorf("%s/%s: %w", spec.kind, spec.variant, err)
		}
	}

	// The plaque (TypePlaque node in scenes/cards/card.tscn) uses
	// card_portrait_border_plaque2.png as a NinePatchRect background —
	// pulled straight from images/ui/cards/, not atlas-backed. Tinted
	// per-rarity at render time via CSS filters.
	if err := extractStandalone(p,
		"images/ui/cards/card_portrait_border_plaque2.png.import",
		filepath.Join(partsDir, "plaque.png"),
	); err != nil {
		return fmt.Errorf("plaque: %w", err)
	}

	enchantDir := filepath.Join(outDir, "enchantments")
	if err := extractEnchantmentIcons(p, enchantDir); err != nil {
		return fmt.Errorf("enchantment icons: %w", err)
	}

	// Enchantment tab background (card_enchant_s in the ui atlas) —
	// the game applies a fixed HSV(0.25, 0.4, 1.0) ShaderMaterial to
	// it in scenes/cards/card.tscn; we bake that tint here and drop
	// it into the enchantments/ dir so the sprite-sheet packer picks
	// it up alongside the icons.
	enchantTab, err := loadAtlasPart(p, "images/atlases/ui_atlas.sprites/card/card_enchant_s.tres")
	if err != nil {
		return fmt.Errorf("load enchant tab: %w", err)
	}
	if rgba, ok := enchantTab.(*image.RGBA); ok {
		enchantTab = tintImage(rgba, HSVParams{H: 0.25, S: 0.4, V: 1.0})
	}
	if err := writePNG(filepath.Join(enchantDir, "tab.png"), enchantTab); err != nil {
		return fmt.Errorf("enchant tab: %w", err)
	}

	if err := writeTints(p, filepath.Join(outDir, "tints.json")); err != nil {
		return fmt.Errorf("tints: %w", err)
	}

	if err := extractFonts(p, filepath.Join(outDir, "fonts")); err != nil {
		return fmt.Errorf("fonts: %w", err)
	}

	if decompiledCS != "" {
		charByID := cardCharacters(p)
		loc, err := loadCardLocalization(p)
		if err != nil {
			return fmt.Errorf("card localization: %w", err)
		}
		keywordLoc, err := loadKeywordLocalization(p)
		if err != nil {
			return fmt.Errorf("keyword localization: %w", err)
		}
		if err := extractMetadata(decompiledCS, filepath.Join(outDir, "cards.json"), charByID, loc, keywordLoc); err != nil {
			return fmt.Errorf("metadata: %w", err)
		}
	}

	return nil
}

// atlasPart describes a single sprite to pull out of the ui_atlas via a
// .tres AtlasTexture reference.
type atlasPart struct {
	kind    string // subdirectory under parts/
	variant string // filename stem (without .png)
	tres    string // path in the pck
}

// atlasParts are the base sprites we copy out unmodified. Frames,
// portrait-borders, banner, and plaque are tinted client-side via CSS
// filters using the HSV params recorded in tints.json — see hsv.go's
// extractTints. The game's shader rotates RGB through YIQ space; CSS's
// hue-rotate is the same matrix, so the result is visually identical.
var atlasParts = []atlasPart{
	// Card frames — one shape per type, hue-rotated per character at render time.
	{"frame", "attack", "images/atlases/ui_atlas.sprites/card/card_frame_attack_s.tres"},
	{"frame", "skill", "images/atlases/ui_atlas.sprites/card/card_frame_skill_s.tres"},
	{"frame", "power", "images/atlases/ui_atlas.sprites/card/card_frame_power_s.tres"},

	// Portrait borders — one shape per type, hue-rotated per rarity.
	// (The card scene assigns the banner material — not the frame
	// material — to this layer.)
	{"portrait_border", "attack", "images/atlases/ui_atlas.sprites/card/card_portrait_border_attack_s.tres"},
	{"portrait_border", "skill", "images/atlases/ui_atlas.sprites/card/card_portrait_border_skill_s.tres"},
	{"portrait_border", "power", "images/atlases/ui_atlas.sprites/card/card_portrait_border_power_s.tres"},

	// Banner — one shape, hue-rotated per rarity.
	{"", "banner", "images/atlases/ui_atlas.sprites/card/card_banner.tres"},

	// Cost orbs, per character (no tinting needed).
	{"orb", "colorless", "images/atlases/ui_atlas.sprites/card/energy_colorless.tres"},
	{"orb", "defect", "images/atlases/ui_atlas.sprites/card/energy_defect.tres"},
	{"orb", "ironclad", "images/atlases/ui_atlas.sprites/card/energy_ironclad.tres"},
	{"orb", "necrobinder", "images/atlases/ui_atlas.sprites/card/energy_necrobinder.tres"},
	{"orb", "quest", "images/atlases/ui_atlas.sprites/card/energy_quest.tres"},
	{"orb", "regent", "images/atlases/ui_atlas.sprites/card/energy_regent.tres"},
	{"orb", "silent", "images/atlases/ui_atlas.sprites/card/energy_silent.tres"},
}

func extractAtlasPart(p *pck.Pack, spec atlasPart, partsDir string) error {
	img, err := loadAtlasPart(p, spec.tres)
	if err != nil {
		return err
	}
	out := filepath.Join(partsDir, spec.kind, spec.variant+".png")
	return writePNG(out, img)
}

// loadAtlasPart resolves a .tres AtlasTexture reference to a cropped image.
func loadAtlasPart(p *pck.Pack, tresPath string) (image.Image, error) {
	raw, err := p.Read(tresPath)
	if err != nil {
		return nil, fmt.Errorf("read %s: %w", tresPath, err)
	}
	ref, err := tres.ParseAtlasTexture(raw)
	if err != nil {
		return nil, fmt.Errorf("parse %s: %w", tresPath, err)
	}
	atlas, err := LoadImported(p, ref.AtlasPath+".import")
	if err != nil {
		return nil, fmt.Errorf("load atlas %s: %w", ref.AtlasPath, err)
	}
	return subImage(atlas, ref.Region), nil
}

// extractStandalone copies a single .png.import to disk as a PNG.
func extractStandalone(p *pck.Pack, importPath, outPath string) error {
	img, err := LoadImported(p, importPath)
	if err != nil {
		return err
	}
	return writePNG(outPath, img)
}

// LoadImported resolves a .import sidecar inside the pack to the decoded
// source image. Exported so main.go can reuse it without duplicating logic.
//
// BC7 (the .bptc.ctex format Godot uses for most atlas textures) compresses
// RGB and alpha as separate blocks, so transparent pixels carry whatever
// chroma happened to pack well — often wildly off-hue noise. When those
// near-transparent pixels are later HSV-tinted (e.g. the card banner under
// a rarity material) the stale chroma rotates into a visible wrong-colour
// speckle on otherwise-transparent edges. Zero RGB below an alpha floor
// so nothing downstream operates on that garbage; alpha is preserved, so
// the composite result is indistinguishable for pixels that would always
// have been near-invisible.
func LoadImported(p *pck.Pack, importPath string) (image.Image, error) {
	importData, err := p.Read(importPath)
	if err != nil {
		return nil, err
	}
	paths, err := godotimport.Parse(importData)
	if err != nil {
		return nil, err
	}
	_, chosen, ok := paths.Preferred()
	if !ok {
		return nil, fmt.Errorf("no import path in %s", importPath)
	}
	ctexData, err := p.Read(chosen)
	if err != nil {
		return nil, err
	}
	img, err := ctex.Decode(ctexData)
	if err != nil {
		return nil, err
	}
	return zeroLowAlphaChroma(img, 32), nil
}

// zeroLowAlphaChroma returns a copy of src in which every pixel with
// alpha < threshold has its RGB replaced with zero. See LoadImported for
// the rationale.
func zeroLowAlphaChroma(src image.Image, threshold uint8) image.Image {
	b := src.Bounds()
	out := image.NewRGBA(image.Rect(0, 0, b.Dx(), b.Dy()))
	draw.Draw(out, out.Bounds(), src, b.Min, draw.Src)
	for i := 3; i < len(out.Pix); i += 4 {
		if out.Pix[i] < threshold {
			out.Pix[i-3] = 0
			out.Pix[i-2] = 0
			out.Pix[i-1] = 0
		}
	}
	return out
}

// extractEnchantmentIcons writes every images/enchantments/*.png in the
// pack as a PNG under outDir, named by the enchantment's snake_case id so
// the web client can resolve them by the ids we emit in deck entries.
func extractEnchantmentIcons(p *pck.Pack, outDir string) error {
	const prefix = "images/enchantments/"
	for _, f := range p.Files {
		if !strings.HasPrefix(f.Path, prefix) || !strings.HasSuffix(f.Path, ".png.import") {
			continue
		}
		rel := strings.TrimSuffix(strings.TrimPrefix(f.Path, prefix), ".import")
		img, err := LoadImported(p, f.Path)
		if err != nil {
			return fmt.Errorf("load %s: %w", f.Path, err)
		}
		if err := writePNG(filepath.Join(outDir, rel), img); err != nil {
			return err
		}
	}
	return nil
}

func subImage(src image.Image, r image.Rectangle) *image.RGBA {
	out := image.NewRGBA(image.Rect(0, 0, r.Dx(), r.Dy()))
	draw.Draw(out, out.Bounds(), src, r.Min, draw.Src)
	return out
}

func writePNG(path string, img image.Image) error {
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return err
	}
	var buf bytes.Buffer
	if err := png.Encode(&buf, img); err != nil {
		return err
	}
	return os.WriteFile(path, buf.Bytes(), 0o644)
}

