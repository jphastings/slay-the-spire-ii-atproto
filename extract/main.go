package main

import (
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"image/png"
	"log"
	"os"
	"path/filepath"
	"strings"

	"github.com/jphastings/slay-the-spire-ii-atproto/extract/ctex"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/godotimport"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/pck"
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
	if err := extractCardLocalization(p, filepath.Join(*outDir, "cards.json")); err != nil {
		log.Fatalf("cards.json: %v", err)
	}
}

// extractImages scans the pack for *.png.import files under prefix, resolves
// each to its imported .ctex, decodes it, and writes a PNG alongside in
// outDir. If recursive is true, subdirectories under prefix are preserved in
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
		relPng := strings.TrimSuffix(rest, ".import") // e.g. ironclad/bash.png
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
	importData, err := p.Read(importPath)
	if err != nil {
		return err
	}
	paths, err := godotimport.Parse(importData)
	if err != nil {
		return err
	}
	// Only decode plain ctex. bptc/s3tc paths wrap GPU-compressed blocks
	// that we can't yet decode.
	plain := paths.Plain()
	if plain == "" {
		return fmt.Errorf("%w: only path.%s= available", ctex.ErrGPUCompressed, firstKey(paths))
	}
	ctexData, err := p.Read(plain)
	if err != nil {
		return err
	}
	img, err := ctex.Decode(ctexData)
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

func firstKey(p godotimport.Paths) string {
	for k := range p {
		if k != "" {
			return k
		}
	}
	return ""
}

// extractCardLocalization reads localization/eng/cards.json from the pack and
// writes it to outPath with indentation.
func extractCardLocalization(p *pck.Pack, outPath string) error {
	data, err := p.Read("localization/eng/cards.json")
	if err != nil {
		return err
	}
	var m map[string]string
	if err := json.Unmarshal(data, &m); err != nil {
		return fmt.Errorf("parse cards.json: %w", err)
	}
	pretty, err := json.MarshalIndent(m, "", "  ")
	if err != nil {
		return err
	}
	if err := os.WriteFile(outPath, pretty, 0o644); err != nil {
		return err
	}
	fmt.Fprintf(os.Stderr, "cards.json → %d keys in %s\n", len(m), outPath)
	return nil
}
