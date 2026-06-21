FROM docker.io/debian:bookworm-slim@sha256:96e378d7e6531ac9a15ad505478fcc2e69f371b10f5cdf87857c4b8188404716
ENV DEBIAN_FRONTEND=noninteractive

# Base system packages.
# checkov:skip=CKV2_DOCKER_1: sudo is required for build functionality
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates git openssh-client python3 sudo \
    && rm -rf /var/lib/apt/lists/*

# Nix users. Users created by nix install, but we run install during
# init phase.
RUN groupadd -g 30000 nixbld && \
    for i in $(seq 1 32); do useradd -l -u "$((30000 + i))" -g nixbld -G nixbld -d /var/empty -s "$(which nologin)" "nixbld$i"; done

# Zuul and base setup.
RUN groupadd --gid 9999 zuul \
    && useradd -l --uid 9999 --gid zuul --create-home --shell /bin/bash zuul \
    && echo "zuul ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/zuul \
    && printf "export PATH=\$PATH:/nix/var/nix/profiles/default/bin:/root/.nix-profile/bin\n" >> /etc/profile.d/zuul_env.sh

# The mounted Nix store provides the bootstrap Nix binary. The build-node
# image build creates /opt/build-node-tools once, so individual Zuul jobs can
# start without rebuilding the toolchain from the patchset flake.
ENV PATH="/opt/build-node-tools/bin:/nix/var/nix/profiles/default/bin:${PATH}"

COPY nix.conf /etc/nix/nix.conf
COPY policy.json /etc/containers/policy.json
COPY flake.nix flake.lock /tmp/build-node-tools/

RUN /nix/var/nix/profiles/default/bin/nix build \
    /tmp/build-node-tools#buildNodeTools \
    --out-link /opt/build-node-tools \
    --max-jobs auto \
    --print-build-logs

# checkov:skip=CKV_DOCKER_8: Zuul kubectl-exec runs jobs as the image user and NFS caches are mounted for root
# checkov:skip=CKV_DOCKER_3: jobs must run as root to reach the root-mounted NFS caches
# The container stays root on purpose: Zuul kubectl-exec runs job commands
# as the image user, and the NFS caches (pre-commit, go) are mounted for
# root. Image 1.1 switched to USER zuul and every CI job lost its caches.

# Zuul overrides the pod command with its own loop; this default matches
# it so the image also idles harmlessly when run standalone.
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD /nix/var/nix/profiles/default/bin/nix --version > /dev/null 2>&1 || exit 1

CMD ["/bin/sh", "-c", "while true; do sleep 30; done;"]
