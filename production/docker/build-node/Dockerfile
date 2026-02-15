FROM docker.io/debian:bookworm-slim

ENV DEBIAN_FRONTEND=noninteractive

# Install dependencies for Nix installation and basic tools
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    git \
    gnupg \
    openssh-server \
    sudo \
    xz-utils \
    libatomic1 \
    && rm -rf /var/lib/apt/lists/*

# Install Nix
SHELL ["/bin/bash", "-o", "pipefail", "-c"]
RUN curl --proto '=https' --tlsv1.2 -sSf -L https://install.determinate.systems/nix | sh -s -- install linux \
    --no-confirm \
    --init none \
    --extra-conf "filter-syscalls = false"

ENV PATH="${PATH}:/nix/var/nix/profiles/default/bin"

# Enable flakes
RUN echo "experimental-features = nix-command flakes" >> /etc/nix/nix.conf

# Copy flake files
WORKDIR /tmp/flake
COPY flake.nix ./

# Install tools from flake
RUN nix profile install .#buildNodeTools

# Install Helm plugins
RUN ln -s "$(nix build .#helmPlugins --print-out-paths --no-link)" /usr/local/share/helm-plugins
ENV HELM_PLUGINS=/usr/local/share/helm-plugins

RUN nix-collect-garbage -d

# Setup Zuul user
RUN groupadd --gid 9999 zuul \
    && useradd --uid 9999 --gid zuul --create-home --shell /bin/bash zuul \
    && echo "zuul ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/zuul

# Setup SSHD
RUN mkdir -p /var/run/sshd
RUN echo "Port 22" >> /etc/ssh/sshd_config \
    && echo "PermitRootLogin yes" >> /etc/ssh/sshd_config \
    && echo "PasswordAuthentication yes" >> /etc/ssh/sshd_config \
    && echo "PubkeyAuthentication yes" >> /etc/ssh/sshd_config \
    && echo "AuthorizedKeysFile .ssh/authorized_keys" >> /etc/ssh/sshd_config

# Setup environment for SSH sessions to include Nix profile
RUN echo "export PATH=\$PATH:/nix/var/nix/profiles/default/bin:/root/.nix-profile/bin" >> /etc/profile.d/zuul_env.sh \
    && echo "export HELM_PLUGINS=/usr/local/share/helm-plugins" >> /etc/profile.d/zuul_env.sh

EXPOSE 22

CMD ["/usr/sbin/sshd", "-D"]
