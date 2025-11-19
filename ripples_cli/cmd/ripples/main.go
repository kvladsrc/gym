package main

import (
	"context"
	"flag"
	"fmt"
	"os"

	"src/ripples_cli/pkg/engine"
	"src/ripples_cli/pkg/llm"
	"src/ripples_cli/pkg/model"
	"src/ripples_cli/pkg/tui"
	"src/ripples_cli/pkg/world"

	tea "github.com/charmbracelet/bubbletea"
)

func main() {
	// Define Flags
	worldPath := flag.String("world", "ripples_cli/assets/world.dot", "Path to the world DOT file")
	apiKey := flag.String("key", "", "Gemini API Key (mandatory)")
	modelName := flag.String("model", "gemini-1.5-flash", "LLM Model Name")
	startNodeFlag := flag.String("start", "", "Start Node ID (optional)")
	journalLimit := flag.Int("journal-limit", 5, "Max recent journal entries before summarization")
	flag.Parse()

	// Validate Flags
	if *apiKey == "" {
		// Fallback to Env Var
		*apiKey = os.Getenv("GEMINI_API_KEY")
	}
	if *apiKey == "" {
		fmt.Println("Error: API Key is required. Use -key flag or GEMINI_API_KEY env var.")
		flag.Usage()
		os.Exit(1)
	}
	if *modelName == "" {
		fmt.Println("Error: Model name is required.")
		flag.Usage()
		os.Exit(1)
	}

	ctx := context.Background()

	// 1. Load World
	nodes, err := world.LoadWorld(*worldPath)
	if err != nil {
		fmt.Printf("Error loading world: %v\n", err)
		os.Exit(1)
	}

	if err := run(ctx, nodes, *apiKey, *modelName, *startNodeFlag, *journalLimit); err != nil {
		fmt.Printf("Error: %v\n", err)
		os.Exit(1)
	}
}

func run(ctx context.Context, nodes map[string]*model.Node, apiKey, modelName, startNodeFlag string, journalLimit int) error {
	// 2. Initialize LLM
	client, err := llm.NewClient(ctx, apiKey, modelName)
	if err != nil {
		return fmt.Errorf("error initializing LLM: %w", err)
	}
	defer client.Close()
	// 3. Initialize Engine
	// Determine start node
	startNodeID := startNodeFlag
	if startNodeID == "" {
		// Pick the first location found
		// To be deterministic, we might want to sort keys, but map iteration is random.
		// Let's just pick *a* location.
		for id, node := range nodes {
			if node.Type == model.LocationType {
				startNodeID = id
				break
			}
		}
	}

	if startNodeID == "" {
		return fmt.Errorf("no start node specified and no locations found in world")
	}

	eng, err := engine.NewEngine(nodes, startNodeID, client, journalLimit)
	if err != nil {
		return fmt.Errorf("failed to create engine: %w", err)
	}

	// 4. Start TUI
	p := tea.NewProgram(tui.NewModel(eng))
	if _, err := p.Run(); err != nil {
		return fmt.Errorf("error running TUI: %w", err)
	}
	return nil
}
