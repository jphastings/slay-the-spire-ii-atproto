# CLAUDE.md

Notes for future agents working in this repo.

## Project shape

Three moving parts that all describe the same "run record":

- `mod/` — C# mod for Slay the Spire 2. Uses Harmony patches on `RunManager.Launch` / `OnEnded` and subscribes to `SaveManager.Saved` to emit run records to the player's atproto PDS. Runs inside the game's Godot.NET runtime.
- `lexicons/me/byjp/pesos/sts2/run.json` — canonical atproto lexicon.
- `web/` — SvelteKit site (`adapter-static`, no SSR). Reads records from PDSes and renders them.

Data flow: **mod writes → user's PDS → web reads**. All clients are peers of the atproto network; there is no central server.

## Build

- **Mod**: `dotnet build /p:Sts2DataDir=mod/refs` from repo root. `mod/refs/sts2.dll` and `0Harmony.dll` are checked in so builds work without the game installed. `mod/local.props` may hardcode an external-drive path that isn't mounted — the `/p:…` override bypasses that.
- **Web**: `cd web && pnpm build`. Dev server: `pnpm dev` (port 5173).

## Decompile `sts2.dll`

```bash
DOTNET_ROLL_FORWARD=LatestMajor ~/.dotnet/tools/ilspycmd mod/refs/sts2.dll -o /tmp/sts2-decomp
```

The roll-forward env var is mandatory — `ilspycmd` is pinned to .NET 8 but only .NET 9 is installed on this machine.

## Schema co-location

Three sources of truth for the run shape. **Change one, change all three:**

1. `lexicons/me/byjp/pesos/sts2/run.json` — external atproto lexicon
2. `web/src/lib/lexicons/sts2-run.ts` — drives web TS types (via `prototypey`)
3. `mod/RunRecord.cs` — drives mod emission

Shape changes are **soft-breaking**: old records stay in PDSes with the old shape. Web code must treat every field as optional and handle missing data gracefully.

`web/static/names.json` is generated from game localization JSON via `web/scripts/build-names.ts`. Re-run when the user drops fresh localization files.

## Version bumps

Two files move together: `mod/atproto-tracker.csproj` (`<Version>`) and `mod/manifest.json` (`"version"`). The csproj has an MSBuild target that stamps the manifest on build, but it only triggers when the csproj `<Version>` changes — so bump both.

## Identity + avatar resolution (web)

For a player `{ steam?, atproto? }`, `resolvePlayer` in `web/src/lib/utils/player.ts`:

- **Avatar priority**: `dev.keytrace.claim.avatarUrl` on their PDS → Steam XML `avatarMedium` → `app.bsky.actor.profile/self` blob (CDN URL: `https://cdn.bsky.app/img/avatar_thumbnail/plain/{did}/{cid}@jpeg`). First hit wins.
- **Steam display name priority**: XML `<steamID>` (display name) → `<customURL>`. `realName` is deliberately excluded for privacy.
- **Link target**: companion run at the same tid on their PDS → their profile page `/{handle}` (when `preferLocal` or no Steam) → external Steam profile.

Caches (see `web/src/lib/utils/cache.ts`): Slingshot identity 30 min, companion record 5 min, Steam info 24 h, keytrace/Bluesky avatars 24 h.

## CORS

The site is fully static, so any third-party API without CORS headers must be proxied client-side. We use `corsproxy.io` for the Steam XML profile endpoint and (via `SteamDidResolver` on the mod side) directly for keytrace. Each PDS sends CORS headers, so direct `fetch` works for atproto calls.

## Temporary code

- `web/src/lib/utils/player.ts` / `mod/SteamDidResolver.cs` both have a 404-guard around the keytrace reverseLookup endpoint because `keytrace.dev/xrpc/dev.keytrace.reverseLookup` isn't publicly deployed yet. Marked with `TODO` in the source — remove once it ships.

## Testing multiplayer UI without a real MP run

The `allies` array is only populated in co-op runs. To eyeball the ally-column layout without starting a multiplayer session, inject fake allies via a chrome-devtools `initScript` that patches `window.fetch` to mutate the `getRecord` response body. Example:

```js
const origFetch = window.fetch;
window.fetch = async (...args) => {
  const url = typeof args[0] === 'string' ? args[0] : args[0].url;
  const res = await origFetch(...args);
  if (url?.includes('com.atproto.repo.getRecord') && url.includes('me.byjp.pesos.sts2.run')) {
    const body = await res.clone().json();
    body.value.allies = [
      { steam: '76561197960265729' },
      { steam: '76561197994000231', atproto: 'did:plc:ephkzpinhaqcabtkugtbzrwu' }
    ];
    return new Response(JSON.stringify(body), { status: res.status, headers: res.headers });
  }
  return res;
};
```

Cheap, leaves no code trace, and survives cross-navigation.

## Mod event surface

A prior exploration catalogued the Harmony-patchable events on the game. Useful when extending tracking:

- `RunManager`: `RunStarted`, `RoomEntered`, `RoomExited`, `ActEntered`, `Launch()`, `OnEnded()`.
- `CombatManager`: `CombatSetUp`, `CombatWon`, `CombatEnded`, `TurnStarted`, `TurnEnded`, `PlayerEndedTurn`, `AboutToSwitchToEnemyTurn`.
- `Player`: `RelicObtained`/`Removed`, `PotionProcured`/`Discarded`, `GoldChanged`, `MaxPotionCountChanged`.
- `Creature`: `CurrentHpChanged`, `MaxHpChanged`, `BlockChanged`, `PowerApplied`/`Increased`/`Decreased`/`Removed`, `Died`, `Revived`.
- `CardModel`: `Played`, `Drawn`, `Upgraded`, `Forged`, `EnergyCostChanged`, etc.
- `SaveManager.Instance.Saved` (event) — fires after every autosave; we use this as our "state settled" signal.

`RunManager.Instance.DebugOnlyGetState()` returns the live `RunState` and is safe to call during `OnEnded` (the serialized payload on its own doesn't carry everything the live state does — e.g., current deck card props).
