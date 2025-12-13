package main

import (
	"flag"
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"src/pipellm/client"
	"src/pipellm/config"
	"src/pipellm/input"
)

func main() {
	bashAlias := flag.Bool("bash-alias", false, "Generate bash aliases for all prompts")
	flag.Parse()

	if *bashAlias {
		generateAliases()
		return
	}

	var promptName string
	if flag.NArg() > 0 {
		// Called with alias name as argument
		promptName = flag.Arg(0)
	} else {
		// Called directly by binary name
		promptName = filepath.Base(os.Args[0])
	}

	cfg, err := config.LoadConfig()
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error loading config: %v\n", err)
		os.Exit(1)
	}

	prompt := cfg.FindPrompt(promptName)
	if prompt == "" {
		fmt.Fprintf(os.Stderr, "No prompt found for name: %s\n", promptName)
		os.Exit(1)
	}

	userInput := input.ReadStdin()

	modelName := cfg.Model
	if modelName == "" {
		modelName = "gemini-pro" // default model
	}

	client, err := client.NewClient(cfg.APIKey, modelName, cfg.GenerationConfig)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error creating client: %v\n", err)
		os.Exit(1)
	}
	response, err := client.SendPrompt(prompt, userInput)
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error calling Gemini API: %v\n", err)
		os.Exit(1)
	}

	fmt.Println(response)
}

func generateAliases() {
	cfg, err := config.LoadConfig()
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error loading config: %v\n", err)
		os.Exit(1)
	}

	execPath, err := os.Executable()
	if err != nil {
		fmt.Fprintf(os.Stderr, "Error getting executable path: %v\n", err)
		os.Exit(1)
	}

	for _, prompt := range cfg.Prompts {
		alias := strings.ToLower(prompt.Name)
		fmt.Printf("alias %s='%s %s'\n", alias, execPath, alias)
	}
}
