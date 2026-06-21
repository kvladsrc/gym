package main

import (
	"os"
	"strings"
	"testing"
)

func TestRun(t *testing.T) {
	configPath := writeConfig(t)

	tests := []struct {
		name    string
		args    []string
		wantErr string
	}{
		{
			name:    "requires command",
			args:    nil,
			wantErr: "usage: blog-engine",
		},
		{
			name:    "requires config",
			args:    []string{"migrate"},
			wantErr: "--config is required",
		},
		{
			name:    "rejects unknown command",
			args:    []string{"unknown", "--config", configPath},
			wantErr: "usage: blog-engine",
		},
		{
			name:    "migrate reports database errors",
			args:    []string{"migrate", "--config", configPath},
			wantErr: "ping database",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			err := run(tt.args)
			if tt.wantErr == "" {
				if err != nil {
					t.Fatalf("run(%v) returned error: %v", tt.args, err)
				}
				return
			}
			if err == nil {
				t.Fatalf("run(%v) returned nil, want error containing %q", tt.args, tt.wantErr)
			}
			if !strings.Contains(err.Error(), tt.wantErr) {
				t.Fatalf("error = %q, want substring %q", err, tt.wantErr)
			}
		})
	}
}

func writeConfig(t *testing.T) string {
	t.Helper()

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
	return file.Name()
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
  model: "gpt-4.1-mini"
publishing:
  base_url: "https://blog.example"
  release_prefix: "releases/"
  current_pointer_key: "current.json"
`
