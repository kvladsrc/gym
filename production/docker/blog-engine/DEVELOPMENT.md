# Blog Engine Development

## Local Checks

Run unit tests from the repository root:

```sh
/nix/var/nix/profiles/default/bin/nix develop -c go test ./production/docker/blog-engine/...
/nix/var/nix/profiles/default/bin/nix develop -c bazelisk test //production/docker/blog-engine/...
```

## Runtime Shape

The blog engine is one image and one binary. Runtime behavior is selected by
the command passed to the container:

```text
blog-engine author --config=/etc/blog-engine/config.yaml
blog-engine worker --config=/etc/blog-engine/config.yaml
blog-engine web --config=/etc/blog-engine/config.yaml
```

## Install Into A Dev Namespace

The Helm chart mounts one Secret-backed config file into every component:

```text
/etc/blog-engine/config.yaml
```

The chart follows the repository's regular non-wrapper service style: it uses
`nxs-universal-chart` for Deployments, Services, and Secrets, with
component-specific settings in `values.yaml`.

The dev values contain placeholder credentials. For real local cluster testing,
provide an override values file:

```sh
helm upgrade --install blog-engine-dev \
  production/kubernetes/helm-charts/blog-engine \
  --namespace blog-engine-dev \
  --create-namespace \
  -f /path/to/local-secret-values.yaml
```

Terraform creates the lab infrastructure used by the default config shape:

- PostgreSQL role/database: `blog_engine`
- MinIO bucket and service account: `blog-engine`

## Local Access

Author UI:

```sh
kubectl -n blog-engine-dev port-forward svc/author 8081:80
```

Public web:

```sh
kubectl -n blog-engine-dev port-forward svc/web 8080:80
```

Then open:

```text
http://127.0.0.1:8081/posts
http://127.0.0.1:8080/
```

## GitOps

The production installation is wired through Flux:

```text
production/kubernetes/flux/apps/blog-engine/
```

Terraform outputs are written to the `production-output` Secret by the
Terraform Controller. The blog-engine HelmRelease reads the PostgreSQL password
and MinIO credentials from that Secret with `valuesFrom`.

Image deployment follows the existing promoted-tag flow:

```text
rc-<commit>             built by Zuul gate
prod-<timestamp>-<sha>  promoted by Zuul promote
```

Flux image automation watches `registry.your.domain/blog-engine` for
`prod-*` tags and updates the HelmRelease image tag setters.
