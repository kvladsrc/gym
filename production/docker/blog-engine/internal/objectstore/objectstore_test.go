package objectstore

import (
	"context"
	"errors"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"src/production/docker/blog-engine/internal/config"
)

func TestPutSignsAndWritesObject(t *testing.T) {
	var gotPath string
	var gotBody string
	var gotContentType string
	var gotAuthorization string
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, req *http.Request) {
		gotPath = req.URL.Path
		gotContentType = req.Header.Get("Content-Type")
		gotAuthorization = req.Header.Get("Authorization")
		raw, err := io.ReadAll(req.Body)
		if err != nil {
			t.Fatalf("ReadAll() returned error: %v", err)
		}
		gotBody = string(raw)
		w.WriteHeader(http.StatusOK)
	}))
	defer server.Close()

	client := testClient(server.URL)
	err := client.Put(context.Background(), "public", "posts/A0.html", "text/html; charset=utf-8", []byte("<html>ok</html>"))
	if err != nil {
		t.Fatalf("Put() returned error: %v", err)
	}

	if gotPath != "/public/posts/A0.html" {
		t.Fatalf("path = %q, want path-style object URL", gotPath)
	}
	if gotBody != "<html>ok</html>" {
		t.Fatalf("body = %q, want rendered HTML", gotBody)
	}
	if gotContentType != "text/html; charset=utf-8" {
		t.Fatalf("content type = %q", gotContentType)
	}
	if !strings.HasPrefix(gotAuthorization, "AWS4-HMAC-SHA256 Credential=access/20260623/us-east-1/s3/aws4_request") {
		t.Fatalf("authorization = %q", gotAuthorization)
	}
}

func TestGetReadsObject(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, req *http.Request) {
		if req.URL.Path != "/public/state.json" {
			t.Fatalf("path = %q, want publication state", req.URL.Path)
		}
		w.Header().Set("Content-Type", "application/json")
		_, _ = w.Write([]byte(`{"global":{"head":"A0","next":"A1"}}`))
	}))
	defer server.Close()

	got, err := testClient(server.URL).Get(context.Background(), "public", "state.json")
	if err != nil {
		t.Fatalf("Get() returned error: %v", err)
	}
	if got.ContentType != "application/json" {
		t.Fatalf("content type = %q", got.ContentType)
	}
	if string(got.Body) != `{"global":{"head":"A0","next":"A1"}}` {
		t.Fatalf("body = %q", got.Body)
	}
}

func TestGetNotFound(t *testing.T) {
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) {
		http.NotFound(w, nil)
	}))
	defer server.Close()

	_, err := testClient(server.URL).Get(context.Background(), "public", "missing")
	if !errors.Is(err, ErrNotFound) {
		t.Fatalf("Get() error = %v, want ErrNotFound", err)
	}
}

func testClient(endpoint string) Client {
	cfg := config.Config{
		S3: config.S3Config{
			Endpoint:       endpoint,
			Region:         "us-east-1",
			AccessKey:      "access",
			SecretKey:      "secret",
			ForcePathStyle: true,
		},
	}
	client := New(cfg)
	client.Now = func() time.Time {
		return time.Date(2026, 6, 23, 12, 0, 0, 0, time.UTC)
	}
	return client
}
