<h1 align="center">Mortar SMAPI Bridge</h1>

<div align="center">

![Licence](https://img.shields.io/badge/licence-AGPL--3.0-blue)

</div>

A SMAPI mod that lets the [Mortar](https://github.com/Rethunk-AI/mortar) mod manager send console commands to a running Stardew Valley. Mortar launches the game through Steam and only reads SMAPI's log, so it has no access to SMAPI's stdin; this mod provides the missing input channel.

Mortar installs and updates it automatically. Install it by hand only if you want to drive SMAPI commands from your own tool: unzip into `Mods/`.

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

## Quick start

```sh
dotnet build -c Release && dotnet test -c Release
```

Prerequisites, the SMAPI dll override and install: [HUMANS.md](HUMANS.md). Gate: `gate` from the repo root.

## Documentation

| Topic | Location |
| --- | --- |
| Build, test, install | [HUMANS.md](HUMANS.md) |
| Rules for agents | [AGENTS.md](AGENTS.md) |
| Contributing | [CONTRIBUTING.md](CONTRIBUTING.md) |
| Security policy | [SECURITY.md](SECURITY.md) |
| Decided work not yet built | [docs/design.md](docs/design.md) |
| Licence | [LICENSE](LICENSE) |

## Licence

Licensed under the [GNU Affero General Public License v3.0](LICENSE). SMAPI is LGPL-3.0 and is referenced, not redistributed.
