# Mortar SMAPI Bridge runbook

How to build, test and install the mod. What it does: [README.md](README.md); rules for agents: [AGENTS.md](AGENTS.md).

## Build and test

Requires the .NET SDK 8 or later (output targets net6.0).

```sh
dotnet build -c Release   # fetches and sha256-verifies SMAPI 4.5.2's StardewModdingAPI.dll and SMAPI.Toolkit.CoreInterfaces.dll into lib/
dotnet test -c Release
```

To use an existing install instead: `dotnet build -c Release -p:SmapiDll="/path/to/StardewModdingAPI.dll" -p:SmapiCoreInterfacesDll="/path/to/smapi-internal/SMAPI.Toolkit.CoreInterfaces.dll"`.

Install by copying `MortarSmapiBridge.dll` and `manifest.json` from `src/MortarSmapiBridge/bin/Release/net6.0/` into `Mods/MortarSmapiBridge/`. Releases ship that folder as `MortarSmapiBridge-<version>.zip` with a `.sha256`.


## Versioning and release

`<Version>` in `src/MortarSmapiBridge/MortarSmapiBridge.csproj` is the only place the version is written. It sets the assembly version and generates the shipped `manifest.json` from `manifest.template.json`. To release: bump `<Version>`, move the CHANGELOG `Unreleased` entries under the new version, commit, push, then tag `v<version>`. CI fails the release if the tag differs from `<Version>`, and names the zip from it.

`MinimumApiVersion` is the lowest SMAPI whose internals the reflection lookup was verified against. SMAPI has no maximum-version field, so `ApiRange` in the mod refuses newer minors at startup; widen it only after re-verifying `SCore.RawCommandQueue` on the new SMAPI.

## Nexus Mods

Not yet published. The release zip is already in the layout Nexus and SMAPI expect (one `MortarSmapiBridge/` folder with `manifest.json` and the dll). Page description text: the README's "Nexus page description" section. When the Nexus page exists, add `"Nexus:<mod id>"` to `UpdateKeys` in `manifest.template.json` (SMAPI checks every listed key). Permissions to set on the page: open source under AGPL-3.0, others may modify and redistribute with the licence retained; credit SMAPI (Pathoschild, LGPL-3.0) as the API the mod builds against.
