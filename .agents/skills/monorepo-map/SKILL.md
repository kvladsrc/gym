---
name: monorepo-map
description: "Use when working in the user's src monorepo and needing repository orientation, task workflow, repo skill routing, top-level directory purpose, where to look for code, infra, generated files, service charts, Cothic, CV, Go tools, C++ algorithms, or which local workflow to use before editing."
---

# Monorepo Map

Use this skill to get oriented before reading large parts of the `src`
monorepo. It is a lightweight repository map, not a replacement for exact file
search. Treat this skill as the entry point for the repository: it points to
the other local skills, the task tracker, the documentation system, and the
Gerrit/Jujutsu review workflow so the user does not need to mention each skill
explicitly.

## First Moves

1. Start from the narrowest likely directory below.
2. Read local README/PLAN/config files before broad code search.
3. Ignore generated, vendored, cache, and build-output paths unless the task is
   specifically about them.
4. Use repository tooling through Nix and `just`; do not invent ad hoc
   validation commands when a recipe exists.
5. For production work, check Kanboard `Ready` tasks first when the user asks
   to continue autonomously.
6. Treat production as GitOps-managed: make production state changes through
   repository edits, Gerrit CLs, CI/review, submit, and Flux reconciliation.
   Use `kubectl`, service APIs, SSH admin commands, and similar direct
   production tools for read-only inspection unless the user explicitly asks
   for a manual emergency operation.

## Repository Skills

All repository-local skills live under `.agents/skills/` (exposed to Claude
Code through the `.claude/skills` symlink). Use this map to choose the right
workflow without requiring the user to reference each skill:

- `monorepo-tooling` - use for Nix, `just`, lint, format, tests, scripts,
  Cothic checks, CV builds, and monorepo tooling commands.
- `gerrit-jujutsu` - use for `jj` status/log/show, Gerrit change
  descriptions, review upload, fetch/rebase, and merged-change cleanup.
- `kanboard` - use for the production Kanboard task tracker at
  `https://b.your.domain/`: list projects/tasks, move cards, assign work,
  and close completed tasks.
- `zuul` - use for the production Zuul CI API at
  `https://ci.your.domain/api`: inspect pipeline status, jobs, projects,
  builds, and buildsets related to Gerrit changes.
- `ntfy` - use for the production ntfy notification service at
  `https://notify.your.domain`: read recent topic notifications, summarize
  incident signals, and correlate alerts with Flux, Zuul, Kubernetes,
  Terraform, or cert-manager state.
- `monorepo-map` - use first for repository orientation, skill routing,
  directory layout, production component locations, and the working process.

## Working Process

For task-driven infrastructure work, use Kanboard as the source of truth and
Gerrit/Jujutsu as the change workflow:

1. Pick a task from the Kanboard `Infrastructure` project `Ready` column.
2. Move that task to `Work in progress` before editing.
3. Create a focused `jj` change for the task. If another CL is open, create a
   separate sibling change from `master@origin` unless a stack is intended.
4. Implement the smallest reviewable change and run the narrowest relevant
   validation through `/nix/var/nix/profiles/default/bin/nix develop -c just`.
5. Describe the change non-interactively with `jj desc`, including the
   Kanboard task id and validation.
6. Upload to Gerrit with `jj gerrit upload -r @`.
7. Watch Zuul check/gate status through the `zuul` skill; fix failures in a
   new patchset on the same change.
8. Wait for review and submit. Do not mark the task done before submit.
9. After submit, move the Kanboard task to `Done`.

If a task is blocked by a missing prerequisite, move it back to `Ready` or
`Backlog`, leave a Kanboard comment with the reason, and take the prerequisite
task instead.

Production mutations must flow through GitOps. Do not create Kubernetes
Secrets, patch live resources, create service accounts, edit Gerrit ACLs, or
call write APIs directly as part of normal implementation. Use direct tools
only to observe current state and verify reconciled changes after merge. If a
manual write is unavoidable, get explicit user approval and document the
follow-up GitOps change that makes the live state reproducible.

Use repository markdown for durable documentation and runbooks, especially
when the documentation must be versioned with infrastructure changes or
reviewed through Gerrit.

## Top-Level Map

- `production/` - production infrastructure, Docker images, Terraform, Talos,
  Flux, Helm charts, and Kubernetes service deployment.
- `.agents/skills/` - local agent skills and helper scripts shared across
  CLIs. Each skill has `SKILL.md`; most also have `agents/openai.yaml`
  (Codex-only metadata). Claude Code loads them via the `.claude/skills`
  symlink.
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
  `just cv build`.
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
  services such as Gerrit, Zuul, Kanboard, Authentik, MinIO,
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

- Cothic: `just cothic check`, `just cothic fmt`, `just cothic lint`,
  `just cothic smoke`.
- CV: `just cv build`.
- Public repo sync: `just sync-gym`.
- Talos certificate renewal: `just talos renew-certs`.

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
- Build/test behavior: inspect `justfile`, `flake.nix`, Bazel files, and the
  `monorepo-tooling` skill.
- Gerrit/JJ state or upload: use the `gerrit-jujutsu` skill; do not use raw
  destructive Git cleanup commands.
