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
	"src/production/docker/blog-engine/internal/posts"
)

const (
	pollInterval = 30 * time.Second
	jobLease     = 5 * time.Minute
)

type store interface {
	LeaseGrammarJob(context.Context, time.Duration) (posts.Job, bool, error)
	CompleteGrammarJob(context.Context, posts.Job, posts.GrammarResult) error
	FailGrammarJob(context.Context, posts.Job, error) error
}

type checker interface {
	CheckGrammar(context.Context, string) (posts.GrammarResult, error)
}

// Run starts the background worker component.
func Run(ctx context.Context, cfg config.Config) error {
	slog.Info("starting worker component", "service", cfg.Service.Name)

	repo, err := posts.Open(cfg.Database.DSN)
	if err != nil {
		return err
	}
	defer closeRepository(repo)

	app := app{
		store:   repo,
		checker: openAIClient{apiKey: cfg.OpenAI.APIKey, model: cfg.OpenAI.Model, httpClient: http.DefaultClient},
	}
	if err := app.processNext(ctx); err != nil {
		slog.Error("worker job failed", "error", err)
	}

	ticker := time.NewTicker(pollInterval)
	defer ticker.Stop()

	for {
		select {
		case <-ctx.Done():
			slog.Info("worker stopped")
			return nil
		case <-ticker.C:
			if err := app.processNext(ctx); err != nil {
				slog.Error("worker job failed", "error", err)
			}
		}
	}
}

type app struct {
	store   store
	checker checker
}

func (app app) processNext(ctx context.Context) error {
	job, ok, err := app.store.LeaseGrammarJob(ctx, jobLease)
	if err != nil {
		return err
	}
	if !ok {
		slog.Debug("no worker jobs available")
		return nil
	}

	result, err := app.checker.CheckGrammar(ctx, job.Source)
	if err != nil {
		if failErr := app.store.FailGrammarJob(ctx, job, err); failErr != nil {
			return fmt.Errorf("grammar check failed: %w; storing failure: %v", err, failErr)
		}
		return err
	}
	if err := app.store.CompleteGrammarJob(ctx, job, result); err != nil {
		return err
	}
	slog.Info("grammar check completed", "job_id", job.ID, "post_id", job.PostID, "revision_id", job.RevisionID)
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
