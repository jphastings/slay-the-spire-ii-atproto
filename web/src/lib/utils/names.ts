let names: Record<string, string> | null = null;
let loading: Promise<void> | null = null;

async function load(): Promise<void> {
	const res = await fetch('/names.json');
	names = await res.json();
}

export async function ensureLoaded(): Promise<void> {
	if (names) return;
	if (!loading) loading = load();
	return loading;
}

export function displayName(id: string): string | undefined {
	return names?.[id];
}
