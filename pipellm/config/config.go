package config

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"gopkg.in/yaml.v3"
)

// Config represents the pipellm configuration loaded from ~/.pipellm.yaml.
type Config struct {
	APIKey           string           `yaml:"api_key"`
	Model            string           `yaml:"model"`
	GenerationConfig GenerationConfig `yaml:"generation_config"`
	Prompts          []Prompt         `yaml:"prompts"`
}

// GenerationConfig holds model generation parameters.
type GenerationConfig struct {
	Temperature     *float32 `yaml:"temperature,omitempty"`
	TopP            *float32 `yaml:"top_p,omitempty"`
	TopK            *int32   `yaml:"top_k,omitempty"`
	MaxOutputTokens *int32   `yaml:"max_output_tokens,omitempty"`
}

// Prompt represents a named prompt template in the configuration.
type Prompt struct {
	Name   string `yaml:"name"`
	Prompt string `yaml:"prompt"`
}

// LoadConfig loads the pipellm configuration from ~/.pipellm.yaml.
// Returns an error if the file cannot be read or parsed.
func LoadConfig() (*Config, error) {
	homeDir, err := os.UserHomeDir()
	if err != nil {
		return nil, err
	}

	configPath := filepath.Join(homeDir, ".pipellm.yaml")
	data, err := os.ReadFile(configPath)
	if err != nil {
		return nil, fmt.Errorf("config file not found at %s", configPath)
	}

	var config Config
	if err := yaml.Unmarshal(data, &config); err != nil {
		return nil, fmt.Errorf("failed to parse config: %v", err)
	}

	return &config, nil
}

// FindPrompt searches for a prompt by name (case-insensitive, trimmed).
// Returns the prompt text if found, or an empty string if not found.
func (c *Config) FindPrompt(name string) string {
	for _, p := range c.Prompts {
		if strings.EqualFold(strings.TrimSpace(p.Name), strings.TrimSpace(name)) {
			return p.Prompt
		}
	}
	return ""
}
