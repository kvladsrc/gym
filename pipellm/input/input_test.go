package input

import (
	"io"
	"os"
	"strings"
	"testing"
)

// mockStdin replaces os.Stdin with a pipe containing the given content.
// Returns a cleanup function that restores the original stdin.
func mockStdin(t *testing.T, content string) (cleanup func()) {
	t.Helper()

	r, w, err := os.Pipe()
	if err != nil {
		t.Fatalf("failed to create pipe: %v", err)
	}

	originalStdin := os.Stdin
	os.Stdin = r

	// Write content in a goroutine.
	go func() {
		defer func() {
			if err := w.Close(); err != nil {
				t.Errorf("failed to close pipe writer: %v", err)
			}
		}()
		if _, err := io.WriteString(w, content); err != nil {
			t.Errorf("failed to write to pipe: %v", err)
		}
	}()

	cleanup = func() {
		os.Stdin = originalStdin
		if err := r.Close(); err != nil {
			t.Errorf("failed to close pipe reader: %v", err)
		}
	}

	return cleanup
}

func TestReadStdin(t *testing.T) {
	// Generate large input to test buffering.
	const lineCount = 100
	lineContent := "Line number " + strings.Repeat("x", 50)
	lines := make([]string, lineCount)
	for i := range lines {
		lines[i] = lineContent
	}
	largeInput := strings.Join(lines, "\n") + "\n"
	largeInputWant := strings.Join(lines, "\n")

	tests := []struct {
		name  string
		input string
		want  string
	}{
		{
			name:  "simple text",
			input: "Hello, World!",
			want:  "Hello, World!",
		},
		{
			name:  "multiline text",
			input: "Line 1\nLine 2\nLine 3",
			want:  "Line 1\nLine 2\nLine 3",
		},
		{
			name:  "text with trailing newline",
			input: "Content\n",
			want:  "Content",
		},
		{
			name:  "empty input",
			input: "",
			want:  "",
		},
		{
			name:  "only whitespace",
			input: "   \n  \n   ",
			want:  "",
		},
		{
			name:  "mixed whitespace",
			input: "\n\n  text  \n\n",
			want:  "text",
		},
		{
			name:  "multiple lines explicit",
			input: "Line 1\nLine 2\nLine 3\n",
			want:  "Line 1\nLine 2\nLine 3",
		},
		{
			name:  "single line explicit",
			input: "Single line input",
			want:  "Single line input",
		},
		{
			name:  "trailing whitespace explicit",
			input: "  Content with spaces  \n\n  \n",
			want:  "Content with spaces",
		},
		{
			name:  "large input",
			input: largeInput,
			want:  largeInputWant,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			cleanup := mockStdin(t, tt.input)
			defer cleanup()

			got := ReadStdin()
			if got != tt.want {
				t.Errorf("ReadStdin() = %q, want %q", got, tt.want)
			}
		})
	}
}
