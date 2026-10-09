# Mortar SMAPI Bridge

C# SMAPI mod (net6.0) that lets Mortar send console commands: the SMAPI loader's companion, not a Stardew-only tool (BepInEx games use mortar-bepinex-bridge). Compiles against `StardewModdingAPI.dll`, `SMAPI.Toolkit.CoreInterfaces.dll` (LGPL) and SMAPI's bundled `0Harmony.dll` (MIT), none of them shipped; never reference or ship game assemblies.

- Build, test, SMAPI dll override, versioning and release: [HUMANS.md](HUMANS.md).
- Command protocol, discovery file and overlay contract: [README.md](README.md).
- Commands reach SMAPI through `SCore.RawCommandQueue`; re-check it on every SMAPI bump, then update `MinimumApiVersion` and `ApiRange`.
- The overlay and command servers stay on `TcpListener` with their own small HTTP handling; `HttpListener` behaves differently per platform.
- Do not launch the game from agents.
- The version lives in the csproj `<Version>`, not MinVer; CI fails a release whose tag differs.

## Verify

`gate` is the offline gate ([HUMANS.md](HUMANS.md)). Nothing is merged on a red gate.

## Gate budget

`.gate.toml` makes the build restore with `--locked-mode` as CI does, so a drifted `packages.lock.json` fails locally too; the detected build restored unlocked. Measured 2026-10-09 with `gate --profile` at load 3 to 4 (CPU is the evidence): warm 2.1 to 2.9 s wall and about 1.7 CPU-s; cold (a clone without `bin/` or `obj/`, throwaway `NUGET_PACKAGES`, the gitignored `lib/` carried over) 6.5 s wall and 3.3 CPU-s, 5.3 s of it restore and build. Within budget; build and test are chained because test runs `--no-build`, and nothing else repeats work.
