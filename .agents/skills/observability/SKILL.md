---
name: observability
description: "Query and analyze production VictoriaMetrics and VictoriaLogs data, inspect available series and log fields, investigate Kubernetes incidents, and validate the observability pipeline."
---

# Production observability

Use the bundled read-only helper for production metrics and logs. It runs a
fixed `curl` command inside the Grafana pod against the internal VictoriaMetrics
and VictoriaLogs services; it does not expose either database publicly or try
to automate an Authentik browser session.

Run it from the repository Nix environment:

```sh
/nix/var/nix/profiles/default/bin/nix develop -c \
  .agents/skills/observability/scripts/observability-api health
```

## Common reads

```sh
# Instant MetricsQL/PromQL query.
.agents/skills/observability/scripts/observability-api \
  metrics query 'up == 0'

# Range query: QUERY START END STEP.
.agents/skills/observability/scripts/observability-api \
  metrics range 'sum(rate(otelcol_exporter_sent_metric_points_total[5m]))' \
  6h now 5m

# List labels or values for one label.
.agents/skills/observability/scripts/observability-api metrics labels
.agents/skills/observability/scripts/observability-api metrics labels job

# Raw log records: QUERY START END LIMIT.
.agents/skills/observability/scripts/observability-api \
  logs query 'k8s.namespace.name:gerrit-cluster _msg:error' 1h now 50

# Counts over time: QUERY START END STEP.
.agents/skills/observability/scripts/observability-api \
  logs hits '_msg:error' 6h now 15m

# Discover log fields and their values before guessing field names.
.agents/skills/observability/scripts/observability-api \
  logs fields '*' 1h
.agents/skills/observability/scripts/observability-api \
  logs values '*' k8s.namespace.name 1h 100
```

Use short time windows first and aggregate before requesting raw records. The
current retention is 14 days for metrics and 7 days for logs. Treat missing
data before the first stored sample as unknown, not healthy.

For incident-analysis query patterns, read
[references/queries.md](references/queries.md). For chart or pipeline changes,
work through the GitOps sources under
`production/kubernetes/helm-charts/observability-wrapper` and
`production/kubernetes/helm-charts/grafana-wrapper`; do not patch live
production resources for routine changes.

## Authentication boundary

Grafana's public UI remains behind Authentik. The helper deliberately uses
Kubernetes access to internal ClusterIP services, so it needs no Authentik
cookie and introduces no public unauthenticated query API. Do not add an
Authentik skip-path for Victoria query endpoints. If a remote MCP endpoint is
introduced later, require Authentik machine-to-machine authentication and keep
the Victoria backends private.
