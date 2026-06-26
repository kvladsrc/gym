package worker

import (
	"bytes"
	"context"
	"errors"
	"io"
	"net/http"
	"strings"
	"testing"
	"time"

	"src/production/docker/blog-engine/internal/config"
	"src/production/docker/blog-engine/internal/posts"
)

func TestProcessNextCompletesPendingCheck(t *testing.T) {
	store := &fakeStore{
		post: posts.Post{
			ID:         2,
			RevisionID: 3,
			Title:      "A title",
			Tags:       []string{"go"},
			Source:     "This are bad.",
		},
		checkID: 4,
		hasPost: true,
	}
	app := app{
		store:     store,
		publisher: fakePublisher{},
		checker: fakeChecker{
			result: posts.GrammarResult{
				SuggestedSource: "This is bad.",
				Summary:         "Fixed subject-verb agreement.",
			},
		},
	}

	if err := app.processNext(context.Background()); err != nil {
		t.Fatalf("processNext() returned error: %v", err)
	}

	if store.completed.ID != store.post.ID {
		t.Fatalf("completed post id = %d, want %d", store.completed.ID, store.post.ID)
	}
	if store.completedCheckID != store.checkID {
		t.Fatalf("completed check id = %d, want %d", store.completedCheckID, store.checkID)
	}
	if store.request.Source != "This are bad." {
		t.Fatalf("request source = %q, want %q", store.request.Source, "This are bad.")
	}
	if store.result.SuggestedSource != "This is bad." {
		t.Fatalf("suggested source = %q", store.result.SuggestedSource)
	}
	if store.failed.ID != 0 {
		t.Fatalf("failed post id = %d, want 0", store.failed.ID)
	}
}

func TestProcessNextCompletesAutoPublishCheck(t *testing.T) {
	store := &fakeStore{
		post: posts.Post{
			ID:          2,
			RevisionID:  3,
			Title:       "A title",
			Source:      "Looks good.",
			AutoPublish: true,
		},
		checkID: 4,
		hasPost: true,
	}
	app := app{
		store:     store,
		publisher: fakePublisher{},
		checker: fakeChecker{
			result: posts.GrammarResult{
				SuggestedSource: "Looks good.",
				Summary:         "No changes.",
			},
		},
	}

	if err := app.processNext(context.Background()); err != nil {
		t.Fatalf("processNext() returned error: %v", err)
	}

	if !store.resultPost.AutoPublish {
		t.Fatalf("completed grammar check did not receive auto-publish post")
	}
	if store.published.ID != 0 {
		t.Fatalf("published post id = %d, want 0", store.published.ID)
	}
}

func TestProcessNextStoresCheckerFailure(t *testing.T) {
	store := &fakeStore{
		post: posts.Post{
			ID:         2,
			RevisionID: 3,
			Title:      "A title",
			Source:     "source",
		},
		checkID: 4,
		hasPost: true,
	}
	wantErr := errors.New("api unavailable")
	app := app{
		store:     store,
		checker:   fakeChecker{err: wantErr},
		publisher: fakePublisher{},
	}

	err := app.processNext(context.Background())
	if !errors.Is(err, wantErr) {
		t.Fatalf("processNext() error = %v, want %v", err, wantErr)
	}
	if store.failed.ID != store.post.ID {
		t.Fatalf("failed post id = %d, want %d", store.failed.ID, store.post.ID)
	}
	if store.failedCheckID != store.checkID {
		t.Fatalf("failed check id = %d, want %d", store.failedCheckID, store.checkID)
	}
	if store.completed.ID != 0 {
		t.Fatalf("completed post id = %d, want 0", store.completed.ID)
	}
}

func TestProcessNextCompletesPendingPublish(t *testing.T) {
	store := &fakeStore{
		publishPost: posts.Post{
			ID:         2,
			RevisionID: 3,
			Title:      "A title",
			Source:     "source",
			Status:     posts.StatusPendingPublish,
		},
		hasPublish: true,
	}
	published := false
	app := app{
		store:     store,
		checker:   fakeChecker{},
		publisher: fakePublisher{published: &published},
	}

	if err := app.processNext(context.Background()); err != nil {
		t.Fatalf("processNext() returned error: %v", err)
	}

	if store.startedPublish.ID != store.publishPost.ID {
		t.Fatalf("started publish post id = %d, want %d", store.startedPublish.ID, store.publishPost.ID)
	}
	if store.published.ID != store.publishPost.ID {
		t.Fatalf("published post id = %d, want %d", store.published.ID, store.publishPost.ID)
	}
	if store.publishedPath != "/posts/published.html" {
		t.Fatalf("published path = %q, want %q", store.publishedPath, "/posts/published.html")
	}
	if !published {
		t.Fatalf("publisher was not called")
	}
	if store.request.Source != "" {
		t.Fatalf("grammar check request source = %q, want empty", store.request.Source)
	}
}

func TestProcessNextStoresPublishFailure(t *testing.T) {
	store := &fakeStore{
		publishPost: posts.Post{
			ID:         2,
			RevisionID: 3,
			Title:      "A title",
			Source:     "#+BEGIN_SRC go",
			Status:     posts.StatusPendingPublish,
		},
		hasPublish: true,
	}
	wantErr := errors.New("render failed")
	app := app{
		store:     store,
		checker:   fakeChecker{},
		publisher: fakePublisher{err: wantErr},
	}

	err := app.processNext(context.Background())
	if !errors.Is(err, wantErr) {
		t.Fatalf("processNext() error = %v, want %v", err, wantErr)
	}
	if store.failedPublish.ID != store.publishPost.ID {
		t.Fatalf("failed publish post id = %d, want %d", store.failedPublish.ID, store.publishPost.ID)
	}
	if store.published.ID != 0 {
		t.Fatalf("published post id = %d, want 0", store.published.ID)
	}
}

func TestOutputText(t *testing.T) {
	tests := []struct {
		name string
		raw  string
		want string
	}{
		{
			name: "output_text shortcut",
			raw:  `{"output_text":"{\"suggested_source\":\"ok\",\"summary\":\"none\"}"}`,
			want: `{"suggested_source":"ok","summary":"none"}`,
		},
		{
			name: "message content",
			raw:  `{"output":[{"type":"message","content":[{"type":"output_text","text":"{\"suggested_source\":\"ok\",\"summary\":\"none\"}"}]}]}`,
			want: `{"suggested_source":"ok","summary":"none"}`,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got, err := outputText([]byte(tt.raw))
			if err != nil {
				t.Fatalf("outputText() returned error: %v", err)
			}
			if got != tt.want {
				t.Fatalf("outputText() = %q, want %q", got, tt.want)
			}
		})
	}
}

func TestOpenAIClientRequiresSuggestedSource(t *testing.T) {
	client := openAIClient{
		apiKey: "test-key",
		model:  "gpt-5.4-nano",
		httpClient: &http.Client{
			Transport: roundTripFunc(func(*http.Request) (*http.Response, error) {
				return &http.Response{
					StatusCode: http.StatusOK,
					Status:     "200 OK",
					Body:       io.NopCloser(bytes.NewBufferString(`{"output_text":"{\"summary\":\"none\"}"}`)),
				}, nil
			}),
		},
	}

	_, err := client.CheckGrammar(context.Background(), "source")
	if err == nil || !strings.Contains(err.Error(), "suggested_source") {
		t.Fatalf("CheckGrammar() error = %v, want missing suggested_source", err)
	}
}

func TestPollInterval(t *testing.T) {
	tests := []struct {
		name    string
		value   string
		want    time.Duration
		wantErr string
	}{
		{
			name: "default",
			want: defaultPollInterval,
		},
		{
			name:  "configured",
			value: "5m",
			want:  5 * time.Minute,
		},
		{
			name:    "rejects invalid duration",
			value:   "5 minutes",
			wantErr: "parse worker.poll_interval",
		},
		{
			name:    "rejects zero",
			value:   "0s",
			wantErr: "worker.poll_interval must be positive",
		},
		{
			name:    "rejects negative",
			value:   "-1s",
			wantErr: "worker.poll_interval must be positive",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got, err := pollInterval(config.Config{
				Worker: config.WorkerConfig{PollInterval: tt.value},
			})
			if tt.wantErr == "" {
				if err != nil {
					t.Fatalf("pollInterval() returned error: %v", err)
				}
				if got != tt.want {
					t.Fatalf("pollInterval() = %v, want %v", got, tt.want)
				}
				return
			}
			if err == nil || !strings.Contains(err.Error(), tt.wantErr) {
				t.Fatalf("pollInterval() error = %v, want substring %q", err, tt.wantErr)
			}
		})
	}
}

type fakeStore struct {
	post             posts.Post
	publishPost      posts.Post
	checkID          int64
	hasPost          bool
	hasPublish       bool
	completed        posts.Post
	completedCheckID int64
	failed           posts.Post
	failedCheckID    int64
	startedPublish   posts.Post
	published        posts.Post
	failedPublish    posts.Post
	request          posts.GrammarRequest
	result           posts.GrammarResult
	resultPost       posts.Post
	publishedPath    string
}

func (store *fakeStore) NextPendingCheck(context.Context) (posts.Post, bool, error) {
	return store.post, store.hasPost, nil
}

func (store *fakeStore) StartGrammarCheck(_ context.Context, _ posts.Post, request posts.GrammarRequest) (int64, error) {
	store.request = request
	return store.checkID, nil
}

func (store *fakeStore) CompleteGrammarCheck(_ context.Context, post posts.Post, checkID int64, result posts.GrammarResult) error {
	store.completed = post
	store.completedCheckID = checkID
	store.result = result
	store.resultPost = post
	return nil
}

func (store *fakeStore) FailGrammarCheck(_ context.Context, post posts.Post, checkID int64, _ error) error {
	store.failed = post
	store.failedCheckID = checkID
	return nil
}

func (store *fakeStore) NextPendingPublish(context.Context) (posts.Post, bool, error) {
	return store.publishPost, store.hasPublish, nil
}

func (store *fakeStore) StartPublish(_ context.Context, post posts.Post) error {
	store.startedPublish = post
	return nil
}

func (store *fakeStore) CompletePublish(_ context.Context, post posts.Post, publishedPath string) error {
	store.published = post
	store.publishedPath = publishedPath
	return nil
}

func (store *fakeStore) FailPublish(_ context.Context, post posts.Post, _ error) error {
	store.failedPublish = post
	return nil
}

type fakeChecker struct {
	result posts.GrammarResult
	err    error
}

func (checker fakeChecker) CheckGrammar(context.Context, string) (posts.GrammarResult, error) {
	return checker.result, checker.err
}

type fakePublisher struct {
	published *bool
	err       error
}

func (publisher fakePublisher) Publish(_ context.Context, post posts.Post) (string, error) {
	if publisher.err != nil {
		return "", publisher.err
	}
	if publisher.published != nil {
		*publisher.published = true
	}
	return "/posts/published.html", nil
}

type roundTripFunc func(*http.Request) (*http.Response, error)

func (fn roundTripFunc) RoundTrip(req *http.Request) (*http.Response, error) {
	return fn(req)
}
