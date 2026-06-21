package config

import (
	"fmt"
	"os"

	"gopkg.in/yaml.v3"
)

// Config contains settings shared by all blog engine commands.
type Config struct {
	Service    ServiceConfig    `yaml:"service"`
	HTTP       HTTPConfig       `yaml:"http"`
	Database   DatabaseConfig   `yaml:"database"`
	S3         S3Config         `yaml:"s3"`
	OpenAI     OpenAIConfig     `yaml:"openai"`
	Publishing PublishingConfig `yaml:"publishing"`
}

// ServiceConfig identifies this blog engine deployment.
type ServiceConfig struct {
	Name string `yaml:"name"`
}

// HTTPConfig contains listen addresses for HTTP components.
type HTTPConfig struct {
	AuthorAddr string `yaml:"author_addr"`
	WebAddr    string `yaml:"web_addr"`
}

// DatabaseConfig contains PostgreSQL connection settings.
type DatabaseConfig struct {
	DSN string `yaml:"dsn"`
}

// S3Config contains object storage settings for author and public content.
type S3Config struct {
	Endpoint       string `yaml:"endpoint"`
	Region         string `yaml:"region"`
	AccessKey      string `yaml:"access_key"`
	SecretKey      string `yaml:"secret_key"`
	AuthorBucket   string `yaml:"author_bucket"`
	PublicBucket   string `yaml:"public_bucket"`
	ForcePathStyle bool   `yaml:"force_path_style"`
}

// OpenAIConfig contains settings for OpenAI-backed checks.
type OpenAIConfig struct {
	APIKey string `yaml:"api_key"`
	Model  string `yaml:"model"`
}

// PublishingConfig contains static publication naming settings.
type PublishingConfig struct {
	BaseURL           string `yaml:"base_url"`
	ReleasePrefix     string `yaml:"release_prefix"`
	CurrentPointerKey string `yaml:"current_pointer_key"`
}

// Load reads, parses, and validates a YAML config file.
func Load(path string) (Config, error) {
	raw, err := os.ReadFile(path)
	if err != nil {
		return Config{}, fmt.Errorf("read config %q: %w", path, err)
	}

	var cfg Config
	if err := yaml.Unmarshal(raw, &cfg); err != nil {
		return Config{}, fmt.Errorf("parse config %q: %w", path, err)
	}
	if err := cfg.Validate(); err != nil {
		return Config{}, fmt.Errorf("validate config %q: %w", path, err)
	}
	return cfg, nil
}

// Validate checks that required config fields are present.
func (cfg Config) Validate() error {
	required := map[string]string{
		"service.name":                   cfg.Service.Name,
		"http.author_addr":               cfg.HTTP.AuthorAddr,
		"http.web_addr":                  cfg.HTTP.WebAddr,
		"database.dsn":                   cfg.Database.DSN,
		"s3.endpoint":                    cfg.S3.Endpoint,
		"s3.region":                      cfg.S3.Region,
		"s3.access_key":                  cfg.S3.AccessKey,
		"s3.secret_key":                  cfg.S3.SecretKey,
		"s3.author_bucket":               cfg.S3.AuthorBucket,
		"s3.public_bucket":               cfg.S3.PublicBucket,
		"openai.api_key":                 cfg.OpenAI.APIKey,
		"openai.model":                   cfg.OpenAI.Model,
		"publishing.base_url":            cfg.Publishing.BaseURL,
		"publishing.release_prefix":      cfg.Publishing.ReleasePrefix,
		"publishing.current_pointer_key": cfg.Publishing.CurrentPointerKey,
	}
	for field, value := range required {
		if value == "" {
			return fmt.Errorf("%s is required", field)
		}
	}
	return nil
}
