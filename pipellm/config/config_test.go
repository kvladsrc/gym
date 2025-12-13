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

func TestLoadConfig(t *testing.T) {
	tests := []struct {
		name             string
		configContent    string
		noConfigFile     bool
		wantAPIKey       string
		wantModel        string
		wantPromptCount  int
		wantFirstPrompt  Prompt
		wantSecondPrompt Prompt
		wantErr          bool
		wantErrSubstr    string
	}{
		{
			name: "success",
			configContent: `api_key: test_api_key_12345
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
`,
			wantAPIKey:       "test_api_key_12345", // pragma: allowlist secret
			wantModel:        "gemini-pro",
			wantPromptCount:  3,
			wantFirstPrompt:  Prompt{Name: "test1", Prompt: "This is test prompt 1"},
			wantSecondPrompt: Prompt{Name: "test2", Prompt: "This is a multi-line test prompt 2"},
		},
		{
			name:          "file not found",
			noConfigFile:  true,
			wantErr:       true,
			wantErrSubstr: "config file not found",
		},
		{
			name: "invalid yaml",
			configContent: `api_key: test_key
prompts:
  - name: test
    prompt: valid
  invalid_yaml_structure: [unclosed array
`,
			wantErr:       true,
			wantErrSubstr: "failed to parse config",
		},
		{
			name: "without model",
			configContent: `api_key: test_api_key_old
prompts:
- name: test
  prompt: Test prompt
`,
			wantAPIKey:      "test_api_key_old", // pragma: allowlist secret
			wantModel:       "",
			wantPromptCount: 1,
			wantFirstPrompt: Prompt{Name: "test", Prompt: "Test prompt"},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			tempDir, cleanup := tempHomeDir(t)
			defer cleanup()

			if !tt.noConfigFile {
				writeConfigFile(t, tempDir, tt.configContent)
			}

			config, err := LoadConfig()

			if (err != nil) != tt.wantErr {
				t.Errorf("LoadConfig() error = %v, wantErr %v", err, tt.wantErr)
				return
			}

			if tt.wantErr {
				if tt.wantErrSubstr != "" && !strings.Contains(err.Error(), tt.wantErrSubstr) {
					t.Errorf("LoadConfig() error mismatch:\n  got:  %v\n  want substring: %q", err, tt.wantErrSubstr)
				}
				return
			}

			if config.APIKey != tt.wantAPIKey { // pragma: allowlist secret
				t.Errorf("APIKey = %q, want %q", config.APIKey, tt.wantAPIKey)
			}

			if config.Model != tt.wantModel {
				t.Errorf("Model = %q, want %q", config.Model, tt.wantModel)
			}

			if len(config.Prompts) != tt.wantPromptCount {
				t.Fatalf("prompt count = %d, want %d", len(config.Prompts), tt.wantPromptCount)
			}

			if tt.wantPromptCount > 0 {
				if config.Prompts[0].Name != tt.wantFirstPrompt.Name {
					t.Errorf("Prompts[0].Name = %q, want %q", config.Prompts[0].Name, tt.wantFirstPrompt.Name)
				}
				if config.Prompts[0].Prompt != tt.wantFirstPrompt.Prompt {
					t.Errorf("Prompts[0].Prompt = %q, want %q", config.Prompts[0].Prompt, tt.wantFirstPrompt.Prompt)
				}
			}

			if tt.wantPromptCount > 1 {
				// Check second prompt for multi-line test
				if strings.TrimSpace(config.Prompts[1].Prompt) != tt.wantSecondPrompt.Prompt {
					t.Errorf("Prompts[1].Prompt = %q, want %q", strings.TrimSpace(config.Prompts[1].Prompt), tt.wantSecondPrompt.Prompt)
				}
			}
		})
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
