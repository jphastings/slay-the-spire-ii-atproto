// Package godotimport parses Godot .import sidecar files to map a source asset
// path (e.g. "images/relics/akabeko.png") to its imported binary path
// (e.g. ".godot/imported/akabeko.png-<hash>.ctex"). Godot writes one entry per
// target import format: `path=`, `path.bptc=`, `path.s3tc=`, etc.
package godotimport

import (
	"bufio"
	"bytes"
	"fmt"
	"strings"
)

// Paths holds every `path*=` value found in a .import file, keyed by its
// qualifier ("" for plain `path=`, "bptc", "s3tc", "etc2", "astc", ...).
type Paths map[string]string

// Plain returns the plain `path=` value, or "" if absent.
func (p Paths) Plain() string { return p[""] }

// Preferred returns the best path to try, in order: plain, bptc, s3tc, etc2,
// astc. Returns ("", "", false) if the file has no path entries.
func (p Paths) Preferred() (qualifier, path string, ok bool) {
	for _, q := range []string{"", "bptc", "s3tc", "etc2", "astc", "etc"} {
		if v, present := p[q]; present {
			return q, v, true
		}
	}
	for q, v := range p {
		return q, v, true
	}
	return "", "", false
}

// Parse reads a .import file's contents.
func Parse(data []byte) (Paths, error) {
	out := Paths{}
	s := bufio.NewScanner(bytes.NewReader(data))
	for s.Scan() {
		line := strings.TrimSpace(s.Text())
		if !strings.HasPrefix(line, "path") {
			continue
		}
		eq := strings.IndexByte(line, '=')
		if eq < 0 {
			continue
		}
		key := line[:eq]
		value := strings.Trim(line[eq+1:], `"`)
		value = strings.TrimPrefix(value, "res://")

		switch {
		case key == "path":
			out[""] = value
		case strings.HasPrefix(key, "path."):
			out[strings.TrimPrefix(key, "path.")] = value
		}
	}
	if err := s.Err(); err != nil {
		return nil, err
	}
	if len(out) == 0 {
		return nil, fmt.Errorf("no path= field found in .import")
	}
	return out, nil
}
