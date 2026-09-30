# Mortar SMAPI Bridge

C# SMAPI mod (net6.0) that lets Mortar send console commands. Compiles against `StardewModdingAPI.dll` only (LGPL); never reference or ship game assemblies.

- SMAPI dll: `scripts/fetch-smapi.sh` (pinned 4.5.2, sha256-verified) fills `lib/`; `dotnet build` runs it if missing. Local override: `-p:SmapiDll=/path/to/StardewModdingAPI.dll`.
- Build: `dotnet build -c Release`
- Test: `dotnet test -c Release` (parser and token check; no game needed)
- Release: push a `v*` tag; CI publishes `MortarSmapiBridge-<version>.zip` and its `.sha256`. Keep `manifest.json` Version equal to the tag.
- Commands reach SMAPI through reflection on `SCore.RawCommandQueue` (no public API in 4.5.2); re-check on every SMAPI bump.
- Do not launch the game from agents.
