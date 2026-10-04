# Mortar SMAPI Bridge

C# SMAPI mod (net6.0) that lets Mortar send console commands. Compiles against `StardewModdingAPI.dll`, `SMAPI.Toolkit.CoreInterfaces.dll` (LGPL) and SMAPI's bundled `0Harmony.dll` (MIT), none of them shipped; never reference or ship game assemblies.

- Build, test, SMAPI dll override, versioning and release: [HUMANS.md](HUMANS.md).
- Command protocol, discovery file and overlay contract: [README.md](README.md).
- Commands reach SMAPI through `SCore.RawCommandQueue`; re-check it on every SMAPI bump, then update `MinimumApiVersion` and `ApiRange`.
- Do not launch the game from agents.

## Verify

`gate` is the offline gate ([HUMANS.md](HUMANS.md)). Nothing is merged on a red gate.
