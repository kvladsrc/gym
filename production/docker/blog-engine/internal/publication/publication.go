package publication

import (
	"bytes"
	"context"
	"crypto/rand"
	"embed"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"html"
	htmltemplate "html/template"
	"regexp"
	"strings"
	"time"

	"src/production/docker/blog-engine/internal/config"
	"src/production/docker/blog-engine/internal/objectstore"
	"src/production/docker/blog-engine/internal/posts"
)

const (
	indexKey      = "index.html"
	styleKey      = "public.css"
	htmlMediaType = "text/html; charset=utf-8"
	cssMediaType  = "text/css; charset=utf-8"
	jsonMediaType = "application/json"
	feedLimit     = 30
	excerptWords  = 45
)

//go:embed templates/*.html static/*.css
var content embed.FS

var (
	postTemplate = htmltemplate.Must(htmltemplate.ParseFS(content, "templates/post.html"))
	feedTemplate = htmltemplate.Must(htmltemplate.ParseFS(content, "templates/feed.html"))
)

var (
	linkPattern      = regexp.MustCompile(`\[\[([^\]\[]+)\]\[([^\]\[]+)\]\]`)
	boldPattern      = regexp.MustCompile(`\*([^*\n]+)\*`)
	italicPattern    = regexp.MustCompile(`/([^/\n]+)/`)
	underlinePattern = regexp.MustCompile(`_([^_\n]+)_`)
	verbatimPattern  = regexp.MustCompile(`=([^=\n]+)=`)
	codePattern      = regexp.MustCompile(`~([^~\n]+)~`)
	strikePattern    = regexp.MustCompile(`\+([^+\n]+)\+`)
)

// Store reads and writes public publication artifacts.
type Store interface {
	Get(context.Context, string, string) (objectstore.Object, error)
	Put(context.Context, string, string, string, []byte) error
}

// State records linked-list heads and reserved placeholder keys.
type State struct {
	Global ListState            `json:"global"`
	Tags   map[string]ListState `json:"tags,omitempty"`
}

// ListState identifies the current head and future placeholder for one list.
type ListState struct {
	Head  string      `json:"head,omitempty"`
	Next  string      `json:"next,omitempty"`
	Items []FeedEntry `json:"items,omitempty"`
}

// FeedEntry is the static metadata needed to render a public feed page.
type FeedEntry struct {
	Title       string   `json:"title"`
	Path        string   `json:"path"`
	Excerpt     string   `json:"excerpt"`
	Tags        []string `json:"tags,omitempty"`
	PublishedOn string   `json:"published_on"`
}

// Publisher renders revisions and uploads static linked-list artifacts.
type Publisher struct {
	Renderer    Renderer
	Store       Store
	Bucket      string
	StateKey    string
	GenerateKey func() (string, error)
}

// Renderer renders a post revision into public static HTML.
type Renderer struct{}

type navLink struct {
	Label string
	Path  string
}

type pageContext struct {
	CanonicalPath string
	PublishedOn   string
	Newer         navLink
	Older         navLink
}

type postTemplateData struct {
	Title         string
	CanonicalPath string
	PublishedOn   string
	TagsHTML      htmltemplate.HTML
	BodyHTML      htmltemplate.HTML
	NavHTML       htmltemplate.HTML
}

type feedTemplateData struct {
	Title         string
	CanonicalPath string
	Items         []feedTemplateItem
}

type feedTemplateItem struct {
	Title       string
	Path        string
	Excerpt     string
	TagsHTML    htmltemplate.HTML
	PublishedOn string
}

// NewPublisher returns the production publisher for worker use.
func NewPublisher(cfg config.Config, store Store) Publisher {
	return Publisher{
		Renderer: Renderer{},
		Store:    store,
		Bucket:   cfg.S3.PublicBucket,
		StateKey: cfg.Publishing.StateKey,
	}
}

// Publish writes the new post into placeholder slots, advances list state, and returns the canonical public path.
func (publisher Publisher) Publish(ctx context.Context, post posts.Post) (string, error) {
	if err := publisher.validate(); err != nil {
		return "", err
	}
	state, err := publisher.loadState(ctx)
	if err != nil {
		return "", err
	}
	if err := publisher.putPublicStyle(ctx); err != nil {
		return "", err
	}

	globalPath, err := publisher.publishGlobal(ctx, post, &state)
	if err != nil {
		return "", err
	}
	if err := publisher.publishTagsAndState(ctx, post, globalPath, &state); err != nil {
		return "", err
	}
	return globalPath, nil
}

// Render converts the supported org-mode subset into a complete HTML document.
func (renderer Renderer) Render(ctx context.Context, post posts.Post) (string, error) {
	return renderer.render(ctx, post, pageContext{})
}

func (publisher Publisher) validate() error {
	if publisher.Store == nil {
		return fmt.Errorf("publication store is required")
	}
	if strings.TrimSpace(publisher.Bucket) == "" {
		return fmt.Errorf("publication bucket is required")
	}
	if strings.TrimSpace(publisher.StateKey) == "" {
		return fmt.Errorf("publication state key is required")
	}
	return nil
}

func (publisher Publisher) publishGlobal(ctx context.Context, post posts.Post, state *State) (string, error) {
	current, next, err := publisher.advance(&state.Global)
	if err != nil {
		return "", err
	}
	currentPath := postPath(current)
	publishedOn := time.Now().UTC().Format("2006-01-02")
	renderedHTML, err := publisher.Renderer.render(ctx, post, pageContext{
		CanonicalPath: currentPath,
		PublishedOn:   publishedOn,
		Newer:         navLink{Label: "Newer", Path: postPath(next)},
		Older:         navLink{Label: "Older", Path: postPath(state.Global.Head)},
	})
	if err != nil {
		return "", err
	}
	for _, key := range []string{currentPath, postPath(next)} {
		if err := publisher.putHTML(ctx, key, renderedHTML); err != nil {
			return "", err
		}
	}
	state.Global = ListState{
		Head:  current,
		Next:  next,
		Items: prependFeedEntry(state.Global.Items, feedEntry(post, currentPath, publishedOn)),
	}
	feedHTML, err := publisher.Renderer.renderFeed("Posts", "/", state.Global.Items)
	if err != nil {
		return "", err
	}
	if err := publisher.putHTML(ctx, indexKey, feedHTML); err != nil {
		return "", err
	}
	return currentPath, nil
}

func (publisher Publisher) publishTagsAndState(ctx context.Context, post posts.Post, globalPath string, state *State) error {
	for _, tag := range post.Tags {
		if err := publisher.publishTag(ctx, post, globalPath, tag, state); err != nil {
			return err
		}
	}
	return publisher.putState(ctx, *state)
}

func (publisher Publisher) publishTag(ctx context.Context, post posts.Post, globalPath string, tag string, state *State) error {
	tagState := state.Tags[tag]
	current, next, err := publisher.advance(&tagState)
	if err != nil {
		return err
	}
	tagPath := tagPostPath(tag, current)
	renderedHTML, err := publisher.Renderer.render(ctx, post, pageContext{
		CanonicalPath: globalPath,
		PublishedOn:   newestPublishedOn(state.Global.Items),
		Newer:         navLink{Label: "Newer in " + tag, Path: tagPostPath(tag, next)},
		Older:         navLink{Label: "Older in " + tag, Path: tagPostPath(tag, tagState.Head)},
	})
	if err != nil {
		return err
	}
	for _, key := range []string{tagPath, tagPostPath(tag, next)} {
		if err := publisher.putHTML(ctx, key, renderedHTML); err != nil {
			return err
		}
	}
	tagState = ListState{
		Head:  current,
		Next:  next,
		Items: prependFeedEntry(tagState.Items, feedEntry(post, globalPath, newestPublishedOn(state.Global.Items))),
	}
	state.Tags[tag] = tagState
	feedHTML, err := publisher.Renderer.renderFeed("Posts tagged "+tag, "/tags/"+tag+"/", tagState.Items)
	if err != nil {
		return err
	}
	return publisher.putHTML(ctx, tagIndexPath(tag), feedHTML)
}

func prependFeedEntry(items []FeedEntry, item FeedEntry) []FeedEntry {
	next := make([]FeedEntry, 0, minimum(len(items)+1, feedLimit))
	next = append(next, item)
	for _, existing := range items {
		if existing.Path == item.Path {
			continue
		}
		next = append(next, existing)
		if len(next) == feedLimit {
			break
		}
	}
	return next
}

func feedEntry(post posts.Post, path string, publishedOn string) FeedEntry {
	return FeedEntry{
		Title:       strings.TrimSpace(post.Title),
		Path:        path,
		Excerpt:     excerpt(post.Source, excerptWords),
		Tags:        append([]string(nil), post.Tags...),
		PublishedOn: publishedOn,
	}
}

func newestPublishedOn(items []FeedEntry) string {
	if len(items) == 0 {
		return time.Now().UTC().Format("2006-01-02")
	}
	return items[0].PublishedOn
}

func (renderer Renderer) renderFeed(title string, canonicalPath string, items []FeedEntry) (string, error) {
	pageTitle := strings.TrimSpace(title)
	if pageTitle == "" {
		pageTitle = "Posts"
	}
	data := feedTemplateData{
		Title:         pageTitle,
		CanonicalPath: canonicalPath,
	}
	for _, item := range items {
		data.Items = append(data.Items, feedTemplateItem{
			Title:       item.Title,
			Path:        item.Path,
			Excerpt:     item.Excerpt,
			TagsHTML:    renderTags(item.Tags),
			PublishedOn: item.PublishedOn,
		})
	}
	var document bytes.Buffer
	if err := feedTemplate.Execute(&document, data); err != nil {
		return "", fmt.Errorf("render feed template: %w", err)
	}
	return document.String(), nil
}

func excerpt(source string, wordLimit int) string {
	var words []string
	for _, line := range strings.Split(normalizeLineEndings(source), "\n") {
		trimmed := strings.TrimSpace(line)
		if trimmed == "" || strings.HasPrefix(trimmed, "#+") {
			continue
		}
		if level, heading := heading(trimmed); level > 0 {
			trimmed = heading
		}
		for _, word := range strings.Fields(plainInline(trimmed)) {
			words = append(words, word)
			if len(words) == wordLimit {
				return strings.Join(words, " ") + "..."
			}
		}
	}
	return strings.Join(words, " ")
}

func plainInline(value string) string {
	value = linkPattern.ReplaceAllString(value, "$2")
	for _, pattern := range []*regexp.Regexp{
		boldPattern,
		italicPattern,
		underlinePattern,
		verbatimPattern,
		codePattern,
		strikePattern,
	} {
		value = pattern.ReplaceAllString(value, "$1")
	}
	return value
}

func minimum(a int, b int) int {
	if a < b {
		return a
	}
	return b
}

func (publisher Publisher) putState(ctx context.Context, state State) error {
	encoded, err := json.MarshalIndent(state, "", "  ")
	if err != nil {
		return fmt.Errorf("encode publication state: %w", err)
	}
	encoded = append(encoded, '\n')
	if err := publisher.Store.Put(ctx, publisher.Bucket, publisher.StateKey, jsonMediaType, encoded); err != nil {
		return fmt.Errorf("upload publication state: %w", err)
	}
	return nil
}

func (publisher Publisher) loadState(ctx context.Context) (State, error) {
	object, err := publisher.Store.Get(ctx, publisher.Bucket, publisher.StateKey)
	if errors.Is(err, objectstore.ErrNotFound) {
		return State{Tags: make(map[string]ListState)}, nil
	}
	if err != nil {
		return State{}, fmt.Errorf("load publication state: %w", err)
	}
	var state State
	if err := json.Unmarshal(object.Body, &state); err != nil {
		return State{}, fmt.Errorf("parse publication state: %w", err)
	}
	if state.Tags == nil {
		state.Tags = make(map[string]ListState)
	}
	return state, nil
}

func (publisher Publisher) advance(list *ListState) (string, string, error) {
	current := strings.TrimSpace(list.Next)
	if current == "" {
		generated, err := publisher.key()
		if err != nil {
			return "", "", err
		}
		current = generated
	}
	next, err := publisher.key()
	if err != nil {
		return "", "", err
	}
	return current, next, nil
}

func (publisher Publisher) key() (string, error) {
	if publisher.GenerateKey != nil {
		return publisher.GenerateKey()
	}
	var raw [8]byte
	if _, err := rand.Read(raw[:]); err != nil {
		return "", fmt.Errorf("generate publication key: %w", err)
	}
	return hex.EncodeToString(raw[:]), nil
}

func (publisher Publisher) putHTML(ctx context.Context, key string, body string) error {
	storeKey := objectKey(key)
	if err := publisher.Store.Put(ctx, publisher.Bucket, storeKey, htmlMediaType, []byte(body)); err != nil {
		return fmt.Errorf("upload public HTML %s: %w", storeKey, err)
	}
	return nil
}

func (publisher Publisher) putPublicStyle(ctx context.Context) error {
	body, err := content.ReadFile("static/public.css")
	if err != nil {
		return fmt.Errorf("read public stylesheet: %w", err)
	}
	if err := publisher.Store.Put(ctx, publisher.Bucket, styleKey, cssMediaType, body); err != nil {
		return fmt.Errorf("upload public CSS %s: %w", styleKey, err)
	}
	return nil
}

func (renderer Renderer) render(_ context.Context, post posts.Post, page pageContext) (string, error) {
	title := strings.TrimSpace(post.Title)
	if title == "" {
		return "", fmt.Errorf("render post %d revision %d: title is required", post.ID, post.RevisionID)
	}
	var body bytes.Buffer
	lines := strings.Split(normalizeLineEndings(post.Source), "\n")
	var paragraph []string
	flushParagraph := func() {
		if len(paragraph) == 0 {
			return
		}
		body.WriteString("<p>")
		body.WriteString(renderParagraph(paragraph))
		body.WriteString("</p>\n")
		paragraph = nil
	}

	for _, line := range lines {
		trimmed := strings.TrimSpace(line)
		if trimmed == "" {
			flushParagraph()
			continue
		}
		level, heading := heading(trimmed)
		if level > 0 {
			flushParagraph()
			body.WriteString(fmt.Sprintf("<h%d>%s</h%d>\n", level, inline(heading), level))
			continue
		}
		if strings.HasPrefix(trimmed, "#+") {
			return "", fmt.Errorf("render post %d revision %d: unsupported org directive %q", post.ID, post.RevisionID, trimmed)
		}
		paragraph = append(paragraph, trimmed)
	}
	flushParagraph()

	publishedOn := page.PublishedOn
	if publishedOn == "" {
		publishedOn = time.Now().UTC().Format("2006-01-02")
	}
	var document bytes.Buffer
	if err := postTemplate.Execute(&document, postTemplateData{
		Title:         title,
		CanonicalPath: page.CanonicalPath,
		PublishedOn:   publishedOn,
		TagsHTML:      renderTags(post.Tags),
		BodyHTML:      htmltemplate.HTML(body.String()),
		NavHTML:       renderNav(page),
	}); err != nil {
		return "", fmt.Errorf("render post template: %w", err)
	}
	return document.String(), nil
}

func renderTags(tags []string) htmltemplate.HTML {
	if len(tags) == 0 {
		return ""
	}
	var builder strings.Builder
	builder.WriteString(`<nav class="post-tags" aria-label="Tags">`)
	for _, tag := range tags {
		builder.WriteString(`<a href="/tags/`)
		builder.WriteString(html.EscapeString(tag))
		builder.WriteString(`/">`)
		builder.WriteString(html.EscapeString(tag))
		builder.WriteString("</a>")
	}
	builder.WriteString("</nav>\n")
	return htmltemplate.HTML(builder.String())
}

func normalizeLineEndings(text string) string {
	text = strings.ReplaceAll(text, "\r\n", "\n")
	return strings.ReplaceAll(text, "\r", "\n")
}

func renderParagraph(lines []string) string {
	var builder strings.Builder
	for i, line := range lines {
		if i > 0 {
			builder.WriteString("<br>\n")
		}
		builder.WriteString(inline(line))
	}
	return builder.String()
}

func renderNav(page pageContext) htmltemplate.HTML {
	if page.Newer.Path == "" && page.Older.Path == "" {
		return ""
	}
	var builder strings.Builder
	builder.WriteString("<nav>\n")
	if page.Newer.Path != "" {
		builder.WriteString(`<a rel="prev" href="`)
		builder.WriteString(html.EscapeString(page.Newer.Path))
		builder.WriteString(`">`)
		builder.WriteString(html.EscapeString(page.Newer.Label))
		builder.WriteString("</a>\n")
	}
	if page.Older.Path != "" {
		builder.WriteString(`<a rel="next" href="`)
		builder.WriteString(html.EscapeString(page.Older.Path))
		builder.WriteString(`">`)
		builder.WriteString(html.EscapeString(page.Older.Label))
		builder.WriteString("</a>\n")
	} else {
		builder.WriteString("<span>End</span>\n")
	}
	builder.WriteString("</nav>\n")
	return htmltemplate.HTML(builder.String())
}

func postPath(key string) string {
	if key == "" {
		return ""
	}
	return "/posts/" + key + ".html"
}

func tagPostPath(tag string, key string) string {
	if tag == "" || key == "" {
		return ""
	}
	return "/tags/" + tag + "/" + key + ".html"
}

func tagIndexPath(tag string) string {
	return "/tags/" + tag + "/index.html"
}

func objectKey(path string) string {
	return strings.TrimPrefix(path, "/")
}

func heading(line string) (int, string) {
	stars := 0
	for stars < len(line) && line[stars] == '*' {
		stars++
	}
	if stars == 0 || stars > 6 || stars >= len(line) || line[stars] != ' ' {
		return 0, ""
	}
	return stars, strings.TrimSpace(line[stars+1:])
}

func inline(value string) string {
	escaped := html.EscapeString(value)
	replacements := make(map[string]string)
	addReplacement := func(replacement string) string {
		token := fmt.Sprintf("BLOGENGINEINLINETOKEN%dEND", len(replacements))
		replacements[token] = replacement
		return token
	}
	escaped = linkPattern.ReplaceAllStringFunc(escaped, func(match string) string {
		parts := linkPattern.FindStringSubmatch(match)
		return addReplacement(fmt.Sprintf(`<a href="%s">%s</a>`, parts[1], parts[2]))
	})
	escaped = boldPattern.ReplaceAllStringFunc(escaped, func(match string) string {
		parts := boldPattern.FindStringSubmatch(match)
		return addReplacement(fmt.Sprintf("<strong>%s</strong>", parts[1]))
	})
	escaped = italicPattern.ReplaceAllStringFunc(escaped, func(match string) string {
		parts := italicPattern.FindStringSubmatch(match)
		return addReplacement(fmt.Sprintf("<em>%s</em>", parts[1]))
	})
	escaped = underlinePattern.ReplaceAllStringFunc(escaped, func(match string) string {
		parts := underlinePattern.FindStringSubmatch(match)
		return addReplacement(fmt.Sprintf("<u>%s</u>", parts[1]))
	})
	escaped = verbatimPattern.ReplaceAllStringFunc(escaped, func(match string) string {
		parts := verbatimPattern.FindStringSubmatch(match)
		return addReplacement(fmt.Sprintf("<code>%s</code>", parts[1]))
	})
	escaped = codePattern.ReplaceAllStringFunc(escaped, func(match string) string {
		parts := codePattern.FindStringSubmatch(match)
		return addReplacement(fmt.Sprintf("<code>%s</code>", parts[1]))
	})
	escaped = strikePattern.ReplaceAllStringFunc(escaped, func(match string) string {
		parts := strikePattern.FindStringSubmatch(match)
		return addReplacement(fmt.Sprintf("<s>%s</s>", parts[1]))
	})
	for token, replacement := range replacements {
		escaped = strings.ReplaceAll(escaped, token, replacement)
	}
	return escaped
}
