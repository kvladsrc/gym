# Observability

The chart deploys a metrics and logs pipeline without tracing:

```text
OTel Agent (DaemonSet) -> OTel Collector (Deployment) -> VictoriaMetrics
                                                   `-> VictoriaLogs
```

The agent reads node, kubelet, and pod log data. The central collector also
watches Kubernetes Events and scrapes `kube-state-metrics` for API object state,
including workload availability and container termination reasons. Applications
send OTLP metrics and logs to `otel-collector.observability.svc.cluster.local`
on port `4317` (gRPC) or `4318` (HTTP). The collector is the only component that
writes to the Victoria backends.

The collector also scrapes existing Prometheus endpoints for the OpenTelemetry
collectors, VictoriaMetrics, VictoriaLogs, Grafana, CoreDNS, Flux, Envoy,
cert-manager, Authentik, MinIO, the Kubernetes API server, Kyverno, Kubernetes
Event Exporter, and `kube-state-metrics`.
Scraped metrics receive Prometheus-compatible `job`, `instance`, `namespace`,
`pod`, `container`, and `node` labels plus the cluster labels used by
provisioned Grafana dashboards.

The 2026-08-21 coverage and incident review is recorded in the repository
[AUDIT.md](../../../../AUDIT.md).

VictoriaMetrics retains 14 days in a 10 GiB local volume. VictoriaLogs retains
7 days in a 15 GiB local volume. Both use the non-backed-up
`local-rebuildable` storage class.

Tracing receivers, pipelines, storage, and Jaeger-compatible ports are not
enabled.
