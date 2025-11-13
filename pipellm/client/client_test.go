package client

import (
	"context"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"

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
	client, err := NewClient("test-api-key", "gemini-pro") // pragma: allowlist secret
	if err != nil {
		t.Fatalf("NewClient() failed: %v", err)
	}
	if client == nil {
		t.Fatal("NewClient() returned nil client")
	}
}

func TestClient_SendPrompt(t *testing.T) {
	const (
		testPrompt   = "Test prompt"
		testInput    = "Test input"
		testResponse = "Test response from AI"
	)

	server := setupTestServer(t, func(w http.ResponseWriter, r *http.Request) {
		// Validate HTTP method.
		if r.Method != http.MethodPost {
			t.Errorf("expected POST request, got %s", r.Method)
		}

		// Validate URL path.
		if !strings.Contains(r.URL.Path, "gemini-pro:generateContent") {
			t.Errorf("expected path to contain 'gemini-pro:generateContent', got %s", r.URL.Path)
		}

		// Validate request body.
		body, err := io.ReadAll(r.Body)
		if err != nil {
			t.Fatalf("failed to read request body: %v", err)
		}
		expectedContent := testPrompt + "\n\n" + testInput
		validateGeminiRequest(t, body, expectedContent)

		// Send mock response.
		w.Header().Set("Content-Type", "application/json")
		if _, err := w.Write([]byte(mockGeminiResponse(testResponse))); err != nil {
			t.Fatalf("failed to write response: %v", err)
		}
	})

	client := setupTestClient(t, server.URL)
	model := client.GenerativeModel("gemini-pro")
	pipellmClient := &Client{model: model}

	response, err := pipellmClient.SendPrompt(testPrompt, testInput)
	if err != nil {
		t.Fatalf("SendPrompt() failed: %v", err)
	}

	if response != testResponse {
		t.Errorf("SendPrompt() response mismatch:\n  got:  %q\n  want: %q", response, testResponse)
	}
}

func TestClient_SendPrompt_NoInput(t *testing.T) {
	const (
		testPrompt   = "Test prompt only"
		testResponse = "Response to prompt only"
	)

	server := setupTestServer(t, func(w http.ResponseWriter, r *http.Request) {
		body, err := io.ReadAll(r.Body)
		if err != nil {
			t.Fatalf("failed to read request body: %v", err)
		}
		validateGeminiRequest(t, body, testPrompt)

		w.Header().Set("Content-Type", "application/json")
		if _, err := w.Write([]byte(mockGeminiResponse(testResponse))); err != nil {
			t.Fatalf("failed to write response: %v", err)
		}
	})

	client := setupTestClient(t, server.URL)
	model := client.GenerativeModel("gemini-pro")
	pipellmClient := &Client{model: model}

	response, err := pipellmClient.SendPrompt(testPrompt, "")
	if err != nil {
		t.Fatalf("SendPrompt() with empty input failed: %v", err)
	}

	if response != testResponse {
		t.Errorf("SendPrompt() response mismatch:\n  got:  %q\n  want: %q", response, testResponse)
	}
}

func TestClient_SendPrompt_NoCandidates(t *testing.T) {
	server := setupTestServer(t, func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		if _, err := w.Write([]byte(`{"candidates": []}`)); err != nil {
			t.Fatalf("failed to write response: %v", err)
		}
	})

	client := setupTestClient(t, server.URL)
	model := client.GenerativeModel("gemini-pro")
	pipellmClient := &Client{model: model}

	_, err := pipellmClient.SendPrompt("Test prompt", "Test input")
	if err == nil {
		t.Fatal("SendPrompt() succeeded with empty candidates, want error")
	}

	wantErrSubstring := "no response from Gemini"
	if !strings.Contains(err.Error(), wantErrSubstring) {
		t.Errorf("SendPrompt() error mismatch:\n  got:  %v\n  want substring: %q", err, wantErrSubstring)
	}
}

func TestClient_SendPrompt_InvalidJSON(t *testing.T) {
	server := setupTestServer(t, func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		if _, err := w.Write([]byte("invalid json response")); err != nil {
			t.Fatalf("failed to write response: %v", err)
		}
	})

	client := setupTestClient(t, server.URL)
	model := client.GenerativeModel("gemini-pro")
	pipellmClient := &Client{model: model}

	_, err := pipellmClient.SendPrompt("Test prompt", "Test input")
	if err == nil {
		t.Fatal("SendPrompt() succeeded with invalid JSON, want error")
	}
}
