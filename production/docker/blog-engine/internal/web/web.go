package web

import (
	"context"
	"embed"
	"html/template"
	"io"
	"io/fs"
	"log/slog"
	"net/http"

	"src/production/docker/blog-engine/internal/config"
	"src/production/docker/blog-engine/internal/httpx"
)

//go:embed templates/*.html static/*.css
var content embed.FS

// Run starts the public blog component.
func Run(ctx context.Context, cfg config.Config) error {
	mux := http.NewServeMux()
	mux.HandleFunc("GET /healthz", func(w http.ResponseWriter, _ *http.Request) {
		writeResponse(w, "ok\n")
	})
	mux.HandleFunc("GET /", home)
	mux.HandleFunc("GET /search", search)
	mux.Handle("GET /static/", http.StripPrefix("/static/", http.FileServer(http.FS(mustSub(content, "static")))))

	slog.Info("starting public web component", "addr", cfg.HTTP.WebAddr)
	return httpx.ListenAndServe(ctx, cfg.HTTP.WebAddr, mux)
}

func home(w http.ResponseWriter, _ *http.Request) {
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	if err := homeTemplate.Execute(w, nil); err != nil {
		slog.Error("failed to render home page", "error", err)
	}
}

func search(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	if err := searchTemplate.Execute(w, searchPageData{Query: r.URL.Query().Get("q")}); err != nil {
		slog.Error("failed to render search page", "error", err)
	}
}

type searchPageData struct {
	Query string
}

var homeTemplate = template.Must(template.ParseFS(content, "templates/home.html"))
var searchTemplate = template.Must(template.ParseFS(content, "templates/search.html"))

func writeResponse(w http.ResponseWriter, body string) {
	if _, err := io.WriteString(w, body); err != nil {
		slog.Debug("failed to write response", "error", err)
	}
}

func mustSub(files fs.FS, dir string) fs.FS {
	sub, err := fs.Sub(files, dir)
	if err != nil {
		panic(err)
	}
	return sub
}
