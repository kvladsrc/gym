# ------------------------------------------------------------------
#  Базовый образ LinuxServer.io (Alpine 3.21, s6-overlay, sshd)
# ------------------------------------------------------------------
FROM lscr.io/linuxserver/openssh-server:9.9_p2-r0-ls197

LABEL org.opencontainers.image.title="zuul-logs-sshd" \
      org.opencontainers.image.description="openssh-server + python 3.12 for Zuul logs pod" \
      org.opencontainers.image.source="https://github.com/your/repo"

RUN apk add --no-cache python3 py3-pip rsync \
    && ln -sf /usr/bin/python3 /usr/local/bin/python

EXPOSE 2222
