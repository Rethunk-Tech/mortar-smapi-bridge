# Mortar SMAPI Bridge

C# SMAPI mod (net6.0) that lets Mortar send console commands: the SMAPI loader's companion, not a Stardew-only tool (BepInEx games use mortar-bepinex-bridge). Compiles against `StardewModdingAPI.dll`, `SMAPI.Toolkit.CoreInterfaces.dll` (LGPL) and SMAPI's bundled `0Harmony.dll` (MIT), none of them shipped; never reference or ship game assemblies.

- Build, test, SMAPI dll override, versioning and release: [HUMANS.md](HUMANS.md).
- Command protocol, discovery file and overlay contract: [README.md](README.md).
- Commands reach SMAPI through `SCore.RawCommandQueue`; re-check it on every SMAPI bump, then update `MinimumApiVersion` and `ApiRange`.
- The overlay and command servers stay on `TcpListener` with their own small HTTP handling: `HttpListener` is Microsoft's not-for-new-development class, built on HTTP.sys on Windows (URL reservations, host matching) and a narrower managed implementation elsewhere, so it would trade a few dozen lines for platform-dependent behaviour.
- Do not launch the game from agents.
- The version lives in the csproj `<Version>`, not MinVer: an untagged MinVer build reports a `0.0.0-alpha` version in SMAPI's mod list, and CI already fails a release whose tag differs.

## Verify

`gate` is the offline gate ([HUMANS.md](HUMANS.md)). Nothing is merged on a red gate.
