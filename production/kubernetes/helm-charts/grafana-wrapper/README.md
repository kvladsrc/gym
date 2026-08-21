# Grafana wrapper

Grafana UI for VictoriaMetrics and VictoriaLogs. The chart provisions Auth
Proxy, datasources, and dashboards from Git; operational configuration is not
managed through the Grafana UI.

Authentik's proxy outpost is the only public route to Grafana and supplies the
trusted identity headers. Grafana users receive the Viewer role with temporary
Explore access, but cannot persist dashboard changes made in the UI.

`cluster-overview.json` is maintained in this repository rather than imported
from the Grafana catalog. The default `Cluster Health & Capacity` dashboard
combines failed scrape targets, OOM and workload state, Kubernetes warning
events, problem logs, and actual CPU/memory use against requests, limits, and
allocatable capacity. It targets the current OpenTelemetry and
`kube-state-metrics` pipeline. Metric definitions come from the official
OpenTelemetry Collector receivers:

- [Kubernetes Cluster Receiver](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/receiver/k8sclusterreceiver)
- [Kubelet Stats Receiver](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/receiver/kubeletstatsreceiver)
- [Host Metrics Receiver](https://github.com/open-telemetry/opentelemetry-collector-contrib/tree/main/receiver/hostmetricsreceiver)

Version-pinned upstream dashboards live unchanged under `dashboards/vendor`.
Helm renders each one into a separate ConfigMap and resolves known import-time
Prometheus datasource placeholders. At render time, the Envoy dashboard's
counter names are adapted to the `_total` suffix emitted by the OpenTelemetry
Prometheus receiver and remote-write pipeline. The dashboards are grouped into
`Observability Stack` and `Platform` folders.
