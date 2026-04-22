// Package compose renders a single Slay the Spire 2 card image by layering
// portrait + frame and overlaying the Kreon-typed title and description.
// Layout, font sizes, colors and positions match the
// `.card-infobox-main-section-sts2` rules from the wiki's load_002.css,
// uniformly scaled up 2×.
package compose

import (
	"image"
	"image/color"
	"image/draw"
	"strings"
	"unicode"

	"golang.org/x/image/font"
	"golang.org/x/image/math/fixed"

	"github.com/jphastings/slay-the-spire-ii-atproto/extract/cardtext"
	"github.com/jphastings/slay-the-spire-ii-atproto/extract/fontpkg"
)

// scale is applied to every wiki-CSS px value so we render at 546×700.
const scale = 2

// Canvas dimensions in the wiki CSS.
const (
	cssW = 273
	cssH = 350
)

// Colors from the wiki stylesheet.
var (
	oldlace    = color.RGBA{0xFD, 0xF5, 0xE6, 0xFF}
	shadowDesc = color.RGBA{0x2F, 0x30, 0x26, 0xFF}
	highlight  = color.RGBA{0xF0, 0xC8, 0x50, 0xFF} // --game-text-highlight-color
)

// Card composes a 546×700 RGBA card image.
//   - portrait: card art (typically 1000×760 from images/packed/card_portraits).
//   - frame:    the card frame, already cropped out of the ui_atlas.
//   - values:   placeholder substitutions ("Damage" → "8"); empty → "?".
func Card(portrait, frame image.Image, title string, desc []cardtext.Line,
	values map[string]string, faces *fontpkg.Faces) (*image.RGBA, error) {
	w, h := cssW*scale, cssH*scale
	canvas := image.NewRGBA(image.Rect(0, 0, w, h))

	// Frame first — it's a fully-opaque paper background with a white
	// portrait window at the top and a dark description panel at the
	// bottom. In the shipped game, separate class-and-type Bg layers
	// fill that white area; we don't have those as standalone assets,
	// so the portrait is drawn on top of the frame instead.
	if frame != nil {
		drawFit(canvas, image.Rect(0, 0, w, h), frame)
	}

	// Portrait: wiki places 178×135 at top=53 left=45.
	pr := image.Rect(45*scale, 53*scale, (45+178)*scale, (53+135)*scale)
	drawFit(canvas, pr, portrait)

	// Title
	nameFace, err := faces.Face(19.2*scale, false)
	if err != nil {
		return nil, err
	}
	drawCentered(canvas,
		[]cardtext.Line{{{Text: title, Style: cardtext.StyleNormal}}}, nameFace,
		image.Rect(51*scale, 42*scale, (cssW-51)*scale, (42+30)*scale),
		oldlace, color.RGBA{}, image.Point{})

	// Description
	descFace, err := faces.Face(16*scale, false)
	if err != nil {
		return nil, err
	}
	drawCentered(canvas,
		substituteValues(desc, values), descFace,
		image.Rect(50*scale, 215*scale, (cssW-50)*scale, (215+97)*scale),
		oldlace, shadowDesc, image.Point{X: 2 * scale, Y: 1 * scale})

	return canvas, nil
}

// substituteValues returns desc with placeholder Runs' Text replaced by
// values[Field] where present.
func substituteValues(desc []cardtext.Line, values map[string]string) []cardtext.Line {
	if len(values) == 0 {
		return desc
	}
	out := make([]cardtext.Line, len(desc))
	for i, line := range desc {
		nl := make(cardtext.Line, len(line))
		for j, r := range line {
			if r.Style == cardtext.StylePlaceholder {
				if v, ok := values[r.Field]; ok {
					r.Text = v
				}
			}
			nl[j] = r
		}
		out[i] = nl
	}
	return out
}

// drawFit draws src into dst's rect, nearest-neighbor scaled.
func drawFit(dst draw.Image, r image.Rectangle, src image.Image) {
	sb := src.Bounds()
	sw, sh := sb.Dx(), sb.Dy()
	if sw == 0 || sh == 0 {
		return
	}
	dw, dh := r.Dx(), r.Dy()
	for y := 0; y < dh; y++ {
		sy := y * sh / dh
		for x := 0; x < dw; x++ {
			sx := x * sw / dw
			dst.Set(r.Min.X+x, r.Min.Y+y, src.At(sb.Min.X+sx, sb.Min.Y+sy))
		}
	}
}

// drawCentered wraps every logical Line in lines to fit inside box, then
// paints the whole stack centered vertically and horizontally.
func drawCentered(dst *image.RGBA, lines []cardtext.Line, face font.Face,
	box image.Rectangle, fill, shadow color.RGBA, shadowOff image.Point) {
	if len(lines) == 0 {
		return
	}
	metrics := face.Metrics()
	lineH := metrics.Height.Ceil()

	var wrapped []cardtext.Line
	for _, ln := range lines {
		wrapped = append(wrapped, wrapLine(ln, face, box.Dx())...)
	}
	for len(wrapped) > 0 && isEmpty(wrapped[len(wrapped)-1]) {
		wrapped = wrapped[:len(wrapped)-1]
	}
	if len(wrapped) == 0 {
		return
	}

	totalH := lineH * len(wrapped)
	y := box.Min.Y + (box.Dy()-totalH)/2 + metrics.Ascent.Ceil()
	for _, dl := range wrapped {
		width := measureLine(dl, face)
		x := box.Min.X + (box.Dx()-width)/2
		drawLine(dst, dl, face, x, y, fill, shadow, shadowOff)
		y += lineH
	}
}

func isEmpty(line cardtext.Line) bool {
	for _, r := range line {
		if strings.TrimSpace(r.Text) != "" {
			return false
		}
	}
	return true
}

// wrapLine breaks one logical Line into display lines each of which fits
// within maxWidth pixels, preserving per-run styles across the break.
func wrapLine(line cardtext.Line, face font.Face, maxWidth int) []cardtext.Line {
	words := tokenize(line)
	if len(words) == 0 {
		return []cardtext.Line{nil}
	}
	var out []cardtext.Line
	var cur cardtext.Line
	curW := 0
	for _, tok := range words {
		tw := measureString(tok.Text, face)
		if curW > 0 && curW+tw > maxWidth {
			out = append(out, cur)
			cur = nil
			curW = 0
			// Drop the leading whitespace token on the new line.
			if isSpace(tok.Text) {
				continue
			}
		}
		if curW == 0 && isSpace(tok.Text) {
			continue
		}
		cur = appendRun(cur, cardtext.Run{Text: tok.Text, Style: tok.Style, Field: tok.Field})
		curW += tw
	}
	if cur != nil {
		out = append(out, cur)
	}
	return out
}

// appendRun merges adjacent runs of the same style to keep runs long.
func appendRun(line cardtext.Line, r cardtext.Run) cardtext.Line {
	if n := len(line); n > 0 && line[n-1].Style == r.Style && line[n-1].Field == r.Field {
		line[n-1].Text += r.Text
		return line
	}
	return append(line, r)
}

// tokenize splits each run into alternating whitespace-only and non-whitespace
// tokens so wrapLine can break on space boundaries.
func tokenize(line cardtext.Line) cardtext.Line {
	var out cardtext.Line
	for _, r := range line {
		i := 0
		for i < len(r.Text) {
			j := i
			inSpace := unicode.IsSpace(rune(r.Text[j]))
			for j < len(r.Text) && unicode.IsSpace(rune(r.Text[j])) == inSpace {
				j++
			}
			out = append(out, cardtext.Run{Text: r.Text[i:j], Style: r.Style, Field: r.Field})
			i = j
		}
	}
	return out
}

func isSpace(s string) bool {
	for _, r := range s {
		if !unicode.IsSpace(r) {
			return false
		}
	}
	return s != ""
}

// drawLine paints line left-to-right starting at (x, y) (baseline), applying
// per-run color and an optional drop-shadow.
func drawLine(dst *image.RGBA, line cardtext.Line, face font.Face, x, y int,
	fill, shadow color.RGBA, shadowOff image.Point) {
	hasShadow := shadow != (color.RGBA{}) && (shadowOff.X != 0 || shadowOff.Y != 0)
	if hasShadow {
		xx := x
		for _, r := range line {
			runColor := fill
			if r.Style == cardtext.StyleHighlight || r.Style == cardtext.StylePlaceholder {
				runColor = highlight
			}
			_ = runColor
			drawRunAt(dst, r.Text, face, xx+shadowOff.X, y+shadowOff.Y, shadow)
			xx += measureString(r.Text, face)
		}
	}
	for _, r := range line {
		col := fill
		if r.Style == cardtext.StyleHighlight || r.Style == cardtext.StylePlaceholder {
			col = highlight
		}
		drawRunAt(dst, r.Text, face, x, y, col)
		x += measureString(r.Text, face)
	}
}

func drawRunAt(dst *image.RGBA, s string, face font.Face, x, y int, col color.RGBA) {
	if s == "" {
		return
	}
	d := &font.Drawer{
		Dst:  dst,
		Src:  image.NewUniform(col),
		Face: face,
		Dot:  fixed.Point26_6{X: fixed.I(x), Y: fixed.I(y)},
	}
	d.DrawString(s)
}

func measureString(s string, face font.Face) int {
	return font.MeasureString(face, s).Ceil()
}

func measureLine(line cardtext.Line, face font.Face) int {
	w := 0
	for _, r := range line {
		w += measureString(r.Text, face)
	}
	return w
}
