import { describe, it, expect } from 'vitest';
import { parseCardText, type ParseOptions } from './cardtext';

// Flatten the parsed runs back to plain text for behavioural assertions.
const render = (desc: string, opts: ParseOptions = {}): string =>
	parseCardText(desc, opts)
		.map((line) => line.map((run) => run.text).join(''))
		.join('\n')
		.trim();

describe('parseCardText grammar', () => {
	it('renders a value-bearing leaf as the number, and a missing one as "?"', () => {
		expect(render('Deal {Damage:diff()} damage.', { values: { Damage: 9 } })).toBe('Deal 9 damage.');
		expect(render('Deal {Damage:diff()} damage.')).toBe('Deal ? damage.');
	});

	it('styles [gold] spans and placeholders as highlights', () => {
		const runs = parseCardText('Gain {Block:diff()} [gold]Block[/gold].', {
			values: { Block: 5 }
		}).flat();
		expect(runs.find((r) => r.text === '5')?.style).toBe('highlight');
		expect(runs.find((r) => r.text === 'Block')?.style).toBe('highlight');
	});

	it('takes the IfUpgraded branch from opts.upgraded and the else branch for InCombat', () => {
		expect(render('{IfUpgraded:strong|weak}', { upgraded: true })).toBe('strong');
		expect(render('{IfUpgraded:strong|weak}', { upgraded: false })).toBe('weak');
		expect(render('{InCombat:fighting|resting}')).toBe('resting');
	});

	it('renders an inline icon token as an icon run', () => {
		const icon = parseCardText('spend {singleStarIcon}')
			.flat()
			.find((r) => r.style === 'icon');
		expect(icon?.icon).toBe('star_icon');
	});
});

describe('choose(...) selector', () => {
	const desc = '{X:choose(a|b|c):First|Second|Third}';

	it('picks the branch matching the recorded value', () => {
		expect(render(desc, { values: { X: 'b' } })).toBe('Second');
		expect(render(desc, { values: { X: 'c' } })).toBe('Third');
	});

	it('accepts a numeric index too', () => {
		expect(render(desc, { values: { X: 2 } })).toBe('Third');
	});

	it('falls back to the first branch when the value is missing or unknown', () => {
		expect(render(desc)).toBe('First');
		expect(render(desc, { values: { X: 'zzz' } })).toBe('First');
	});

	it('does not split on "|" inside the choose(...) option list', () => {
		// Regression: the option list itself must not leak into the output.
		expect(render(desc, { values: { X: 'a' } })).toBe('First');
	});
});

describe('value-driven boolean conditionals', () => {
	it('honours a recorded flag, hiding the branch when false', () => {
		expect(render('{Flag:shown|}', { values: { Flag: 1 } })).toBe('shown');
		expect(render('{Flag:shown|}', { values: { Flag: 0 } })).toBe('');
	});

	it('falls back to the non-empty branch when no value is recorded', () => {
		expect(render('{Flag:shown|}')).toBe('shown');
	});
});

// The card that motivated all of the above: Mad Science has a chosen CardType
// (Attack/Skill/Power) plus one rider effect, recorded per-instance. Only the
// chosen base + rider should render.
describe('Mad Science', () => {
	const MAD =
		'{CardType:choose(Attack|Skill|Power):Deal {Damage:diff()} damage{Violence: {ViolenceHits:diff()} times|}.|Gain {Block:diff()} [gold]Block[/gold].|}{HasRider:{Sapping:\\nApply {SappingWeak:diff()} [gold]Weak[/gold].\\nApply {SappingVulnerable:diff()} [gold]Vulnerable[/gold].|}{Choking:\\nWhenever you play a card this turn, the enemy loses {ChokingDamage:diff()} HP.|}{Energized:\\nGain {EnergizedEnergy:energyIcons()}.|}{Wisdom:\\nDraw {WisdomCards:diff()} cards.|}{Chaos:\\nAdd a random card into your [gold]Hand[/gold]. It\'s free to play this turn.|}{Expertise:Gain {ExpertiseStrength:diff()} [gold]Strength[/gold].\\nGain {ExpertiseDexterity:diff()} [gold]Dexterity[/gold].|}{Curious:Powers cost {CuriousReduction:diff()} {energyPrefix:energyIcons(1)} less.|}{Improvement:At the end of combat, [gold]Upgrade[/gold] a random card.|}|{CardType:choose(Attack|Skill|Power):\\n???|\\n???|???}}';

	const RIDERS = [
		'Sapping',
		'Violence',
		'Choking',
		'Energized',
		'Wisdom',
		'Chaos',
		'Expertise',
		'Curious',
		'Improvement'
	];

	// Mirror the mod's captured state: static vars + chosen CardType + a 1/0
	// flag per rider (exactly what AddExtraArgsToDescription emits).
	function values(type: string, rider: string): Record<string, number | string> {
		const v: Record<string, number | string> = {
			Damage: 12,
			Block: 8,
			SappingWeak: 2,
			SappingVulnerable: 2,
			ViolenceHits: 3,
			ChokingDamage: 6,
			WisdomCards: 3,
			ExpertiseStrength: 2,
			ExpertiseDexterity: 2,
			CardType: type,
			HasRider: rider === 'None' ? 0 : 1
		};
		for (const r of RIDERS) v[r] = r === rider ? 1 : 0;
		return v;
	}

	const render2 = (type: string, rider: string) => render(MAD, { values: values(type, rider) });

	it('renders the chosen attack with its rider', () => {
		expect(render2('Attack', 'Violence')).toBe('Deal 12 damage 3 times.');
		expect(render2('Attack', 'Choking')).toBe(
			'Deal 12 damage.\nWhenever you play a card this turn, the enemy loses 6 HP.'
		);
	});

	it('renders the chosen skill base with its rider', () => {
		expect(render2('Skill', 'Sapping')).toBe('Gain 8 Block.\nApply 2 Weak.\nApply 2 Vulnerable.');
	});

	it('renders a power whose base effect is the rider alone', () => {
		expect(render2('Power', 'Improvement')).toBe('At the end of combat, Upgrade a random card.');
	});

	it('shows only the chosen rider, never the others', () => {
		const out = render2('Skill', 'Sapping');
		expect(out).not.toContain('Draw');
		expect(out).not.toContain('Strength');
		expect(out).not.toContain('???');
	});

	it('falls back to showing every branch when no choice was recorded', () => {
		// Old records predate choice capture; degrade to the full template
		// rather than a blank card.
		const out = render(MAD, { values: { Damage: 12, Block: 8 } });
		expect(out).toContain('Deal 12 damage');
		expect(out).toContain('Draw');
		expect(out).toContain('Upgrade');
	});
});
