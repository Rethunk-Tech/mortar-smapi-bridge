# TODO

Work that is decided but not built. `AGENTS.md` and `README.md` describe what
exists; this file is the only place that describes what does not.

Sections are unordered.

## OPTIONS /state has CORS on every reply but no preflight

`OverlayServer.Process` (`src/MortarSmapiBridge/OverlayServer.cs:145`) splits
the request line and, at `:148`, treats only `GET` and `HEAD` as allowed.
Any other method, including `OPTIONS`, returns 405 with `Allow: GET, HEAD`
(`:150`). Every successful and error `Response` (`:231`) already sends
`Access-Control-Allow-Origin: *` (`:245`).

A browser `fetch` from a non-null origin that is not a simple GET (custom
headers other than CORS-safelisted ones, or a non-GET method) sends OPTIONS
first. That preflight hits the 405 path and never learns that GET is allowed
with `Authorization`. The OBS example (`examples/overlay/index.html:219`)
uses `fetch` with only `cache: "no-store"`, which is a simple GET from a
file or OBS origin and does not preflight. That is why this is unblocked in
the documented client and still a gap for any page served from `http:`.

Decided shape: when `parts[0]` is `OPTIONS` and the path is `/state`, return
204 with no body, `Allow: GET, HEAD, OPTIONS`,
`Access-Control-Allow-Origin: *`, `Access-Control-Allow-Headers` naming
`Authorization`, and `Access-Control-Allow-Methods: GET, HEAD`. Do not
authenticate OPTIONS: a preflight has no Bearer token. Other paths stay 404
the same way GET does. Non-OPTIONS, non-GET, non-HEAD stay 405; add OPTIONS
to that `Allow` list.

Rejected: answering OPTIONS on every path. Rejected: treating OPTIONS as GET
and returning the snapshot. Rejected: `HttpListener`, which this repo
refuses (`AGENTS.md`). Rejected: changing the OBS example to send
Authorization as a header so it starts preflighting.

This does not change GET or HEAD status, JSON, or `Content-Length`.

### Read

1. `src/MortarSmapiBridge/OverlayServer.cs:148` — GET/HEAD gate and the 405
   `Allow` at `:150`. OPTIONS sits next to HEAD the way HEAD sat next to GET.
2. `src/MortarSmapiBridge/OverlayServer.cs:231` `Response` — CORS is already
   on every reply; OPTIONS 204 should reuse it, not a second header builder.
3. `tests/MortarSmapiBridge.Tests/OverlayTests.cs` `Request` — the helper
   already takes `method`. `HeadStateMatchesGetHeadersWithoutABody` asserts
   POST is 405 and `Allow` contains GET, HEAD; extend that Allow list.
4. `README.md` Stream overlay paragraph — documents GET and HEAD on `/state`.
   OPTIONS belongs in that sentence once it exists.

### Traps

- **A preflight has no token.** Requiring Bearer on OPTIONS makes CORS
  fail for the clients this would serve. Auth stays on GET and HEAD.
- **204 has no JSON body.** Reusing `Response(200, snapshot)` would leak
  state to an unauthenticated OPTIONS. A dedicated 204 string, or
  `Response` taught empty-body 204, is the shape.
- **GET replies stay as they are.** Adding `Access-Control-Allow-Headers`
  to GET is a header-contract change for existing OBS clients. Put the extra
  CORS headers on OPTIONS only.
- **POST 405 tests match the Allow line.** `HeadStateMatchesGetHeadersWithoutABody`
  currently expects `Allow: GET, HEAD`. Updating that string is part of this
  work, not a follow-up.

### Acceptance

- `OPTIONS /state` without a token returns 204, empty body, `Allow` naming
  GET, HEAD, and OPTIONS, and `Access-Control-Allow-Headers` containing
  `Authorization`.
- `GET /state` and `HEAD /state` status, JSON/body rules, and
  `Content-Length` stay as they are today, including 401 without a token.
- `POST /state` is still 405; `Allow` includes OPTIONS.
- `OPTIONS /other` is 404, not 204.
- README names OPTIONS next to GET and HEAD. Gate stays green.

## perf JSON always says measured true, even after stop

`FrameStats.Json` (`src/MortarSmapiBridge/Perf/FrameStats.cs:118`) opens every
payload with `"measured":true` at `:123`. `FrameProfiler.EmptySummary`
(`FrameProfiler.cs` `EmptySummary`) is that same JSON for an unused window.
`perf stop` (`FrameProfiler.Query` at `:76`) returns that last summary and
sets `halt`; it does not change the JSON. After unwrap, `perf` still reads
as a live measurement.

`perf start` answers `{"measured":true}` (`:73`) meaning the window will
begin. Callers that treat `measured` as "wrappers are on" cannot tell stop
from a finished ten-minute window, because both leave the last summary in
place with `measured` true.

Decided shape: add `"active":true` while `FrameProfiler.on` is true and
`"active":false` otherwise, written next to `measured` in `Json` (pass a
bool from `Summarise` / `EmptySummary`). Keep `"measured":true` on every
summary so existing parsers that only read `measured`, `plugins`, and
`frameMs` keep working. `perf start`'s tiny ack stays `{"measured":true}`
with no `active` key: that line is not a summary.

Rejected: flipping `measured` to false after stop. Mortar and the README
frame-timings block already describe that key as present on the summary.
Rejected: a new `perf status` line. `perf` is the status.

This is deferred because it extends the summary object Mortar already
parses; the extra key is compatible in JSON but it is still an existing
contract, not a missing twin of a verb.

### Read

1. `src/MortarSmapiBridge/Perf/FrameStats.cs:123` — the hard-coded
   `measured` true. `Json` is the only serializer.
2. `src/MortarSmapiBridge/Perf/FrameProfiler.cs:53` `TryAnswer` and `:132`
   `Summarise` — the game thread is the only writer of `on` and of
   `summary`. `active` must be sampled there, not on the socket thread.
3. `tests/MortarSmapiBridge.Tests/FrameStatsTests.cs:107` — empty window
   still has `measured` true. New tests assert `active` once `Json` grows
   the argument; they must not require `measured` to become false.
4. `README.md` Frame timings `perf` JSON block — lists `measured` first.

### Traps

- **Socket-thread `on` is a data race.** `summary` is the published string.
  Rebuild it in `Stop` and in the once-a-second `Summarise` with the then
  current `on`, the way today's `measured` is frozen into the string.
- **EmptySummary is used before the first start.** It has to carry
  `active:false`. Changing only `Summarise` leaves the unused-window `perf`
  reply without the key.
- **Do not add `active` to `perf start`'s ack.** That reply is
  `{"measured":true}` today (`FrameProfiler.cs:75`,
  `FrameProfilerTests.cs` `PerfAndPerfStartStayTheExistingReplies`).

### Acceptance

- `perf` after a stop, and `EmptySummary`, include `"active":false` and
  still `"measured":true`.
- During an open window the once-a-second summary includes `"active":true`.
- `perf start` is still exactly `{"measured":true}`.
- FrameStats tests that read `measured`, `plugins`, and baseline stay
  passing. README's `perf` JSON shape lists `active`. Gate stays green.

## Overlay snapshot has no friendship, pet, or date-derived fields

`OverlaySnapshot` (`src/MortarSmapiBridge/OverlayState.cs:4`) is the in-game
object: location, player, calendar, money, weather, health, stamina, skills.
`ReadOverlaySnapshot` (`ModEntry.cs:155`) fills those from `Game1` and the
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
2. `src/MortarSmapiBridge/ModEntry.cs:155` `ReadOverlaySnapshot` — every
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
  `GameType()` is the overlay's rule (`AGENTS.md`, `ModEntry.cs:86`).
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
(`ModEntry.cs:119`). GMCM capture is files on disk. There is no loopback
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

## OBS field=inGame can never render

`poll` in `examples/overlay/index.html:217` fetches `/state` and, at `:222`,
calls `hide()` whenever `!data.inGame`. `valueFor` (`:129`) has no `inGame`
case; an unknown field returns `""`, and `renderField` (`:163`) hides on
empty. README says the page is blank while not in game.

`inGame` is the one snapshot flag that exists in both the loaded and
unloaded objects, but the example never shows it: the not-in-game object
is hidden before `field` runs, and the in-game object would show nothing
for `field=inGame` because `valueFor` ignores it.

Decided shape: keep the full-snapshot view blank when not in game (current
README). For `field=inGame` only, skip the `!data.inGame` hide, add
`valueFor`/`labels` entries that render `true` or `false`, and add the row
to the README `field` table. Other fields keep hiding when not in game.

Rejected: showing the whole HUD with empty strings at the title screen.
Rejected: treating `inGame` as another skill-style nested key.

This is deferred because it changes the documented "blank while not in
game" rule for one field; that is a behavior change for anyone who already
passes `field=inGame` and relies on a blank source.

### Read

1. `examples/overlay/index.html:222` — the hide that runs before
   `renderField`.
2. `examples/overlay/index.html:129` `valueFor` and `:82` `labels`.
3. `README.md` Stream overlay — "While not in game, or if the bridge is
   unreachable or errors, the page is blank" and the `field` table, which
   omits `inGame`.
4. `src/MortarSmapiBridge/OverlayServer.cs` `NotInGameJson` — the payload
   `field=inGame` would read.

### Traps

- **Do not open a hole for other fields.** `field=money` at the title
  screen must stay blank. Branch on `field === "inGame"` (the query
  string), not on `data.inGame`.
- **Boolean rendering is a string.** OBS text sources need `"true"` /
  `"false"`, not a JSON boolean left in the element.
- **Unreachable and HTTP errors still hide.** The `catch` at `:229` is
  unchanged. Only a 200 `{"inGame":false}` with `field=inGame` shows text.

### Acceptance

- `?field=inGame` shows `true` in a loaded save and `false` at the title
  screen (token and port valid). Other `field=` values stay blank when
  `inGame` is false.
- No `field` still blanks the full layout when not in game.
- README `field` table includes `inGame`. Gate stays green (example is
  not compiled; do not add a browser test).
