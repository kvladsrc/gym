package llm

// LocationResponse represents the LLM response for a location.
type LocationResponse struct {
	PlayerOptions []Option `json:"player_options"`
}

// NPCResponse represents the LLM response for an NPC interaction.
type NPCResponse struct {
	NPCResponse   string   `json:"npc_response"`
	PlayerOptions []Option `json:"player_options"`
}

// Option represents a player choice.
type Option struct {
	Text  string  `json:"text"`
	Event *string `json:"event"`
}

// SummaryResponse represents the LLM response for a journal summary.
type SummaryResponse struct {
	Summary string `json:"summary"`
}
