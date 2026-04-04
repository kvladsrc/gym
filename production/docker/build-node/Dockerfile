FROM docker.io/debian:bookworm-slim
ENV DEBIAN_FRONTEND=noninteractive

# Base system packages.
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates curl git gnupg openssh-server sudo xz-utils libatomic1 \
    && rm -rf /var/lib/apt/lists/*

# Nix users. Users created by nix install, but we run install during
# init phase.
RUN groupadd -g 30000 nixbld && \
    for i in $(seq 1 32); do useradd -l -u "$((30000 + i))" -g nixbld -G nixbld -d /var/empty -s "$(which nologin)" "nixbld$i"; done

# Zuul and base setup.
RUN groupadd --gid 9999 zuul \
    && useradd -l --uid 9999 --gid zuul --create-home --shell /bin/bash zuul \
    && echo "zuul ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/zuul \
    && mkdir -p /var/run/sshd \
    && printf "Port 22\nPermitRootLogin yes\nPasswordAuthentication yes\nPubkeyAuthentication yes\n" >> /etc/ssh/sshd_config \
    && printf "export PATH=\$PATH:/nix/var/nix/profiles/default/bin:/root/.nix-profile/bin\n" >> /etc/profile.d/zuul_env.sh

# Nix store update.
ENV PATH="/nix/var/nix/profiles/default/bin:${PATH}"

WORKDIR /tmp/flake
COPY flake.nix ./
RUN nix profile install .#buildNodeTools \
    && nix-collect-garbage -d

EXPOSE 22

CMD ["/usr/sbin/sshd", "-D"]
