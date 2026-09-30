# Mortar SMAPI Bridge

C# SMAPI mod (net6.0) that lets Mortar send console commands. Compiles against `StardewModdingAPI.dll` only (LGPL); never reference or ship game assemblies.

- Build, test and the SMAPI dll override: [HUMANS.md](HUMANS.md). `scripts/fetch-smapi.sh` pins the SMAPI version; `dotnet build` runs it when `lib/` is empty.
- Release: push a `v*` tag; CI publishes `MortarSmapiBridge-<version>.zip` and its `.sha256`. Keep `manifest.json` Version equal to the tag.
- Commands reach SMAPI through reflection on `SCore.RawCommandQueue` (README § Protocol); re-check it on every SMAPI bump.
- Do not launch the game from agents.
