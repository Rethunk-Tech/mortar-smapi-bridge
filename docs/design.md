# Design: decided work not yet built

## Stream overlay data (NOMAD, 2026-09-30; after Mortar's Nexus milestone)

Streamers want the game's live state in OBS overlays: the current location, the player's name, the date (season, day, year) and time of day, money, weather, health and stamina, and skill levels.

- **Endpoint:** a read-only `GET /state` on the bridge's loopback listener returning that state as JSON, polled by an OBS browser source (a small example overlay page goes in this repo).
- **Stable address:** OBS keeps a fixed URL, so overlay mode listens on a fixed, user-chosen port (opt-in, off by default), not the per-session port the command channel uses.
- **Separate token:** a persistent read-only overlay token, separate from the per-session command token, so an overlay can never run console commands. Mortar shows the overlay URL and token with Copy buttons and can regenerate the token.
- **Reading state:** through SMAPI's reflection helpers (NOMAD, 2026-09-30), so the mod still compiles against SMAPI's `StardewModdingAPI.dll` alone and CI builds and tests everything. Game field names can change in a game update, so the overlay pins the game versions it was verified against the way `ApiRange` pins SMAPI (`src/MortarSmapiBridge/ApiRange.cs`), and outside that range it stays off and logs a warning while the command channel keeps working.

### Where it lands

- Listener: `BridgeServer` (`src/MortarSmapiBridge/BridgeServer.cs:14`) binds `IPAddress.Loopback` on port 0; overlay mode needs a second listener bound to the fixed port.
- Command token: generated in `ModEntry` (`src/MortarSmapiBridge/ModEntry.cs:26`) and checked by `CommandLine.TokenMatches` (`src/MortarSmapiBridge/CommandLine.cs:73`); the overlay token is a separate value, so this check stays untouched.
- State file: `WriteStateFile` (`src/MortarSmapiBridge/ModEntry.cs:31`, `:70`) writes port, token and pid; the overlay port and token are not added to it, since they persist across sessions.

### Acceptance criteria

- With overlay mode off, no second port is open.
- `GET /state` with the overlay token returns the fields above as JSON; a missing or wrong token returns 401 and the command token is rejected.
- No overlay request can reach the command queue.
- `dotnet build` and `dotnet test` pass in CI without Stardew Valley installed.
- Outside the verified game version range the overlay listener does not start and SMAPI's log says why.
