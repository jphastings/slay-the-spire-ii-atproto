// Package cardtext parses the game's card description strings into typed
// runs suitable for rich-text rendering.
//
// Input format (from localization/eng/cards.json):
//
//	"Deal {Damage:diff()} damage.\nApply {VulnerablePower:diff()} [gold]Vulnerable[/gold]."
//
// Tokens recognised:
//   - [tag] / [/tag]      BBCode; [gold] sets StyleHighlight, others ignored
//   - {FieldName:fn()}    placeholder; becomes StylePlaceholder with Field=FieldName
//   - \n                  starts a new Line
//   - anything else       StyleNormal text
package cardtext

// RunStyle tells the renderer which typographic treatment a run wants.
type RunStyle int

const (
	StyleNormal      RunStyle = iota // default body color
	StyleHighlight                   // [gold]…[/gold]
	StylePlaceholder                 // {Name:…}
)

// Run is a contiguous span of text that shares one style.
type Run struct {
	Text  string
	Style RunStyle

	// Field is set on StylePlaceholder runs — the identifier inside the
	// braces, e.g. "Damage" for "{Damage:diff()}". Callers can look it up
	// in a substitution map; when missing, renderers emit Text as-is (it
	// defaults to "?").
	Field string
}

// Line is one logical line — runs flow inline within a Line; hard \n starts
// a new Line.
type Line []Run

// Parse turns a description string into its typed runs.
func Parse(desc string) []Line {
	var lines []Line
	var cur Line
	var buf []byte
	style := StyleNormal

	flush := func() {
		if len(buf) > 0 {
			cur = append(cur, Run{Text: string(buf), Style: style})
			buf = buf[:0]
		}
	}
	newline := func() {
		flush()
		lines = append(lines, cur)
		cur = nil
	}

	for i := 0; i < len(desc); {
		c := desc[i]
		switch c {
		case '\\':
			if i+1 < len(desc) && desc[i+1] == 'n' {
				newline()
				i += 2
				continue
			}
		case '\n':
			newline()
			i++
			continue
		case '[':
			// [tag] or [/tag]
			end := indexByte(desc, i, ']')
			if end < 0 {
				buf = append(buf, c)
				i++
				continue
			}
			tag := desc[i+1 : end]
			flush()
			switch {
			case tag == "gold":
				style = StyleHighlight
			case tag == "/gold":
				style = StyleNormal
			// Unknown tags: swallow them silently; leave style alone.
			}
			i = end + 1
			continue
		case '{':
			end := indexByte(desc, i, '}')
			if end < 0 {
				buf = append(buf, c)
				i++
				continue
			}
			inner := desc[i+1 : end]
			// "FieldName:fn()" — keep only FieldName.
			name := inner
			if colon := indexByte(inner, 0, ':'); colon >= 0 {
				name = inner[:colon]
			}
			flush()
			cur = append(cur, Run{Text: "?", Style: StylePlaceholder, Field: name})
			i = end + 1
			continue
		}
		buf = append(buf, c)
		i++
	}
	flush()
	if cur != nil || len(lines) == 0 {
		lines = append(lines, cur)
	}
	return lines
}

func indexByte(s string, from int, b byte) int {
	for i := from; i < len(s); i++ {
		if s[i] == b {
			return i
		}
	}
	return -1
}
