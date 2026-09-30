<h1 align="center">Mortar SMAPI Bridge</h1>

<div align="center">

![Licence](https://img.shields.io/badge/licence-AGPL--3.0-blue)

</div>

A SMAPI mod that lets the [Mortar](https://github.com/Rethunk-AI/mortar) mod manager send console commands to a running Stardew Valley. Mortar launches the game through Steam and only reads SMAPI's log, so it has no access to SMAPI's stdin; this mod provides the missing input channel.

## Nexus page description

**Mortar SMAPI Bridge** lets the Mortar mod manager send SMAPI console commands to a running Stardew Valley, so Mortar can do things like reload or configure mods without you typing in the SMAPI console. It adds no gameplay and changes no game content.

- **Install:** Mortar installs and updates it automatically. Install it by hand only if you want to drive SMAPI commands from your own tool: unzip into `Mods/`.
- **Requires:** SMAPI 4.5.x and Stardew Valley 1.6.14 or later. On a newer SMAPI minor version the mod disables itself and logs a warning.
- **Safety:** it listens on `127.0.0.1` only (never the network, never the internet) and every request needs a random token stored in a file only your user can read. It makes no outgoing connections.
- **Source and licence:** [github.com/Rethunk-AI/mortar-smapi-bridge](https://github.com/Rethunk-AI/mortar-smapi-bridge), AGPL-3.0. Credit: built on SMAPI by Pathoschild (LGPL-3.0), which is referenced, not redistributed.

## How it works

On launch the mod listens on `127.0.0.1` (port chosen by the OS) and writes `mortar-smapi-bridge.json` in its own folder:

```json
{"port":51234,"token":"<64 hex chars>","pid":4242}
```

The file is restricted to the current user (mode 0600 on Linux and macOS) and deleted on exit. The listener never binds the network. Every request must carry the random 32-byte token, compared in constant time. The mod makes no outgoing connections.

## Protocol

One connection per command. The client sends two LF-terminated lines:

```text
<token>
<command line>
```

The mod replies with one line and closes: `ok` or `error: <message>`.

The command line is parsed quote-aware (double quotes group, backslash escapes) and must be non-empty; the limit is 4096 bytes. The token line is limited to 128 bytes.

`ok` means the command was queued. It runs on the game's next update tick exactly as if typed in the SMAPI console (built-ins like `help`, other mods' commands, `screen=N`), and its output appears in the SMAPI log. An unknown command is reported there, not in the reply. Each received command is logged at Trace level.

SMAPI has no public API to run arbitrary commands (`ICommandHelper` only adds them), so the mod adds the line to SMAPI's internal console input queue by reflection (`SCore.RawCommandQueue`). The mod only runs on the SMAPI minor version it was tested against (4.5.x); a newer one, or a renamed queue, disables the bridge with a logged message.

## Stream overlay

Overlay mode is off by default. Set `OverlayEnabled` to `true` in `config.json`, choose the fixed `OverlayPort` (the bridge accepts 1 to 65535; Mortar writes 1024 to 65535), and restart the game:

```json
{
  "OverlayEnabled": true,
  "OverlayPort": 8123,
  "OverlayToken": "<generated on first enable>"
}
```

The token is persistent, separate from the command token, and the config file is restricted to the current user. On the verified Stardew Valley 1.6.15 build, the loopback-only listener serves `GET /state` on `http://127.0.0.1:<OverlayPort>/state`. Authenticate with `Authorization: Bearer <OverlayToken>` or `?token=<OverlayToken>`. While no save is loaded it returns `{"inGame":false}`. In a loaded save it returns `"inGame":true` plus location, player name, season, day, year, time of day, money, weather, health/max health, stamina/max stamina, and the six skill levels. The listener only serves this read-only endpoint; it has no path to the command queue.

The snapshot is collected on SMAPI's game thread and the listener serves the last snapshot. Game state is only read when SMAPI reports the world is ready (`Context.IsWorldReady`). A snapshot read failure is logged once per distinct error message. If the game version is outside the verified range, the overlay stays off and logs a warning; the command channel remains available.

A small OBS browser-source example is in [`examples/overlay/index.html`](examples/overlay/index.html). Open it with `?token=...&port=...`. With no `field`, it shows the full snapshot. Add `field=<name>` for one value per browser source (`location`, `player`, `season`, `day`, `year`, `time`, `date`, `money`, `weather`, `health`, `stamina`, `skill.farming`, `skill.fishing`, `skill.foraging`, `skill.mining`, `skill.combat`, `skill.luck`). `time` is the in-game clock (for example `6:10 PM`); `date` is `Spring 12, Year 2`; health and stamina are `current/max`. Add `label=1` to prefix a short label. Style in OBS Custom CSS using the element `id` and `class` for that value (`#player` / `.player`, `#skill-farming` / `.skill.farming`, and the same pattern for the other names; `field=skill.farming` uses `#skill-farming`). While not in game, or if the bridge is unreachable or errors, the page is blank (a console message once per state change).

## Quick start

Prerequisites, the SMAPI dll override, install and gate: [HUMANS.md](HUMANS.md).

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
