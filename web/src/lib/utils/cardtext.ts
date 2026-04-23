// Parses Slay the Spire 2 card description strings into typed runs for
// rich-text rendering. The grammar is richer than the v1 stub: beyond
// BBCode `[gold]…[/gold]` and leaf placeholders `{Field:diff()}`, it
// supports conditional branches and nested expansions:
//
//   {IfUpgraded:show:UPGRADED|BASE}       bool, keyed off opts.upgraded
//   {InCombat:IN_COMBAT|OUT_OF_COMBAT}   implicit show: on any boolean field
//   {Field:plural:SINGULAR|PLURAL}        default to PLURAL (count unknown)
//   {Field:cond:>N?TRUE|FALSE}            default to TRUE (common case)
//   {Field:diff()} / {Field:diff(N)}      leaf placeholder, renders "?"
//   {Field:energyIcons()} / (N)           leaf placeholder, renders "?"
//
// Branches may embed BBCode and further placeholders; brace matching is
// balanced so nested `{}` / `[]` don't trip up the splitter.

export type RunStyle = 'normal' | 'highlight' | 'placeholder';

export interface Run {
	text: string;
	style: RunStyle;
	field?: string;
}

export type Line = Run[];

export interface ParseOptions {
	upgraded?: boolean;
}

export function parseCardText(desc: string, opts: ParseOptions = {}): Line[] {
	const lines: Line[] = [];
	let cur: Line = [];
	let buf = '';
	let style: RunStyle = 'normal';

	const flush = () => {
		if (buf.length > 0) {
			cur.push({ text: buf, style });
			buf = '';
		}
	};
	const newline = () => {
		flush();
		lines.push(cur);
		cur = [];
	};

	const process = (s: string) => {
		for (let i = 0; i < s.length; ) {
			const c = s[i];
			if (c === '\\' && s[i + 1] === 'n') {
				newline();
				i += 2;
				continue;
			}
			if (c === '\n') {
				newline();
				i++;
				continue;
			}
			if (c === '[') {
				const end = s.indexOf(']', i);
				if (end < 0) {
					buf += c;
					i++;
					continue;
				}
				const tag = s.slice(i + 1, end);
				flush();
				if (tag === 'gold') style = 'highlight';
				else if (tag === '/gold') style = 'normal';
				i = end + 1;
				continue;
			}
			if (c === '{') {
				const end = findMatchingClose(s, i);
				if (end < 0) {
					buf += c;
					i++;
					continue;
				}
				const inner = s.slice(i + 1, end);
				const ev = evaluatePlaceholder(inner, opts);
				if (ev === null) {
					const field = inner.slice(0, inner.indexOf(':') === -1 ? inner.length : inner.indexOf(':'));
					flush();
					cur.push({ text: '?', style: 'placeholder', field });
				} else {
					// Recursively process the expanded branch so nested
					// placeholders and BBCode keep working.
					process(ev);
				}
				i = end + 1;
				continue;
			}
			buf += c;
			i++;
		}
	};

	process(desc);
	flush();
	if (cur.length > 0 || lines.length === 0) lines.push(cur);
	return lines;
}

// evaluatePlaceholder returns the expanded branch text for conditional /
// plural / cond forms, or null when the placeholder is a leaf (diff /
// energyIcons / unknown) and should render as a `?` chip instead.
function evaluatePlaceholder(inner: string, opts: ParseOptions): string | null {
	const colon1 = inner.indexOf(':');
	if (colon1 < 0) return null; // bare {Field}
	const field = inner.slice(0, colon1);
	const rest = inner.slice(colon1 + 1);

	// Function-call leaves: diff(), diff(N), energyIcons(), energyIcons(N).
	if (/^\w+\(\d*\)$/.test(rest)) return null;

	// Named operator: plural / show / cond followed by ':'
	const colon2 = rest.indexOf(':');
	const kindWords = ['plural', 'show', 'cond'];
	if (colon2 > 0 && kindWords.includes(rest.slice(0, colon2))) {
		const kind = rest.slice(0, colon2);
		const args = rest.slice(colon2 + 1);
		return applyOp(field, kind, args, opts);
	}

	// Implicit boolean conditional: `{InCombat:TRUE|FALSE}`.
	return applyOp(field, 'show', rest, opts);
}

function applyOp(field: string, kind: string, args: string, opts: ParseOptions): string {
	if (kind === 'show') {
		const branches = splitBranches(args);
		if (field === 'IfUpgraded') {
			return opts.upgraded ? (branches[0] ?? '') : (branches[1] ?? '');
		}
		if (field === 'InCombat') {
			// Deck viewer is never in combat, so always take the else branch.
			return branches[1] ?? '';
		}
		// Unknown boolean — prefer the non-empty branch so cards read well.
		return branches.find((b) => b.length > 0) ?? '';
	}
	if (kind === 'plural') {
		// plural:SINGULAR|PLURAL — count unknown, default to PLURAL.
		const branches = splitBranches(args);
		return branches[1] ?? branches[0] ?? '';
	}
	if (kind === 'cond') {
		// cond:COND?TRUE|FALSE — default to TRUE (the common "active" branch).
		const q = findTopLevel(args, '?');
		if (q < 0) return args;
		const branches = splitBranches(args.slice(q + 1));
		return branches[0] ?? '';
	}
	return '';
}

// splitBranches splits `a|b|c` on `|`, respecting nested `{}` and `[]`.
function splitBranches(args: string): string[] {
	const parts: string[] = [];
	let brace = 0;
	let brack = 0;
	let start = 0;
	for (let i = 0; i < args.length; i++) {
		const c = args[i];
		if (c === '{') brace++;
		else if (c === '}') brace--;
		else if (c === '[') brack++;
		else if (c === ']') brack--;
		else if (c === '|' && brace === 0 && brack === 0) {
			parts.push(args.slice(start, i));
			start = i + 1;
		}
	}
	parts.push(args.slice(start));
	return parts;
}

// findTopLevel returns the index of ch at brace/bracket depth 0, or -1.
function findTopLevel(s: string, ch: string): number {
	let brace = 0;
	let brack = 0;
	for (let i = 0; i < s.length; i++) {
		const c = s[i];
		if (c === '{') brace++;
		else if (c === '}') brace--;
		else if (c === '[') brack++;
		else if (c === ']') brack--;
		else if (c === ch && brace === 0 && brack === 0) return i;
	}
	return -1;
}

// findMatchingClose returns the index of the `}` that closes the `{` at
// openIdx, or -1 if unbalanced.
function findMatchingClose(s: string, openIdx: number): number {
	let depth = 0;
	for (let i = openIdx; i < s.length; i++) {
		const c = s[i];
		if (c === '{') depth++;
		else if (c === '}') {
			depth--;
			if (depth === 0) return i;
		}
	}
	return -1;
}
