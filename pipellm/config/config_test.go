package config

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

// tempHomeDir creates a temporary directory and sets it as HOME for the test.
// Returns a cleanup function that restores the original HOME.
func tempHomeDir(t *testing.T) (tempDir string, cleanup func()) {
	t.Helper()

	tempDir = t.TempDir()
	originalHome := os.Getenv("HOME")

	if err := os.Setenv("HOME", tempDir); err != nil {
		t.Fatalf("failed to set HOME: %v", err)
	}

	cleanup = func() {
		if err := os.Setenv("HOME", originalHome); err != nil {
			t.Errorf("failed to restore HOME: %v", err)
		}
	}

	return tempDir, cleanup
}

// writeConfigFile writes a config file to the given directory.
func writeConfigFile(t *testing.T, dir, content string) string {
	t.Helper()

	configPath := filepath.Join(dir, ".pipellm.yaml")
	if err := os.WriteFile(configPath, []byte(content), 0644); err != nil {
		t.Fatalf("failed to write config file: %v", err)
	}

	return configPath
}

func TestLoadConfig_Success(t *testing.T) {
	const configContent = `api_key: test_api_key_12345
model: gemini-pro
prompts:
- name: test1
  prompt: This is test prompt 1
- name: test2
  prompt: >
    This is a multi-line
    test prompt 2
- name: CaseSensitive
  prompt: Case test prompt
`

	tempDir, cleanup := tempHomeDir(t)
	defer cleanup()

	writeConfigFile(t, tempDir, configContent)

	config, err := LoadConfig()
	if err != nil {
		t.Fatalf("LoadConfig() failed: %v", err)
	}

	// Verify API key.
	wantAPIKey := "test_api_key_12345" // pragma: allowlist secret
	if config.APIKey != wantAPIKey {   // pragma: allowlist secret
		t.Errorf("APIKey mismatch:\n  got:  %q\n  want: %q", config.APIKey, wantAPIKey)
	}

	// Verify model.
	wantModel := "gemini-pro"
	if config.Model != wantModel {
		t.Errorf("Model mismatch:\n  got:  %q\n  want: %q", config.Model, wantModel)
	}

	// Verify number of prompts.
	wantPromptCount := 3
	if got := len(config.Prompts); got != wantPromptCount {
		t.Fatalf("prompt count mismatch: got %d, want %d", got, wantPromptCount)
	}

	// Verify first prompt.
	if got, want := config.Prompts[0].Name, "test1"; got != want {
		t.Errorf("Prompts[0].Name = %q, want %q", got, want)
	}
	if got, want := config.Prompts[0].Prompt, "This is test prompt 1"; got != want {
		t.Errorf("Prompts[0].Prompt = %q, want %q", got, want)
	}

	// Verify multi-line prompt (YAML > operator folds lines with spaces).
	wantMultiLine := "This is a multi-line test prompt 2"
	if got := strings.TrimSpace(config.Prompts[1].Prompt); got != wantMultiLine {
		t.Errorf("Prompts[1].Prompt mismatch:\n  got:  %q\n  want: %q", got, wantMultiLine)
	}
}

func TestLoadConfig_FileNotFound(t *testing.T) {
	_, cleanup := tempHomeDir(t)
	defer cleanup()

	// Don't create a config file.
	_, err := LoadConfig()
	if err == nil {
		t.Fatal("LoadConfig() succeeded with missing config file, want error")
	}

	wantErrSubstring := "config file not found"
	if !strings.Contains(err.Error(), wantErrSubstring) {
		t.Errorf("error message mismatch:\n  got:  %v\n  want substring: %q", err, wantErrSubstring)
	}
}

func TestLoadConfig_InvalidYAML(t *testing.T) {
	const invalidYAML = `api_key: test_key
prompts:
  - name: test
    prompt: valid
  invalid_yaml_structure: [unclosed array
`

	tempDir, cleanup := tempHomeDir(t)
	defer cleanup()

	writeConfigFile(t, tempDir, invalidYAML)

	_, err := LoadConfig()
	if err == nil {
		t.Fatal("LoadConfig() succeeded with invalid YAML, want error")
	}

	wantErrSubstring := "failed to parse config"
	if !strings.Contains(err.Error(), wantErrSubstring) {
		t.Errorf("error message mismatch:\n  got:  %v\n  want substring: %q", err, wantErrSubstring)
	}
}

func TestLoadConfig_WithoutModel(t *testing.T) {
	// Test backwards compatibility: config without model field.
	const configContent = `api_key: test_api_key_old
prompts:
- name: test
  prompt: Test prompt
`

	tempDir, cleanup := tempHomeDir(t)
	defer cleanup()

	writeConfigFile(t, tempDir, configContent)

	config, err := LoadConfig()
	if err != nil {
		t.Fatalf("LoadConfig() failed: %v", err)
	}

	// Model should be empty when not specified.
	if config.Model != "" {
		t.Errorf("Model = %q, want empty string", config.Model)
	}
}

func TestConfig_FindPrompt(t *testing.T) {
	config := &Config{
		APIKey: "test_key",
		Model:  "gemini-pro",
		Prompts: []Prompt{
			{Name: "test1", Prompt: "First test prompt"},
			{Name: "Test2", Prompt: "Second test prompt"},
			{Name: "CAPS", Prompt: "Caps test prompt"},
			{Name: "  spaced  ", Prompt: "Spaced test prompt"},
		},
	}

	tests := []struct {
		name   string
		search string
		want   string
	}{
		{
			name:   "exact match lowercase",
			search: "test1",
			want:   "First test prompt",
		},
		{
			name:   "case insensitive match",
			search: "test2",
			want:   "Second test prompt",
		},
		{
			name:   "case insensitive uppercase",
			search: "caps",
			want:   "Caps test prompt",
		},
		{
			name:   "match with trimmed spaces",
			search: "spaced",
			want:   "Spaced test prompt",
		},
		{
			name:   "not found returns empty",
			search: "nonexistent",
			want:   "",
		},
		{
			name:   "empty search returns empty",
			search: "",
			want:   "",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := config.FindPrompt(tt.search)
			if got != tt.want {
				t.Errorf("FindPrompt(%q) = %q, want %q", tt.search, got, tt.want)
			}
		})
	}
}
