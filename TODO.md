# TODO

Work that is decided but not built. `AGENTS.md` and `README.md` describe what
exists; this file is the only place that describes what does not.

Sections are unordered.

## Command channel has no status query beside perf

`ModEntry.Answer` (`src/MortarSmapiBridge/ModEntry.cs:68`) forwards every
question to `FrameProfiler.TryAnswer`. Unknown lines, including `status`,
`health`, and `overlay`, are queued as SMAPI console commands (`BridgeServer`
`Process` after `Query` returns null).

The command file (`mortar-smapi-bridge.json`) already tells Mortar the
port and token. Overlay bind success is only a log line
(`ModEntry.cs:108`). GMCM capture is files on disk. There is no loopback
question that reports whether the overlay listener is up, whether a perf
window is on, or whether GMCM ran.

Decided shape: do not add `status` until Mortar specifies the keys. A
minimal future reply would be one JSON object from `Answer`, not a console
command, named `status`, with booleans Mortar can use without parsing logs:
overlay listening, perf `active`, and nothing else. It must not become a
dump of config tokens.

Rejected: returning the overlay token or command token. Rejected: HTTP
`GET /health` on the overlay port (that listener has no path to the
command queue on purpose, `README.md` Stream overlay). Rejected: inventing
the key list here.

### Read

1. `src/MortarSmapiBridge/ModEntry.cs:68` `Answer` — the only query switch.
2. `src/MortarSmapiBridge/Perf/FrameProfiler.cs:63` `Query` — adding a
   non-perf line here would mix concerns; a sibling on `ModEntry` is the
   place for `status`.
3. `src/MortarSmapiBridge/BridgeServer.cs:97` — a non-null `Query` result
   is `ok <json>` and is never queued.
4. `README.md` Protocol — the question list is `perf`, `perf start`,
   `perf stop`.

### Traps

- **`status` may already be a SMAPI or mod command.** Today it is queued.
  Taking it as a query changes that line's contract for anyone who typed
  it in Mortar. Confirm Mortar never sends `status` as a console command
  before claiming the name.
- **Do not put tokens in the JSON.** The overlay token lives in
  `config.json`; the command token lives in the state file. A status
  reply is booleans, maybe the overlay port if it bound, never secrets.
- **Command channel may be off** while overlay is on (`ModEntry.cs:38`
  returns before `BridgeServer` starts). A status query cannot exist in
  that case; overlay-up is not reachable through the command port then.

### Acceptance

- Mortar names `status` (or another unused line) and the boolean keys.
  `Answer` returns JSON; the line is not queued. README Protocol lists it.
  Tests using `BridgeServer` with that query callback (or `Answer`) assert
  it is not enqueued. Gate stays green.
