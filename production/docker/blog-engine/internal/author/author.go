package author

import (
	"context"
	"embed"
	"html/template"
	"io"
	"io/fs"
	"log/slog"
	"net/http"
	"net/url"
	"strconv"
	"strings"

	"src/production/docker/blog-engine/internal/config"
	"src/production/docker/blog-engine/internal/httpx"
	"src/production/docker/blog-engine/internal/posts"
)

type store interface {
	Create(context.Context, posts.CreateInput) (posts.Post, error)
	Get(context.Context, int64) (posts.Post, error)
	ListLatest(context.Context) ([]posts.Post, error)
	Publish(context.Context, int64) (posts.Post, error)
	Recheck(context.Context, int64) (posts.Post, error)
	Update(context.Context, int64, posts.UpdateInput) (posts.Post, error)
}

type app struct {
	store      store
	publicBlog string
}

//go:embed templates/*.html static/*.css
var content embed.FS

// Run starts the author UI component.
func Run(ctx context.Context, cfg config.Config) error {
	repo, err := posts.Open(cfg.Database.DSN)
	if err != nil {
		return err
	}
	defer closeRepository(repo)

	app := app{store: repo, publicBlog: cfg.Publishing.BaseURL}
	mux := http.NewServeMux()
	mux.HandleFunc("GET /healthz", func(w http.ResponseWriter, _ *http.Request) {
		writeResponse(w, "ok\n")
	})
	mux.HandleFunc("GET /", home)
	mux.HandleFunc("GET /posts", app.posts)
	mux.HandleFunc("POST /posts", app.createPost)
	mux.HandleFunc("GET /posts/{id}", app.postDetail)
	mux.HandleFunc("GET /posts/{id}/edit", app.edit)
	mux.HandleFunc("POST /posts/{id}/publish", app.publishPost)
	mux.HandleFunc("POST /posts/{id}/recheck", app.recheckPost)
	mux.HandleFunc("POST /posts/{id}/revisions", app.updatePost)
	mux.HandleFunc("GET /create", app.create)
	mux.HandleFunc("GET /help", app.help)
	mux.Handle("GET /static/", http.StripPrefix("/static/", http.FileServer(http.FS(mustSub(content, "static")))))

	slog.Info("starting author component", "addr", cfg.HTTP.AuthorAddr)
	return httpx.ListenAndServe(ctx, cfg.HTTP.AuthorAddr, mux)
}

func home(w http.ResponseWriter, r *http.Request) {
	http.Redirect(w, r, "/posts", http.StatusSeeOther)
}

func (app app) help(w http.ResponseWriter, _ *http.Request) {
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	if err := helpTemplate.Execute(w, pageData{PublicBlog: app.publicBlog}); err != nil {
		slog.Error("failed to render help page", "error", err)
	}
}

func (app app) posts(w http.ResponseWriter, r *http.Request) {
	list, err := app.store.ListLatest(r.Context())
	if err != nil {
		http.Error(w, "failed to load posts", http.StatusInternalServerError)
		slog.Error("failed to list posts", "error", err)
		return
	}

	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	if err := postsTemplate.Execute(w, postsPageData{
		PublicBlog: app.publicBlog,
		Posts:      list,
	}); err != nil {
		slog.Error("failed to render posts page", "error", err)
	}
}

func (app app) create(w http.ResponseWriter, _ *http.Request) {
	app.renderEdit(w, editPageData{
		Heading: "Create post",
		Action:  "/posts",
	})
}

func (app app) createPost(w http.ResponseWriter, r *http.Request) {
	if err := r.ParseForm(); err != nil {
		http.Error(w, "failed to parse form", http.StatusBadRequest)
		return
	}

	input := posts.CreateInput{
		Title:       r.PostForm.Get("title"),
		Tags:        posts.ParseTags(r.PostForm.Get("tags")),
		Source:      r.PostForm.Get("source"),
		AutoPublish: r.PostForm.Get("auto_publish") == "on",
	}
	if err := input.Validate(); err != nil {
		w.WriteHeader(http.StatusBadRequest)
		app.renderEdit(w, editPageData{
			Heading:     "Create post",
			Action:      "/posts",
			Error:       err.Error(),
			Title:       input.Title,
			Tags:        posts.FormatTags(input.Tags),
			Source:      input.Source,
			AutoPublish: input.AutoPublish,
		})
		return
	}
	if _, err := app.store.Create(r.Context(), input); err != nil {
		http.Error(w, "failed to create post", http.StatusInternalServerError)
		slog.Error("failed to create post", "error", err)
		return
	}
	http.Redirect(w, r, "/posts", http.StatusSeeOther)
}

func (app app) postDetail(w http.ResponseWriter, r *http.Request) {
	post, ok := app.loadPost(w, r)
	if !ok {
		return
	}

	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	if err := detailTemplate.Execute(w, detailPageData{
		PublicBlog: app.publicBlog,
		Post:       post,
	}); err != nil {
		slog.Error("failed to render post detail page", "error", err)
	}
}

func (app app) edit(w http.ResponseWriter, r *http.Request) {
	post, ok := app.loadPost(w, r)
	if !ok {
		return
	}
	source := post.Source
	if r.URL.Query().Get("source") == "suggested" && post.Suggested != "" {
		source = post.Suggested
	}
	app.renderEdit(w, editPageData{
		Heading:     "Edit post",
		Action:      "/posts/" + strconv.FormatInt(post.ID, 10) + "/revisions",
		Title:       post.Title,
		Tags:        posts.FormatTags(post.Tags),
		Source:      source,
		AutoPublish: post.AutoPublish,
	})
}

func (app app) updatePost(w http.ResponseWriter, r *http.Request) {
	id, ok := parsePostID(w, r)
	if !ok {
		return
	}
	if err := r.ParseForm(); err != nil {
		http.Error(w, "failed to parse form", http.StatusBadRequest)
		return
	}

	input := posts.UpdateInput{
		Title:       r.PostForm.Get("title"),
		Tags:        posts.ParseTags(r.PostForm.Get("tags")),
		Source:      r.PostForm.Get("source"),
		AutoPublish: r.PostForm.Get("auto_publish") == "on",
	}
	if err := input.Validate(); err != nil {
		w.WriteHeader(http.StatusBadRequest)
		app.renderEdit(w, editPageData{
			Heading:     "Edit post",
			Action:      "/posts/" + strconv.FormatInt(id, 10) + "/revisions",
			Error:       err.Error(),
			Title:       input.Title,
			Tags:        posts.FormatTags(input.Tags),
			Source:      input.Source,
			AutoPublish: input.AutoPublish,
		})
		return
	}
	if _, err := app.store.Update(r.Context(), id, input); err != nil {
		http.Error(w, "failed to update post", http.StatusInternalServerError)
		slog.Error("failed to update post", "post_id", id, "error", err)
		return
	}
	http.Redirect(w, r, "/posts/"+strconv.FormatInt(id, 10), http.StatusSeeOther)
}

func (app app) recheckPost(w http.ResponseWriter, r *http.Request) {
	id, ok := parsePostID(w, r)
	if !ok {
		return
	}
	if _, err := app.store.Recheck(r.Context(), id); err != nil {
		http.Error(w, "failed to recheck post", http.StatusInternalServerError)
		slog.Error("failed to recheck post", "post_id", id, "error", err)
		return
	}
	http.Redirect(w, r, "/posts/"+strconv.FormatInt(id, 10), http.StatusSeeOther)
}

func (app app) publishPost(w http.ResponseWriter, r *http.Request) {
	id, ok := parsePostID(w, r)
	if !ok {
		return
	}
	if _, err := app.store.Publish(r.Context(), id); err != nil {
		http.Error(w, "failed to publish post", http.StatusInternalServerError)
		slog.Error("failed to publish post", "post_id", id, "error", err)
		return
	}
	http.Redirect(w, r, "/posts/"+strconv.FormatInt(id, 10), http.StatusSeeOther)
}

func (app app) loadPost(w http.ResponseWriter, r *http.Request) (posts.Post, bool) {
	id, ok := parsePostID(w, r)
	if !ok {
		return posts.Post{}, false
	}
	post, err := app.store.Get(r.Context(), id)
	if err != nil {
		http.Error(w, "post not found", http.StatusNotFound)
		slog.Error("failed to load post", "post_id", id, "error", err)
		return posts.Post{}, false
	}
	return post, true
}

func parsePostID(w http.ResponseWriter, r *http.Request) (int64, bool) {
	id, err := strconv.ParseInt(r.PathValue("id"), 10, 64)
	if err != nil || id <= 0 {
		http.Error(w, "invalid post id", http.StatusBadRequest)
		return 0, false
	}
	return id, true
}

type editPageData struct {
	PublicBlog  string
	Heading     string
	Action      string
	Error       string
	Title       string
	Tags        string
	Source      string
	AutoPublish bool
}

type pageData struct {
	PublicBlog string
}

type postsPageData struct {
	PublicBlog string
	Posts      []posts.Post
}

type detailPageData struct {
	PublicBlog string
	posts.Post
}

func (app app) renderEdit(w http.ResponseWriter, data editPageData) {
	data.PublicBlog = app.publicBlog
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	if err := editTemplate.Execute(w, data); err != nil {
		slog.Error("failed to render edit page", "error", err)
	}
}

var templateFuncs = template.FuncMap{
	"canPublish":    canPublish,
	"publicPostURL": publicPostURL,
	"join":          strings.Join,
	"safeHTML":      func(value string) template.HTML { return template.HTML(value) },
}

func canPublish(post posts.Post) bool {
	return post.Status != posts.StatusChecking && post.Status != posts.StatusPublishing
}

func publicPostURL(base string, path string) string {
	base = strings.TrimSpace(base)
	path = strings.TrimSpace(path)
	if base == "" || path == "" {
		return ""
	}
	parsed, err := url.Parse(base)
	if err != nil {
		return ""
	}
	parsed.Path = strings.TrimRight(parsed.Path, "/") + "/" + strings.TrimLeft(path, "/")
	parsed.RawQuery = ""
	parsed.Fragment = ""
	return parsed.String()
}

var postsTemplate = template.Must(template.New("posts.html").Funcs(templateFuncs).ParseFS(content, "templates/posts.html"))
var detailTemplate = template.Must(template.New("detail.html").Funcs(templateFuncs).ParseFS(content, "templates/detail.html"))
var editTemplate = template.Must(template.New("edit.html").Funcs(templateFuncs).ParseFS(content, "templates/edit.html"))
var helpTemplate = template.Must(template.New("help.html").Funcs(templateFuncs).ParseFS(content, "templates/help.html"))

func writeResponse(w http.ResponseWriter, body string) {
	if _, err := io.WriteString(w, body); err != nil {
		slog.Debug("failed to write response", "error", err)
	}
}

func closeRepository(repo *posts.Repository) {
	if err := repo.Close(); err != nil {
		slog.Error("failed to close posts repository", "error", err)
	}
}

func mustSub(files fs.FS, dir string) fs.FS {
	sub, err := fs.Sub(files, dir)
	if err != nil {
		panic(err)
	}
	return sub
}
