# Grafana wrapper

Grafana UI for VictoriaMetrics and VictoriaLogs. The chart provisions Auth
Proxy, datasources, dashboards, and alerting resources from Git; operational
configuration is not managed through the Grafana UI.

The provisioned `KubernetesContainerOOMKilled` rule correlates an OOM
termination marker with a restart-counter increase during the previous five
minutes. Each affected namespace, pod, and container is notified separately in
the `prod-k8s-warnings` ntfy topic, including a resolved notification after the
event window closes. The restart correlation prevents historical termination
markers from keeping the alert active indefinitely.

Authentik's proxy outpost is the only public route to Grafana and supplies the
trusted identity headers. Grafana users receive the Viewer role with temporary
Explore access, but cannot persist dashboard changes made in the UI.

`cluster-overview.json` is maintained in this repository rather than imported
from the Grafana catalog. The default `Cluster Health & Capacity` dashboard
combines failed scrape targets, OOM and workload state, Kubernetes warning
events, log volume by OpenTelemetry severity, problem logs, and actual
CPU/memory use against requests, limits, and allocatable capacity. It targets
the current OpenTelemetry and
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
