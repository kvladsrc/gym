package input

import (
	"bufio"
	"os"
	"strings"
)

// ReadStdin reads all input from stdin and returns it as a trimmed string.
// Returns an empty string if stdin is a terminal (no piped input).
func ReadStdin() string {
	stat, _ := os.Stdin.Stat()
	if (stat.Mode() & os.ModeCharDevice) != 0 {
		// Terminal mode - no piped input
		return ""
	}

	var input strings.Builder
	scanner := bufio.NewScanner(os.Stdin)
	for scanner.Scan() {
		input.WriteString(scanner.Text() + "\n")
	}
	return strings.TrimSpace(input.String())
}
