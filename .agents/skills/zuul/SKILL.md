---
name: zuul
description: "Use when working with the production Zuul CI instance: checking the public REST API, listing tenants/jobs/projects/builds, inspecting pipeline status, and relating Zuul results to Gerrit changes."
---

# Zuul

Use this skill for the production Zuul CI instance deployed from:

```sh
production/kubernetes/helm-charts/zuul
production/kubernetes/flux/apps/zuul/helmrelease.yaml
zuul.yaml
```

The web UI is exposed at `https://ci.your.domain/` through the Authentik
proxy outpost. The REST API is under `/api` and is intentionally public.

## API Facts

- Public API base URL: `https://ci.your.domain/api`.
- Default tenant: `your.domain`.
- Production version checked on 2026-05-17: `14.2.1.dev40 b5681c282`.
- Authentik proxy should skip `^/api(/.*)?$` for the Zuul provider.
- The Zuul API currently reports `read_protected: false`, so basic read
  workflows do not require Authentik login or API credentials.

## Access

Prefer the bundled helper for simple API reads:

```sh
.agents/skills/zuul/scripts/zuul-api /info
.agents/skills/zuul/scripts/zuul-api /tenants
.agents/skills/zuul/scripts/zuul-api /tenant/your.domain/status
.agents/skills/zuul/scripts/zuul-api /tenant/your.domain/jobs
```

Manual calls are just HTTPS GET requests:

```sh
curl -fsS https://ci.your.domain/api/info
curl -fsS https://ci.your.domain/api/tenant/your.domain/status
```

Use `ZUUL_API_URL` only when intentionally targeting another deployment:

```sh
ZUUL_API_URL=https://ci.example.test/api .agents/skills/zuul/scripts/zuul-api /info
```

## Common Reads

List tenants:

```sh
.agents/skills/zuul/scripts/zuul-api /tenants
```

Inspect pipeline status:

```sh
.agents/skills/zuul/scripts/zuul-api /tenant/your.domain/status
```

List jobs and projects:

```sh
.agents/skills/zuul/scripts/zuul-api /tenant/your.domain/jobs
.agents/skills/zuul/scripts/zuul-api /tenant/your.domain/projects
```

Inspect recent builds or buildsets:

```sh
.agents/skills/zuul/scripts/zuul-api '/tenant/your.domain/builds?limit=20'
.agents/skills/zuul/scripts/zuul-api '/tenant/your.domain/buildsets?limit=20'
```

## Authentik Validation

When changing Zuul Authentik proxy settings, verify that API endpoints do not
redirect to Authentik:

```sh
curl -sS -i https://ci.your.domain/api/info
curl -sS -i https://ci.your.domain/api
```

Expected result after deployment: both requests should be handled by Zuul, not
redirected to `/outpost.goauthentik.io/start`. Endpoint-specific HTTP errors
from Zuul are acceptable; Authentik redirects are not.

## Safety

- Do not add credentials to the skill or helper; public read API access is the
  intended path.
- Treat production Zuul and Authentik Terraform changes as deployment-affecting
  infra changes. Keep regex changes narrow and verify with `curl -i`.
- Use Gerrit/Jujutsu workflows from the `gerrit-jujutsu` skill when uploading
  related changes.
