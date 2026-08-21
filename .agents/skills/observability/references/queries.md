# Investigation queries

Start with service health and coverage, then narrow the time and label set.
All examples are read-only and are intended for `observability-api`.

## Metrics

Current failed scrape targets:

```promql
up == 0
```

Container restarts during the retained window:

```promql
sum by (namespace, pod, container) (
  increase(kube_pod_container_status_restarts_total[7d])
) > 0
```

Latest OOM termination markers:

```promql
max by (namespace, pod, container) (
  max_over_time(
    kube_pod_container_status_last_terminated_reason{reason="OOMKilled"}[7d]
  )
) > 0
```

Unavailable deployment replicas:

```promql
kube_deployment_status_replicas_unavailable > 0
```

OTel refused or failed telemetry:

```promql
sum by (pod) (rate(otelcol_receiver_refused_log_records_total[5m]))
sum by (pod) (rate(otelcol_exporter_send_failed_log_records_total[5m]))
sum by (pod) (rate(otelcol_exporter_send_failed_metric_points_total[5m]))
```

Discover exact metric names before adapting a dashboard:

```promql
count by (__name__) ({__name__=~".*(cpu|memory|filesystem|disk).*"})
```

## Logs

Count log volume by namespace:

```logsql
* | stats by (k8s.namespace.name) count()
```

Group Kubernetes warning events:

```logsql
object.type:Warning | stats by (object.reason) count()
```

Group likely errors by workload:

```logsql
_msg:error | stats by (k8s.namespace.name, k8s.pod.name) count()
```

Inspect one namespace without relying on pod suffixes:

```logsql
k8s.namespace.name:gerrit-cluster _msg:error
```

Use `logs fields` and `logs values` when a service emits structured JSON. A
plain text search for `error` is only a lead: health checks, stack traces, and
user data can contain that word without representing a current failure.

## Interpretation

- Correlate a metric transition with logs and Kubernetes events from the same
  period before assigning a cause.
- A series can disappear when a pod is replaced; absence is not proof of zero.
- Counter totals need `rate()` or `increase()` for a bounded interval.
- Separate current faults from historical faults that have already recovered.
- Report the observed interval and retention boundary with every historical
  conclusion.
