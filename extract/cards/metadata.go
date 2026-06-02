package cards

import (
	"encoding/json"
	"fmt"
	"os"
	"regexp"
	"sort"
	"strconv"
	"strings"

	"github.com/jphastings/slay-the-spire-ii-atproto/extract/pck"
)

// CardMeta is the per-card metadata the web client needs to pick the right
// visual parts. ID is the game's snake_case card id (e.g. "bash"); the
// web app re-uppercases to form lexicon ids like CARD.BASH.
type CardMeta struct {
	ID           string             `json:"id"`
	ClassName    string             `json:"class"`                   // original C# class name (for debug)
	Character    string             `json:"character,omitempty"`     // ironclad, silent, defect, etc.
	Cost         string             `json:"cost"`                    // "0", "1", "2", or "?" when unknown
	StarCost     int                `json:"starCost,omitempty"`      // Regent star cost; 0/absent = none (base CanonicalStarCost is -1)
	Type         string             `json:"type"`                    // attack | skill | power | ...
	Rarity       string             `json:"rarity"`
	Title        string             `json:"title,omitempty"`         // localized display name
	Description  string             `json:"description,omitempty"`   // raw game description (BBCode + {Placeholders})
	Vars         map[string]float64 `json:"vars,omitempty"`          // default values for {Field:diff()} placeholders
	UpgradedVars map[string]float64 `json:"upgradedVars,omitempty"`  // placeholders on the upgraded card; omitted when identical to Vars
}

var (
	cardClassRE   = regexp.MustCompile(`\bclass\s+(\w+)\s*:\s*CardModel\b`)
	constructorRE = regexp.MustCompile(
		`public\s+\w+\(\)\s*:\s*base\(\s*([^,]+)\s*,\s*CardType\.(\w+)\s*,\s*CardRarity\.(\w+)`,
	)
	// Matches the CanonicalKeywords getter body and captures whichever
	// of the two shapes it uses:
	//   new CardKeyword[N] { CardKeyword.A, CardKeyword.B }       (group 1)
	//   new ...SingleElementList<CardKeyword>(CardKeyword.A)     (group 2)
	canonicalKeywordsRE = regexp.MustCompile(
		`CanonicalKeywords\s*=>\s*[\s\S]*?(?:\]\s*\{([^}]*)\}|<CardKeyword>\(\s*(CardKeyword\.\w+)\s*\))`,
	)
	cardKeywordRefRE = regexp.MustCompile(`CardKeyword\.(\w+)`)
	// Regent cards override `public override int CanonicalStarCost => N;`.
	// The base CardModel returns -1 (no star cost), so only overriding
	// cards match here; non-Regent cards leave StarCost at 0.
	canonicalStarCostRE = regexp.MustCompile(`CanonicalStarCost\s*=>\s*(\d+)`)
)

// Keyword orderings from sts2.dll (CardKeywordOrder in the decomp).
// GetDescriptionForPile inserts the before-description keywords with
// Insert(0, …) in this order — so the rendered list ends up in the
// reverse order (Unplayable first, then Innate, Retain, Sly, Ethereal).
var (
	beforeDescriptionKeywords = []string{"Ethereal", "Sly", "Retain", "Innate", "Unplayable"}
	afterDescriptionKeywords  = []string{"Exhaust", "Eternal"}
)

// extractMetadata scans a decompiled sts2 C# source file for every
// `class X : CardModel { … }` declaration and writes a JSON array of
// CardMeta to outPath. charByID supplies each card's character pool; loc
// is the English card localization (…/cards.json), and keywordLoc is the
// keyword localization (…/card_keywords.json) used to compose
// descriptions for cards whose main text is empty (mainly curses and
// statuses like Clumsy).
func extractMetadata(csPath, outPath string, charByID, loc, keywordLoc map[string]string) error {
	src, err := os.ReadFile(csPath)
	if err != nil {
		return err
	}

	seen := map[string]CardMeta{}
	locs := cardClassRE.FindAllSubmatchIndex(src, -1)
	if len(locs) == 0 {
		return fmt.Errorf("no CardModel subclasses found in %s", csPath)
	}

	for _, m := range locs {
		classStart := m[0]
		className := string(src[m[2]:m[3]])
		bodyStart, bodyEnd := classBodyRange(src, m[1])
		if bodyStart < 0 {
			continue
		}
		body := src[bodyStart:bodyEnd]

		ctor := constructorRE.FindSubmatch(body)
		if ctor == nil {
			continue
		}
		id := classToID(className)
		if _, ok := seen[id]; ok {
			continue
		}

		keywords := parseCanonicalKeywords(body)
		up := strings.ToUpper(id)
		desc := composeDescription(loc[up+".description"], keywords, keywordLoc)
		vars, upgradedVars := parseCardVars(body)

		meta := CardMeta{
			ID:           id,
			ClassName:    className,
			Character:    charByID[id],
			Cost:         normaliseCost(strings.TrimSpace(string(ctor[1]))),
			StarCost:     parseStarCost(body),
			Type:         strings.ToLower(string(ctor[2])),
			Rarity:       strings.ToLower(string(ctor[3])),
			Title:        loc[up+".title"],
			Description:  desc,
			Vars:         vars,
			UpgradedVars: upgradedVars,
		}
		seen[id] = meta
		_ = classStart
	}

	ids := make([]string, 0, len(seen))
	for id := range seen {
		ids = append(ids, id)
	}
	sort.Strings(ids)
	out := make([]CardMeta, 0, len(ids))
	for _, id := range ids {
		out = append(out, seen[id])
	}

	buf, err := json.MarshalIndent(out, "", "  ")
	if err != nil {
		return err
	}
	return os.WriteFile(outPath, buf, 0o644)
}

// classBodyRange finds the `{ … }` body that starts at or after startAt
// and returns (innerStart, innerEnd) — the byte offsets that bracket the
// body contents (excluding the braces themselves). Returns (-1, -1) if
// no balanced body is found.
func classBodyRange(src []byte, startAt int) (int, int) {
	open := -1
	for i := startAt; i < len(src); i++ {
		if src[i] == '{' {
			open = i
			break
		}
	}
	if open < 0 {
		return -1, -1
	}
	depth := 0
	for i := open; i < len(src); i++ {
		switch src[i] {
		case '{':
			depth++
		case '}':
			depth--
			if depth == 0 {
				return open + 1, i
			}
		}
	}
	return -1, -1
}

// parseStarCost reads the `CanonicalStarCost => N` override out of a card's
// class body. Returns 0 when the card doesn't override it (i.e. has no star
// cost) — the base CardModel returns -1, so only Regent star-costing cards
// carry a positive value here.
func parseStarCost(body []byte) int {
	m := canonicalStarCostRE.FindSubmatch(body)
	if m == nil {
		return 0
	}
	n, err := strconv.Atoi(string(m[1]))
	if err != nil {
		return 0
	}
	return n
}

// parseCanonicalKeywords pulls the CardKeyword.XXX identifiers out of a
// class body's CanonicalKeywords getter.
func parseCanonicalKeywords(body []byte) []string {
	m := canonicalKeywordsRE.FindSubmatch(body)
	if m == nil {
		return nil
	}
	section := m[1]
	if len(section) == 0 {
		section = m[2]
	}
	if len(section) == 0 {
		return nil
	}
	var out []string
	seen := map[string]bool{}
	for _, k := range cardKeywordRefRE.FindAllSubmatch(section, -1) {
		name := string(k[1])
		if !seen[name] {
			out = append(out, name)
			seen[name] = true
		}
	}
	return out
}

// composeDescription mirrors CardModel.GetDescriptionForPile in sts2.dll:
// before-description keywords are prepended as separate lines (each
// rendered as "[gold]Title[/gold]."), the main description sits in the
// middle, and after-description keywords are appended. Keywords not
// present on the card are skipped. When the main description is empty
// and there are no keywords we return "" so the UI can hide the block.
func composeDescription(main string, keywords []string, keywordLoc map[string]string) string {
	present := map[string]bool{}
	for _, k := range keywords {
		present[k] = true
	}

	// Render in display order: [Unplayable, Innate, Retain, Sly, Ethereal]
	// (the reverse of the iteration order in the game's Insert(0, …) loop).
	var lines []string
	for i := len(beforeDescriptionKeywords) - 1; i >= 0; i-- {
		kw := beforeDescriptionKeywords[i]
		if !present[kw] {
			continue
		}
		if line := keywordLine(kw, keywordLoc); line != "" {
			lines = append(lines, line)
		}
	}
	if main != "" {
		lines = append(lines, main)
	}
	for _, kw := range afterDescriptionKeywords {
		if !present[kw] {
			continue
		}
		if line := keywordLine(kw, keywordLoc); line != "" {
			lines = append(lines, line)
		}
	}
	return strings.Join(lines, "\n")
}

// keywordLine formats a single keyword line the way sts2.dll's
// CardKeyword.GetCardText does: "[gold]Title[/gold].". The "." is the
// "PERIOD" entry in card_keywords.json (always "." in English).
func keywordLine(keyword string, loc map[string]string) string {
	title := loc[strings.ToUpper(toSnakeLower(keyword))+".title"]
	if title == "" {
		return ""
	}
	period := loc["PERIOD"]
	if period == "" {
		period = "."
	}
	return "[gold]" + title + "[/gold]" + period
}

// toSnakeLower converts PascalCase to snake_case with the same rules as
// classToID. Kept private because it's only used for keyword loc keys
// (which in practice are all single-word, but be defensive).
func toSnakeLower(s string) string {
	var b strings.Builder
	for i, r := range s {
		if i > 0 && r >= 'A' && r <= 'Z' {
			b.WriteByte('_')
		}
		if r >= 'A' && r <= 'Z' {
			r += 'a' - 'A'
		}
		b.WriteRune(r)
	}
	return b.String()
}

// loadCardLocalization reads localization/eng/cards.json out of the pack.
func loadCardLocalization(p *pck.Pack) (map[string]string, error) {
	return loadLocJSON(p, "localization/eng/cards.json")
}

// loadKeywordLocalization reads localization/eng/card_keywords.json, which
// holds the "[gold]Ethereal[/gold]" / "[gold]Unplayable[/gold]" titles
// that get composed into curse/status card descriptions.
func loadKeywordLocalization(p *pck.Pack) (map[string]string, error) {
	return loadLocJSON(p, "localization/eng/card_keywords.json")
}

func loadLocJSON(p *pck.Pack, path string) (map[string]string, error) {
	raw, err := p.Read(path)
	if err != nil {
		return nil, err
	}
	var m map[string]string
	if err := json.Unmarshal(raw, &m); err != nil {
		return nil, err
	}
	return m, nil
}

// cardCharacters walks the pack for every `card_atlas.sprites/{char}/{id}.tres`
// — the directory-per-character organisation gives us the card→character
// mapping without parsing any C#. Returns a map id → character.
//
// Some cards only have sprites at nested paths (e.g.
// `card_atlas.sprites/ironclad/beta/spite.tres`). We prefer the flat
// mapping when both exist (so a `beta/` duplicate can't shadow the real
// card) and fall back to the nested path otherwise, so cards that only
// live under a subdir still get a character assignment.
func cardCharacters(p *pck.Pack) map[string]string {
	out := map[string]string{}
	nested := map[string]string{}
	const prefix = "images/atlases/card_atlas.sprites/"
	for _, f := range p.Files {
		if !strings.HasPrefix(f.Path, prefix) || !strings.HasSuffix(f.Path, ".tres") {
			continue
		}
		rest := strings.TrimSuffix(strings.TrimPrefix(f.Path, prefix), ".tres")
		parts := strings.Split(rest, "/")
		if len(parts) < 2 {
			continue
		}
		char := parts[0]
		id := parts[len(parts)-1]
		if len(parts) == 2 {
			if _, exists := out[id]; !exists {
				out[id] = char
			}
			continue
		}
		if _, exists := nested[id]; !exists {
			nested[id] = char
		}
	}
	for id, char := range nested {
		if _, exists := out[id]; !exists {
			out[id] = char
		}
	}
	return out
}

// classToID converts PascalCase class names to snake_case ids matching the
// portrait path (e.g. BattleTrance → battle_trance).
func classToID(class string) string {
	var b strings.Builder
	for i, r := range class {
		if i > 0 && r >= 'A' && r <= 'Z' {
			b.WriteByte('_')
		}
		if r >= 'A' && r <= 'Z' {
			r = r + ('a' - 'A')
		}
		b.WriteRune(r)
	}
	return b.String()
}

// normaliseCost reduces the first `base(...)` argument to a bare cost string.
// Typical values: "0", "1", "2", "3", "-2" (X cost sentinel in some games),
// or arbitrary C# expressions when the class uses computed costs. We keep
// integers verbatim and return "?" for anything else so the UI can fall
// back to rendering nothing in the orb.
func normaliseCost(raw string) string {
	raw = strings.TrimSpace(raw)
	// Literal integer (possibly negative).
	if isIntLiteral(raw) {
		return raw
	}
	return "?"
}

func isIntLiteral(s string) bool {
	if s == "" {
		return false
	}
	i := 0
	if s[0] == '-' || s[0] == '+' {
		i = 1
	}
	if i == len(s) {
		return false
	}
	for ; i < len(s); i++ {
		if s[i] < '0' || s[i] > '9' {
			return false
		}
	}
	return true
}
