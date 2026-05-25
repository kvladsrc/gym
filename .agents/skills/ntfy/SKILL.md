---
name: ntfy
description: "Use when working with the production ntfy notification service at notify.your.domain: checking API access, reading recent notifications from known topics, decoding JSON payloads, correlating notifications with Flux, Zuul, Kubernetes, Terraform, or cert-manager events, and validating Authentik path exceptions for read-only subscription endpoints."
---

# ntfy

Use this skill to inspect production notifications for incident analysis. The
default workflow is read-only: subscribe, poll, and summarize messages. Do not
publish messages unless the user explicitly asks for a test notification.

## Service

- External URL: `https://notify.your.domain`
- Cluster-local URL: `http://ntfy.ntfy.svc.cluster.local`
- Production topics:
  - `prod-gitops` - Flux reconciliation errors.
  - `prod-ci` - Zuul check and gate failures.
  - `prod-k8s-warnings` - filtered Kubernetes Warning events, including
    tofu-controller/Terraform and cert-manager warnings.
- Reserved topics:
  - `prod-infra`
  - `prod-certs`
  - `prod-backups`

## Access

Prefer the bundled helper:

```sh
.agents/skills/ntfy/scripts/ntfy-api topics
.agents/skills/ntfy/scripts/ntfy-api latest prod-gitops
.agents/skills/ntfy/scripts/ntfy-api poll prod-k8s-warnings 30m
.agents/skills/ntfy/scripts/ntfy-api summary prod-ci 2h
```

Environment:

- `NTFY_URL` - base URL, default `https://notify.your.domain`.
- `NTFY_TOKEN` - optional bearer token.
- `NTFY_USERNAME` and `NTFY_PASSWORD` - optional basic auth.

The production Authentik bypass should cover read-only subscription endpoints
for known topics:

```text
^/(prod-gitops|prod-ci|prod-k8s-warnings|prod-infra|prod-certs|prod-backups)/(json|sse|raw|ws)$
```

This intentionally does not bypass the publish path `/<topic>`.

## Analysis Workflow

1. Pick the topic from the suspected system:
   - Flux or reconciliation failures: `prod-gitops`.
   - Zuul or Gerrit review CI failures: `prod-ci`.
   - Kubernetes, Terraform/tofu-controller, or cert-manager warnings:
     `prod-k8s-warnings`.
2. Poll recent cached messages with `summary TOPIC SINCE`, usually `30m`,
   `2h`, or `12h`.
3. Look at `time`, `title`, `priority`, `tags`, and `message`.
4. If the message body is JSON, inspect fields such as `reason`, `namespace`,
   `kind`, `name`, `count`, `involvedObject`, `reportingController`, and
   `metadata.revision`.
5. Correlate with the source system using the matching skill:
   - `$zuul` for CI build/buildset failures.
   - `$gerrit-jujutsu` for Gerrit changes.
   - `$monorepo-map` for GitOps paths and repository ownership.

## Safety

- Treat ntfy as an observation source unless the user asks for a test publish.
- Do not commit tokens or cookies.
- Do not broaden Authentik bypasses beyond the subscription endpoints needed
  for read-only analysis.
