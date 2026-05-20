---
name: kanboard
description: "Use when working with the production Kanboard instance deployed from production/kubernetes/helm-charts/kanboard-wrapper: discovering the JSON-RPC API, listing projects and tasks, creating tasks, moving tasks between board columns, assigning tasks, or opening/closing/updating tasks."
---

# Kanboard

Use this skill for the production Kanboard instance deployed by:

```sh
production/kubernetes/helm-charts/kanboard-wrapper
production/kubernetes/flux/apps/kanboard/helmrelease.yaml
```

The release is exposed through Gateway API at `https://b.your.domain/`.
The chart enables reverse-proxy auth for the web UI, but Kanboard also exposes
a JSON-RPC API at `/jsonrpc.php`.

## API Facts

- Kanboard API protocol: JSON-RPC 2.0.
- Production version checked on 2026-05-16: `v1.2.50`.
- Public Gateway API endpoint: `https://b.your.domain/jsonrpc.php`.
- Authentik proxy skips only `^/jsonrpc\.php$`; Kanboard still requires Basic
  auth for that endpoint.
- Store the Kanboard API username in `KANBOARD_API_USERNAME`; default is
  `jsonrpc` for the global application API token.
- Store the matching Kanboard API token or password in `KANBOARD_API_TOKEN`.
- The `jsonrpc` application API credential can call global procedures such as
  `getAllProjects`, `getAllUsers`, and project-wide task queries.
- Personal user API credentials use the Kanboard username and that user's API
  token or password. They are limited by user permissions; use `getMe` and
  `getMyProjects` first to discover what the credential can access.
- Do not paste the API token into commits, skill files, final answers, or shell
  history.
- Official docs: `https://docs.kanboard.org/v1/api/`.

## Access

Prefer the bundled helper for JSON-RPC calls. In normal agent use, provide the
API token through the environment:

```sh
export KANBOARD_API_USERNAME='jsonrpc'
export KANBOARD_API_TOKEN='<from password manager or shell secret>'
```

The helper calls `https://b.your.domain/jsonrpc.php` by default:

```sh
skills/kanboard/scripts/kanboard-rpc getVersion
skills/kanboard/scripts/kanboard-rpc getAllProjects
skills/kanboard/scripts/kanboard-rpc getAllTasks '{"project_id":1,"status_id":1}'
```

The helper redacts token and password fields from JSON responses before
printing them.

When using a personal user API credential instead of the global `jsonrpc`
application token, start with user-scoped discovery:

```sh
skills/kanboard/scripts/kanboard-rpc getMe
skills/kanboard/scripts/kanboard-rpc getMyProjects
```

Manual external call:

```sh
curl -sS -u "$KANBOARD_API_USERNAME:$KANBOARD_API_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"getAllProjects","id":1}' \
  https://b.your.domain/jsonrpc.php
```

## Read Workflows

List projects:

```sh
skills/kanboard/scripts/kanboard-rpc getAllProjects
```

List active tasks for a project:

```sh
skills/kanboard/scripts/kanboard-rpc getAllTasks '{"project_id":1,"status_id":1}'
```

List closed tasks for a project:

```sh
skills/kanboard/scripts/kanboard-rpc getAllTasks '{"project_id":1,"status_id":0}'
```

Get board columns before moving tasks. Production projects use the same column
names, but IDs are project-specific:

```sh
skills/kanboard/scripts/kanboard-rpc getColumns '{"project_id":1}'
```

Current production column IDs:

- Project 1: Backlog `1`, Ready `2`, Work in progress `3`, Done `4`.
- Project 2: Backlog `5`, Ready `6`, Work in progress `7`, Done `8`.
- Project 3: Backlog `9`, Ready `10`, Work in progress `11`, Done `12`.
- Project 4: Backlog `13`, Ready `14`, Work in progress `15`, Done `16`.
- Project 6: Backlog `21`, Ready `22`, Work in progress `23`, Done `24`.
- Project 7: Backlog `25`, Ready `26`, Work in progress `27`, Done `28`.

List users before assigning work:

```sh
skills/kanboard/scripts/kanboard-rpc getAllUsers
```

## Create And Edit Tasks

Create a task in a project:

```sh
params='{
  "project_id": 1,
  "title": "Task title",
  "column_id": 1,
  "owner_id": 2
}'
skills/kanboard/scripts/kanboard-rpc createTask "$params"
```

Update task fields such as title, description, owner, priority, due date, or
color:

```sh
skills/kanboard/scripts/kanboard-rpc updateTask '{"id":123,"title":"New title","owner_id":2}'
```

Take a task into work: assign it and move it to the project-specific
`Work in progress` column. Use `getTask` first to preserve its current
`position` and `swimlane_id`, then call `updateTask` and `moveTaskPosition`:

```sh
skills/kanboard/scripts/kanboard-rpc getTask '{"task_id":123}'
skills/kanboard/scripts/kanboard-rpc updateTask '{"id":123,"owner_id":2}'
params='{
  "project_id": 1,
  "task_id": 123,
  "column_id": 3,
  "position": 1,
  "swimlane_id": 1
}'
skills/kanboard/scripts/kanboard-rpc moveTaskPosition "$params"
```

Move a task to Done without closing it:

```sh
params='{
  "project_id": 1,
  "task_id": 123,
  "column_id": 4,
  "position": 1,
  "swimlane_id": 1
}'
skills/kanboard/scripts/kanboard-rpc moveTaskPosition "$params"
```

Close or reopen a task:

```sh
skills/kanboard/scripts/kanboard-rpc closeTask '{"task_id":123}'
skills/kanboard/scripts/kanboard-rpc openTask '{"task_id":123}'
```

## Safety

- Treat create, update, move, close, open, and remove calls as production
  mutations. Confirm intent when the requested project/task is ambiguous.
- Store the API token outside the repository. Use `KANBOARD_API_TOKEN` from the
  shell, direnv, a password manager, or the Codex runtime environment.
- If using a dedicated Kanboard web user, set `KANBOARD_API_USERNAME` to that
  username and `KANBOARD_API_TOKEN` to its Kanboard API token or password.
- Before moving a task, call `getTask` and `getColumns` so the `project_id`,
  `column_id`, `position`, and `swimlane_id` are explicit.
- Do not call `removeTask` unless the user explicitly asks to delete.
- Use only the Gateway API path for Kanboard automation; do not use
  `kubectl exec` for routine API calls.
