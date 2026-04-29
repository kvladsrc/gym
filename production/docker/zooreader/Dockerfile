FROM docker.io/nginx:alpine

# Remove default nginx config
RUN rm /etc/nginx/conf.d/default.conf

# Copy custom nginx config
COPY nginx.conf /etc/nginx/conf.d/

# Copy static game files
COPY index.html style.css script.js player.png barrier.png grass.png desert.png ice.png lava.png /usr/share/nginx/html/

# Add healthcheck
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost/ || exit 1

# Drop to non-root user
RUN addgroup -g 101 -S nginx && \
    adduser -u 101 -S -D -G nginx nginx

USER nginx

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
