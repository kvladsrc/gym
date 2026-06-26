package web

import (
	"context"
	"errors"
	"io"
	"log/slog"
	"mime"
	"net/http"
	"path"
	"strings"

	"src/production/docker/blog-engine/internal/config"
	"src/production/docker/blog-engine/internal/httpx"
	"src/production/docker/blog-engine/internal/objectstore"
)

type store interface {
	Get(context.Context, string, string) (objectstore.Object, error)
}

// Run starts the public blog component.
func Run(ctx context.Context, cfg config.Config) error {
	app := app{
		store:  objectstore.New(cfg),
		bucket: cfg.S3.PublicBucket,
	}

	mux := http.NewServeMux()
	mux.HandleFunc("GET /healthz", func(w http.ResponseWriter, _ *http.Request) {
		writeResponse(w, "ok\n")
	})
	mux.HandleFunc("GET /", app.proxy)

	slog.Info("starting public web component", "addr", cfg.HTTP.WebAddr)
	return httpx.ListenAndServe(ctx, cfg.HTTP.WebAddr, mux)
}

type app struct {
	store  store
	bucket string
}

func (app app) proxy(w http.ResponseWriter, req *http.Request) {
	objectKey, err := requestObjectKey(req.URL.Path)
	if err != nil {
		http.NotFound(w, req)
		return
	}

	object, err := app.store.Get(req.Context(), app.bucket, objectKey)
	if errors.Is(err, objectstore.ErrNotFound) {
		http.NotFound(w, req)
		return
	}
	if err != nil {
		slog.Error("failed to load public object", "key", objectKey, "error", err)
		http.Error(w, "failed to load public object\n", http.StatusBadGateway)
		return
	}

	contentType := object.ContentType
	if contentType == "" {
		contentType = mime.TypeByExtension(path.Ext(objectKey))
	}
	if contentType != "" {
		w.Header().Set("Content-Type", contentType)
	}
	if _, err := w.Write(object.Body); err != nil {
		slog.Debug("failed to write public object", "error", err)
	}
}

func requestObjectKey(requestPath string) (string, error) {
	if hasDotDotSegment(requestPath) {
		return "", errors.New("invalid request path")
	}
	cleanPath := path.Clean("/" + requestPath)
	if cleanPath == "/" {
		return "index.html", nil
	}
	if strings.HasSuffix(requestPath, "/") {
		cleanPath += "/index.html"
	}
	return strings.TrimPrefix(cleanPath, "/"), nil
}

func hasDotDotSegment(value string) bool {
	for _, segment := range strings.Split(value, "/") {
		if segment == ".." {
			return true
		}
	}
	return false
}

func writeResponse(w http.ResponseWriter, body string) {
	if _, err := io.WriteString(w, body); err != nil {
		slog.Debug("failed to write response", "error", err)
	}
}
