# TODO

Work that is decided but not built. `AGENTS.md` and `README.md` describe what
exists; this file is the only place that describes what does not.

Sections are unordered.

## Overlay snapshot has no friendship, pet, or date-derived fields

`OverlaySnapshot` (`src/MortarSmapiBridge/OverlayState.cs:4`) is the in-game
object: location, player, calendar, money, weather, health, stamina, skills.
`ReadOverlaySnapshot` (`ModEntry.cs:144`) fills those from `Game1` and the
farmer. The OBS `field` table in `README.md` and `valueFor` in
`examples/overlay/index.html:129` are the same set, plus derived `time`,
`date`, `player`, and `health`/`stamina` ratios.

There is no next field that is an obvious twin of an existing one (GET had
HEAD; `perf start` had `perf stop`). Friendship, pets, spouse, festival
today, and similar Stardew HUD items are product choices. Adding any of
them changes the in-game JSON key list that
`OverlayTests.InGameSnapshotJsonShape` pins (`OverlayTests.cs` `fields`
array) and the README key list.

Decided shape: do not add keys until Mortar names a specific overlay field
and the Game1/farmer member that supplies it. Then extend the record, the
reader, the README table, `valueFor`, and the shape test in one commit per
field. Each new key is appended, never renamed.

Rejected: a nested `extra` object for speculative keys. Rejected: reading
game types the overlay version gate (`ApiRange.IsTestedGame`) has not
covered.

### Read

1. `src/MortarSmapiBridge/OverlayState.cs:4` — the record is the schema.
2. `src/MortarSmapiBridge/ModEntry.cs:144` `ReadOverlaySnapshot` — every
   current field has one reflection read here.
3. `tests/MortarSmapiBridge.Tests/OverlayTests.cs` `InGameSnapshotJsonShape`
   — ordered key list; a new key is an append and a test update.
4. `README.md` in-game JSON keys sentence and the `field` table.
5. `examples/overlay/index.html:129` `valueFor` and `:82` `labels`.

### Traps

- **World-ready only.** Snapshot reads already run only when
  `Context.IsWorldReady`. A field that exists at the title screen still
  must survive `SetNotInGame`'s `{"inGame":false}`-only object.
- **Do not ship game assemblies.** Reflection through SMAPI's helper and
  `GameType()` is the overlay's rule (`AGENTS.md`, `ModEntry.cs:80`).
- **Web JSON names are camelCase** (`OverlayServer.Serialize` uses
  `JsonSerializerDefaults.Web`). A record property `PetName` becomes
  `petName`; the README table and `valueFor` must use that spelling.

### Acceptance

- One named field, one commit: record, reader, README, example `valueFor`
  and labels, shape test. `{"inGame":false}` still has that single key.
- Gate stays green. No new overlay path.

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
