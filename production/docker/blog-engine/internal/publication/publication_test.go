package publication

import (
	"context"
	"encoding/json"
	"errors"
	"strings"
	"testing"

	"src/production/docker/blog-engine/internal/objectstore"
	"src/production/docker/blog-engine/internal/posts"
)

func TestRender(t *testing.T) {
	renderer := Renderer{}
	got, err := renderer.Render(context.Background(), posts.Post{
		ID:         1,
		RevisionID: 2,
		Title:      `A <title>`,
		Source: strings.Join([]string{
			"* Intro",
			"This is *bold*, /italic/, _underlined_, +struck+, =verbatim=, ~code~, and [[https://example.com][a link]].",
			"This line stays on a new line.",
			"",
			"Second paragraph.",
		}, "\n"),
		Tags: []string{"go", "blog"},
	})
	if err != nil {
		t.Fatalf("Render() returned error: %v", err)
	}
	for _, want := range []string{
		"<title>A &lt;title&gt;</title>",
		`<link rel="stylesheet" href="/public.css" />`,
		"<h1>A &lt;title&gt;</h1>",
		`<p class="post-meta">Published `,
		`<nav class="post-tags" aria-label="Tags"><a href="/tags/go/">go</a><a href="/tags/blog/">blog</a></nav>`,
		"<h1>Intro</h1>",
		"<strong>bold</strong>",
		"<em>italic</em>",
		"<u>underlined</u>",
		"<s>struck</s>",
		"<code>verbatim</code>",
		"<code>code</code>",
		`<a href="https://example.com">a link</a>`,
		"This is <strong>bold</strong>, <em>italic</em>, <u>underlined</u>, " +
			"<s>struck</s>, <code>verbatim</code>, <code>code</code>, and " +
			`<a href="https://example.com">a link</a>.<br>` + "\n" +
			"This line stays on a new line.",
		"<p>Second paragraph.</p>",
	} {
		if !strings.Contains(got, want) {
			t.Fatalf("rendered HTML missing %q:\n%s", want, got)
		}
	}
	if strings.Contains(got, `<title>A <title></title>`) {
		t.Fatalf("rendered HTML contains unescaped title:\n%s", got)
	}
}

func TestRenderPreservesCRLFLineBreaks(t *testing.T) {
	renderer := Renderer{}
	got, err := renderer.Render(context.Background(), posts.Post{
		ID:         1,
		RevisionID: 2,
		Title:      "Title",
		Source:     "First line\r\nSecond line\r\n\r\nNext paragraph.",
	})
	if err != nil {
		t.Fatalf("Render() returned error: %v", err)
	}
	for _, want := range []string{
		"<p>First line<br>\nSecond line</p>",
		"<p>Next paragraph.</p>",
	} {
		if !strings.Contains(got, want) {
			t.Fatalf("rendered HTML missing %q:\n%s", want, got)
		}
	}
}

func TestRenderRejectsUnsupportedDirective(t *testing.T) {
	renderer := Renderer{}
	_, err := renderer.Render(context.Background(), posts.Post{
		ID:         1,
		RevisionID: 2,
		Title:      "Title",
		Source:     "#+BEGIN_SRC go",
	})
	if err == nil || !strings.Contains(err.Error(), "unsupported org directive") {
		t.Fatalf("Render() error = %v, want unsupported directive", err)
	}
}

func TestPublishAdvancesFlatLinkedLists(t *testing.T) {
	store := newFakeStore()
	publisher := Publisher{
		Renderer: Renderer{},
		Store:    store,
		Bucket:   "public",
		StateKey: "state.json",
		GenerateKey: sequenceKeys(
			"A0", "A1",
			"G0", "G1",
			"A2",
			"G2",
		),
	}

	firstPath, err := publisher.Publish(context.Background(), posts.Post{
		ID:         7,
		RevisionID: 11,
		Title:      "First",
		Tags:       []string{"go"},
		Source:     "Body A0",
	})
	if err != nil {
		t.Fatalf("first Publish() returned error: %v", err)
	}
	if firstPath != "/posts/A0.html" {
		t.Fatalf("first published path = %q, want %q", firstPath, "/posts/A0.html")
	}

	assertObjectContains(t, store, "public", "posts/A0.html", "<h1>First</h1>")
	assertObjectContains(t, store, "public", "posts/A0.html", `href="/public.css"`)
	assertObjectContains(t, store, "public", "posts/A0.html", `href="/posts/A1.html"`)
	assertObjectContains(t, store, "public", "public.css", `font-family: "Mononoki Nerd Font"`)
	assertObjectContains(t, store, "public", "posts/A1.html", "<h1>First</h1>")
	assertObjectContains(t, store, "public", "tags/go/G0.html", `href="/tags/go/G1.html"`)
	assertObjectContains(t, store, "public", "tags/go/index.html", "<h1>Posts tagged go</h1>")
	assertObjectContains(t, store, "public", "tags/go/index.html", `<h2><a href="/posts/A0.html">First</a></h2>`)
	assertObjectContains(t, store, "public", "index.html", "<h1>Posts</h1>")
	assertObjectContains(t, store, "public", "index.html", `<h2><a href="/posts/A0.html">First</a></h2>`)
	assertObjectContains(t, store, "public", "index.html", "<p>Body A0</p>")

	firstPlaceholder := store.mustBody(t, "public", "posts/A1.html")
	secondPath, err := publisher.Publish(context.Background(), posts.Post{
		ID:         8,
		RevisionID: 12,
		Title:      "Second",
		Tags:       []string{"go"},
		Source:     "Body A1",
	})
	if err != nil {
		t.Fatalf("second Publish() returned error: %v", err)
	}
	if secondPath != "/posts/A1.html" {
		t.Fatalf("second published path = %q, want %q", secondPath, "/posts/A1.html")
	}

	assertObjectContains(t, store, "public", "posts/A0.html", "<h1>First</h1>")
	assertObjectContains(t, store, "public", "posts/A1.html", "<h1>Second</h1>")
	assertObjectContains(t, store, "public", "posts/A1.html", `href="/posts/A2.html"`)
	assertObjectContains(t, store, "public", "posts/A1.html", `href="/posts/A0.html"`)
	assertObjectContains(t, store, "public", "posts/A2.html", "<h1>Second</h1>")
	assertObjectContains(t, store, "public", "tags/go/G1.html", `href="/tags/go/G2.html"`)
	assertObjectContains(t, store, "public", "tags/go/G1.html", `href="/tags/go/G0.html"`)
	assertObjectContains(t, store, "public", "index.html", `<h2><a href="/posts/A1.html">Second</a></h2>`)
	assertObjectContains(t, store, "public", "index.html", `<h2><a href="/posts/A0.html">First</a></h2>`)
	assertObjectContains(t, store, "public", "tags/go/index.html", `<h2><a href="/posts/A1.html">Second</a></h2>`)
	assertObjectContains(t, store, "public", "tags/go/index.html", `<h2><a href="/posts/A0.html">First</a></h2>`)

	if store.mustBody(t, "public", "posts/A1.html") == firstPlaceholder {
		t.Fatalf("posts/A1.html was not replaced with the real second post")
	}

	var state State
	if err := json.Unmarshal([]byte(store.mustBody(t, "public", "state.json")), &state); err != nil {
		t.Fatalf("state did not decode: %v", err)
	}
	if state.Global.Head != "A1" || state.Global.Next != "A2" {
		t.Fatalf("global state = %+v", state.Global)
	}
	if len(state.Global.Items) != 2 {
		t.Fatalf("global feed items = %+v, want 2 items", state.Global.Items)
	}
	if state.Global.Items[0].Title != "Second" || state.Global.Items[0].Path != "/posts/A1.html" {
		t.Fatalf("first global feed item = %+v", state.Global.Items[0])
	}
	if state.Tags["go"].Head != "G1" || state.Tags["go"].Next != "G2" {
		t.Fatalf("go state = %+v", state.Tags["go"])
	}
	if len(state.Tags["go"].Items) != 2 {
		t.Fatalf("go feed items = %+v, want 2 items", state.Tags["go"].Items)
	}
}

func TestExcerptStripsInlineMarkup(t *testing.T) {
	got := excerpt(strings.Join([]string{
		"* Intro",
		"This is *bold* with [[https://example.com][a link]] and ~code~.",
	}, "\n"), 8)

	want := "Intro This is bold with a link and..."
	if got != want {
		t.Fatalf("excerpt() = %q, want %q", got, want)
	}
}

func assertObjectContains(t *testing.T, store *fakeStore, bucket string, key string, want string) {
	t.Helper()
	body := store.mustBody(t, bucket, key)
	if !strings.Contains(body, want) {
		t.Fatalf("%s/%s missing %q:\n%s", bucket, key, want, body)
	}
}

func sequenceKeys(values ...string) func() (string, error) {
	index := 0
	return func() (string, error) {
		if index >= len(values) {
			return "", errors.New("no more keys")
		}
		value := values[index]
		index++
		return value, nil
	}
}

type fakeObject struct {
	contentType string
	body        []byte
}

type fakeStore struct {
	objects map[string]fakeObject
}

func newFakeStore() *fakeStore {
	return &fakeStore{objects: make(map[string]fakeObject)}
}

func (store *fakeStore) Get(_ context.Context, bucket string, key string) (objectstore.Object, error) {
	object, ok := store.objects[bucket+"/"+key]
	if !ok {
		return objectstore.Object{}, objectstore.ErrNotFound
	}
	return objectstore.Object{
		Body:        append([]byte(nil), object.body...),
		ContentType: object.contentType,
	}, nil
}

func (store *fakeStore) Put(_ context.Context, bucket string, key string, contentType string, body []byte) error {
	store.objects[bucket+"/"+key] = fakeObject{
		contentType: contentType,
		body:        append([]byte(nil), body...),
	}
	return nil
}

func (store *fakeStore) mustBody(t *testing.T, bucket string, key string) string {
	t.Helper()
	object, ok := store.objects[bucket+"/"+key]
	if !ok {
		t.Fatalf("missing object %s/%s", bucket, key)
	}
	return string(object.body)
}
