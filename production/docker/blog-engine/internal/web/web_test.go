package web

import (
	"context"
	"errors"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"src/production/docker/blog-engine/internal/objectstore"
)

func TestProxyServesStaticObject(t *testing.T) {
	store := fakeStore{
		objects: map[string]objectstore.Object{
			"public/index.html": {
				Body:        []byte("<!doctype html><h1>Published</h1>"),
				ContentType: "text/html; charset=utf-8",
			},
		},
	}
	app := app{store: store, bucket: "public"}

	req := httptest.NewRequest(http.MethodGet, "/", nil)
	rec := httptest.NewRecorder()
	app.proxy(rec, req)

	if rec.Code != http.StatusOK {
		t.Fatalf("status = %d, want %d", rec.Code, http.StatusOK)
	}
	if rec.Header().Get("Content-Type") != "text/html; charset=utf-8" {
		t.Fatalf("content type = %q", rec.Header().Get("Content-Type"))
	}
	if !strings.Contains(rec.Body.String(), "<h1>Published</h1>") {
		t.Fatalf("body = %q", rec.Body.String())
	}
}

func TestProxyReturnsNotFoundForMissingObject(t *testing.T) {
	app := app{
		store:  fakeStore{objects: map[string]objectstore.Object{}},
		bucket: "public",
	}

	req := httptest.NewRequest(http.MethodGet, "/", nil)
	rec := httptest.NewRecorder()
	app.proxy(rec, req)

	if rec.Code != http.StatusNotFound {
		t.Fatalf("status = %d, want %d", rec.Code, http.StatusNotFound)
	}
}

func TestRequestObjectKey(t *testing.T) {
	tests := []struct {
		name        string
		requestPath string
		want        string
		wantErr     bool
	}{
		{
			name:        "home",
			requestPath: "/",
			want:        "index.html",
		},
		{
			name:        "nested index",
			requestPath: "/tags/go/",
			want:        "tags/go/index.html",
		},
		{
			name:        "post",
			requestPath: "/posts/A1.html",
			want:        "posts/A1.html",
		},
		{
			name:        "reject traversal",
			requestPath: "/../secret",
			wantErr:     true,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got, err := requestObjectKey(tt.requestPath)
			if tt.wantErr {
				if err == nil {
					t.Fatalf("requestObjectKey() returned nil error")
				}
				return
			}
			if err != nil {
				t.Fatalf("requestObjectKey() returned error: %v", err)
			}
			if got != tt.want {
				t.Fatalf("key = %q, want %q", got, tt.want)
			}
		})
	}
}

type fakeStore struct {
	objects map[string]objectstore.Object
}

func (store fakeStore) Get(_ context.Context, bucket string, key string) (objectstore.Object, error) {
	object, ok := store.objects[bucket+"/"+key]
	if !ok {
		return objectstore.Object{}, objectstore.ErrNotFound
	}
	if strings.Contains(string(object.Body), "force-error") {
		return objectstore.Object{}, errors.New("forced")
	}
	return object, nil
}
