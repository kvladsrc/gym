---
name: monorepo-tooling
description: "Use when working in the user's src/gym monorepo and needing the correct local tooling workflow: Nix dev shell invocation, just recipes, lint/test/fmt commands, project-specific checks, scripts, or repository tooling. Trigger for requests mentioning nix develop, just, lint, fmt, tests, scripts, Cothic checks, CV/resume builds, sync-gym, or monorepo tooling."
---

# Monorepo Tooling

Use the repository's own tooling instead of guessing ad hoc commands. The
default workflow is:

1. Enter the repository's Nix development environment.
2. Use `just` as the task orchestrator for tools, checks, generators, and
   scripts.
3. Run commands from the monorepo root unless a recipe documents otherwise.

## Nix Entry Point

Run repository tooling through the monorepo Nix shell. In agent sessions, call
Nix by its absolute path because `nix` may be missing from sandbox `PATH`:

```sh
/nix/var/nix/profiles/default/bin/nix develop -c just <recipe>
```

If a Nix command fails with cache, daemon, or sandbox access errors, rerun the
same command outside the sandbox (accepting the permission prompt if one
appears) rather than bypassing the toolchain.

In managed agent sandboxes, the common Nix failure mode is:

```text
cannot connect to socket at '/nix/var/nix/daemon-socket/socket':
Operation not permitted
```

Rerun the exact same command with elevated sandbox permissions. Warnings such
as `SQLite database ... eval-cache ... is busy` are usually Nix cache noise
when the command exits successfully; do not treat them as failures by
themselves.

## Just Recipes

Use `just` as the entry point for repository tasks. List available recipes
before guessing names:

```sh
/nix/var/nix/profiles/default/bin/nix develop -c just --list
```

Common top-level recipes:

```sh
/nix/var/nix/profiles/default/bin/nix develop -c just lint
/nix/var/nix/profiles/default/bin/nix develop -c just fmt
/nix/var/nix/profiles/default/bin/nix develop -c just test
```

Use project-specific recipes when the task targets a project or component:

```sh
/nix/var/nix/profiles/default/bin/nix develop -c just cothic check
/nix/var/nix/profiles/default/bin/nix develop -c just cothic fmt
/nix/var/nix/profiles/default/bin/nix develop -c just cothic lint
/nix/var/nix/profiles/default/bin/nix develop -c just cothic smoke
/nix/var/nix/profiles/default/bin/nix develop -c just cv build
/nix/var/nix/profiles/default/bin/nix develop -c just sync-gym
```

For CV/resume work, prefer `just cv build`; for syncing public output, use
`just sync-gym` and report that it can update many files in `~/repos/gym`.

## Scripts

Prefer `just` recipes over calling `scripts/` directly. If a needed script has
no recipe, inspect the script and surrounding recipes first, then run it through
the Nix shell so it uses repository-provided dependencies.

## Validation

Choose the narrowest relevant validation first, then broaden when the change
touches shared tooling or cross-project behavior:

1. Run the narrow build/check first, for example `just test`, `just lint`,
   `just cv build`, or `just cothic check`.
2. Run full lint with `just lint`.
3. Run full tests with `just test` when the change can affect runtime behavior.

Some pre-commit hooks can auto-fix on the first pass. If `just lint` reports
"files were modified by this hook", inspect the changes, make the generator
produce that format directly, then rerun `just lint`.

For Go changes that add or remove module dependencies, check all three
integration points:

```sh
go.mod
go.sum
MODULE.bazel
MODULE.bazel.lock
```

`go.sum` may already contain the needed checksum and remain unchanged. Bazel
commands can update `MODULE.bazel.lock`; inspect the lockfile after tests and
include it only when it reflects the dependency change.

## Repository Hygiene

- Keep edits scoped to the requested project or tooling files.
- Do not stage, commit, restore, reset, or otherwise alter VCS state unless the
  user explicitly asks.
- Prefer fixing generated output at the generator/source level instead of adding
  pre-commit exclusions.
