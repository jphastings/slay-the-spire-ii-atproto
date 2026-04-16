# Slay the Spire 2: AT Protocol Tracker

A [Slay the Spire 2](https://store.steampowered.com/app/2868840/) mod that posts end-of-run summaries to your [atproto](https://atproto.com) PDS.

Runs are stored as `me.byjp.pesos.sts2.run` records. A rolling `games.gamesgamesgamesgames.actor.stats` record is updated alongside each run so the game shows up in [HappyView](https://github.com/gamesgamesgamesgamesgames/happyview)-style game-catalog apps.

## Status

Early. The run-end Harmony hook is pinned (`MegaCrit.Sts2.Core.Runs.RunManager.OnEnded`) and the mod builds clean. Run-state extraction uses reflection for tolerance against Early Access patch drift, so some deck/relic id field names may need fine-tuning on first publish.

## Install (no build required)

1. Unzip the latest release into your game's mods folder (see [Platforms](#platforms)) so that `atproto-tracker.dll` ends up at `…/mods/atproto-tracker/atproto-tracker.dll`.
2. Create a `config.json` in that same folder with your handle (or DID) and an app password:

   ```json
   {
     "handle": "you.example.com",
     "appPassword": "xxxx-xxxx-xxxx-xxxx"
   }
   ```

   The mod resolves your PDS automatically via [Slingshot](https://slingshot.microcosm.blue/) at startup and authenticates immediately, so any credential problem surfaces before your first run. The main menu shows a small `@` in the bottom-right corner: green when authenticated, red (with a strike-through) when credentials are missing or invalid — click it for details.

   If you skip this step, the mod will write a template `config.json` next to the DLL on first launch — just fill it in and restart the game. App passwords are created at [bsky.app/settings/app-passwords](https://bsky.app/settings/app-passwords).

3. Launch the game. Each run creates a new `me.byjp.pesos.sts2.run` record on your PDS and updates a single rolling `games.gamesgamesgamesgames.actor.stats` record (the mod caches its rkey back into `config.json` after the first run).

> [!TIP]
> Your save hasn't vanished! Follow this guidance to make sure it's available during modded play.

Slay the Spire 2 keeps **separate save files for modded and unmodded profiles** — the first time you launch with any mod installed, the game will look empty. Your existing progress is still on disk under a sibling folder; copy `progress.save` across:

| OS                 | Unmodded                                                                        | Modded                                                                               |
| ------------------ | ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| Windows            | `%APPDATA%\SlayTheSpire2\steam\<STEAM_ID>\1\profile1\saves`                     | `%APPDATA%\SlayTheSpire2\steam\<STEAM_ID>\modded\profile1\saves`                     |
| macOS              | `~/Library/Application Support/SlayTheSpire2/steam/<STEAM_ID>/1/profile1/saves` | `~/Library/Application Support/SlayTheSpire2/steam/<STEAM_ID>/modded/profile1/saves` |
| Linux / Steam Deck | `~/.local/share/SlayTheSpire2/steam/<STEAM_ID>/1/profile1/saves`                | `~/.local/share/SlayTheSpire2/steam/<STEAM_ID>/modded/profile1/saves`                |

Copy (don't move) so you keep an unmodded backup.

## Build

Prerequisite: [.NET SDK 9+](https://dotnet.microsoft.com/download). The build references `sts2.dll` and `0Harmony.dll` directly from your game install — no need to copy them.

```sh
# Build only
dotnet build mod/atproto-tracker.csproj -c Release

# Build and install into the game's mods/atproto-tracker/ directory
dotnet build mod/atproto-tracker.csproj -c Release -t:Install
```

The csproj auto-detects the default Steam install path on macOS, Linux, and Windows. If your install is elsewhere (e.g. a non-default Steam library), copy [mod/local.props.template](mod/local.props.template) to `mod/local.props` and set `Sts2DataDir` and `ModsPath`.

### CI / reference assemblies

CI builds against API-only reference assemblies in [mod/refs/](mod/refs/) so the game's proprietary DLLs never leave the user's machine. Regenerate them after a game update:

```sh
scripts/generate-refs.sh  # reads Sts2DataDir from mod/local.props
```

### Releases

Bump `<Version>` in [mod/atproto-tracker.csproj](mod/atproto-tracker.csproj) and push to `main`. The build stamps the version into both the assembly and `manifest.json` automatically. The [release workflow](.github/workflows/release.yml) creates a `v<version>` tag and publishes a zip with `atproto-tracker.dll` + `manifest.json` + `config.example.json`.

## Layout

- [lexicons/](lexicons/) — atproto lexicon JSON for `me.byjp.pesos.sts2.run`
- [mod/](mod/) — C# mod project (Godot .NET / HarmonyX, targets .NET 9)

## Platforms

One DLL, all platforms. Install path differs:

### Windows

In Steam, right-click *Slay the Spire 2* → **Manage → Browse Local Files**. Open the `mods` folder (create it if missing) and extract the release zip so you have `mods/atproto-tracker/{atproto-tracker.dll,manifest.json,config.json}`.

### macOS

Right click on your `SlayTheSpire2.app` (usually in `/Users/jp/Library/Application Support/Steam/steamapps/common`) and choose "Show Package".

Navigate to `Contents/MacOS/mods/` (making the `mods` directory, if needed), then extract the release zip into that folder so you have `Contents/MacOS/mods/atproto-tracker/{atproto-tracker.dll,manifest.json,config.json}`


  — installing here is necessary, but invalidates the app's code signature. Steam-installed builds still launch fine, but Gatekeeper may complain on first run.
- **Linux / Steam Deck**: In Steam, right-click *Slay the Spire 2* → **Manage → Browse Local Files** (in Desktop Mode on Steam Deck). Open the `mods` folder (create it if missing) and extract the release zip so you have `mods/atproto-tracker/atproto-tracker.dll`.

## Credits

- Mod loader conventions and starter code from [jiegec/STS2FirstMod](https://github.com/jiegec/STS2FirstMod)
- Run-state field accessors inspired by [Gennadiyev/STS2MCP](https://github.com/Gennadiyev/STS2MCP)
- Hook catalogue via [elliotttate/sts2-modding-mcp](https://github.com/elliotttate/sts2-modding-mcp)
- `games.gamesgamesgamesgames.*` lexicons by [Birbhouse Games](https://birb.house)
