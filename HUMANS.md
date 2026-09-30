# Mortar SMAPI Bridge runbook

How to build, test and install the mod. What it does: [README.md](README.md); rules for agents: [AGENTS.md](AGENTS.md).

## Build and test

Requires the .NET SDK 8 or later (output targets net6.0).

```sh
dotnet build -c Release   # fetches and sha256-verifies SMAPI 4.5.2's StardewModdingAPI.dll into lib/
dotnet test -c Release
```

To use an existing install instead: `dotnet build -c Release -p:SmapiDll="/path/to/StardewModdingAPI.dll"`.

Install by copying `MortarSmapiBridge.dll` and `manifest.json` from `src/MortarSmapiBridge/bin/Release/net6.0/` into `Mods/MortarSmapiBridge/`. Releases ship that folder as `MortarSmapiBridge-<version>.zip` with a `.sha256`.

