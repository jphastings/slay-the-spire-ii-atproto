package main

import (
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"image"
	"image/draw"
	"image/png"
	"log"
	"os"
	"path/filepath"
	"strings"

	"github.com/jphastings/slay-the-spire-ii-atproto/extract/cardtext"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/compose"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/ctex"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/fontpkg"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/godotimport"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/pck"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/tres"
)

func main() {
	list := flag.Bool("list", false, "list all file paths in the pack and exit")
	grep := flag.String("grep", "", "with -list: substring filter")
	outDir := flag.String("out", "out", "output directory")
	flag.Usage = func() {
		fmt.Fprintf(os.Stderr, "usage: %s [flags] <pck-file>\n", os.Args[0])
		flag.PrintDefaults()
	}
	flag.Parse()
	if flag.NArg() != 1 {
		flag.Usage()
		os.Exit(2)
	}

	p, err := pck.Open(flag.Arg(0))
	if err != nil {
		log.Fatalf("open pack: %v", err)
	}
	defer p.Close()
	fmt.Fprintf(os.Stderr, "pack_format_version=%d files=%d\n", p.Version, len(p.Files))

	if *list {
		for _, f := range p.Files {
			if *grep != "" && !strings.Contains(f.Path, *grep) {
				continue
			}
			fmt.Printf("%10d  %s\n", f.Size, f.Path)
		}
		return
	}

	if err := extractImages(p, "images/potions/", filepath.Join(*outDir, "potions"), false); err != nil {
		log.Fatalf("potions: %v", err)
	}
	if err := extractImages(p, "images/relics/", filepath.Join(*outDir, "relics"), false); err != nil {
		log.Fatalf("relics: %v", err)
	}
	if err := extractImages(p, "images/packed/card_portraits/", filepath.Join(*outDir, "card_portraits"), true); err != nil {
		log.Fatalf("card portraits: %v", err)
	}
	if err := extractImages(p, "images/atlases/", filepath.Join(*outDir, "atlases"), false); err != nil {
		log.Fatalf("atlases: %v", err)
	}
	locDir := filepath.Join(*outDir, "localization", "eng")
	for _, name := range []string{"cards", "relics", "potions", "characters", "monsters"} {
		src := "localization/eng/" + name + ".json"
		dst := filepath.Join(locDir, name+".json")
		if err := extractLocalization(p, src, dst); err != nil {
			log.Fatalf("%s: %v", src, err)
		}
	}
	if err := composeCards(p, *outDir); err != nil {
		log.Fatalf("compose cards: %v", err)
	}
}

// extractImages scans the pack for *.png.import files under prefix, resolves
// each to its imported .ctex, decodes it, and writes a PNG alongside in
// outDir.
func extractImages(p *pck.Pack, prefix, outDir string, recursive bool) error {
	if err := os.MkdirAll(outDir, 0o755); err != nil {
		return err
	}
	var ok, gpuCompressed, failed int
	for _, f := range p.Files {
		if !strings.HasPrefix(f.Path, prefix) || !strings.HasSuffix(f.Path, ".png.import") {
			continue
		}
		rest := strings.TrimPrefix(f.Path, prefix)
		if !recursive && strings.Contains(rest, "/") {
			continue
		}
		relPng := strings.TrimSuffix(rest, ".import")
		out := filepath.Join(outDir, relPng)
		if err := os.MkdirAll(filepath.Dir(out), 0o755); err != nil {
			return err
		}
		switch err := convertImported(p, f.Path, out); {
		case err == nil:
			ok++
		case errors.Is(err, ctex.ErrGPUCompressed):
			gpuCompressed++
		default:
			failed++
			fmt.Fprintf(os.Stderr, "  %s: %v\n", relPng, err)
		}
	}
	fmt.Fprintf(os.Stderr, "%s → %d/%d ok (skipped %d GPU-compressed, %d errors) in %s\n",
		prefix, ok, ok+gpuCompressed+failed, gpuCompressed, failed, outDir)
	return nil
}

func convertImported(p *pck.Pack, importPath, outPath string) error {
	img, err := loadImported(p, importPath)
	if err != nil {
		return err
	}
	out, err := os.Create(outPath)
	if err != nil {
		return err
	}
	defer out.Close()
	return png.Encode(out, img)
}

// loadImported resolves a .import sidecar inside the pack to the decoded
// source image.
func loadImported(p *pck.Pack, importPath string) (image.Image, error) {
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
		return nil, fmt.Errorf("no import path in .import")
	}
	ctexData, err := p.Read(chosen)
	if err != nil {
		return nil, err
	}
	return ctex.Decode(ctexData)
}

// extractLocalization reads a JSON localization file from the pack and writes
// it to outPath with indentation.
func extractLocalization(p *pck.Pack, srcPath, outPath string) error {
	data, err := p.Read(srcPath)
	if err != nil {
		return err
	}
	var m map[string]string
	if err := json.Unmarshal(data, &m); err != nil {
		return fmt.Errorf("parse %s: %w", srcPath, err)
	}
	pretty, err := json.MarshalIndent(m, "", "  ")
	if err != nil {
		return err
	}
	if err := os.MkdirAll(filepath.Dir(outPath), 0o755); err != nil {
		return err
	}
	if err := os.WriteFile(outPath, pretty, 0o644); err != nil {
		return err
	}
	fmt.Fprintf(os.Stderr, "%s → %d keys in %s\n", srcPath, len(m), outPath)
	return nil
}

// composeCards orchestrates per-card image composition: frame + portrait +
// typed title/description laid out to match the wiki's card-infobox-image.
func composeCards(p *pck.Pack, outDir string) error {
	locRaw, err := p.Read("localization/eng/cards.json")
	if err != nil {
		return err
	}
	var loc map[string]string
	if err := json.Unmarshal(locRaw, &loc); err != nil {
		return err
	}

	// Load the attack frame — used as the generic frame for every card in
	// this pass. Card-type/rarity/background composition is a separate
	// task (see plan).
	frameTres, err := p.Read("images/atlases/ui_atlas.sprites/card/card_frame_attack_s.tres")
	if err != nil {
		return err
	}
	frameRef, err := tres.ParseAtlasTexture(frameTres)
	if err != nil {
		return fmt.Errorf("parse frame.tres: %w", err)
	}
	frameAtlas, err := loadImported(p, frameRef.AtlasPath+".import")
	if err != nil {
		return fmt.Errorf("load frame atlas %s: %w", frameRef.AtlasPath, err)
	}
	frame := subImage(frameAtlas, frameRef.Region)

	faces, err := fontpkg.LoadKreon(p)
	if err != nil {
		return fmt.Errorf("load Kreon: %w", err)
	}

	cardsOut := filepath.Join(outDir, "cards")
	if err := os.MkdirAll(cardsOut, 0o755); err != nil {
		return err
	}

	var ok, failed int
	for _, f := range p.Files {
		if !strings.HasPrefix(f.Path, "images/packed/card_portraits/") || !strings.HasSuffix(f.Path, ".png.import") {
			continue
		}
		rel := strings.TrimSuffix(strings.TrimPrefix(f.Path, "images/packed/card_portraits/"), ".png.import")
		parts := strings.Split(rel, "/")
		if len(parts) != 2 {
			continue // skip nested beta/token variants
		}
		class, id := parts[0], parts[1]

		portrait, err := loadImported(p, f.Path)
		if err != nil {
			failed++
			fmt.Fprintf(os.Stderr, "  %s/%s portrait: %v\n", class, id, err)
			continue
		}
		up := strings.ToUpper(id)
		title := loc[up+".title"]
		desc := cardtext.Parse(loc[up+".description"])

		card, err := compose.Card(portrait, frame, title, desc, nil, faces)
		if err != nil {
			failed++
			fmt.Fprintf(os.Stderr, "  %s/%s compose: %v\n", class, id, err)
			continue
		}

		outPath := filepath.Join(cardsOut, class, id+".png")
		if err := os.MkdirAll(filepath.Dir(outPath), 0o755); err != nil {
			return err
		}
		outFile, err := os.Create(outPath)
		if err != nil {
			return err
		}
		if err := png.Encode(outFile, card); err != nil {
			outFile.Close()
			failed++
			continue
		}
		outFile.Close()
		ok++
	}
	fmt.Fprintf(os.Stderr, "cards → %d/%d composed in %s\n", ok, ok+failed, cardsOut)
	return nil
}

// subImage returns an *image.RGBA copy of the given rectangle of src.
func subImage(src image.Image, r image.Rectangle) image.Image {
	out := image.NewRGBA(image.Rect(0, 0, r.Dx(), r.Dy()))
	draw.Draw(out, out.Bounds(), src, r.Min, draw.Src)
	return out
}
