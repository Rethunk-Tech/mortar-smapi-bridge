# Contributing

A private Rethunk-AI project until its first release. This is the workflow for maintainers and agents; the rules the code keeps are in [`AGENTS.md`](AGENTS.md), and every command is in [`HUMANS.md`](HUMANS.md).

## Before review

Run `dotnet build -c Release` and `dotnet test -c Release`; CI runs the same two commands. Nothing is merged on a red gate, and no hook is bypassed.

## Commits

- Conventional Commits: `type(scope): subject`, one logical change each.
- Stage explicit paths only.
- The body, when there is one, says why; the diff already says what.

## Releases

Push a `v*` tag with `manifest.json` Version equal to it; CI publishes the zip and its `.sha256`.
