# Vendored dashboards

Do not edit these JSON files. Update them from their pinned upstream source.
Helm binds the VictoriaLogs import placeholder to the `victoriametrics`
datasource without modifying the vendored file.

| File | Upstream pin |
| --- | --- |
| `victoriametrics-single-node.json` | [VictoriaMetrics](https://github.com/VictoriaMetrics/VictoriaMetrics/blob/v1.149.0/dashboards/victoriametrics.json) `v1.149.0` |
| `victorialogs-single-node.json` | [VictoriaLogs](https://github.com/VictoriaMetrics/VictoriaLogs/blob/v1.52.0/dashboards/victorialogs.json) `v1.52.0` |
| `opentelemetry-collector.json` | [Grafana dashboard 15983](https://grafana.com/grafana/dashboards/15983-opentelemetry-collector/), revision 30 |
| `grafana-internal.json` | [Grafana dashboard 24838](https://grafana.com/grafana/dashboards/24838-grafana-internal-metrics/), revision 1 |
| `coredns.json` | [Grafana dashboard 15762](https://grafana.com/grafana/dashboards/15762-kubernetes-system-coredns/), revision 22 |
| `kyverno.json` | Kyverno chart `3.8.2` (`v1.18.2`) |
| `flux-control-plane.json` | [flux2-monitoring-example](https://github.com/fluxcd/flux2-monitoring-example/tree/7ab65dc8b90f7a6751d88f18bbb4e1bee33bf334) `7ab65dc8` |
| `authentik.json` | [authentik monitoring documentation](https://docs.goauthentik.io/sys-mgmt/ops/monitoring), dashboard snapshot `2026-08-21` / Grafana dashboard `23043` |
| `kubernetes-api-server.json` | [grafana-dashboards-kubernetes](https://github.com/dotdc/grafana-dashboards-kubernetes/tree/7fc1dc8ccd23eeb8c58ddd8be2a122922d424937) `7fc1dc8c` / Grafana dashboard `15761` |
| `envoy-overview.json` | [Grafana dashboard 24459](https://grafana.com/grafana/dashboards/24459-envoy-overview/), revision 1 |
| `minio.json` | [MinIO official dashboard](https://github.com/minio/minio/blob/7aac2a2c5b7c882e68c1ce017d8256be2feea27f/docs/metrics/prometheus/grafana/minio-dashboard.json) `7aac2a2c` |
