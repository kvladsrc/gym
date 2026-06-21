package migrate

import (
	"context"
	"fmt"
	"log/slog"

	"src/production/docker/blog-engine/internal/config"
	"src/production/docker/blog-engine/internal/posts"
)

// Run applies database migrations.
func Run(ctx context.Context, cfg config.Config) error {
	repo, err := posts.Open(cfg.Database.DSN)
	if err != nil {
		return err
	}
	defer func() {
		if err := repo.Close(); err != nil {
			slog.Error("failed to close posts repository", "error", err)
		}
	}()

	if err := repo.Migrate(ctx); err != nil {
		return fmt.Errorf("migrate posts: %w", err)
	}
	slog.Info("database migrations applied")
	return nil
}
