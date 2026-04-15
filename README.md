# sts2.at

A [Slay the Spire 2](https://store.steampowered.com/app/2868840/) mod that posts end-of-run summaries to your [atproto](https://atproto.com) PDS.

Runs are stored as `at.sts2.run` records. A rolling `games.gamesgamesgamesgames.actor.stats` record is updated alongside each run so the game shows up in [HappyView](https://github.com/gamesgamesgamesgamesgames/happyview)-style game-catalog apps.

## Status

Early. The run-end Harmony hook is pinned (`MegaCrit.Sts2.Core.Runs.RunManager.OnEnded`) and the mod builds clean. Run-state extraction uses reflection for tolerance against Early Access patch drift, so some deck/relic id field names may need fine-tuning on first publish.

## Install (no build required)

Drop the latest release zip into your game's mods folder (see [Platforms](#platforms)), then launch the game once. The mod will create `config.json` next to its DLL; edit it and restart:

```json
{
  "pdsUrl": "https://bsky.social",
  "handle": "you.example.com",
  "appPassword": "xxxx-xxxx-xxxx-xxxx",
  "gameRef": "at://did:web:gamesgamesgamesgames.games/games.gamesgamesgamesgames.game/3mglj4k2edl2l"
}
```

`gameRef` defaults to the canonical StS2 `actor.game` record published under `did:web:gamesgamesgamesgames.games`. Set it to empty string to skip the stats record update.

App passwords live at [bsky.app/settings/app-passwords](https://bsky.app/settings/app-passwords). The mod caches the rkey of your `actor.stats` record after first run, so subsequent runs update that same record instead of creating new ones.

## Build

Prerequisite: [.NET SDK 9+](https://dotnet.microsoft.com/download). The build references `sts2.dll` and `0Harmony.dll` directly from your game install — no need to copy them.

```sh
# Build only
dotnet build mod/sts2.at.csproj -c Release

# Build and install into the game's mods/sts2.at/ directory
dotnet build mod/sts2.at.csproj -c Release -t:Install
```

The csproj auto-detects the default Steam install path on macOS, Linux, and Windows. If your install is elsewhere (e.g. a non-default Steam library), copy [mod/local.props.template](mod/local.props.template) to `mod/local.props` and set `Sts2DataDir` and `ModsPath`.

## Layout

- [lexicons/](lexicons/) — atproto lexicon JSON for `at.sts2.run`
- [mod/](mod/) — C# mod project (Godot .NET / HarmonyX, targets .NET 9)

## Platforms

One DLL, all platforms. Install path differs:

- **Windows**: `…/Slay the Spire 2/mods/sts2.at/`
- **macOS**: `…/SlayTheSpire2.app/Contents/MacOS/mods/sts2.at/` — the loader only searches inside the `.app` bundle, so installing here invalidates the app's code signature. Steam-installed builds still launch fine, but Gatekeeper may complain on first run.
- **Linux / Steam Deck**: `…/Slay the Spire 2/mods/sts2.at/`

## Credits

- Mod loader conventions and starter code from [jiegec/STS2FirstMod](https://github.com/jiegec/STS2FirstMod)
- Run-state field accessors inspired by [Gennadiyev/STS2MCP](https://github.com/Gennadiyev/STS2MCP)
- Hook catalogue via [elliotttate/sts2-modding-mcp](https://github.com/elliotttate/sts2-modding-mcp)
- `games.gamesgamesgamesgames.*` lexicons by [Birbhouse Games](https://birb.house)
