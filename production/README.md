# A Professional-Grade Development Environment in a 16GB Box

This project is a personal CI/CD infrastructure designed to support
hobby programming, competitive coding, and textbook exercises. It was
born out of a simple realization: it is much more efficient to solve
problems in a familiar, production-like environment than to struggle
with fragmented local setups.

The goal of this project is to bridge the gap between enterprise-level
workflows (Code Review, Gating, GitOps) and strict hardware
constraints (running entirely on 16 GiB of RAM within VirtualBox).

## Why this project?

Instead of reinventing the wheel for every small task, I've built a
"mini-production" that mimics the developer experience of a
large-scale commercial infrastructure.

The core services are self-hosted. External dependencies are limited
to DNS, certificate issuance, and optional APIs, while source control,
CI runners, identity, storage, and application hosting stay local.

## Tech Stack & Components

Despite its small footprint, the project integrates a full suite of
professional tools:

- **Infrastructure:** Kubernetes on Talos Linux (via VirtualBox) — a
  secure and immutable foundation.
- **Edge:** Envoy Gateway and cert-manager — public routing, TLS, and
  direct or Authentik-protected service access.
- **Code Review:** Gerrit — providing a robust, patch-based review
  workflow.
- **CI/CD Pipelines:** Zuul — implementing "Check", "Gate", and
  post-submit pipelines.
- **GitOps:** FluxCD — keeping the cluster state synchronized with the
  repository.
- **Infrastructure as Code:** OpenTofu Controller — managing DNS,
  Authentik objects, PostgreSQL resources, and MinIO resources.
- **Identity Management:** Authentik — a unified SSO for internal
  services.
- **Data Services:** PostgreSQL and MinIO — shared database and
  S3-compatible storage with per-service credentials.
- **Observability:** OpenTelemetry Collector, VictoriaMetrics,
  VictoriaLogs, and Grafana — node, Kubernetes, application metrics,
  pod logs, and Git-provisioned dashboards.

## Core Philosophy

- **Developer Experience (DX) First:** Mimic the "corporate" feel
  where testing and deployment are automated.
- **Resource Efficiency:** Every component is tuned to stay within the
  16 GiB RAM limit without sacrificing functionality.
- **Reproducibility:** The cluster and lab configuration are defined as
  code, allowing for quick teardown and reconstruction.

## Documentation

- [Systems overview](../systems_overview.md) lists user-facing services.
- [Production operations](OPERATIONS.md) covers common GitOps changes.
