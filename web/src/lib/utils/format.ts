export function formatDuration(seconds: number): string {
	const h = Math.floor(seconds / 3600);
	const m = Math.floor((seconds % 3600) / 60);
	const s = seconds % 60;
	if (h > 0) return `${h}h ${m}m ${s}s`;
	if (m > 0) return `${m}m ${s}s`;
	return `${s}s`;
}

export function formatRelativeTime(iso: string): string {
	const then = new Date(iso);
	const now = new Date();
	const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
	const startOfThen = new Date(then.getFullYear(), then.getMonth(), then.getDate());
	const msPerDay = 24 * 60 * 60 * 1000;
	const dayDiff = Math.round((startOfToday.getTime() - startOfThen.getTime()) / msPerDay);

	if (dayDiff <= 0) return 'Today';
	if (dayDiff === 1) return 'Yesterday';

	// Week starts Monday.
	const weekdayFromMonday = (startOfToday.getDay() + 6) % 7;
	const startOfThisWeek = new Date(startOfToday);
	startOfThisWeek.setDate(startOfToday.getDate() - weekdayFromMonday);
	const startOfLastWeek = new Date(startOfThisWeek);
	startOfLastWeek.setDate(startOfThisWeek.getDate() - 7);

	if (startOfThen >= startOfThisWeek) return 'This week';
	if (startOfThen >= startOfLastWeek) return 'Last week';

	const startOfThisMonth = new Date(now.getFullYear(), now.getMonth(), 1);
	const startOfLastMonth = new Date(now.getFullYear(), now.getMonth() - 1, 1);
	if (startOfThen >= startOfThisMonth) return 'This month';
	if (startOfThen >= startOfLastMonth) return 'Last month';

	const startOfThisYear = new Date(now.getFullYear(), 0, 1);
	const startOfLastYear = new Date(now.getFullYear() - 1, 0, 1);
	if (startOfThen >= startOfThisYear) return 'This year';
	if (startOfThen >= startOfLastYear) return 'Last year';

	return 'A long while ago';
}

import { displayName } from './names';

export function humanizeId(id: string): string {
	// Try the names lookup first (loaded from /names.json)
	const known = displayName(id);
	if (known) return known;

	// Fallback: strip namespace prefix, lowercase, then title-case each word.
	// e.g. 'CARD.LEADING_STRIKE' → 'Leading Strike'
	const name = id.includes('.') ? id.slice(id.indexOf('.') + 1) : id;
	return name
		.toLowerCase()
		.replace(/[._]/g, ' ')
		.replace(/\b\w/g, (c) => c.toUpperCase());
}

export function formatAscension(level: number): string {
	return `A${level}`;
}
