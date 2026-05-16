---
name: hedgedoc
description: "Use when working with the production HedgeDoc wiki at hedgedoc.your.domain: checking server status/config, reading note metadata or markdown exports, creating/importing notes, inspecting note history, or using HedgeDoc HTTP API endpoints."
---

# HedgeDoc

Use this skill for the production HedgeDoc instance deployed from:

```sh
production/kubernetes/helm-charts/hedgedoc
production/kubernetes/flux/apps/hedgedoc/helmrelease.yaml
```

The service is exposed at `https://hedgedoc.your.domain/` and is used as a
wiki, documentation space, and temporary notes store.

## API Facts

- Production version checked on 2026-05-17: `1.10.8-b09975a3ba1191cc354112097236ca57eab763c8`.
- Official API docs: `https://docs.hedgedoc.org/dev/api/`.
- Public endpoints include `/status` and `/config`.
- Note endpoints use the note id or alias directly in the URL, for example
  `/<NOTE>/info`, `/<NOTE>/download`, and `/<NOTE>/revision`.
- New markdown notes can be imported with `POST /new` or `POST /new/<ALIAS>`
  using `Content-Type: text/markdown`. The server returns a redirect to the
  created note when creation succeeds.
- User endpoints such as `/me` and `/history` require a logged-in session.
- The production chart sets `CMD_ALLOW_ANONYMOUS=false` and
  `CMD_ALLOW_ANONYMOUS_EDITS=false`, so unauthenticated note creation or user
  history access may redirect to `/` or return forbidden.
- Production auth supports direct HedgeDoc password login for the dedicated
  API user. On 2026-05-17, logging in as `codex@your.domain` created a
  session and `/me` returned the `codex` profile.
- Do not assume Basic auth works: on 2026-05-17, Basic auth with
  `HEDGEDOC_API_USERNAME` and `HEDGEDOC_API_PASSWORD` returned
  `{"status":"forbidden"}` for `/me`.

## Access

Prefer the bundled helper for HTTP API calls:

```sh
skills/hedgedoc/scripts/hedgedoc-api status
skills/hedgedoc/scripts/hedgedoc-api config
skills/hedgedoc/scripts/hedgedoc-api info NOTE
skills/hedgedoc/scripts/hedgedoc-api download NOTE
```

The helper calls `https://hedgedoc.your.domain` by default. Use
`HEDGEDOC_URL` only when intentionally targeting another instance:

```sh
HEDGEDOC_URL=https://hedgedoc.example.test \
  skills/hedgedoc/scripts/hedgedoc-api status
```

For endpoints that require a session, provide a cookie jar path:

```sh
export HEDGEDOC_COOKIE_FILE=/tmp/hedgedoc-cookies.txt
skills/hedgedoc/scripts/hedgedoc-api me
skills/hedgedoc/scripts/hedgedoc-api history
```

If password login is enabled on a target HedgeDoc instance, the helper can
create or update the cookie jar:

```sh
export HEDGEDOC_API_USERNAME='codex'
export HEDGEDOC_API_PASSWORD='<from password manager or shell secret>'
export HEDGEDOC_COOKIE_FILE=/tmp/hedgedoc-cookies.txt
skills/hedgedoc/scripts/hedgedoc-api login
```

HedgeDoc's `/login` form expects an email address. The helper uses
`HEDGEDOC_API_EMAIL` when set. Otherwise it uses `HEDGEDOC_API_USERNAME`; for
the production instance, a short username such as `codex` is expanded to
`codex@your.domain`.

Do not paste `HEDGEDOC_API_PASSWORD`, cookies, or `connect.sid` values into
commits, skill files, final answers, or shell history.

## Common Reads

Check server status:

```sh
skills/hedgedoc/scripts/hedgedoc-api status
```

Read runtime config and version:

```sh
skills/hedgedoc/scripts/hedgedoc-api config
```

Inspect a note:

```sh
skills/hedgedoc/scripts/hedgedoc-api info NOTE
skills/hedgedoc/scripts/hedgedoc-api download NOTE
skills/hedgedoc/scripts/hedgedoc-api revisions NOTE
skills/hedgedoc/scripts/hedgedoc-api revision NOTE REVISION_ID
```

Read the logged-in user's profile or history:

```sh
skills/hedgedoc/scripts/hedgedoc-api me
skills/hedgedoc/scripts/hedgedoc-api history
```

## Create Notes

Import markdown from a file:

```sh
skills/hedgedoc/scripts/hedgedoc-api create ./note.md
```

Import markdown from stdin:

```sh
printf '%s\n' '# Title' '' 'Body' |
  skills/hedgedoc/scripts/hedgedoc-api create -
```

Import markdown with an alias when free-url mode is enabled:

```sh
skills/hedgedoc/scripts/hedgedoc-api create-alias my-note ./note.md
```

The helper prints the `Location:` header for successful redirects. Use that URL
as the note identifier for follow-up reads.

## Raw Requests

Use `raw` for an endpoint that the helper does not wrap yet:

```sh
skills/hedgedoc/scripts/hedgedoc-api raw GET /status
skills/hedgedoc/scripts/hedgedoc-api raw GET /NOTE/info
skills/hedgedoc/scripts/hedgedoc-api raw POST /new ./note.md text/markdown
```

The `raw` form is:

```sh
skills/hedgedoc/scripts/hedgedoc-api raw METHOD PATH [BODY_FILE|-] [CONTENT_TYPE]
```

## Safety

- Treat note creation, history replacement, pinning, and deletion as
  production mutations.
- Fetch note metadata before editing or importing over an alias.
- Do not delete history or notes unless the user explicitly asks.
- Keep credentials and cookie jars outside the repository.
- Use only HTTPS Gateway/API access for routine automation; do not use
  `kubectl exec` for HedgeDoc API work.
