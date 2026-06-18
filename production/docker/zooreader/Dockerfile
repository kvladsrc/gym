FROM docker.io/nginxinc/nginx-unprivileged:alpine

LABEL org.opencontainers.image.title="Zooreader"

# Remove default nginx config
RUN rm /etc/nginx/conf.d/default.conf

# Copy custom nginx config
COPY nginx.conf /etc/nginx/conf.d/

# Copy static game files
COPY index.html style.css main.js player.png grass.png desert.png ice.png lava.png /usr/share/nginx/html/
COPY src/ /usr/share/nginx/html/src/

# Add healthcheck
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/ || exit 1

EXPOSE 8080

USER 101

CMD ["nginx", "-g", "daemon off;"]
