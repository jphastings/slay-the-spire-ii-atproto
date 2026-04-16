export function formatDuration(seconds: number): string {
	const h = Math.floor(seconds / 3600);
	const m = Math.floor((seconds % 3600) / 60);
	const s = seconds % 60;
	if (h > 0) return `${h}h ${m}m ${s}s`;
	if (m > 0) return `${m}m ${s}s`;
	return `${s}s`;
}

const rtf = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });
const units: [Intl.RelativeTimeFormatUnit, number][] = [
	['year', 365 * 24 * 60 * 60 * 1000],
	['month', 30 * 24 * 60 * 60 * 1000],
	['week', 7 * 24 * 60 * 60 * 1000],
	['day', 24 * 60 * 60 * 1000],
	['hour', 60 * 60 * 1000],
	['minute', 60 * 1000],
	['second', 1000]
];

export function formatRelativeTime(iso: string): string {
	const diff = new Date(iso).getTime() - Date.now();
	for (const [unit, ms] of units) {
		if (Math.abs(diff) >= ms) {
			return rtf.format(Math.round(diff / ms), unit);
		}
	}
	return 'just now';
}

export function humanizeId(id: string): string {
	// Strip namespace prefix (e.g. "character.ironclad" → "ironclad")
	const name = id.includes('.') ? id.slice(id.indexOf('.') + 1) : id;
	return name
		.replace(/[._]/g, ' ')
		.replace(/\b\w/g, (c) => c.toUpperCase());
}

export function formatAscension(level: number): string {
	return `A${level}`;
}
