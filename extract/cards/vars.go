package cards

import (
	"regexp"
	"strconv"
	"strings"
)

// CardVar is a single DynamicVar entry extracted from a card's CanonicalVars.
// Name is the key the game uses when resolving `{Name:diff()}` placeholders in
// the card's description. Value is the numeric base value.
type CardVar struct {
	Name  string
	Value float64
}

// DynamicVars accessor → key name. The typed DynamicVarSet getters (e.g.
// DynamicVars.Vulnerable) return PowerVars whose base Name is typeof(T).Name,
// so the typed accessor name doesn't match the key name for powers.
var dynamicVarsAccessorToKey = map[string]string{
	"Block":            "Block",
	"Cards":            "Cards",
	"CalculatedBlock":  "CalculatedBlock",
	"CalculatedDamage": "CalculatedDamage",
	"CalculationBase":  "CalculationBase",
	"CalculationExtra": "CalculationExtra",
	"Damage":           "Damage",
	"Dexterity":        "DexterityPower",
	"Doom":             "DoomPower",
	"Energy":           "Energy",
	"ExtraDamage":      "ExtraDamage",
	"Forge":            "Forge",
	"Gold":             "Gold",
	"Heal":             "Heal",
	"HpLoss":           "HpLoss",
	"MaxHp":            "MaxHp",
	"OstyDamage":       "OstyDamage",
	"Poison":           "PoisonPower",
	"Repeat":           "Repeat",
	"Stars":            "Stars",
	"Strength":         "StrengthPower",
	"Summon":           "Summon",
	"Vulnerable":       "VulnerablePower",
	"Weak":             "WeakPower",
}

// DynamicVar subclass → implicit key name (for the no-explicit-name ctor).
// The base DynamicVar / IntVar / StringVar / BoolVar classes have no default
// name and are therefore not listed here. PowerVar<T> uses T's full name and
// is handled separately.
var varClassDefaultName = map[string]string{
	"BlockVar":            "Block",
	"CardsVar":            "Cards",
	"CalculationBaseVar":  "CalculationBase",
	"CalculationExtraVar": "CalculationExtra",
	"DamageVar":           "Damage",
	"EnergyVar":           "Energy",
	"ExtraDamageVar":      "ExtraDamage",
	"ForgeVar":            "Forge",
	"GoldVar":             "Gold",
	"HealVar":             "Heal",
	"HpLossVar":           "HpLoss",
	"MaxHpVar":            "MaxHp",
	"OstyDamageVar":       "OstyDamage",
	"RepeatVar":           "Repeat",
	"StarsVar":            "Stars",
	"SummonVar":           "Summon",
}

var (
	canonicalVarsRE = regexp.MustCompile(
		`CanonicalVars\s*=>\s*new\s+(?:global::)?(?:<>z__ReadOnlySingleElementList|<>z__ReadOnlyArray)<DynamicVar>\s*\(`,
	)
	newVarRE = regexp.MustCompile(
		`new\s+(DynamicVar|IntVar|PowerVar<([A-Za-z_][A-Za-z0-9_]*)>|([A-Za-z_][A-Za-z0-9_]*Var))\s*\(`,
	)
	upgradeAccessorRE = regexp.MustCompile(
		`DynamicVars\.([A-Za-z_][A-Za-z0-9_]*)\.UpgradeValueBy\(\s*(-?\d+(?:\.\d+)?)m?\s*\)`,
	)
	upgradeKeyedRE = regexp.MustCompile(
		`DynamicVars\[\s*"([A-Za-z_][A-Za-z0-9_]*)"\s*\]\.UpgradeValueBy\(\s*(-?\d+(?:\.\d+)?)m?\s*\)`,
	)
	onUpgradeRE = regexp.MustCompile(`(?s)protected override void OnUpgrade\(\)\s*\{`)
	// private int _foo = 42;  /  private const int _foo = 42;
	fieldInitRE = regexp.MustCompile(
		`(?m)^\s*(?:private|protected|public|internal)?\s*(?:const\s+)?(?:int|decimal|long|uint|short|byte|sbyte|float|double)\s+(_?\w+)\s*=\s*(-?\d+(?:\.\d+)?)(?:m|M|d|D|f|F|L|l|u|U)?\s*;`,
	)
	// Property header: `public <type> Name {` or `public <type> Name =>`.
	// We use this to find the start and then either walk the block body or
	// grab the expression-bodied form inline.
	propHeaderRE = regexp.MustCompile(
		`(?m)public\s+(?:virtual\s+|override\s+)?(?:int|decimal|long|uint|short|byte|float|double)\s+([A-Z]\w*)\s*(\{|=>)`,
	)
	returnBackingRE = regexp.MustCompile(`return\s+(_?\w+)\s*;`)
	exprBackingRE   = regexp.MustCompile(`^\s*(_?\w+)\s*;`)
	// Static MaxUpgradeLevel override so we can skip upgradedVars for un-upgradeable cards.
	maxUpgradeLevelRE = regexp.MustCompile(
		`public override int MaxUpgradeLevel\s*=>\s*(\d+)`,
	)
)

// parseCardVars returns the base and upgraded var maps for a single card's
// class body. upgraded is nil when the card cannot be upgraded. vars is nil
// when the card defines no CanonicalVars.
func parseCardVars(body []byte) (vars, upgraded map[string]float64) {
	src := string(body)
	canonical := extractCanonicalVarsLiteral(src)
	if canonical == "" {
		return nil, nil
	}

	constants := collectConstants(src)
	entries := parseVarLiteral(canonical, constants)
	if len(entries) == 0 {
		return nil, nil
	}

	base := map[string]float64{}
	for _, e := range entries {
		base[e.Name] = e.Value
	}

	// Upgradable? MaxUpgradeLevel defaults to 1; classes override to 0 to
	// opt out (curses, most quest cards).
	maxUp := 1
	if m := maxUpgradeLevelRE.FindStringSubmatch(src); m != nil {
		if v, err := strconv.Atoi(m[1]); err == nil {
			maxUp = v
		}
	}
	if maxUp <= 0 {
		return base, nil
	}

	deltas := extractUpgradeDeltas(src)
	up := map[string]float64{}
	for k, v := range base {
		up[k] = v
	}
	for name, d := range deltas {
		up[name] += d
	}
	// If the upgrade doesn't touch any var, omit upgradedVars.
	if len(deltas) == 0 {
		return base, nil
	}
	return base, up
}

// extractCanonicalVarsLiteral returns the source slice of the expression
// list passed to the CanonicalVars initializer — everything between the
// outermost `(` / `{` that opens the list and its matching close. Returns
// "" when no CanonicalVars override is present.
func extractCanonicalVarsLiteral(body string) string {
	m := canonicalVarsRE.FindStringIndex(body)
	if m == nil {
		return ""
	}
	// The regex ends just past the opening `(` of the wrapper list ctor.
	// For the single-element form the next non-whitespace is `new …Var(`
	// and we want the whole expression list up to the wrapper's closing
	// `)`. For the array form the very next chars are `new DynamicVar[N]{`
	// and we want the `{ … }` contents.
	i := m[1]
	inner, ok := readBalanced(body, i-1, '(', ')')
	if !ok {
		return ""
	}
	// Strip a leading `new DynamicVar[N] { … }` wrapper if present, keeping
	// only the contents of the braces.
	trim := strings.TrimSpace(inner)
	if strings.HasPrefix(trim, "new DynamicVar[") {
		brace := strings.IndexByte(trim, '{')
		if brace < 0 {
			return ""
		}
		inside, ok := readBalanced(trim, brace, '{', '}')
		if !ok {
			return ""
		}
		return inside
	}
	return inner
}

// readBalanced reads the substring inside a pair of open/close characters
// starting at openIdx (which must point at the opener). It returns the
// interior, not including the delimiters themselves, and respects nested
// pairs of the same delimiters.
func readBalanced(s string, openIdx int, open, close byte) (string, bool) {
	if openIdx < 0 || openIdx >= len(s) || s[openIdx] != open {
		return "", false
	}
	depth := 0
	for i := openIdx; i < len(s); i++ {
		c := s[i]
		switch c {
		case open:
			depth++
		case close:
			depth--
			if depth == 0 {
				return s[openIdx+1 : i], true
			}
		}
	}
	return "", false
}

// parseVarLiteral walks a string containing one or more `new …Var(…)` ctor
// calls separated by commas and returns them as (name, value) entries in
// declaration order.
func parseVarLiteral(src string, constants map[string]float64) []CardVar {
	var out []CardVar
	for i := 0; i < len(src); {
		m := newVarRE.FindStringSubmatchIndex(src[i:])
		if m == nil {
			break
		}
		match := src[i+m[0] : i+m[1]]
		className := strings.TrimSpace(strings.TrimSuffix(match[len("new "):], "("))
		className = strings.TrimSpace(className)
		// Ctor argument list starts at the `(` at m[1]-1 (in sub-slice coords)
		// which is i+m[1]-1 in full-string coords.
		argStart := i + m[1] - 1
		args, ok := readBalanced(src, argStart, '(', ')')
		if !ok {
			break
		}
		i = argStart + len(args) + 2 // past the closing `)`

		// `new DynamicVar[…]` array openings show up as new DynamicVar — skip
		// them: those aren't DynamicVar instantiations, they're the outer
		// array wrapper the caller has already unwrapped.
		if strings.HasPrefix(className, "DynamicVar[") {
			continue
		}

		cv, ok := parseVarCtor(className, args, constants)
		if ok {
			out = append(out, cv)
		}
	}
	return out
}

// parseVarCtor extracts the (name, value) pair for a single DynamicVar-family
// ctor call given the class name and the raw argument list.
func parseVarCtor(className, args string, constants map[string]float64) (CardVar, bool) {
	parts := splitTopLevelArgs(args)
	if len(parts) == 0 {
		return CardVar{}, false
	}
	first := strings.TrimSpace(parts[0])

	// Detect explicit-name form: the first arg is a string literal.
	if strings.HasPrefix(first, "\"") && len(parts) >= 2 {
		name := strings.Trim(first, "\"")
		val, ok := resolveValue(strings.TrimSpace(parts[1]), constants)
		if !ok {
			return CardVar{}, false
		}
		return CardVar{Name: name, Value: val}, true
	}

	// StringVar / BoolVar / base DynamicVar without a numeric argument —
	// ignore (no placeholder number to display).
	switch className {
	case "DynamicVar", "IntVar":
		// These must use the explicit-name form; if we got here without a
		// string literal, we can't interpret the value.
		return CardVar{}, false
	case "StringVar", "BoolVar":
		return CardVar{}, false
	}

	// PowerVar<T> implicit name = T
	if strings.HasPrefix(className, "PowerVar<") {
		typeParam := strings.TrimSuffix(strings.TrimPrefix(className, "PowerVar<"), ">")
		val, ok := resolveValue(first, constants)
		if !ok {
			return CardVar{}, false
		}
		return CardVar{Name: typeParam, Value: val}, true
	}

	// Calculated*Var takes only a ValueProp, no numeric value.
	if className == "CalculatedBlockVar" || className == "CalculatedDamageVar" {
		return CardVar{}, false
	}

	name, ok := varClassDefaultName[className]
	if !ok {
		return CardVar{}, false
	}
	val, ok := resolveValue(first, constants)
	if !ok {
		return CardVar{}, false
	}
	return CardVar{Name: name, Value: val}, true
}

// resolveValue parses a numeric literal or resolves an identifier to its
// constant value via the given lookup table.
func resolveValue(expr string, constants map[string]float64) (float64, bool) {
	expr = strings.TrimSpace(expr)
	expr = strings.TrimSuffix(expr, "m")
	expr = strings.TrimSuffix(expr, "M")
	if v, err := strconv.ParseFloat(expr, 64); err == nil {
		return v, true
	}
	// Identifier reference to a backing field / public property.
	if v, ok := constants[expr]; ok {
		return v, true
	}
	return 0, false
}

// collectConstants builds a name→value lookup from a class body containing
// field/const initializers and simple getter-returns-field properties. It
// returns both field names (with any leading underscore) and public property
// names that resolve back to those fields.
func collectConstants(body string) map[string]float64 {
	out := map[string]float64{}
	for _, m := range fieldInitRE.FindAllStringSubmatch(body, -1) {
		name := m[1]
		v, err := strconv.ParseFloat(m[2], 64)
		if err != nil {
			continue
		}
		out[name] = v
	}
	// Resolve property getters that just return a backing field.
	for _, idx := range propHeaderRE.FindAllStringSubmatchIndex(body, -1) {
		propName := body[idx[2]:idx[3]]
		kind := body[idx[4]:idx[5]] // "{" or "=>"
		var backing string
		if kind == "{" {
			inside, ok := readBalanced(body, idx[5]-1, '{', '}')
			if !ok {
				continue
			}
			m := returnBackingRE.FindStringSubmatch(inside)
			if m == nil {
				continue
			}
			backing = m[1]
		} else {
			m := exprBackingRE.FindStringSubmatch(body[idx[5]:])
			if m == nil {
				continue
			}
			backing = m[1]
		}
		if v, ok := out[backing]; ok {
			out[propName] = v
		}
	}
	return out
}

// extractUpgradeDeltas walks the OnUpgrade method body (if any) and returns a
// map of var-key → accumulated delta. Un-parseable calls are skipped.
func extractUpgradeDeltas(body string) map[string]float64 {
	m := onUpgradeRE.FindStringIndex(body)
	if m == nil {
		return nil
	}
	braceAt := m[1] - 1
	if braceAt < 0 || braceAt >= len(body) || body[braceAt] != '{' {
		return nil
	}
	inner, ok := readBalanced(body, braceAt, '{', '}')
	if !ok {
		return nil
	}
	deltas := map[string]float64{}
	for _, mm := range upgradeAccessorRE.FindAllStringSubmatch(inner, -1) {
		key, ok := dynamicVarsAccessorToKey[mm[1]]
		if !ok {
			continue
		}
		if v, err := strconv.ParseFloat(mm[2], 64); err == nil {
			deltas[key] += v
		}
	}
	for _, mm := range upgradeKeyedRE.FindAllStringSubmatch(inner, -1) {
		if v, err := strconv.ParseFloat(mm[2], 64); err == nil {
			deltas[mm[1]] += v
		}
	}
	return deltas
}

// splitTopLevelArgs splits a C# argument list on commas at nesting depth 0.
func splitTopLevelArgs(args string) []string {
	var parts []string
	depth := 0
	start := 0
	inStr := false
	for i := 0; i < len(args); i++ {
		c := args[i]
		if inStr {
			if c == '"' && args[i-1] != '\\' {
				inStr = false
			}
			continue
		}
		switch c {
		case '"':
			inStr = true
		case '(', '[', '{', '<':
			depth++
		case ')', ']', '}', '>':
			depth--
		case ',':
			if depth == 0 {
				parts = append(parts, args[start:i])
				start = i + 1
			}
		}
	}
	parts = append(parts, args[start:])
	return parts
}
