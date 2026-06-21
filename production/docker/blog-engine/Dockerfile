FROM docker.io/golang:1.25-alpine AS build

WORKDIR /src

RUN apk add --no-cache ca-certificates

COPY go.mod go.sum ./
COPY production/docker/blog-engine/ production/docker/blog-engine/

RUN CGO_ENABLED=0 GOOS=linux go build \
    -trimpath \
    -ldflags="-s -w" \
    -o /out/blog-engine \
    ./production/docker/blog-engine/cmd/blog-engine

FROM scratch

LABEL org.opencontainers.image.title="Blog Engine"
# checkov:skip=CKV_DOCKER_2: scratch runtime has no shell or curl; Kubernetes probes cover health checks

COPY --from=build /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/
COPY --from=build /out/blog-engine /blog-engine

USER 65532:65532

EXPOSE 8080 8081

ENTRYPOINT ["/blog-engine"]
