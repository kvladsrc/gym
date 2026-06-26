package worker

import (
	"bytes"
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"strings"
	"time"

	"src/production/docker/blog-engine/internal/config"
	"src/production/docker/blog-engine/internal/objectstore"
	"src/production/docker/blog-engine/internal/posts"
	"src/production/docker/blog-engine/internal/publication"
)

const defaultPollInterval = 30 * time.Second

type store interface {
	NextPendingCheck(context.Context) (posts.Post, bool, error)
	StartGrammarCheck(context.Context, posts.Post, posts.GrammarRequest) (int64, error)
	CompleteGrammarCheck(context.Context, posts.Post, int64, posts.GrammarResult) error
	FailGrammarCheck(context.Context, posts.Post, int64, error) error
	NextPendingPublish(context.Context) (posts.Post, bool, error)
	StartPublish(context.Context, posts.Post) error
	CompletePublish(context.Context, posts.Post, string) error
	FailPublish(context.Context, posts.Post, error) error
}

type checker interface {
	CheckGrammar(context.Context, string) (posts.GrammarResult, error)
}

type publisher interface {
	Publish(context.Context, posts.Post) (string, error)
}

// Run starts the background worker component.
func Run(ctx context.Context, cfg config.Config) error {
	interval, err := pollInterval(cfg)
	if err != nil {
		return err
	}
	slog.Info("starting worker component", "service", cfg.Service.Name, "poll_interval", interval)

	repo, err := posts.Open(cfg.Database.DSN)
	if err != nil {
		return err
	}
	defer closeRepository(repo)

	app := app{
		store:     repo,
		checker:   openAIClient{apiKey: cfg.OpenAI.APIKey, model: cfg.OpenAI.Model, httpClient: http.DefaultClient},
		publisher: publication.NewPublisher(cfg, objectstore.New(cfg)),
	}
	if err := app.processNext(ctx); err != nil {
		slog.Error("worker check failed", "error", err)
	}

	ticker := time.NewTicker(interval)
	defer ticker.Stop()

	for {
		select {
		case <-ctx.Done():
			slog.Info("worker stopped")
			return nil
		case <-ticker.C:
			if err := app.processNext(ctx); err != nil {
				slog.Error("worker check failed", "error", err)
			}
		}
	}
}

func pollInterval(cfg config.Config) (time.Duration, error) {
	raw := strings.TrimSpace(cfg.Worker.PollInterval)
	if raw == "" {
		return defaultPollInterval, nil
	}
	interval, err := time.ParseDuration(raw)
	if err != nil {
		return 0, fmt.Errorf("parse worker.poll_interval: %w", err)
	}
	if interval <= 0 {
		return 0, fmt.Errorf("worker.poll_interval must be positive")
	}
	return interval, nil
}

type app struct {
	store     store
	checker   checker
	publisher publisher
}

func (app app) processNext(ctx context.Context) error {
	processed, err := app.processPendingCheck(ctx)
	if err != nil {
		return err
	}
	if processed {
		return nil
	}
	return app.processPendingPublish(ctx)
}

func (app app) processPendingCheck(ctx context.Context) (bool, error) {
	post, ok, err := app.store.NextPendingCheck(ctx)
	if err != nil {
		return false, err
	}
	if !ok {
		slog.Debug("no posts pending check")
		return false, nil
	}

	checkID, err := app.store.StartGrammarCheck(ctx, post, posts.GrammarRequest{
		Title:  post.Title,
		Tags:   post.Tags,
		Source: post.Source,
	})
	if err != nil {
		return false, err
	}
	result, err := app.checker.CheckGrammar(ctx, post.Source)
	if err != nil {
		if failErr := app.store.FailGrammarCheck(ctx, post, checkID, err); failErr != nil {
			return true, fmt.Errorf("grammar check failed: %w; storing failure: %v", err, failErr)
		}
		return true, err
	}
	if err := app.store.CompleteGrammarCheck(ctx, post, checkID, result); err != nil {
		return true, err
	}
	slog.Info("grammar check completed", "post_id", post.ID, "revision_id", post.RevisionID, "check_id", checkID)
	return true, nil
}

func (app app) processPendingPublish(ctx context.Context) error {
	post, ok, err := app.store.NextPendingPublish(ctx)
	if err != nil {
		return err
	}
	if !ok {
		slog.Debug("no posts pending publish")
		return nil
	}

	if err := app.store.StartPublish(ctx, post); err != nil {
		return err
	}
	publishedPath, err := app.publisher.Publish(ctx, post)
	if err != nil {
		if failErr := app.store.FailPublish(ctx, post, err); failErr != nil {
			return fmt.Errorf("publish failed: %w; storing failure: %v", err, failErr)
		}
		return err
	}
	if err := app.store.CompletePublish(ctx, post, publishedPath); err != nil {
		if failErr := app.store.FailPublish(ctx, post, err); failErr != nil {
			return fmt.Errorf("publish failed: %w; storing failure: %v", err, failErr)
		}
		return err
	}
	slog.Info("publish completed", "post_id", post.ID, "revision_id", post.RevisionID)
	return nil
}

type openAIClient struct {
	apiKey     string
	model      string
	httpClient *http.Client
}

func (client openAIClient) CheckGrammar(ctx context.Context, source string) (posts.GrammarResult, error) {
	if strings.TrimSpace(client.apiKey) == "" {
		return posts.GrammarResult{}, errors.New("openai api key is required")
	}
	if strings.TrimSpace(client.model) == "" {
		return posts.GrammarResult{}, errors.New("openai model is required")
	}
	httpClient := client.httpClient
	if httpClient == nil {
		httpClient = http.DefaultClient
	}

	body := responseRequest{
		Model: client.model,
		Input: []responseMessage{
			{
				Role:    "developer",
				Content: "You are a grammar checker for org-mode blog posts. Return only JSON with keys suggested_source and summary. Preserve org-mode markup and the author's style. Fix only necessary grammar mistakes. If no changes are needed, suggested_source must exactly equal the input.",
			},
			{
				Role:    "user",
				Content: source,
			},
		},
	}
	encoded, err := json.Marshal(body)
	if err != nil {
		return posts.GrammarResult{}, fmt.Errorf("encode openai request: %w", err)
	}
	req, err := http.NewRequestWithContext(ctx, http.MethodPost, "https://api.openai.com/v1/responses", bytes.NewReader(encoded))
	if err != nil {
		return posts.GrammarResult{}, fmt.Errorf("create openai request: %w", err)
	}
	req.Header.Set("Authorization", "Bearer "+client.apiKey)
	req.Header.Set("Content-Type", "application/json")

	resp, err := httpClient.Do(req)
	if err != nil {
		return posts.GrammarResult{}, fmt.Errorf("call openai responses api: %w", err)
	}
	defer func() {
		_ = resp.Body.Close()
	}()
	raw, err := io.ReadAll(io.LimitReader(resp.Body, 1<<20))
	if err != nil {
		return posts.GrammarResult{}, fmt.Errorf("read openai response: %w", err)
	}
	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		return posts.GrammarResult{}, fmt.Errorf("openai responses api returned %s: %s", resp.Status, strings.TrimSpace(string(raw)))
	}

	text, err := outputText(raw)
	if err != nil {
		return posts.GrammarResult{}, err
	}
	var result posts.GrammarResult
	if err := json.Unmarshal([]byte(text), &result); err != nil {
		return posts.GrammarResult{}, fmt.Errorf("parse grammar result JSON: %w", err)
	}
	if strings.TrimSpace(result.SuggestedSource) == "" {
		return posts.GrammarResult{}, errors.New("grammar result missing suggested_source")
	}
	return result, nil
}

type responseRequest struct {
	Model string            `json:"model"`
	Input []responseMessage `json:"input"`
}

type responseMessage struct {
	Role    string `json:"role"`
	Content string `json:"content"`
}

func outputText(raw []byte) (string, error) {
	var parsed struct {
		OutputText string `json:"output_text"`
		Output     []struct {
			Type    string `json:"type"`
			Content []struct {
				Type string `json:"type"`
				Text string `json:"text"`
			} `json:"content"`
		} `json:"output"`
	}
	if err := json.Unmarshal(raw, &parsed); err != nil {
		return "", fmt.Errorf("parse openai response: %w", err)
	}
	if parsed.OutputText != "" {
		return parsed.OutputText, nil
	}
	for _, item := range parsed.Output {
		if item.Type != "message" {
			continue
		}
		for _, content := range item.Content {
			if content.Type == "output_text" && content.Text != "" {
				return content.Text, nil
			}
		}
	}
	return "", errors.New("openai response did not contain output text")
}

func closeRepository(repo *posts.Repository) {
	if err := repo.Close(); err != nil {
		slog.Error("failed to close posts repository", "error", err)
	}
}
