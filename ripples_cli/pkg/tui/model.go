package tui

import (
	"context"
	"fmt"
	"strings"

	"src/ripples_cli/pkg/engine"

	tea "github.com/charmbracelet/bubbletea"
	"github.com/charmbracelet/lipgloss"
)

// State represents the current state of the TUI.
type State int

const (
	// StateLoading indicates the game is processing (LLM generation).
	StateLoading State = iota
	// StateDisplaying indicates the game is waiting for player input.
	StateDisplaying
	// StateError indicates an error occurred.
	StateError
)

// Model is the Bubble Tea model for the game TUI.
type Model struct {
	Engine *engine.Engine
	State  State
	Err    error

	// Selection
	Cursor int
}

// NewModel creates a new TUI model.
func NewModel(eng *engine.Engine) Model {
	return Model{
		Engine: eng,
		State:  StateLoading,
	}
}

type loadedMsg struct{}
type errMsg error

// Init initializes the TUI model.
func (m Model) Init() tea.Cmd {
	return tea.Batch(tea.ClearScreen, func() tea.Msg {
		err := m.Engine.Start(context.Background())
		if err != nil {
			return errMsg(err)
		}
		return loadedMsg{}
	})
}

// Update handles TUI messages and updates the model.
func (m Model) Update(msg tea.Msg) (tea.Model, tea.Cmd) {
	switch msg := msg.(type) {
	case tea.KeyMsg:
		switch msg.String() {
		case "q", "ctrl+c":
			return m, tea.Quit
		case "up", "k":
			if m.Cursor > 0 {
				m.Cursor--
			}
		case "down", "j":
			if m.Cursor < len(m.Engine.CurrentOptions)-1 {
				m.Cursor++
			}
		case "enter":
			if m.State == StateDisplaying {
				m.State = StateLoading
				idx := m.Cursor
				m.Cursor = 0
				return m, func() tea.Msg {
					err := m.Engine.SelectOption(context.Background(), idx)
					if err != nil {
						return errMsg(err)
					}
					return loadedMsg{}
				}
			}
		}
	case loadedMsg:
		m.State = StateDisplaying
	case errMsg:
		m.Err = msg
		m.State = StateError
	}
	return m, nil
}

// View renders the TUI.
func (m Model) View() string {
	if m.State == StateError {
		return fmt.Sprintf("Error: %v\nPress q to quit.", m.Err)
	}
	if m.State == StateLoading {
		return "Thinking...\n"
	}

	s := strings.Builder{}

	// 1. Description (Wrapped)
	descStyle := lipgloss.NewStyle().Width(80)
	s.WriteString(descStyle.Render(m.Engine.LastText) + "\n\n")

	// 2. Options
	for i, opt := range m.Engine.CurrentOptions {
		cursor := " "
		if m.Cursor == i {
			cursor = ">"
		}

		marker := ""
		if opt.Event != nil && *opt.Event != "" {
			marker = " [!]"
		}

		// Wrap option text
		optStyle := lipgloss.NewStyle().Width(76) // 80 - 4 for cursor/marker
		wrappedText := optStyle.Render(opt.Text)

		// Handle multiline rendering
		lines := strings.Split(wrappedText, "\n")
		for j, line := range lines {
			prefix := "  " // Indent for continuation lines
			if j == 0 {
				prefix = fmt.Sprintf("%s ", cursor)
			}

			suffix := ""
			if j == 0 {
				suffix = marker
			}

			s.WriteString(fmt.Sprintf("%s%s%s\n", prefix, line, suffix))
		}
	}

	// 3. Journal (Last 3 entries)
	s.WriteString("\n--- Recent Events ---\n")
	entries := m.Engine.State.GlobalJournal.RecentEntries
	start := len(entries) - 3
	if start < 0 {
		start = 0
	}
	for _, e := range entries[start:] {
		// Wrap journal entries too
		journalStyle := lipgloss.NewStyle().Width(80).Foreground(lipgloss.Color("240"))
		s.WriteString(journalStyle.Render(fmt.Sprintf("- %s", e.Text)) + "\n")
	}

	s.WriteString("\n(q to quit)\n")
	return s.String()
}
