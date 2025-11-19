package engine

import (
	"context"
	"fmt"
	"strings"

	"src/ripples_cli/pkg/llm"
	"src/ripples_cli/pkg/model"
)

// GameOption represents a choice available to the player.
type GameOption struct {
	Text       string
	Event      *string
	NextNodeID string // If empty, stays in current node
}

// Engine manages the game state and interaction loop.
type Engine struct {
	WorldNodes map[string]*model.Node
	State      *model.WorldState
	LLM        *llm.Client

	// Configuration
	JournalLimit int

	// Current options available to the player
	CurrentOptions []GameOption
	// Last text displayed (Location description or NPC response)
	LastText string
}

// NewEngine creates a new game engine with the given world nodes, start node, and LLM client.
func NewEngine(nodes map[string]*model.Node, startNodeID string, llmClient *llm.Client, journalLimit int) (*Engine, error) {
	// Verify start node exists
	if _, ok := nodes[startNodeID]; !ok {
		return nil, fmt.Errorf("start node %s not found", startNodeID)
	}

	return &Engine{
		WorldNodes:   nodes,
		State:        model.NewWorldState(startNodeID),
		LLM:          llmClient,
		JournalLimit: journalLimit,
	}, nil
}

// Start begins the game loop.
func (e *Engine) Start(ctx context.Context) error {
	return e.processCurrentNode(ctx, "")
}

// SelectOption handles the player's choice of an option.
func (e *Engine) SelectOption(ctx context.Context, index int) error {
	if index < 0 || index >= len(e.CurrentOptions) {
		return fmt.Errorf("invalid option index")
	}

	option := e.CurrentOptions[index]

	// Update Journal
	if err := e.updateJournal(ctx, option); err != nil {
		// Log error but continue? Or return?
		// For now, let's just print to stderr if possible or ignore.
		// Since we can't easily log to TUI from here without returning error.
		// Let's return error for now.
		return err
	}

	// Transition
	if option.NextNodeID != "" {
		e.State.CurrentNodeID = option.NextNodeID
		// If transitioning FROM NPC to Location, we might want to log "End dialog"
		// But for now, just proceed.
	}

	return e.processCurrentNode(ctx, option.Text)
}

func (e *Engine) updateJournal(ctx context.Context, opt GameOption) error {
	// Add to local journal of current node
	currentNode := e.WorldNodes[e.State.CurrentNodeID]
	localJournal := e.State.GetNodeJournal(currentNode.ID)

	// Log the player's action text
	localJournal.RecentEntries = append(localJournal.RecentEntries, model.JournalEntry{
		GameDay: e.State.GameDay,
		Text:    fmt.Sprintf("Player: %s", opt.Text),
	})

	// Check for summarization (Local)
	if len(localJournal.RecentEntries) > e.JournalLimit {
		if err := e.summarizeLocalJournal(ctx, currentNode.ID); err != nil {
			return err
		}
	}

	// If there is an event, add to global journal
	if opt.Event != nil && *opt.Event != "" {
		e.State.GlobalJournal.RecentEntries = append(e.State.GlobalJournal.RecentEntries, model.JournalEntry{
			GameDay: e.State.GameDay,
			Text:    *opt.Event,
		})

		// Check for summarization (Global)
		if len(e.State.GlobalJournal.RecentEntries) > e.JournalLimit {
			if err := e.summarizeGlobalJournal(ctx); err != nil {
				return err
			}
		}
	}
	return nil
}

func (e *Engine) summarizeLocalJournal(ctx context.Context, nodeID string) error {
	journal := e.State.GetNodeJournal(nodeID)
	data := struct {
		OldEntries     string
		CurrentSummary string
	}{
		OldEntries:     formatJournal(journal.RecentEntries),
		CurrentSummary: journal.Summary,
	}

	resp, err := e.LLM.SummarizeJournal(ctx, data)
	if err != nil {
		return err
	}

	journal.Summary = resp.Summary
	// Keep the last entry to maintain context continuity?
	// Or clear all. The prompt says "compress list of old entries".
	// Let's clear all for simplicity as they are now in summary.
	journal.RecentEntries = []model.JournalEntry{}
	return nil
}

func (e *Engine) summarizeGlobalJournal(ctx context.Context) error {
	journal := &e.State.GlobalJournal
	data := struct {
		OldEntries     string
		CurrentSummary string
	}{
		OldEntries:     formatJournal(journal.RecentEntries),
		CurrentSummary: journal.Summary,
	}

	resp, err := e.LLM.SummarizeJournal(ctx, data)
	if err != nil {
		return err
	}

	journal.Summary = resp.Summary
	journal.RecentEntries = []model.JournalEntry{}
	return nil
}

func (e *Engine) processCurrentNode(ctx context.Context, lastPlayerAction string) error {
	currentNode := e.WorldNodes[e.State.CurrentNodeID]

	// Prepare common data
	globalSummary := e.State.GlobalJournal.Summary
	globalRecent := formatJournal(e.State.GlobalJournal.RecentEntries)
	localJournal := e.State.GetNodeJournal(currentNode.ID)
	localSummary := localJournal.Summary
	localRecent := formatJournal(localJournal.RecentEntries)

	var newOptions []GameOption
	var displayText string

	if currentNode.Type == model.LocationType {
		// Generate Location Actions
		adjacentLocs, adjacentNPCs := e.getAdjacents(currentNode)

		data := struct {
			NodeDescription   string
			AdjacentLocations string
			AdjacentNPCs      string
			GlobalSummary     string
			GlobalRecent      string
			LocalSummary      string
			LocalRecent       string
		}{
			NodeDescription:   currentNode.Description,
			AdjacentLocations: adjacentLocs,
			AdjacentNPCs:      adjacentNPCs,
			GlobalSummary:     globalSummary,
			GlobalRecent:      globalRecent,
			LocalSummary:      localSummary,
			LocalRecent:       localRecent,
		}

		resp, err := e.LLM.GenerateLocationActions(ctx, data)
		if err != nil {
			return err
		}

		displayText = currentNode.Description

		// Add generated options
		for _, opt := range resp.PlayerOptions {
			newOptions = append(newOptions, GameOption{
				Text:  opt.Text,
				Event: opt.Event,
			})
		}

	} else if currentNode.Type == model.NPCType {
		// Generate NPC Response

		currentLocation := "Unknown"
		// Find parent location (reverse lookup or check adjacent)
		for _, adjID := range currentNode.Adjacent {
			if adjNode, ok := e.WorldNodes[adjID]; ok && adjNode.Type == model.LocationType {
				currentLocation = adjNode.Name
				break
			}
		}

		data := struct {
			NPCName         string
			NPCDescription  string
			CurrentLocation string
			GlobalSummary   string
			GlobalRecent    string
			LocalSummary    string
			LocalRecent     string
		}{
			NPCName:         currentNode.Name,
			NPCDescription:  currentNode.Description,
			CurrentLocation: currentLocation,
			GlobalSummary:   globalSummary,
			GlobalRecent:    globalRecent,
			LocalSummary:    localSummary,
			LocalRecent:     localRecent,
		}

		resp, err := e.LLM.GenerateNPCResponse(ctx, data)
		if err != nil {
			return err
		}

		displayText = fmt.Sprintf("%s: %s", currentNode.Name, resp.NPCResponse)

		// Add generated options
		for _, opt := range resp.PlayerOptions {
			newOptions = append(newOptions, GameOption{
				Text:  opt.Text,
				Event: opt.Event,
			})
		}
	}

	// Add Transition Options
	for _, adjID := range currentNode.Adjacent {
		adjNode := e.WorldNodes[adjID]
		var text string
		if adjNode.Type == model.LocationType {
			if currentNode.Type == model.NPCType {
				text = "Leave conversation"
			} else {
				text = fmt.Sprintf("Go to %s", adjNode.Name)
			}
		} else {
			text = fmt.Sprintf("Talk to %s", adjNode.Name)
		}

		newOptions = append(newOptions, GameOption{
			Text:       text,
			NextNodeID: adjID,
		})
	}

	e.CurrentOptions = newOptions
	e.LastText = displayText

	return nil
}

func (e *Engine) getAdjacents(node *model.Node) (string, string) {
	var locs []string
	var npcs []string

	for _, adjID := range node.Adjacent {
		adj := e.WorldNodes[adjID]
		if adj.Type == model.LocationType {
			locs = append(locs, adj.Name)
		} else {
			npcs = append(npcs, adj.Name)
		}
	}

	return strings.Join(locs, ", "), strings.Join(npcs, ", ")
}

func formatJournal(entries []model.JournalEntry) string {
	var lines []string
	for _, e := range entries {
		lines = append(lines, fmt.Sprintf("- %s", e.Text))
	}
	return strings.Join(lines, "\n")
}
