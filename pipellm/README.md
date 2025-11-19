# PipeLLM

A simple and lightweight CLI tool for working with the **Gemini API**
using named prompts and shell aliases.

## Installation

### 1. Build with Go

```bash
bazelisk build //pipellm:pipellm
```

### 2. Create a config file `~/.pipellm.yaml`

```yaml
api_key: your_gemini_api_key_here

prompts:
- name: summary
  prompt: >
    Summarize the following text into a single short paragraph in
    English. The summary should be clear, concise, and easy to read,
    capturing the most important ideas.

    Text:

- name: kharms
  prompt: >
    Rewrite the following text in the style of Daniil Kharms. The
    result should be slightly surreal, whimsical, and playful, but
    still preserve the original meaning and be understandable.

    Text:
```

### 3. Generate shell aliases

Add them to `.bashrc` (or `.zshrc`):

```bash
bazelisk run //pipellm:pipellm -- --bash-alias >> ~/.bashrc
source ~/.bashrc
```

---

## Usage

Run prompts directly in pipelines:

```bash
echo "Long text" | summary
echo "Plain text" | kharms
```

Or combine them:

```bash
cat error.txt | grep ERROR | summary | kharms
# → Everything has vanished like smoke, the file exists no more.
```
