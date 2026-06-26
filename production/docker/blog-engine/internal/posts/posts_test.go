package posts

import (
	"strings"
	"testing"
)

func TestParseTags(t *testing.T) {
	tests := []struct {
		name string
		raw  string
		want []string
	}{
		{
			name: "empty",
			raw:  "",
			want: nil,
		},
		{
			name: "trims and skips empty tags",
			raw:  "go, infra, , blog ",
			want: []string{"go", "infra", "blog"},
		},
		{
			name: "deduplicates",
			raw:  "go,go, infra,go",
			want: []string{"go", "infra"},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			got := ParseTags(tt.raw)
			if strings.Join(got, "|") != strings.Join(tt.want, "|") {
				t.Fatalf("ParseTags(%q) = %#v, want %#v", tt.raw, got, tt.want)
			}
		})
	}
}

func TestSchemaDoesNotDropExistingTables(t *testing.T) {
	if strings.Contains(strings.ToUpper(Schema), "DROP TABLE") {
		t.Fatalf("Schema must not drop existing tables:\n%s", Schema)
	}
	for _, want := range []string{
		"CREATE TABLE IF NOT EXISTS posts",
		"ALTER TABLE posts ADD COLUMN IF NOT EXISTS published_path",
		"CREATE INDEX IF NOT EXISTS posts_updated_at_idx",
	} {
		if !strings.Contains(Schema, want) {
			t.Fatalf("Schema missing %q:\n%s", want, Schema)
		}
	}
}

func TestCreateInputValidate(t *testing.T) {
	tests := []struct {
		name    string
		input   CreateInput
		wantErr string
	}{
		{
			name: "valid",
			input: CreateInput{
				Title:  "Title",
				Tags:   []string{"go", "blog"},
				Source: "* Heading",
			},
		},
		{
			name: "requires title",
			input: CreateInput{
				Source: "* Heading",
			},
			wantErr: "title is required",
		},
		{
			name: "requires source",
			input: CreateInput{
				Title: "Title",
			},
			wantErr: "source is required",
		},
		{
			name: "rejects tag with space",
			input: CreateInput{
				Title:  "Title",
				Tags:   []string{"go blog"},
				Source: "* Heading",
			},
			wantErr: `tag "go blog" must be a lowercase slug`,
		},
		{
			name: "rejects uppercase tag",
			input: CreateInput{
				Title:  "Title",
				Tags:   []string{"Go"},
				Source: "* Heading",
			},
			wantErr: `tag "Go" must be a lowercase slug`,
		},
		{
			name: "rejects cyrillic tag",
			input: CreateInput{
				Title:  "Title",
				Tags:   []string{"тест"},
				Source: "* Heading",
			},
			wantErr: `tag "тест" must be a lowercase slug`,
		},
		{
			name: "accepts tag with hyphen",
			input: CreateInput{
				Title:  "Title",
				Tags:   []string{"go-blog"},
				Source: "* Heading",
			},
		},
		{
			name: "rejects leading hyphen",
			input: CreateInput{
				Title:  "Title",
				Tags:   []string{"-blog"},
				Source: "* Heading",
			},
			wantErr: `tag "-blog" must be a lowercase slug`,
		},
		{
			name: "rejects double hyphen",
			input: CreateInput{
				Title:  "Title",
				Tags:   []string{"go--blog"},
				Source: "* Heading",
			},
			wantErr: `tag "go--blog" must be a lowercase slug`,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			err := tt.input.Validate()
			if tt.wantErr == "" {
				if err != nil {
					t.Fatalf("Validate() returned error: %v", err)
				}
				return
			}
			if err == nil || err.Error() != tt.wantErr {
				t.Fatalf("Validate() error = %v, want %q", err, tt.wantErr)
			}
		})
	}
}
func TestRenderDiffHTMLUsesLineDiff(t *testing.T) {
	original := strings.Join([]string{
		"* Welcome to Org-mode",
		"** Sub-heading",
		"",
		"To markup text in Org, simply surround it with one or more marker characters.",
		"*Bold*, /italic/ and _underline_ are fairly intuitive, and the ability to use",
		"+strikethrough+ is a plus.  You can _/*combine*/_ the basic markup in any",
		"order, however ~code~ and =verbatim= need to be the *_~inner-most~_* markers",
		"if they are present since their contents are interpreted =_literally_=.",
	}, "\n")
	suggested := strings.Replace(original, "plus.  You", "plus. You", 1)

	diff := renderDiffHTML(original, suggested)

	wantParts := []string{
		`<pre class="diff">`,
		`<span class="diff-line meta">@@`,
		`<span class="diff-line del">-+strikethrough+ is a plus.  You can _/*combine*/_ the basic markup in any</span>`,
		`<span class="diff-line add">++strikethrough+ is a plus. You can _/*combine*/_ the basic markup in any</span>`,
		`<span class="diff-line"> order, however ~code~ and =verbatim= need to be the *_~inner-most~_* markers</span>`,
		`</pre>`,
	}
	for _, want := range wantParts {
		if !strings.Contains(diff, want) {
			t.Fatalf("renderDiffHTML() missing %q in:\n%s", want, diff)
		}
	}
	if strings.Contains(diff, htmlSpan("del", "- "+original)) {
		t.Fatalf("renderDiffHTML() rendered original as one deleted block:\n%s", diff)
	}
	if strings.Contains(diff, htmlSpan("add", "+ "+suggested)) {
		t.Fatalf("renderDiffHTML() rendered suggested as one added block:\n%s", diff)
	}
}

func TestRenderDiffHTMLIgnoresLineEndingChanges(t *testing.T) {
	original := "Hello, world!\r\nI'm testing my grammar checks and diff templating.\r\nPlease, check the post details."
	suggested := "Hello, world!\nI'm testing my grammar checks and diff templating.\nPlease, check the post details."

	if diff := renderDiffHTML(original, suggested); diff != "" {
		t.Fatalf("renderDiffHTML() = %q, want empty diff for line ending changes", diff)
	}
}

func TestRenderDiffHTMLEscapesLines(t *testing.T) {
	diff := renderDiffHTML("Use <tag>", "Use <safe>")

	if !strings.Contains(diff, "-Use &lt;tag&gt;") {
		t.Fatalf("renderDiffHTML() did not escape deleted line:\n%s", diff)
	}
	if !strings.Contains(diff, "+Use &lt;safe&gt;") {
		t.Fatalf("renderDiffHTML() did not escape added line:\n%s", diff)
	}
}

func htmlSpan(class string, text string) string {
	return `<span class="diff-line ` + class + `">` + text + `</span>`
}
