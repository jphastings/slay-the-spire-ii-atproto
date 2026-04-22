// Package tres parses Godot .tres (text resource) files, enough to extract
// AtlasTexture references — which is what card/relic/potion sprite atlases
// use to point into a shared sheet.
package tres

import (
	"bufio"
	"bytes"
	"fmt"
	"image"
	"regexp"
	"strconv"
	"strings"
)

// AtlasTexture is an AtlasTexture resource parsed from a .tres file.
type AtlasTexture struct {
	AtlasPath string     // path of the backing Texture2D, "res://" stripped
	Region    image.Rectangle
}

var (
	extResourceRE = regexp.MustCompile(`^\[ext_resource\s+(.+?)\]\s*$`)
	kvRE          = regexp.MustCompile(`(\w+)\s*=\s*("[^"]*"|\S+)`)
	rectRE        = regexp.MustCompile(`Rect2\s*\(\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\)`)
)

// ParseAtlasTexture reads a .tres file that declares an [resource] of type
// AtlasTexture and returns the atlas path and region.
func ParseAtlasTexture(data []byte) (AtlasTexture, error) {
	extResources := map[string]string{} // id -> path

	s := bufio.NewScanner(bytes.NewReader(data))
	var at AtlasTexture
	var haveAtlas, haveRegion bool

	for s.Scan() {
		line := strings.TrimSpace(s.Text())
		if line == "" {
			continue
		}
		if m := extResourceRE.FindStringSubmatch(line); m != nil {
			attrs := parseKV(m[1])
			extResources[attrs["id"]] = strings.TrimPrefix(attrs["path"], "res://")
			continue
		}
		// Inside [resource] section: look for atlas and region.
		if strings.HasPrefix(line, "atlas") {
			eq := strings.Index(line, "=")
			if eq < 0 {
				continue
			}
			rhs := strings.TrimSpace(line[eq+1:])
			if strings.HasPrefix(rhs, "ExtResource(") {
				id := strings.Trim(rhs[len("ExtResource("):len(rhs)-1], `"`)
				if p, ok := extResources[id]; ok {
					at.AtlasPath = p
					haveAtlas = true
				}
			}
		}
		if strings.HasPrefix(line, "region") {
			if m := rectRE.FindStringSubmatch(line); m != nil {
				x, _ := strconv.ParseFloat(m[1], 64)
				y, _ := strconv.ParseFloat(m[2], 64)
				w, _ := strconv.ParseFloat(m[3], 64)
				h, _ := strconv.ParseFloat(m[4], 64)
				at.Region = image.Rect(int(x), int(y), int(x+w), int(y+h))
				haveRegion = true
			}
		}
	}
	if err := s.Err(); err != nil {
		return AtlasTexture{}, err
	}
	if !haveAtlas || !haveRegion {
		return AtlasTexture{}, fmt.Errorf("not an AtlasTexture: atlas=%v region=%v", haveAtlas, haveRegion)
	}
	return at, nil
}

// parseKV extracts `k="v"` and `k=v` pairs.
func parseKV(s string) map[string]string {
	out := map[string]string{}
	for _, m := range kvRE.FindAllStringSubmatch(s, -1) {
		out[m[1]] = strings.Trim(m[2], `"`)
	}
	return out
}
