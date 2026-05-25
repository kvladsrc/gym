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

## Operations

- [Lightweight notifications](notifications.md) define the low-RAM `ntfy`
  operating model, topic naming, severity conventions, and task workflow.

## Resource Tuning

The cluster is sized for hobby use and usually runs on a single node with
limited RAM. Production memory changes should keep enough headroom for CI
bursts and node-level services while avoiding excessive Kubernetes limit
overcommit.

Flux controllers are Go processes with `GOMEMLIMIT` wired to their Kubernetes
memory limit. On 2026-05-21 their observed steady-state RSS was much lower than
the historical 1-2 GiB limits: source-controller ~29 MiB,
kustomize-controller ~27 MiB, helm-controller ~78 MiB,
notification-controller ~21 MiB, and tofu-controller ~25 MiB. Their limits are
therefore intentionally conservative rather than minimal: 512 MiB for source,
kustomize, helm, and tofu-controller, and 256 MiB for notification-controller.

Gerrit is a JVM service and should keep heap smaller than the Kubernetes memory
limit to leave room for native memory, plugins, Lucene, and process overhead. On
2026-05-21 the single-user Gerrit pod used about 1.15 GiB RSS with `-Xmx2g`,
request 2 GiB, and limit 3 GiB. For the hobby workload, Gerrit uses
`-Xmx1536m`, request 1280 MiB, and limit 2 GiB; this keeps rollback simple while
reducing reserved memory.

OpenGrok is useful but optional and can otherwise become the largest resident
memory consumer. On 2026-05-22 the pod used about 1.35 GiB RSS while running
BestEffort with the default Tomcat JVM sizing. The wrapper caps the web JVM at
`-Xmx512m`, caps the indexer at `-Xmx768m`, runs one sync worker, and syncs every
240 minutes. The pod requests 512 MiB and is limited to 1 GiB; if this proves too
tight during indexing, prefer making OpenGrok on-demand over raising the steady
memory footprint again.

Zuul control-plane pods should not run BestEffort. On 2026-05-22, after check
and gate activity, scheduler, web, fingergw, and nodepool-launcher were each
around 157-186 MiB RSS, while executor was around 703 MiB RSS. Scheduler, web,
fingergw, and nodepool-launcher therefore request 192 MiB and are limited to
512 MiB. Executor requests 512 MiB and is limited to 2 GiB to preserve headroom
for job setup, Ansible, and log serving. Transient Nodepool build-node pods stay
BestEffort until the Nodepool-to-Zuul-launcher migration decides their resource
model.

Kyverno resource policy starts in audit mode. `require-container-resources`
reports pods and pod controllers without CPU and memory requests and limits, but
does not block Flux reconciliation. Move it to enforcement only after policy
reports are reviewed, long-running production workloads are compliant, and
documented exceptions exist for transient or system workloads such as Zuul
build-node pods.
