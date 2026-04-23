package main

import (
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"image"
	"image/png"
	"log"
	"os"
	"path/filepath"
	"strings"

	"github.com/jphastings/slay-the-spire-ii-atproto/extract/cards"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/ctex"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/godotimport"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/pck"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/sprite"
)

func main() {
	list := flag.Bool("list", false, "list all file paths in the pack and exit")
	grep := flag.String("grep", "", "with -list: substring filter")
	cat := flag.String("cat", "", "print the contents of a single file in the pack and exit")
	outDir := flag.String("out", "out", "output directory")
	decompCS := flag.String("decompiled-cs", "", "path to a decompiled sts2.dll .cs file for card metadata extraction (optional)")
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

	if *cat != "" {
		data, err := p.Read(*cat)
		if err != nil {
			log.Fatalf("read %s: %v", *cat, err)
		}
		os.Stdout.Write(data)
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
	if err := cards.Extract(p, filepath.Join(*outDir, "cards"), *decompCS); err != nil {
		log.Fatalf("cards: %v", err)
	}

	// Pack relic + potion icons into sprite sheets so the web ships one
	// file per kind instead of hundreds. Tiles are 128×128 to match the
	// size RelicList / PotionList already render at.
	for _, kind := range []string{"relics", "potions"} {
		srcDir := filepath.Join(*outDir, kind)
		outPNG := filepath.Join(*outDir, kind+"_sprite.png")
		outJSON := filepath.Join(*outDir, kind+"_sprite.json")
		n, err := sprite.BuildUniform(srcDir, outPNG, outJSON, 128)
		if err != nil {
			log.Fatalf("sprite %s: %v", kind, err)
		}
		fmt.Fprintf(os.Stderr, "sprite %s → %d icons in %s\n", kind, n, outPNG)
	}

	// Card orbs + enchantment icons/tab: packed (heterogeneous) sheets so
	// each tile keeps its native pixel size — orbs vary 71–74 px across
	// characters, and the enchant tab (100×76) ships in the same sheet as
	// the 64×64 icons.
	packed := []struct{ name, srcDir string }{
		{"orb", filepath.Join(*outDir, "cards", "parts", "orb")},
		{"enchant", filepath.Join(*outDir, "cards", "enchantments")},
	}
	for _, pk := range packed {
		outPNG := filepath.Join(*outDir, pk.name+"_sprite.png")
		outJSON := filepath.Join(*outDir, pk.name+"_sprite.json")
		n, err := sprite.BuildPacked(pk.srcDir, outPNG, outJSON, 512)
		if err != nil {
			log.Fatalf("sprite %s: %v", pk.name, err)
		}
		fmt.Fprintf(os.Stderr, "sprite %s → %d tiles in %s\n", pk.name, n, outPNG)
	}

	// Card portraits: one sprite per character pool. 596 raw portraits
	// across ~11 characters would deploy as 596 PDS records, exceeding
	// the wisp CLI chunker's per-subfs size. Per-character sheets keep
	// each chunk small while preserving character-scoped fetches.
	// Tile is 356×271 to match the per-card webp size we shipped before.
	portraitsRoot := filepath.Join(*outDir, "card_portraits")
	chars, err := os.ReadDir(portraitsRoot)
	if err != nil {
		log.Fatalf("read card_portraits: %v", err)
	}
	for _, c := range chars {
		if !c.IsDir() {
			continue
		}
		charDir := filepath.Join(portraitsRoot, c.Name())
		outPNG := filepath.Join(portraitsRoot, c.Name()+"_sprite.png")
		outJSON := filepath.Join(portraitsRoot, c.Name()+"_sprite.json")
		n, err := sprite.Build(charDir, outPNG, outJSON, 356, 271)
		if err != nil {
			log.Fatalf("sprite portraits %s: %v", c.Name(), err)
		}
		fmt.Fprintf(os.Stderr, "sprite portraits %s → %d cards in %s\n", c.Name(), n, outPNG)
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

