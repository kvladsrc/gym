# Lab server GitOps

The `lab.your.domain` Debian host reconciles its configuration from the
merged `master` branch with `ansible-pull`. A systemd timer runs the playbook
every five minutes, so both new commits and manual configuration drift are
applied.

## Managed state

- base administration packages;
- Docker Engine and membership of the `docker` group;
- Cockpit with a dedicated, automatically renewed TLS certificate;
- the root-owned Gerrit deploy key location and known host;
- the `ansible-pull` wrapper, service, and timer.

Package changes belong in `vars/main.yaml`. The host installs packages with
Debian APT during its next reconciliation.

Cockpit is available directly at `https://lab.your.domain`, without an
Authentik proxy or Kubernetes ingress. Certbot obtains and renews a dedicated
Let's Encrypt certificate for the existing lab hostname through DNS-01.

## Bootstrap

Bootstrap is the only push operation. Before running it, create a read-only
Gerrit account and place its private key at
`/etc/ansible-pull/id_ed25519` on the host.

Also place a Cloudflare API token with `Zone:DNS:Edit` permission for
`your.domain` in `/etc/ansible-pull/cloudflare.ini`:

```ini
dns_cloudflare_api_token = token-value
```

The file must be owned by root with mode `0600`. It is the only Cockpit secret
bootstrapped outside Git; Certbot uses it for DNS-01 issuance and renewal.

```console
ansible-playbook \
  --inventory production/playbooks/lab/inventory.yaml \
  production/playbooks/lab/bootstrap.yaml
```

The bootstrap playbook verifies that the key exists, installs Ansible and Git,
then enables `ansible-pull.timer`. It does not install workload packages such
as Docker; those are installed by the first pull from merged `master`.

## Operations

Inspect reconciliation state:

```console
ssh lab.your.domain sudo systemctl status ansible-pull.timer
ssh lab.your.domain sudo journalctl -u ansible-pull.service
```

Trigger an immediate reconciliation:

```console
ssh lab.your.domain sudo systemctl start ansible-pull.service
```

If a merged change is faulty, revert it in Gerrit. The next successful pull
will converge the host to the reverted configuration. Ansible changes are not
atomic, so SSH access and the VirtualBox console remain the recovery paths.
