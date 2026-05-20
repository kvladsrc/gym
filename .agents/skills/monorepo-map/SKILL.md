---
name: monorepo-map
description: "Use when working in the user's src monorepo and needing repository orientation: top-level directory purpose, where to look for code, infra, generated files, service charts, agent skills, Cothic, CV, Go tools, C++ algorithms, or which existing skill/tooling workflow to use before editing."
---

# Monorepo Map

Use this skill to get oriented before reading large parts of the `src`
monorepo. It is a lightweight repository map, not a replacement for exact file
search. Use `$monorepo-tooling` for commands, `$gerrit-jujutsu` for review
workflow, and service-specific skills such as `$kanboard`, `$hedgedoc`, or
`$zuul` for production APIs.

## First Moves

1. Start from the narrowest likely directory below.
2. Read local README/PLAN/config files before broad code search.
3. Ignore generated, vendored, cache, and build-output paths unless the task is
   specifically about them.
4. Use repository tooling through Nix and `just`; do not invent ad hoc
   validation commands when a recipe exists.

## Top-Level Map

- `production/` - production infrastructure, Docker images, Terraform, Talos,
  Flux, Helm charts, and Kubernetes service deployment.
- `skills/` - local Codex skills and helper scripts. Each skill has
  `SKILL.md`; most also have `agents/openai.yaml`.
- `cothic/` - Godot 4.6 prototype that turns a repository into an explorable
  isometric world. Main code is under `cothic/scripts/`; checks are in
  `cothic/tools/`.
- `cpp/` - C++ algorithm practice and competitive programming. `codeforces/`
  is contest solutions; `warmup/` is educational data structures and
  algorithms; other subdirectories are course/book/problem sets.
- `pipellm/` - Go CLI around named LLM prompts and shell aliases.
- `ripples_cli/` - Go text RPG engine with Gemini-powered dialogue over a
  static world graph and dynamic event journal.
- `cv/` - resume/CV sources and generated web/PDF output; build with
  `just cv-build`.
- `scripts/` - repository automation, including CV rendering, public repo sync,
  and Talos certificate renewal. Prefer `just` recipes over direct calls.
- `home-manager/` and `gentoo/` - local desktop/Linux configuration.
- `static/` - static assets referenced by the README and public pages.
- `archive/` - historical code and old projects. Treat as reference unless the
  user explicitly asks to modify archived material.
- `pet_project/`, `julia/`, `lisp/` - experiments, language exercises, and
  older learning projects.

## Production Layout

- `production/kubernetes/flux/` - Flux Kustomizations, sources, secrets, app
  wiring, and Terraform controller objects.
- `production/kubernetes/helm-charts/` - local Helm charts and wrappers for
  services such as Gerrit, Zuul, Kanboard, HedgeDoc, Authentik, MinIO,
  PostgreSQL, Redis, Envoy Gateway, and Zooreader.
- `production/kubernetes/talos/` - Talos cluster config and generated `_out`
  artifacts.
- `production/terraform/` - Terraform root and modules for Cloudflare, MinIO,
  Authentik, PostgreSQL, and related production resources.
- `production/docker/` - custom Docker image contexts, including Zooreader.
- `production/playbooks/` - operational Ansible playbooks.

When changing a production service, inspect both its Flux app wiring and its
Helm chart. For example, Kanboard lives in:

```text
production/kubernetes/flux/apps/kanboard/
production/kubernetes/helm-charts/kanboard-wrapper/
```

## Tooling And Validation

Primary entry points:

```sh
/nix/var/nix/profiles/default/bin/nix develop -c just --list
/nix/var/nix/profiles/default/bin/nix develop -c just lint
/nix/var/nix/profiles/default/bin/nix develop -c just test
```

Targeted recipes:

- Cothic: `just cothic-check`, `just cothic-fmt`, `just cothic-lint`,
  `just cothic-smoke`.
- CV: `just cv-build`.
- Public repo sync: `just sync-gym`.
- Talos certificate renewal: `just talos-renew-certs`.

## Paths To Avoid By Default

- VCS/build state: `.git/`, `.jj/`, `.cache/`, `bazel-*`.
- Dependencies and generated state: `node_modules/`, `cothic/.godot/`,
  `cothic/tmp/`, `production/terraform/.terraform/`,
  `production/kubernetes/talos/_out/`.
- Vendor charts: `production/kubernetes/helm-charts/vendor/`.
- Large generated files such as `compile_commands.json` unless the task is
  about compiler database behavior.

## Search Heuristics

- Known symbol or filename: use exact file search first.
- Unknown location but known subsystem: start from the mapped directory and
  local README/config files.
- Kubernetes service behavior: inspect Flux app, Helm chart values/templates,
  and any service-specific skill before editing.
- Build/test behavior: inspect `justfile`, `flake.nix`, Bazel files, and
  `$monorepo-tooling`.
- Gerrit/JJ state or upload: use `$gerrit-jujutsu`; do not use raw destructive
  Git cleanup commands.
