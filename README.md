# Slay the Spire 2: AT Protocol Tracker

A [Slay the Spire 2](https://store.steampowered.com/app/2868840/) mod that posts end-of-run summaries to your [atproto](https://atproto.com) PDS.

Runs are stored as `me.byjp.pesos.sts2.run` records. A rolling `games.gamesgamesgamesgames.actor.stats` record is updated alongside each run so the game shows up in [HappyView](https://github.com/gamesgamesgamesgamesgames/happyview)-style game-catalog apps.

> [!NOTE]
> I've added machine translations for non-English languages, so they're likely to be a bit wonky. If people are interested, I'll try to figure out how to include improved translations from humans too!

## Status

This is an early prototype from an experienced but non-games developer 😅 I'd love any feedback, if you've more context in this space!

The run-end Harmony hook is pinned (`MegaCrit.Sts2.Core.Runs.RunManager.OnEnded`) and the mod builds clean. Run-state extraction uses reflection for tolerance against Early Access patch drift, so some deck/relic id field names may need fine-tuning later.

## Install (no build required)

1. Unzip the [latest release](https://github.com/jphastings/slay-the-spire-ii-atproto/releases) into your game's mods folder (see [Platforms](#platforms)).
2. Create a `config.json` in that same folder with your handle (or DID) and an [app password](https://bsky.app/settings/app-passwords):

   ```json
   {
     "handle": "you.example.com",
     "appPassword": "xxxx-xxxx-xxxx-xxxx"
   }
   ```

   The mod resolves your PDS automatically at startup (via [Slingshot](https://slingshot.microcosm.blue/) 😍) and authenticates immediately, so any credential problem surfaces before your first run.

   > [!TIP]
   > The main menu shows a small `@` in the bottom-left corner: green when authenticated, red (with a strike-through) when credentials are missing or invalid — click it for details.

   If you skip this step, the mod will write a template `config.json` next to the DLL on first launch — just fill it in and restart the game.

3. Launch the game. Each run creates a new `me.byjp.pesos.sts2.run` record on your PDS and updates a single rolling `games.gamesgamesgamesgames.actor.stats` record (the mod caches its rkey back into `config.json` after the first run).

> [!TIP]
> Your save hasn't vanished! Modded StS2 saves/loads from a different place. Follow this guidance to make sure it's available during modded play.

Slay the Spire 2 keeps **separate save files for modded and unmodded profiles** — the first time you launch with any mod installed, the game will look empty. Your existing progress is still on disk under a sibling folder; copy `progress.save` across:

Saves live in Steam Cloud's remote storage. The `<profileN>` folder is each of `profile1`, `profile2`, `profile3` (copy whichever you use).

| OS                 | Unmodded                                                                            | Modded                                                                                     |
| ------------------ | ----------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| Windows            | `<STEAM_INSTALL>\userdata\<STEAM_ID>\2868840\remote\<profileN>`                     | `<STEAM_INSTALL>\userdata\<STEAM_ID>\2868840\remote\modded\<profileN>`                     |
| macOS              | `~/Library/Application Support/Steam/userdata/<STEAM_ID>/2868840/remote/<profileN>` | `~/Library/Application Support/Steam/userdata/<STEAM_ID>/2868840/remote/modded/<profileN>` |
| Linux / Steam Deck | `~/.local/share/Steam/userdata/<STEAM_ID>/2868840/remote/<profileN>`                | `~/.local/share/Steam/userdata/<STEAM_ID>/2868840/remote/modded/<profileN>`                |

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

Bump `<Version>` in [mod/atproto-tracker.csproj](mod/atproto-tracker.csproj) and push to `main`. The build stamps the version into both the assembly and `manifest.json` automatically. The [release workflow](.github/workflows/release.yml) creates a `v<version>` tag and publishes a zip with `atproto-tracker.dll` + `manifest.json` + `config.json.example`.

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
