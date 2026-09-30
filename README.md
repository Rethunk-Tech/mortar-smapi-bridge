<h1 align="center">Mortar SMAPI Bridge</h1>

<div align="center">

![Licence](https://img.shields.io/badge/licence-AGPL--3.0-blue)

</div>

---

A tiny SMAPI mod that lets the [Mortar](https://github.com/Rethunk-AI/mortar) mod manager send console commands to a running Stardew Valley. Mortar launches the game through Steam and only reads SMAPI's log, so it has no access to SMAPI's stdin; this mod provides the missing input channel.

Mortar installs it automatically. You do not need it otherwise.

## How it works

On launch the mod listens on `127.0.0.1` (port chosen by the OS) and writes `mortar-smapi-bridge.json` in its own folder:

```json
{"port":51234,"token":"<64 hex chars>","pid":4242}
```

The file is restricted to the current user (mode 0600 on Linux/macOS) and deleted on exit.

## Protocol

One connection per command. The client sends two LF-terminated lines:

```
<token>
<command line>
```

The mod replies with one line and closes: `ok` or `error: <message>`.

- The command line is parsed quote-aware (double quotes group, backslash escapes) and must be non-empty; the limit is 4096 bytes. The token line is limited to 128 bytes.
- `ok` means the command was queued. It runs on the game's next update tick exactly as if typed in the SMAPI console (built-ins like `help`, other mods' commands, `screen=N`), and its output appears in the SMAPI log. An unknown command is reported there, not in the reply.
- Each received command is logged at Trace level.

SMAPI 4.5.2 has no public API to run arbitrary commands (`ICommandHelper` only adds them), so the mod adds the line to SMAPI's internal console input queue by reflection. A future SMAPI that renames it disables the bridge with a logged error.

## Security

The listener binds loopback only. Every request must carry the random 32-byte token, compared in constant time; the token exists only in the user-only state file. Anything that can read that file as your user could already run code as you.

## Quick start

```sh
dotnet build -c Release && dotnet test -c Release
```

Prerequisites, the SMAPI dll override and install: [HUMANS.md](HUMANS.md).

## Documentation

| Topic | Location |
| --- | --- |
| Build, test, install | [HUMANS.md](HUMANS.md) |
| Rules for agents | [AGENTS.md](AGENTS.md) |
| Contributing | [CONTRIBUTING.md](CONTRIBUTING.md) |
| Security policy | [SECURITY.md](SECURITY.md) |
| Decided work not yet built | [docs/design.md](docs/design.md) |
| Licence | [LICENSE](LICENSE) |

## License

AGPL-3.0, see `LICENSE`. SMAPI is LGPL-3.0 and is referenced, not redistributed.
