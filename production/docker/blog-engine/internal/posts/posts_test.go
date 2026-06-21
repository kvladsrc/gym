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
