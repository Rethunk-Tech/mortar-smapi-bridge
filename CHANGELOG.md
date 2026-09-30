# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.1] - 2026-09-30

### Added

- `MinimumGameVersion` (1.6.14) in the manifest.
- The mod disables itself with a warning on a SMAPI minor version newer than the tested 4.5.x.

### Changed

- `MinimumApiVersion` raised from 4.0.0 to 4.5.2, the version the reflection lookup was verified against.
- The version is set once in the csproj; the manifest is generated from it and CI rejects a mismatching tag.

## [1.0.0]

### Added

- Loopback console-command bridge for Mortar.
