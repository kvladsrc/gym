package config

import (
	"os"
	"strings"
	"testing"
)

func TestLoad(t *testing.T) {
	file, err := os.CreateTemp(t.TempDir(), "config-*.yaml")
	if err != nil {
		t.Fatalf("CreateTemp() returned error: %v", err)
	}
	if _, err := file.WriteString(validConfigYAML); err != nil {
		t.Fatalf("WriteString() returned error: %v", err)
	}
	if err := file.Close(); err != nil {
		t.Fatalf("Close() returned error: %v", err)
	}

	path := file.Name()
	cfg, err := Load(path)
	if err != nil {
		t.Fatalf("Load(%q) returned error: %v", path, err)
	}

	if cfg.Service.Name != "blog-engine" {
		t.Fatalf("service name = %q, want blog-engine", cfg.Service.Name)
	}
	if cfg.Publishing.StateKey != "state.json" {
		t.Fatalf("state key = %q, want state.json", cfg.Publishing.StateKey)
	}
	if cfg.Worker.PollInterval != "45s" {
		t.Fatalf("worker poll interval = %q, want 45s", cfg.Worker.PollInterval)
	}
}

const validConfigYAML = `---
service:
  name: blog-engine
http:
  author_addr: "127.0.0.1:8081"
  web_addr: "127.0.0.1:8080"
database:
  dsn: "postgres://example"
s3:
  endpoint: "http://minio:9000"
  region: "us-east-1"
  access_key: "access"
  secret_key: "secret"
  author_bucket: "blog-author"
  public_bucket: "blog-public"
openai:
  api_key: "api-key"
  model: "gpt-5.4-nano"
publishing:
  base_url: "https://blog.example"
  state_key: "state.json"
worker:
  poll_interval: "45s"
`

func TestValidateRequiredFields(t *testing.T) {
	tests := []struct {
		name    string
		mutate  func(*Config)
		wantErr string
	}{
		{
			name: "valid config",
		},
		{
			name: "requires author addr",
			mutate: func(cfg *Config) {
				cfg.HTTP.AuthorAddr = ""
			},
			wantErr: "http.author_addr is required",
		},
		{
			name: "requires web addr",
			mutate: func(cfg *Config) {
				cfg.HTTP.WebAddr = ""
			},
			wantErr: "http.web_addr is required",
		},
		{
			name: "requires public bucket",
			mutate: func(cfg *Config) {
				cfg.S3.PublicBucket = ""
			},
			wantErr: "s3.public_bucket is required",
		},
		{
			name: "requires state key",
			mutate: func(cfg *Config) {
				cfg.Publishing.StateKey = ""
			},
			wantErr: "publishing.state_key is required",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			cfg := validConfig()
			if tt.mutate != nil {
				tt.mutate(&cfg)
			}

			err := cfg.Validate()
			if tt.wantErr == "" {
				if err != nil {
					t.Fatalf("Validate() returned error: %v", err)
				}
				return
			}
			if err == nil {
				t.Fatalf("Validate() returned nil, want error containing %q", tt.wantErr)
			}
			if !strings.Contains(err.Error(), tt.wantErr) {
				t.Fatalf("error = %q, want substring %q", err, tt.wantErr)
			}
		})
	}
}

func validConfig() Config {
	return Config{
		Service: ServiceConfig{Name: "blog-engine"},
		HTTP: HTTPConfig{
			AuthorAddr: "127.0.0.1:8081",
			WebAddr:    "127.0.0.1:8080",
		},
		Database: DatabaseConfig{DSN: "postgres://example"},
		S3: S3Config{
			Endpoint:     "http://minio:9000",
			Region:       "us-east-1",
			AccessKey:    "access",
			SecretKey:    "secret",
			AuthorBucket: "blog-author",
			PublicBucket: "blog-public",
		},
		OpenAI: OpenAIConfig{
			APIKey: "api-key",
			Model:  "gpt-5.4-nano",
		},
		Publishing: PublishingConfig{
			BaseURL:  "https://blog.example",
			StateKey: "state.json",
		},
		Worker: WorkerConfig{PollInterval: "45s"},
	}
}
