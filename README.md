<h1 align="center">Mortar SMAPI Bridge</h1>

<div align="center">

![Licence](https://img.shields.io/badge/licence-AGPL--3.0-blue)

</div>

---

The SMAPI loader's companion for the [Mortar](https://github.com/Rethunk-Tech/mortar) mod manager: a SMAPI mod that lets Mortar send console commands to a running SMAPI game (Stardew Valley today). Games on other loaders have their own companion, such as [mortar-bepinex-bridge](https://github.com/Rethunk-Tech/mortar-bepinex-bridge) for BepInEx. Mortar starts the game through its launcher (Steam, GOG, Heroic or Lutris) and reads SMAPI's log, so it has no access to SMAPI's stdin; this mod provides the missing input channel.

## Quick start

```sh
dotnet build -c Release && dotnet test -c Release
```

Prerequisites, the SMAPI dll override, install and gate: [HUMANS.md](HUMANS.md).

## Highlights

- Loopback TCP command channel: one connection per SMAPI console line, authenticated with a per-run token
- Writes `mortar-smapi-bridge.json` (`port`, `token`) in the mod folder and deletes it on exit
- Optional loopback overlay: `GET /state` for OBS, separate persistent token, no path to the command queue
- Command channel runs on SMAPI 4.5 and later 4.x; the overlay runs on Stardew Valley 1.6.14 up to, but not including, 1.7. The two gates are independent: either can be off while the other runs.
- [GMCM menu capture](#gmcm-menu-capture): each mod's Generic Mod Config Menu options as JSON, and edits from Mortar applied back
- [Startup timings](#startup-timings): per-mod time from launch to the title screen, and the game methods each mod's Harmony patches can replace



## Nexus page description

**Mortar SMAPI Bridge** lets the Mortar mod manager send SMAPI console commands to a running Stardew Valley, so Mortar can do things like reload or configure mods without you typing in the SMAPI console. It adds no gameplay and changes no game content.

- **Install:** Mortar installs and updates it automatically. Install it by hand only if you want to drive SMAPI commands from your own tool: unzip into `Mods/`.
- **Requires:** SMAPI 4.5.2 or a later 4.x and Stardew Valley 1.6.14 or later (`MinimumGameVersion`). On SMAPI 5, or a 4.x whose internal console queue moved, the command channel stays off and logs a warning. Stream overlay runs on Stardew Valley 1.6.14 up to, but not including, 1.7; on any other game version it stays off.
- **Safety:** it listens on `127.0.0.1` only (never the network, never the internet) and every request needs a random token stored in a file only your user can read. It makes no outgoing connections.
- **Source and licence:** [github.com/Rethunk-Tech/mortar-smapi-bridge](https://github.com/Rethunk-Tech/mortar-smapi-bridge), AGPL-3.0. Credit: built on SMAPI by Pathoschild (LGPL-3.0), which is referenced, not redistributed.

## How it works

On launch the mod listens on `127.0.0.1` (port chosen by the OS) and writes `mortar-smapi-bridge.json` in its own folder:

```json
{"port":51234,"token":"<64 hex chars>"}
```

The file is restricted to the current user (mode 0600 on Linux and macOS) and deleted on exit. Every request must carry the random 32-byte token, compared in constant time.

## Protocol

One connection per command. The client sends two LF-terminated lines:

```text
<token>
<command line>
```

The mod replies with one line and closes: `ok` or `error: <message>`.

The command line is parsed quote-aware (double quotes group, backslash escapes) and must be non-empty; the limit is 4096 bytes. The token line is limited to 128 bytes.

`ok` means the command was queued. It runs on the game's next update tick exactly as if typed in the SMAPI console (built-ins like `help`, other mods' commands, `screen=N`), and its output appears in the SMAPI log. An unknown command is reported there, not in the reply. Each received command is logged at Trace level.

SMAPI has no public API to run arbitrary commands (`ICommandHelper` only adds them), so the mod adds the line to SMAPI's internal console input queue by reflection (`SCore.RawCommandQueue`). The command channel runs on SMAPI 4 from 4.5, where the lookup was verified; SMAPI 5, or a renamed queue in a later 4.x, turns it off with a logged message.

## Stream overlay

Overlay mode is off by default. Set `OverlayEnabled` to `true` in `config.json`, choose the fixed `OverlayPort` (the bridge accepts 1 to 65535; Mortar writes 1024 to 65535), and restart the game:

```json
{
  "OverlayEnabled": true,
  "OverlayPort": 8123,
  "OverlayToken": "<generated on first enable>"
}
```

The token is persistent, separate from the command token, and the config file is restricted to the current user. On Stardew Valley 1.6.14 and later 1.6.x the loopback-only listener serves `GET /state` on `http://127.0.0.1:<OverlayPort>/state`. Authenticate with `Authorization: Bearer <OverlayToken>` or `?token=<OverlayToken>`. While no save is loaded it returns `{"inGame":false}`. In a loaded save the JSON keys are `inGame`, `location`, `playerName`, `season`, `day`, `year`, `timeOfDay`, `money`, `weather`, `health`, `maxHealth`, `stamina`, `maxStamina`, and `skills` (`farming`, `fishing`, `foraging`, `mining`, `combat`, `luck`). The listener only serves this read-only endpoint; it has no path to the command queue.

The snapshot is collected on SMAPI's game thread and the listener serves the last snapshot. Game state is only read when SMAPI reports the world is ready (`Context.IsWorldReady`). A snapshot read failure is logged once per distinct error message. If the game version is outside 1.6.14 to 1.6.x, the overlay stays off and logs a warning; the command channel remains available.

A small OBS browser-source example is in [`examples/overlay/index.html`](examples/overlay/index.html). Open it with `?token=...&port=...`. With no `field`, it shows the full snapshot. Add `field=<name>` for one value per browser source. Add `label=1` to prefix a short label. Style in OBS Custom CSS using the element `id` and `class` for that value (`#player` / `.player`, `#skill-farming` / `.skill.farming`; `field=skill.farming` uses `#skill-farming`). While not in game, or if the bridge is unreachable or errors, the page is blank (a console message once per state change).

| `field` | Shown as |
| --- | --- |
| `location` | location name |
| `player` | player name |
| `season` | season |
| `day` | day of month |
| `year` | year |
| `time` | in-game clock (for example `6:10 PM`) |
| `date` | `Spring 12, Year 2` |
| `money` | money |
| `weather` | weather |
| `health` | `current/max` |
| `stamina` | `current/max` |
| `skill.farming` | farming level |
| `skill.fishing` | fishing level |
| `skill.foraging` | foraging level |
| `skill.mining` | mining level |
| `skill.combat` | combat level |
| `skill.luck` | luck level |

## GMCM menu capture

On unless `GmcmEnabled` is `false` in `config.json`. Files go in the profile dir: the nearest folder above the mod that holds Mortar's `profile.json`; without one, nothing is written.

- **When:** on the first update after `GameLaunched`, when leaving a GMCM menu, and on `SaveLoaded`.
- **Capture:** `gmcm/<UniqueID>.json` per mod, written atomically; `gmcm/_index.json` lists the captured mods and the GMCM version. `fieldId` is omitted when it looks like a GUID.

  ```text
  {schema: 1, mod: {id, name, version}, gmcmVersion, capturedAt, titleScreenOnlyDefault,
   pages: [{id, title, options: [{index, kind, fieldId, name, tooltip, value, min, max, interval,
     choices: [{value, label}], formatSamples, editable, titleScreenOnly}]}]}
  ```

- **Apply:** on that first title-screen tick, before capture, the bridge reads `gmcm-pending/<UniqueID>.json` `{schema: 1, edits: [{page, index, kind, fieldId, name, value}]}`. Each edit matches by `fieldId` when it is not a GUID, else by `(page, index, kind, name)`; then every option runs `BeforeSave` → `ModConfig.Save` → `AfterSave`.
- **Result:** `gmcm-pending/<UniqueID>.result.json` `{applied, skipped: [{edit, reason}]}`; the pending file is deleted only after a successful save. Unregistered mods and failed matches are skipped with reasons.

## Startup timings

On unless `StartupTimings` is `false` in `config.json`, and only inside a Mortar profile. Mortar loads the bridge first through `SMAPI-config.json`'s `ModsToLoadEarly`, so no mod's `Entry` runs before it.

- **Measured:** from the bridge's `Entry` to the title screen, each mod's exclusive time in every SMAPI event handler and in the asset edits and loads it registered. Content Patcher's time is split by content pack: loading `content.json` and `config.json`, parsing patches, evaluating each patch's tokens, and its edits. Exclusive means time spent in another measured mod's code, such as an asset edit triggered from an update tick, is charged to that mod only.
- **`StartupProfile: true`:** also times each other mod's `Entry` and `GetApi` from SMAPI's side: a postfix on SMAPI's own per-mod `SetApi` call charges the time since the previous call to the mod that just finished, so no mod's own method is patched. A measured launch also skips the title intro animation, so the title time measures loading rather than a fixed cut scene.
- **At the title screen:** the event hooks are removed and the clock stops. The Harmony patches stay in place but do nothing, since unpatching regenerates every patched method, which freezes the title screen for seconds.
- **Report:** `startup/<process start UTC>.json` in the profile dir, the last 10 kept. Phases are ms from process start.

  ```text
  {schema: 1, smapi, game, processStart,
   phases: {bridgeEntry, entryDone, gameLaunched, titleMenu, titleScreen},
   entryTimed, entryMissed,
   mods: [{id, name, version, entryMs, eventMs: {<event>: ms}, assetMs, loadMs,
     packs: [{id, name, assetMs, loadMs, ms}]}],
   otherMs, replaces: {<Harmony ID>: ["<Type.FullName>::<method>"]}}
  ```

- **`replaces`:** read from Harmony's patch registry as the report is written. Per Harmony ID (by convention the mod's UniqueID; the bridge's own IDs are left out), the methods it can replace outright: transpilers, and prefixes that return `bool`. Mortar uses it to hint at mods that do the same job.

## Documentation

| Topic | Location |
| --- | --- |
| Build, test, install | [HUMANS.md](HUMANS.md) |
| Rules for agents | [AGENTS.md](AGENTS.md) |
| Contributing | [CONTRIBUTING.md](CONTRIBUTING.md) |
| Security policy | [SECURITY.md](SECURITY.md) |
| OBS overlay example | [examples/overlay/index.html](examples/overlay/index.html) |
| Licence | [LICENSE](LICENSE) |

## Licence

Licensed under the [GNU Affero General Public License v3.0](LICENSE). SMAPI is LGPL-3.0 and is referenced, not redistributed.
