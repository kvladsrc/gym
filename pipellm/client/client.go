package client

import (
	"context"
	"fmt"

	"github.com/google/generative-ai-go/genai"
	"google.golang.org/api/option"
)

// Client wraps a Gemini generative model for making API requests.
type Client struct {
	model *genai.GenerativeModel
}

// NewClient creates a new Gemini API client with the specified API key and model name.
// Returns an error if the client cannot be initialized.
func NewClient(apiKey, modelName string) (*Client, error) {
	ctx := context.Background()
	client, err := genai.NewClient(ctx, option.WithAPIKey(apiKey))
	if err != nil {
		return nil, fmt.Errorf("failed to create Gemini client: %w", err)
	}

	model := client.GenerativeModel(modelName)
	return &Client{
		model: model,
	}, nil
}

// SendPrompt sends a prompt to the Gemini API and returns the response.
// If input is provided, it is appended to the prompt with a blank line separator.
func (c *Client) SendPrompt(prompt, input string) (string, error) {
	fullPrompt := prompt
	if input != "" {
		fullPrompt = prompt + "\n\n" + input
	}

	ctx := context.Background()
	resp, err := c.model.GenerateContent(ctx, genai.Text(fullPrompt))
	if err != nil {
		return "", err
	}

	if len(resp.Candidates) == 0 || resp.Candidates[0].Content == nil || len(resp.Candidates[0].Content.Parts) == 0 {
		return "", fmt.Errorf("no response from Gemini")
	}

	var result string
	for _, part := range resp.Candidates[0].Content.Parts {
		if txt, ok := part.(genai.Text); ok {
			result += string(txt)
		}
	}

	return result, nil
}
