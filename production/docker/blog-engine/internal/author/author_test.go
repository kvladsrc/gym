package author

import (
	"context"
	"net/http"
	"net/http/httptest"
	"net/url"
	"strings"
	"testing"
	"time"

	"src/production/docker/blog-engine/internal/posts"
)

func TestPages(t *testing.T) {
	now := time.Date(2026, 6, 20, 12, 30, 0, 0, time.UTC)
	app := app{
		store: &fakeStore{
			list: []posts.Post{
				{
					ID:          1,
					RevisionID:  1,
					Title:       "First post",
					Tags:        []string{"go", "blog"},
					Source:      "* Heading",
					Suggested:   "* Better heading",
					DiffHTML:    "<pre>diff</pre>",
					Status:      posts.StatusPendingCheck,
					CheckStatus: "",
					UpdatedAt:   now,
				},
			},
		},
	}
	tests := []struct {
		name    string
		path    string
		handler http.HandlerFunc
		want    []string
	}{
		{
			name:    "posts page renders table",
			path:    "/posts",
			handler: app.posts,
			want: []string{
				`<nav><a href="/posts">Posts</a> <a href="/create">Create</a></nav>`,
				"<h1>Posts</h1>",
				"<table>",
				"<th>Status</th>",
				"First post",
				`href="/posts/1"`,
				"go, blog",
			},
		},
		{
			name:    "create page renders post form",
			path:    "/create",
			handler: app.create,
			want: []string{
				`<nav><a href="/posts">Posts</a> <a href="/create">Create</a></nav>`,
				`<form method="post" action="/posts">`,
				`name="title"`,
				`name="tags"`,
				`name="source"`,
				`name="auto_publish"`,
			},
		},
		{
			name:    "post detail renders actions and artifacts",
			path:    "/posts/1",
			handler: app.postDetail,
			want: []string{
				`<nav><a href="/posts">Posts</a> <a href="/create">Create</a></nav>`,
				"<h1>First post</h1>",
				"Edit original",
				"Edit suggested",
				"Publish",
				"Recheck",
				"* Heading",
				"* Better heading",
				"<pre>diff</pre>",
			},
		},
		{
			name:    "edit page renders original post form",
			path:    "/posts/1/edit",
			handler: app.edit,
			want: []string{
				`<nav><a href="/posts">Posts</a> <a href="/create">Create</a></nav>`,
				"<h1>Edit post</h1>",
				`action="/posts/1/revisions"`,
				`value="First post"`,
				"* Heading",
			},
		},
		{
			name:    "edit page can start from suggested source",
			path:    "/posts/1/edit?source=suggested",
			handler: app.edit,
			want: []string{
				"* Better heading",
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			req := httptest.NewRequest(http.MethodGet, tt.path, nil)
			if strings.HasPrefix(tt.path, "/posts/1") {
				req.SetPathValue("id", "1")
			}
			rec := httptest.NewRecorder()

			tt.handler(rec, req)

			if rec.Code != http.StatusOK {
				t.Fatalf("status = %d, want %d", rec.Code, http.StatusOK)
			}
			body := rec.Body.String()
			for _, want := range tt.want {
				if !strings.Contains(body, want) {
					t.Fatalf("body missing %q:\n%s", want, body)
				}
			}
		})
	}
}

func TestCreatePost(t *testing.T) {
	tests := []struct {
		name       string
		form       url.Values
		wantStatus int
		wantTitle  string
		wantTags   []string
		wantBody   string
	}{
		{
			name: "creates post",
			form: url.Values{
				"title":        {"A title"},
				"tags":         {"go, blog"},
				"source":       {"* Heading"},
				"auto_publish": {"on"},
			},
			wantStatus: http.StatusSeeOther,
			wantTitle:  "A title",
			wantTags:   []string{"go", "blog"},
		},
		{
			name: "renders validation errors",
			form: url.Values{
				"source": {"* Heading"},
			},
			wantStatus: http.StatusBadRequest,
			wantBody:   "title is required",
		},
		{
			name: "rejects invalid tags",
			form: url.Values{
				"title":  {"A title"},
				"tags":   {"go blog"},
				"source": {"* Heading"},
			},
			wantStatus: http.StatusBadRequest,
			wantBody:   `tag &#34;go blog&#34; must be a lowercase slug`,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			store := &fakeStore{}
			app := app{store: store}
			req := httptest.NewRequest(http.MethodPost, "/posts", strings.NewReader(tt.form.Encode()))
			req.Header.Set("Content-Type", "application/x-www-form-urlencoded")
			rec := httptest.NewRecorder()

			app.createPost(rec, req)

			if rec.Code != tt.wantStatus {
				t.Fatalf("status = %d, want %d", rec.Code, tt.wantStatus)
			}
			if tt.wantBody != "" && !strings.Contains(rec.Body.String(), tt.wantBody) {
				t.Fatalf("body missing %q:\n%s", tt.wantBody, rec.Body.String())
			}
			if tt.wantTitle == "" {
				if len(store.created) != 0 {
					t.Fatalf("created posts = %d, want 0", len(store.created))
				}
				return
			}
			if len(store.created) != 1 {
				t.Fatalf("created posts = %d, want 1", len(store.created))
			}
			if store.created[0].Title != tt.wantTitle {
				t.Fatalf("created title = %q, want %q", store.created[0].Title, tt.wantTitle)
			}
			if strings.Join(store.created[0].Tags, ",") != strings.Join(tt.wantTags, ",") {
				t.Fatalf("created tags = %#v, want %#v", store.created[0].Tags, tt.wantTags)
			}
		})
	}
}

func TestUpdatePost(t *testing.T) {
	tests := []struct {
		name       string
		form       url.Values
		wantStatus int
		wantTitle  string
		wantTags   []string
		wantBody   string
	}{
		{
			name: "creates new revision",
			form: url.Values{
				"title":        {"Updated title"},
				"tags":         {"go, distributed-systems"},
				"source":       {"* Updated"},
				"auto_publish": {"on"},
			},
			wantStatus: http.StatusSeeOther,
			wantTitle:  "Updated title",
			wantTags:   []string{"go", "distributed-systems"},
		},
		{
			name: "renders validation errors",
			form: url.Values{
				"title":  {"Updated title"},
				"source": {""},
			},
			wantStatus: http.StatusBadRequest,
			wantBody:   "source is required",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			store := &fakeStore{}
			app := app{store: store}
			req := httptest.NewRequest(http.MethodPost, "/posts/1/revisions", strings.NewReader(tt.form.Encode()))
			req.SetPathValue("id", "1")
			req.Header.Set("Content-Type", "application/x-www-form-urlencoded")
			rec := httptest.NewRecorder()

			app.updatePost(rec, req)

			if rec.Code != tt.wantStatus {
				t.Fatalf("status = %d, want %d", rec.Code, tt.wantStatus)
			}
			if tt.wantBody != "" && !strings.Contains(rec.Body.String(), tt.wantBody) {
				t.Fatalf("body missing %q:\n%s", tt.wantBody, rec.Body.String())
			}
			if tt.wantTitle == "" {
				if len(store.updated) != 0 {
					t.Fatalf("updated posts = %d, want 0", len(store.updated))
				}
				return
			}
			if len(store.updated) != 1 {
				t.Fatalf("updated posts = %d, want 1", len(store.updated))
			}
			if store.updated[0].Title != tt.wantTitle {
				t.Fatalf("updated title = %q, want %q", store.updated[0].Title, tt.wantTitle)
			}
			if strings.Join(store.updated[0].Tags, ",") != strings.Join(tt.wantTags, ",") {
				t.Fatalf("updated tags = %#v, want %#v", store.updated[0].Tags, tt.wantTags)
			}
		})
	}
}

func TestHomeRedirectsToPosts(t *testing.T) {
	req := httptest.NewRequest(http.MethodGet, "/", nil)
	rec := httptest.NewRecorder()

	home(rec, req)

	if rec.Code != http.StatusSeeOther {
		t.Fatalf("status = %d, want %d", rec.Code, http.StatusSeeOther)
	}
	if location := rec.Header().Get("Location"); location != "/posts" {
		t.Fatalf("Location = %q, want %q", location, "/posts")
	}
}

type fakeStore struct {
	list    []posts.Post
	created []posts.CreateInput
	updated []posts.UpdateInput
}

func (store *fakeStore) Create(_ context.Context, input posts.CreateInput) (posts.Post, error) {
	store.created = append(store.created, input)
	return posts.Post{ID: int64(len(store.created)), Title: input.Title}, nil
}

func (store *fakeStore) ListLatest(context.Context) ([]posts.Post, error) {
	return store.list, nil
}

func (store *fakeStore) Get(_ context.Context, id int64) (posts.Post, error) {
	for _, post := range store.list {
		if post.ID == id {
			return post, nil
		}
	}
	return posts.Post{}, nil
}

func (store *fakeStore) Update(_ context.Context, _ int64, input posts.UpdateInput) (posts.Post, error) {
	store.updated = append(store.updated, input)
	return posts.Post{ID: int64(len(store.updated)), Title: input.Title}, nil
}
