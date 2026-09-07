package llm

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"strings"
	"text/template"

	"github.com/google/generative-ai-go/genai"
	"google.golang.org/api/option"
)

// Client is a wrapper around the Gemini Generative AI client.
type Client struct {
	client *genai.Client
	model  *genai.GenerativeModel
}

// NewClient creates a new Gemini client with the given API key and model name.
func NewClient(ctx context.Context, apiKey, modelName string) (*Client, error) {
	if apiKey == "" {
		return nil, fmt.Errorf("API key is required")
	}

	client, err := genai.NewClient(ctx, option.WithAPIKey(apiKey))
	if err != nil {
		return nil, err
	}

	if modelName == "" {
		modelName = "gemini-1.5-flash"
	}

	return &Client{
		client: client,
		model:  client.GenerativeModel(modelName),
	}, nil
}

// Close closes the underlying Gemini client.
func (c *Client) Close() {
	_ = c.client.Close()
}

// GenerateLocationActions generates a description and options for a location.
// GenerateLocationActions generates a description and options for a location.
func (c *Client) GenerateLocationActions(ctx context.Context, data interface{}) (*LocationResponse, error) {
	prompt, err := executeTemplate(LocationPromptTemplate, data)
	if err != nil {
		return nil, err
	}

	resp, err := c.model.GenerateContent(ctx, genai.Text(prompt))
	if err != nil {
		return nil, err
	}

	if len(resp.Candidates) == 0 || len(resp.Candidates[0].Content.Parts) == 0 {
		return nil, fmt.Errorf("no content generated")
	}

	part, ok := resp.Candidates[0].Content.Parts[0].(genai.Text)
	if !ok {
		return nil, fmt.Errorf("unexpected content type")
	}

	var result LocationResponse
	if err := json.Unmarshal([]byte(cleanJSON(string(part))), &result); err != nil {
		return nil, err
	}

	return &result, nil
}

// GenerateNPCResponse generates a response and options for an NPC interaction.
func (c *Client) GenerateNPCResponse(ctx context.Context, data interface{}) (*NPCResponse, error) {
	prompt, err := executeTemplate(NPCPromptTemplate, data)
	if err != nil {
		return nil, err
	}

	resp, err := c.model.GenerateContent(ctx, genai.Text(prompt))
	if err != nil {
		return nil, err
	}

	if len(resp.Candidates) == 0 || len(resp.Candidates[0].Content.Parts) == 0 {
		return nil, fmt.Errorf("no content generated")
	}

	part, ok := resp.Candidates[0].Content.Parts[0].(genai.Text)
	if !ok {
		return nil, fmt.Errorf("unexpected content type")
	}

	var result NPCResponse
	if err := json.Unmarshal([]byte(cleanJSON(string(part))), &result); err != nil {
		return nil, err
	}

	return &result, nil
}

// SummarizeJournal generates a summary of the journal entries.
func (c *Client) SummarizeJournal(ctx context.Context, data interface{}) (*SummaryResponse, error) {
	prompt, err := executeTemplate(SummaryPromptTemplate, data)
	if err != nil {
		return nil, err
	}

	resp, err := c.model.GenerateContent(ctx, genai.Text(prompt))
	if err != nil {
		return nil, err
	}

	if len(resp.Candidates) == 0 || len(resp.Candidates[0].Content.Parts) == 0 {
		return nil, fmt.Errorf("no content generated")
	}

	part, ok := resp.Candidates[0].Content.Parts[0].(genai.Text)
	if !ok {
		return nil, fmt.Errorf("unexpected content type")
	}

	var result SummaryResponse
	if err := json.Unmarshal([]byte(cleanJSON(string(part))), &result); err != nil {
		return nil, err
	}

	return &result, nil
}

func executeTemplate(tmplStr string, data interface{}) (string, error) {
	tmpl, err := template.New("prompt").Parse(tmplStr)
	if err != nil {
		return "", err
	}
	var buf bytes.Buffer
	if err := tmpl.Execute(&buf, data); err != nil {
		return "", err
	}
	return buf.String(), nil
}

func cleanJSON(input string) string {
	input = strings.TrimSpace(input)
	input = strings.TrimPrefix(input, "```json")
	input = strings.TrimPrefix(input, "```")
	input = strings.TrimSuffix(input, "```")
	return strings.TrimSpace(input)
}
