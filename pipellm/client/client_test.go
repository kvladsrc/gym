package client

import (
	"context"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

	"src/pipellm/config"

	"github.com/google/generative-ai-go/genai"
	"google.golang.org/api/option"
)

// mockGeminiResponse creates a mock Gemini API response JSON.
func mockGeminiResponse(responseText string) string {
	return `{
		"candidates": [{
			"content": {
				"parts": [{"text": "` + responseText + `"}],
				"role": "model"
			}
		}]
	}`
}

// geminiRequest represents the structure of a Gemini API request for validation.
type geminiRequest struct {
	Contents []struct {
		Parts []struct {
			Text string `json:"text"`
		} `json:"parts"`
	} `json:"contents"`
}

// validateGeminiRequest validates that the request body matches expected content.
func validateGeminiRequest(t *testing.T, body []byte, expectedContent string) {
	t.Helper()

	var req geminiRequest
	if err := json.Unmarshal(body, &req); err != nil {
		t.Fatalf("failed to unmarshal request body: %v", err)
	}

	if len(req.Contents) != 1 || len(req.Contents[0].Parts) != 1 {
		t.Fatalf("expected 1 content with 1 part, got %+v", req)
	}

	if got := req.Contents[0].Parts[0].Text; got != expectedContent {
		t.Errorf("request content mismatch:\n  got:  %q\n  want: %q", got, expectedContent)
	}
}

// setupTestServer creates a test HTTP server with the given handler.
func setupTestServer(t *testing.T, handler http.HandlerFunc) *httptest.Server {
	t.Helper()
	server := httptest.NewServer(handler)
	t.Cleanup(server.Close)
	return server
}

// setupTestClient creates a genai client configured to use the test server.
func setupTestClient(t *testing.T, serverURL string) *genai.Client {
	t.Helper()

	ctx := context.Background()
	client, err := genai.NewClient(ctx,
		option.WithAPIKey("test-api-key"), // pragma: allowlist secret
		option.WithEndpoint(serverURL),
	)
	if err != nil {
		t.Fatalf("failed to create test genai client: %v", err)
	}

	t.Cleanup(func() {
		if err := client.Close(); err != nil {
			t.Errorf("failed to close client: %v", err)
		}
	})

	return client
}

func TestNewClient(t *testing.T) {
	// NewClient should succeed with valid parameters even without a real API key.
	// Actual API requests will fail, but client creation should not.
	client, err := NewClient("test-api-key", "gemini-pro", config.GenerationConfig{}) // pragma: allowlist secret
	if err != nil {
		t.Fatalf("NewClient() failed: %v", err)
	}
	if client == nil {
		t.Fatal("NewClient() returned nil client")
	}
}

func TestClient_SendPrompt(t *testing.T) {
	tests := []struct {
		name          string
		prompt        string
		input         string
		mockResponse  string
		wantResponse  string
		wantErr       bool
		wantErrSubstr string
	}{
		{
			name:         "success with input",
			prompt:       "Test prompt",
			input:        "Test input",
			mockResponse: mockGeminiResponse("Test response from AI"),
			wantResponse: "Test response from AI",
		},
		{
			name:         "success without input",
			prompt:       "Test prompt only",
			input:        "",
			mockResponse: mockGeminiResponse("Response to prompt only"),
			wantResponse: "Response to prompt only",
		},
		{
			name:          "no candidates error",
			prompt:        "Test prompt",
			input:         "Test input",
			mockResponse:  `{"candidates": []}`,
			wantErr:       true,
			wantErrSubstr: "no response from Gemini",
		},
		{
			name:         "invalid json error",
			prompt:       "Test prompt",
			input:        "Test input",
			mockResponse: "invalid json response",
			wantErr:      true,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			server := setupTestServer(t, func(w http.ResponseWriter, r *http.Request) {
				// Validate HTTP method.
				if r.Method != http.MethodPost {
					t.Errorf("expected POST request, got %s", r.Method)
				}

				// Validate URL path.
				if !strings.Contains(r.URL.Path, "gemini-pro:generateContent") {
					t.Errorf("expected path to contain 'gemini-pro:generateContent', got %s", r.URL.Path)
				}

				// Validate request body if it's a valid JSON case
				if !tt.wantErr && tt.mockResponse != "invalid json response" {
					body, err := io.ReadAll(r.Body)
					if err != nil {
						t.Fatalf("failed to read request body: %v", err)
					}
					expectedContent := tt.prompt
					if tt.input != "" {
						expectedContent += "\n\n" + tt.input
					}
					validateGeminiRequest(t, body, expectedContent)
				}

				// Send mock response.
				w.Header().Set("Content-Type", "application/json")
				if _, err := w.Write([]byte(tt.mockResponse)); err != nil {
					t.Fatalf("failed to write response: %v", err)
				}
			})

			client := setupTestClient(t, server.URL)
			model := client.GenerativeModel("gemini-pro")
			pipellmClient := &Client{model: model}

			response, err := pipellmClient.SendPrompt(tt.prompt, tt.input)

			if (err != nil) != tt.wantErr {
				t.Errorf("SendPrompt() error = %v, wantErr %v", err, tt.wantErr)
				return
			}

			if tt.wantErr {
				if tt.wantErrSubstr != "" && !strings.Contains(err.Error(), tt.wantErrSubstr) {
					t.Errorf("SendPrompt() error mismatch:\n  got:  %v\n  want substring: %q", err, tt.wantErrSubstr)
				}
				return
			}

			if response != tt.wantResponse {
				t.Errorf("SendPrompt() response mismatch:\n  got:  %q\n  want: %q", response, tt.wantResponse)
			}
		})
	}
}
