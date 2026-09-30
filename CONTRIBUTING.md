# Contributing

A private Rethunk-AI project until its first release. This is the workflow for maintainers and agents working on this C# SMAPI mod; the rules the code keeps are in [`AGENTS.md`](AGENTS.md), and every command is in [`HUMANS.md`](HUMANS.md).

## Before review

Run `gate` ([`HUMANS.md`](HUMANS.md)). Nothing is merged on a red gate, and no hook is bypassed.

## Commits

- Conventional Commits: `type(scope): subject`, one logical change each.
- Stage explicit paths only.
- The body, when there is one, says why; the diff already says what.

## Releases

The version lives only in the csproj `<Version>`. It generates the shipped `manifest.json` from `manifest.template.json`. Push a `v*` tag that matches that version; CI publishes `MortarSmapiBridge-<version>.zip` and its `.sha256`, and fails the release if the tag differs.

## Pending work

Decided work that is not built yet is specified in [`docs/design.md`](docs/design.md). When an item lands, delete it there in the same change.
