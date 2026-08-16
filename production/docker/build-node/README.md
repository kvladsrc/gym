# Build node

The image uses a persistent `/nix` store. Normally Zuul updates that store
while building `Dockerfile` with `production/playbooks/build-node-image.yaml`.

## Initialize an empty store

This is a rare, manual recovery or migration operation. The one-shot Job runs
Nix from its container image and builds `buildNodeStore` from this directory's
`flake.nix` into a new local store. It neither mounts nor copies the old store.

The target is `/var/mnt/local-path-provisioner/zuul/nix` on the Talos node.
Run this only when that store is empty or a previous initialization attempt is
incomplete:

```console
kubectl apply -k production/docker/build-node
kubectl wait --namespace zuul \
  --for=condition=complete job/build-node-store-init \
  --timeout=2h
kubectl logs --namespace zuul job/build-node-store-init
```

If the Job fails, inspect its logs, delete it with the cleanup command below,
and apply it again. Nix safely resumes an incomplete target store.

After a successful run, remove only the temporary Kubernetes objects. The
hostPath data is not deleted:

```console
kubectl delete -k production/docker/build-node
```

The regular `build-node-image` Zuul job can then use the initialized local
store without a special bootstrap path.
