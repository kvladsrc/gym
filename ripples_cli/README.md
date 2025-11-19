# Ripples of Words (Go CLI)

A text-based RPG where dialogues and interactions are generated in
real-time by an LLM (Gemini), based on a world graph and a living
journal of events.

## Features

- **Dynamic Storytelling**: NPCs react to your actions and past
    events.
- **Living Journal**: The game remembers what happens. Old entries
    are automatically summarized to maintain context without exceeding
    token limits.
- **Graph-Based World**: Locations and NPCs are defined in a DOT
    file, making the world easy to extend.
- **TUI**: Built with [Bubble
    Tea](https://github.com/charmbracelet/bubbletea) for a clean
    terminal interface.

## Prerequisites

- **Gemini API Key**: You need an API key from [Google AI
    Studio](https://aistudio.google.com/).

## Build and Run

This project is part of a Bazel monorepo.

### 1. Build

```bash
bazelisk build //ripples_cli/...
```

### 2. Run

Set your API key and run the game:

```bash
export GEMINI_API_KEY="your_api_key" # pragma: allowlist secret
bazelisk run //ripples_cli/cmd/ripples -- --world $(pwd)/ripples_cli/assets/world.dot
```

### Options

- `-key`: Gemini API Key (can also be set via `GEMINI_API_KEY` env
    var).
- `-world`: Path to the DOT file defining the world (default:
    `ripples_cli/assets/world.dot`).
- `-model`: LLM model name (default: `gemini-1.5-flash`).
- `-journal-limit`: Number of recent journal entries to keep before
    summarizing (default: 5).

## Project Structure

- `cmd/ripples`: Main entry point.
- `pkg/engine`: Core game logic (state management, summarization).
- `pkg/llm`: Gemini client and prompt templates.
- `pkg/model`: Data structures (Node, Journal, WorldState).
- `pkg/tui`: Terminal User Interface.
- `pkg/world`: DOT file loader.
- `assets`: Game data (world graph).
