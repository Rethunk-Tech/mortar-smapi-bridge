<h1 align="center">Mortar SMAPI Bridge</h1>

<div align="center">

![Licence](https://img.shields.io/badge/licence-AGPL--3.0-blue)

</div>

---

The SMAPI loader's companion for [Mortar](https://github.com/Rethunk-Tech/mortar): a SMAPI mod that lets Mortar send console commands to a running SMAPI game (Stardew Valley today). Other loaders have their own companion ([mortar-bepinex-bridge](https://github.com/Rethunk-Tech/mortar-bepinex-bridge) for BepInEx). Mortar starts the game through its launcher and reads SMAPI's log, so it has no stdin; this mod is the input channel.

Build, SMAPI dll override, install, versioning and gate: [HUMANS.md](HUMANS.md).

## Nexus page description

**Mortar SMAPI Bridge** lets the Mortar mod manager send SMAPI console commands to a running Stardew Valley, so Mortar can do things like reload or configure mods without you typing in the SMAPI console. It adds no gameplay and changes no game content.

- **Install:** Mortar installs and updates it automatically. Install it by hand only if you want to drive SMAPI commands from your own tool: unzip into `Mods/`.
- **Requires:** SMAPI 4.5.2 or a later 4.x and Stardew Valley 1.6.14 or later (`MinimumGameVersion`). On SMAPI 5, or a 4.x whose internal console queue moved, the command channel stays off and logs a warning. Stream overlay runs on Stardew Valley 1.6.14 up to, but not including, 1.7; on any other game version it stays off.
- **Safety:** it listens on `127.0.0.1` only (never the network, never the internet) and every request needs a random token stored in a file only your user can read. It makes no outgoing connections.
- **Source and licence:** [github.com/Rethunk-Tech/mortar-smapi-bridge](https://github.com/Rethunk-Tech/mortar-smapi-bridge), AGPL-3.0. Credit: built on SMAPI by Pathoschild (LGPL-3.0), which is referenced, not redistributed.

## Discovery and protocol

On launch the mod listens on `127.0.0.1` (OS-chosen port) and writes user-only `mortar-smapi-bridge.json` in its folder (`{"port":...,"token":"<64 hex>"}`; 0600 / user-only ACL), deleted on exit. Token compare is constant-time.

One connection per command: two LF-terminated lines (`<token>` then `<command line>`). Reply one line and close: `ok`, `ok <json>` for `perf` / `perf start` / `perf stop`, or `error: <message>` (`unauthorized`, `missing or oversized command`, `empty command`, `unterminated quote or escape`). Unfinished input within 5s is dropped with no reply.

Command line is quote-aware (double quotes group, backslash escapes), non-empty, max 4096 bytes; token line max 128 bytes. `ok` means queued for the next update tick as if typed in the SMAPI console; output and unknown-command errors go to the SMAPI log. Received commands log at Trace.

SMAPI has no public API for arbitrary commands (`ICommandHelper` only adds them), so the line is added to `SCore.RawCommandQueue` by reflection. Verified on SMAPI 4 from 4.5.2; SMAPI 5 or a renamed queue turns the channel off with a log message.

## Stream overlay

Off by default. Set `OverlayEnabled` to `true` in user-only `config.json`, set `OverlayPort` (bridge accepts 1–65535; Mortar writes 1024–65535), restart. `OverlayToken` is generated on first enable and is not the command token.

On Stardew Valley 1.6.14–1.6.x the loopback listener serves `GET /state` on `http://127.0.0.1:<OverlayPort>/state`. `HEAD /state` is the same without a body. Unauthenticated `OPTIONS /state` returns 204 with `Allow: GET, HEAD, OPTIONS`, `Access-Control-Allow-Headers: Authorization`, `Access-Control-Allow-Methods: GET, HEAD`. Other methods: 405 with the same `Allow`. Auth: `Authorization: Bearer <OverlayToken>` or `?token=<OverlayToken>`. Unfinished request within 5s is dropped. After the first one-second tick with no save loaded: `{"inGame":false}`. Before that tick, or after a snapshot read failure: 503 `{"error":"state unavailable"}`. In a loaded save the JSON keys are `inGame`, `location`, `playerName`, `season`, `day`, `year`, `timeOfDay`, `money`, `weather`, `health`, `maxHealth`, `stamina`, `maxStamina`, and `skills` (`farming`, `fishing`, `foraging`, `mining`, `combat`, `luck`). Replies send `Access-Control-Allow-Origin: *`. No path to the command queue.

The snapshot is taken on SMAPI's game thread; the listener serves the last one. Game state is read only when `Context.IsWorldReady`. A snapshot read failure is logged once per distinct message. Outside 1.6.14–1.6.x the overlay stays off (warning); the command channel stays up.

OBS example: [`examples/overlay/index.html`](examples/overlay/index.html) with `?token=...&port=...`. No `field`: full snapshot. `field=<name>`: one value. `label=1`: prefix a short label. Style with `#player` / `.player`, `#skill-farming` / `.skill.farming` (`field=skill.farming` → `#skill-farming`). Off-game the page is blank except `field=inGame` (`true`/`false`). Unreachable or error: blank (one console message per state change).

| `field` | Shown as |
| --- | --- |
| `inGame` | `true` or `false` |
| `location` | location name |
| `player` | player name |
| `season` | season |
| `day` | day of month |
| `year` | year |
| `time` | in-game clock (for example `6:10 PM`) |
| `date` | `Spring 12, Year 2` |
| `money` | money with a trailing `g` |
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

On unless `GmcmEnabled` is `false`. Files go in the profile dir (nearest ancestor with Mortar's `profile.json`); without one, nothing is written.

- **When:** first update after `GameLaunched`, leaving a GMCM menu, and `SaveLoaded`.
- **Capture:** `gmcm/<UniqueID>.json` per mod, atomic; `gmcm/_index.json` is `{schema: 1, gmcmVersion, capturedAt, mods: [{id, name, version}]}`. `fieldId` omitted when it looks like a GUID.

  ```text
  {schema: 1, mod: {id, name, version}, gmcmVersion, capturedAt, titleScreenOnlyDefault,
   pages: [{id, title, options: [{index, kind, fieldId, name, tooltip, value, min, max, interval,
     choices: [{value, label}], formatSamples, editable, titleScreenOnly}]}]}
  ```

- **Apply:** on that first title-screen tick, before capture, `gmcm-pending/<file>.json` (not `_`-prefixed, not `*.result.json`, ≤1 MiB) `{schema: 1, uniqueId, edits: [{page, index, kind, fieldId, name, value}]}`. `uniqueId` selects the GMCM mod; if omitted, the file stem. Match by `fieldId` when it is not a GUID, else `(page, index, kind, name)`; then every option `BeforeSave` → `ModConfig.Save` → `AfterSave`.
- **Result:** `gmcm-pending/<file>.result.json` `{schema: 1, applied, skipped: [{edit, reason}]}`; pending deleted only after a successful save. Unregistered mods and failed matches are skipped with reasons.

## Startup timings

On unless `StartupTimings` is `false`, and only inside a Mortar profile. Mortar loads the bridge first via `SMAPI-config.json` `ModsToLoadEarly`, so no other `Entry` runs before it.

- **Measured:** from the bridge's `Entry` to the title screen, each mod's exclusive time in every SMAPI event handler and in the asset edits and loads it registered. Content Patcher's time is split by content pack (`content.json` / `config.json` load, patch parse, token eval, edits). Exclusive: time in another measured mod (e.g. an asset edit from an update tick) is charged to that mod only.
- **`StartupProfile: true`:** also times each other mod's `Entry` and `GetApi` from SMAPI's side: a postfix on SMAPI's per-mod `SetApi` charges the time since the previous call to the mod that just finished, so no mod's own method is patched. A measured launch skips the title intro, so title time is load, not a cut scene.
- **At the title screen:** event hooks come off and the clock stops. Harmony patches stay but no-op: unpatching regenerates every patched method and freezes the title screen for seconds. If the title screen is never reached, timing stops after 15 minutes and `phases.titleScreen` is `0`.
- **Report:** `startup/<yyyyMMddTHHmmssZ>.json` in the profile dir (process start UTC), last 10 kept. Phases are ms from process start.

  ```text
  {schema: 1, loader, game, processStart,
   phases: {bridgeEntry, entryDone, gameLaunched, titleMenu, titleScreen},
   entryTimed, entryMissed,
   mods: [{id, name, version, entryMs, eventMs: {<event>: ms}, assetMs, loadMs,
     packs: [{id, name, assetMs, loadMs, ms}]}],
   otherMs, replaces: {<Harmony ID>: ["<Type.FullName>::<method>"]}}
  ```

- **`replaces`:** Harmony patch registry at report time. Per Harmony ID (convention: mod UniqueID; the bridge's own IDs omitted), methods it can replace outright: transpilers, and prefixes that return `bool`. Mortar uses it to hint at mods that do the same job.

## Intro skip

Inside a Mortar profile, when `startup/skip-intro` exists in the profile folder (Mortar writes it before a launch when the player turned on "Skip the intro", and removes it otherwise), the title screen jumps to its buttons instead of playing the logo animation. The file's text ("intro" or "menu") makes no difference here: Stardew Valley's title buttons are its main menu. A measured launch (`StartupProfile`) skips the intro without the file.

## Frame timings

Off until Mortar asks; nothing is wrapped before then. `perf start` (answered `ok {"measured":true}`) begins a window on the next update tick: every mod's per-frame handlers (`UpdateTicking`, `UpdateTicked`, `OneSecondUpdateTicking`, `OneSecondUpdateTicked`, `Rendering`, `Rendered`, and World / ActiveMenu / Hud / Step variants) become timed delegates, the same swap startup timing uses, so a mod that removes its own handler still can. Window max ten minutes; wrappers come off then and the last summary stays. `perf stop` unwraps on the next tick and answers with that summary (`ok` plus the same JSON as `perf`). `perf start` again restarts it.

SMAPI's `performance` counters are a rolling average and peak per mod and event, with no 95th percentile and no share of the frame, so handlers are timed here.

- **Frame:** one `UpdateTicking` to the next, including vsync wait. A mod's cost is the sum of its timed handlers in that frame; the bridge's own handlers are left out.
- **`perf`:** summary rebuilt once a second:

  ```text
  ok {measured, active, seconds, frames, fps,
      frameMs: {avg, p50, p95, p99, max},
      monoUsedBytes, monoHeapBytes, gcCollections,
      baseline: {frameMs, modsMs, withoutModsMs},
      plugins: [{guid: <UniqueID>, msPerFrame, p95Ms, peakMs, share, callsPerFrame}, ...]}
  ```

  `measured` stays true on every summary. `active` is true only while a window is running. `msPerFrame` averages over every frame (a rare mod: small average, high peak). `p95Ms` is the 95th percentile of per-frame cost over all frames (zero if it ran in fewer than 5% of them), ~10% resolution. `share` is `msPerFrame` over the average frame. `baseline.withoutModsMs` is average frame minus timed handlers (game + SMAPI + Harmony/content work outside events), not a no-mods measurement; profile an empty profile for that.

Licensed under [AGPL-3.0](LICENSE). SMAPI is LGPL-3.0, referenced not redistributed.
