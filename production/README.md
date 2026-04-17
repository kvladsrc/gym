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

The core philosophy is **total independence**. I find it more
comfortable to work without dependencies on cloud providers,
proprietary SaaS, or external CI runners like GitHub Actions.

## Tech Stack & Components

Despite its small footprint, the project integrates a full suite of
professional tools:

- **Infrastructure:** Kubernetes on Talos Linux (via VirtualBox) — a
  secure, immutable, and maintenance-free foundation.
- **Code Review:** Gerrit — providing a robust, patch-based review
  workflow.
- **CI/CD Pipelines:** Zuul — implementing "Check" and "Gate"
  pipelines to ensure speculative execution and code quality before
  merging.
- **GitOps:** FluxCD — keeping the cluster state synchronized with the
  repository.
- **Identity Management:** Authentik — a unified SSO for secure access
  to all internal services.

## Core Philosophy

- **Developer Experience (DX) First:** Mimic the "corporate" feel
  where testing and deployment are automated.
- **Resource Efficiency:** Every component is tuned to stay within the
  16 GiB RAM limit without sacrificing functionality.
- **Reproducibility:** The entire lab is defined as code, allowing for
  quick teardown and reconstruction.
