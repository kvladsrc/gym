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

	"src/production/docker/blog-engine/internal/posts"
)

func TestProcessNextCompletesGrammarJob(t *testing.T) {
	store := &fakeStore{
		job: posts.Job{
			ID:         1,
			Kind:       posts.JobKindGrammarCheck,
			PostID:     2,
			RevisionID: 3,
			Source:     "This are bad.",
		},
		hasJob: true,
	}
	app := app{
		store: store,
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

	if store.completed.ID != store.job.ID {
		t.Fatalf("completed job id = %d, want %d", store.completed.ID, store.job.ID)
	}
	if store.result.SuggestedSource != "This is bad." {
		t.Fatalf("suggested source = %q", store.result.SuggestedSource)
	}
	if store.failed.ID != 0 {
		t.Fatalf("failed job id = %d, want 0", store.failed.ID)
	}
}

func TestProcessNextStoresCheckerFailure(t *testing.T) {
	store := &fakeStore{
		job: posts.Job{
			ID:         1,
			Kind:       posts.JobKindGrammarCheck,
			PostID:     2,
			RevisionID: 3,
			Source:     "source",
		},
		hasJob: true,
	}
	wantErr := errors.New("api unavailable")
	app := app{
		store:   store,
		checker: fakeChecker{err: wantErr},
	}

	err := app.processNext(context.Background())
	if !errors.Is(err, wantErr) {
		t.Fatalf("processNext() error = %v, want %v", err, wantErr)
	}
	if store.failed.ID != store.job.ID {
		t.Fatalf("failed job id = %d, want %d", store.failed.ID, store.job.ID)
	}
	if store.completed.ID != 0 {
		t.Fatalf("completed job id = %d, want 0", store.completed.ID)
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

type fakeStore struct {
	job       posts.Job
	hasJob    bool
	completed posts.Job
	failed    posts.Job
	result    posts.GrammarResult
}

func (store *fakeStore) LeaseGrammarJob(context.Context, time.Duration) (posts.Job, bool, error) {
	return store.job, store.hasJob, nil
}

func (store *fakeStore) CompleteGrammarJob(_ context.Context, job posts.Job, result posts.GrammarResult) error {
	store.completed = job
	store.result = result
	return nil
}

func (store *fakeStore) FailGrammarJob(_ context.Context, job posts.Job, _ error) error {
	store.failed = job
	return nil
}

type fakeChecker struct {
	result posts.GrammarResult
	err    error
}

func (checker fakeChecker) CheckGrammar(context.Context, string) (posts.GrammarResult, error) {
	return checker.result, checker.err
}

type roundTripFunc func(*http.Request) (*http.Response, error)

func (fn roundTripFunc) RoundTrip(req *http.Request) (*http.Response, error) {
	return fn(req)
}
