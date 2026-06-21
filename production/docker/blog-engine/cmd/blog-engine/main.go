package main

import (
	"context"
	"flag"
	"fmt"
	"log/slog"
	"os"
	"os/signal"
	"syscall"

	"src/production/docker/blog-engine/internal/author"
	"src/production/docker/blog-engine/internal/config"
	"src/production/docker/blog-engine/internal/migrate"
	"src/production/docker/blog-engine/internal/web"
	"src/production/docker/blog-engine/internal/worker"
)

func main() {
	if err := run(os.Args[1:]); err != nil {
		slog.Error("blog-engine failed", "error", err)
		os.Exit(1)
	}
}

func run(args []string) error {
	if len(args) == 0 {
		return usageError()
	}

	cmd := args[0]
	fs := flag.NewFlagSet(cmd, flag.ContinueOnError)
	configPath := fs.String("config", "", "path to YAML config file")
	if err := fs.Parse(args[1:]); err != nil {
		return err
	}
	if *configPath == "" {
		return fmt.Errorf("%s: --config is required", cmd)
	}

	cfg, err := config.Load(*configPath)
	if err != nil {
		return err
	}

	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt, syscall.SIGTERM)
	defer stop()

	switch cmd {
	case "author":
		return author.Run(ctx, cfg)
	case "worker":
		return worker.Run(ctx, cfg)
	case "web":
		return web.Run(ctx, cfg)
	case "migrate":
		return migrate.Run(ctx, cfg)
	default:
		return usageError()
	}
}

func usageError() error {
	return fmt.Errorf("usage: blog-engine <author|worker|web|migrate> --config=/path/to/config.yaml")
}
