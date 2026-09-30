# Design: decided work not yet built

## Stream overlay data (NOMAD, 2026-09-30; after Mortar's Nexus milestone)

Streamers want the game's live state in OBS overlays: the current location, the player's name, the date (season, day, year) and time of day, money, weather, health and stamina, and skill levels.

- **Endpoint:** a read-only `GET /state` on the bridge's loopback listener returning that state as JSON, polled by an OBS browser source (a small example overlay page goes in this repo).
- **Stable address:** OBS keeps a fixed URL, so overlay mode listens on a fixed, user-chosen port (opt-in, off by default), not the per-session port the command channel uses.
- **Separate token:** a persistent read-only overlay token, separate from the per-session command token, so an overlay can never run console commands. Mortar shows the overlay URL and token with Copy buttons and can regenerate the token.
- **Open question, decide before building:** reading game state needs Stardew Valley's own assemblies at compile time, which are proprietary and cannot be downloaded in CI, while the command bridge builds from SMAPI's LGPL `StardewModdingAPI.dll` alone. Options: build releases locally against the Steam install (CI builds and tests only the command part), or read state through SMAPI's reflection helpers so CI still builds from SMAPI's DLL (more fragile across game updates).
