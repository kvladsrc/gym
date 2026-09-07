package model

// NodeType defines the type of a node (location or npc).
type NodeType string

const (
	// LocationType represents a physical location.
	LocationType NodeType = "location"
	// NPCType represents a non-player character.
	NPCType NodeType = "npc"
)

// Node represents a point in the world graph.
type Node struct {
	ID          string
	Type        NodeType
	Name        string
	Description string
	Adjacent    []string
}

// ActionType defines the type of action (transition or generated).
type ActionType string

const (
	// ActionTransition represents moving to another node.
	ActionTransition ActionType = "transition"
	// ActionGenerated represents an LLM-generated action.
	ActionGenerated ActionType = "generated"
)

// Action represents a player action.
type Action struct {
	Type       ActionType `json:"type"`
	Text       string     `json:"text"`
	Event      *string    `json:"event"`
	NextNodeID string     `json:"nextNodeId"`
}

// JournalEntry represents a single entry in the journal.
type JournalEntry struct {
	GameDay int    `json:"game_day"`
	Text    string `json:"text"`
}

// Journal stores the history of events.
type Journal struct {
	Summary       string         `json:"summary"`
	RecentEntries []JournalEntry `json:"recent_entries"`
}

// WorldState holds the dynamic state of the game world.
type WorldState struct {
	CurrentNodeID string
	GameDay       int
	GlobalJournal Journal
	// Map of NodeID to Local Journal
	NodeJournals map[string]*Journal
}

// NewWorldState creates a new initial world state.
func NewWorldState(startNodeID string) *WorldState {
	return &WorldState{
		CurrentNodeID: startNodeID,
		GameDay:       1,
		GlobalJournal: Journal{
			RecentEntries: []JournalEntry{},
		},
		NodeJournals: make(map[string]*Journal),
	}
}

// GetNodeJournal returns the journal for a specific node, creating it if necessary.
func (w *WorldState) GetNodeJournal(nodeID string) *Journal {
	if _, ok := w.NodeJournals[nodeID]; !ok {
		w.NodeJournals[nodeID] = &Journal{
			RecentEntries: []JournalEntry{},
		}
	}
	return w.NodeJournals[nodeID]
}
