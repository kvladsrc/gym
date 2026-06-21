package posts

import (
	"context"
	"database/sql"
	"errors"
	"fmt"
	"html"
	"strings"
	"time"

	"github.com/jackc/pgx/v5/stdlib"
)

var _ = stdlib.GetDefaultDriver

const (
	// StatusPendingCheck means a post is saved and waiting for checks.
	StatusPendingCheck = "pending_check"
	// StatusNeedsChanges means checks found author-visible grammar issues.
	StatusNeedsChanges = "needs_changes"
	// StatusReady means checks passed and the post can be published.
	StatusReady = "ready"
	// StatusError means the latest revision failed for a technical reason.
	StatusError = "error"
	// StatusArchived means a post is hidden from the active author workflow.
	StatusArchived = "archived"

	// CheckStatusSuccess means all checks passed for a revision.
	CheckStatusSuccess = "success"
	// CheckStatusFailed means at least one check failed for a revision.
	CheckStatusFailed = "failed"

	// JobKindGrammarCheck identifies the initial OpenAI grammar check.
	JobKindGrammarCheck = "grammar_check"
	// JobStatusPending means a job is waiting for a worker lease.
	JobStatusPending = "pending"
	// JobStatusRunning means a worker currently owns the job lease.
	JobStatusRunning = "running"
	// JobStatusSucceeded means the job completed successfully.
	JobStatusSucceeded = "succeeded"
	// JobStatusFailed means the job failed permanently for now.
	JobStatusFailed = "failed"
)

// Schema contains the idempotent PostgreSQL schema for blog posts.
const Schema = `
CREATE TABLE IF NOT EXISTS posts (
	id BIGSERIAL PRIMARY KEY,
	title TEXT NOT NULL,
	tags TEXT NOT NULL DEFAULT '',
	source TEXT NOT NULL,
	auto_publish BOOLEAN NOT NULL DEFAULT false,
	status TEXT NOT NULL,
	check_status TEXT NOT NULL DEFAULT '',
	latest_revision_id BIGINT,
	created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
	updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS post_revisions (
	id BIGSERIAL PRIMARY KEY,
	post_id BIGINT NOT NULL REFERENCES posts(id),
	source TEXT NOT NULL,
	suggested_source TEXT NOT NULL DEFAULT '',
	diff_html TEXT NOT NULL DEFAULT '',
	check_status TEXT NOT NULL DEFAULT '',
	check_summary TEXT NOT NULL DEFAULT '',
	created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS jobs (
	id BIGSERIAL PRIMARY KEY,
	kind TEXT NOT NULL,
	post_id BIGINT NOT NULL REFERENCES posts(id),
	revision_id BIGINT NOT NULL REFERENCES post_revisions(id),
	status TEXT NOT NULL,
	attempts INTEGER NOT NULL DEFAULT 0,
	lease_until TIMESTAMPTZ,
	last_error TEXT NOT NULL DEFAULT '',
	created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
	updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE posts ADD COLUMN IF NOT EXISTS latest_revision_id BIGINT;

INSERT INTO post_revisions (post_id, source, check_status, created_at)
SELECT id, source, check_status, created_at
FROM posts
WHERE latest_revision_id IS NULL;

UPDATE posts
SET latest_revision_id = latest.id
FROM (
	SELECT DISTINCT ON (post_id) post_id, id
	FROM post_revisions
	ORDER BY post_id, created_at DESC, id DESC
) AS latest
WHERE posts.id = latest.post_id
	AND posts.latest_revision_id IS NULL;

CREATE INDEX IF NOT EXISTS posts_updated_at_idx ON posts (updated_at DESC);
CREATE INDEX IF NOT EXISTS post_revisions_post_id_created_at_idx
	ON post_revisions (post_id, created_at DESC, id DESC);
CREATE INDEX IF NOT EXISTS jobs_pending_idx
	ON jobs (status, kind, created_at, id);
`

// Post is the latest author-facing revision stored for a blog post.
type Post struct {
	ID           int64
	RevisionID   int64
	Title        string
	Tags         []string
	Source       string
	Suggested    string
	DiffHTML     string
	AutoPublish  bool
	Status       string
	CheckStatus  string
	CheckSummary string
	CreatedAt    time.Time
	UpdatedAt    time.Time
}

// Job is a durable worker task joined to the source revision it should process.
type Job struct {
	ID         int64
	Kind       string
	PostID     int64
	RevisionID int64
	Attempts   int
	Source     string
}

// GrammarResult is the persisted output of the grammar checker.
type GrammarResult struct {
	SuggestedSource string `json:"suggested_source"`
	Summary         string `json:"summary"`
}

// CreateInput contains author form data for a new post.
type CreateInput struct {
	Title       string
	Tags        []string
	Source      string
	AutoPublish bool
}

// UpdateInput contains author form data for a new immutable revision.
type UpdateInput struct {
	Title       string
	Tags        []string
	Source      string
	AutoPublish bool
}

// Validate checks that the post has the minimum data needed to enter checks.
func (input CreateInput) Validate() error {
	return validateInput(input.Title, input.Tags, input.Source)
}

// Validate checks that the revision has the minimum data needed to enter checks.
func (input UpdateInput) Validate() error {
	return validateInput(input.Title, input.Tags, input.Source)
}

func validateInput(title string, tags []string, source string) error {
	if strings.TrimSpace(title) == "" {
		return errors.New("title is required")
	}
	if strings.TrimSpace(source) == "" {
		return errors.New("source is required")
	}
	for _, tag := range tags {
		if !isValidTag(tag) {
			return fmt.Errorf("tag %q must be a lowercase slug", tag)
		}
	}
	return nil
}

// ParseTags normalizes a comma-separated author tag list.
func ParseTags(raw string) []string {
	seen := make(map[string]struct{})
	var tags []string
	for _, part := range strings.Split(raw, ",") {
		tag := strings.TrimSpace(part)
		if tag == "" {
			continue
		}
		if _, ok := seen[tag]; ok {
			continue
		}
		seen[tag] = struct{}{}
		tags = append(tags, tag)
	}
	return tags
}

// FormatTags serializes normalized tags for compact SQL storage.
func FormatTags(tags []string) string {
	return strings.Join(tags, ",")
}

func isValidTag(tag string) bool {
	if tag == "" {
		return false
	}
	lastHyphen := false
	for _, r := range tag {
		if r == '-' {
			if lastHyphen {
				return false
			}
			lastHyphen = true
			continue
		}
		if r < 'a' || r > 'z' {
			return false
		}
		lastHyphen = false
	}
	return !lastHyphen && tag[0] != '-'
}

// Repository persists posts in PostgreSQL.
type Repository struct {
	db *sql.DB
}

// Open connects to PostgreSQL using the pgx database/sql driver.
func Open(dsn string) (*Repository, error) {
	db, err := sql.Open("pgx", dsn)
	if err != nil {
		return nil, fmt.Errorf("open database: %w", err)
	}
	if err := db.Ping(); err != nil {
		_ = db.Close()
		return nil, fmt.Errorf("ping database: %w", err)
	}
	return &Repository{db: db}, nil
}

// Close closes the underlying database handle.
func (repo *Repository) Close() error {
	return repo.db.Close()
}

// Migrate applies the blog post schema.
func (repo *Repository) Migrate(ctx context.Context) error {
	if _, err := repo.db.ExecContext(ctx, Schema); err != nil {
		return fmt.Errorf("apply posts schema: %w", err)
	}
	return nil
}

// Create stores a post as the latest revision awaiting checks.
func (repo *Repository) Create(ctx context.Context, input CreateInput) (Post, error) {
	if err := input.Validate(); err != nil {
		return Post{}, err
	}

	tx, err := repo.db.BeginTx(ctx, nil)
	if err != nil {
		return Post{}, fmt.Errorf("begin create post: %w", err)
	}
	defer func() {
		_ = tx.Rollback()
	}()

	var postID int64
	err = tx.QueryRowContext(ctx, `
INSERT INTO posts (title, tags, source, auto_publish, status)
VALUES ($1, $2, $3, $4, $5)
RETURNING id
`,
		strings.TrimSpace(input.Title),
		FormatTags(input.Tags),
		strings.TrimSpace(input.Source),
		input.AutoPublish,
		StatusPendingCheck,
	).Scan(&postID)
	if err != nil {
		return Post{}, fmt.Errorf("create post: %w", err)
	}

	var revisionID int64
	err = tx.QueryRowContext(ctx, `
INSERT INTO post_revisions (post_id, source)
VALUES ($1, $2)
RETURNING id
`, postID, strings.TrimSpace(input.Source)).Scan(&revisionID)
	if err != nil {
		return Post{}, fmt.Errorf("create post revision: %w", err)
	}

	if _, err := tx.ExecContext(ctx, `
UPDATE posts
SET latest_revision_id = $1
WHERE id = $2
`, revisionID, postID); err != nil {
		return Post{}, fmt.Errorf("set latest revision: %w", err)
	}
	if err := enqueueGrammarCheck(ctx, tx, postID, revisionID); err != nil {
		return Post{}, err
	}
	if err := tx.Commit(); err != nil {
		return Post{}, fmt.Errorf("commit create post: %w", err)
	}

	return repo.Get(ctx, postID)
}

// Update creates a new immutable revision and makes it the author-facing one.
func (repo *Repository) Update(ctx context.Context, id int64, input UpdateInput) (Post, error) {
	if err := input.Validate(); err != nil {
		return Post{}, err
	}

	tx, err := repo.db.BeginTx(ctx, nil)
	if err != nil {
		return Post{}, fmt.Errorf("begin update post: %w", err)
	}
	defer func() {
		_ = tx.Rollback()
	}()

	var revisionID int64
	err = tx.QueryRowContext(ctx, `
INSERT INTO post_revisions (post_id, source)
VALUES ($1, $2)
RETURNING id
`, id, strings.TrimSpace(input.Source)).Scan(&revisionID)
	if err != nil {
		return Post{}, fmt.Errorf("create post revision: %w", err)
	}

	result, err := tx.ExecContext(ctx, `
UPDATE posts
SET title = $1,
	tags = $2,
	source = $3,
	auto_publish = $4,
	status = $5,
	check_status = '',
	latest_revision_id = $6,
	updated_at = now()
WHERE id = $7
`,
		strings.TrimSpace(input.Title),
		FormatTags(input.Tags),
		strings.TrimSpace(input.Source),
		input.AutoPublish,
		StatusPendingCheck,
		revisionID,
		id,
	)
	if err != nil {
		return Post{}, fmt.Errorf("update post: %w", err)
	}
	if rows, err := result.RowsAffected(); err != nil {
		return Post{}, fmt.Errorf("count updated posts: %w", err)
	} else if rows == 0 {
		return Post{}, sql.ErrNoRows
	}
	if err := enqueueGrammarCheck(ctx, tx, id, revisionID); err != nil {
		return Post{}, err
	}
	if err := tx.Commit(); err != nil {
		return Post{}, fmt.Errorf("commit update post: %w", err)
	}

	return repo.Get(ctx, id)
}

// ListLatest returns posts ordered for the author table.
func (repo *Repository) ListLatest(ctx context.Context) ([]Post, error) {
	rows, err := repo.db.QueryContext(ctx, `
SELECT p.id, COALESCE(r.id, 0), p.title, p.tags, COALESCE(r.source, p.source),
	COALESCE(r.suggested_source, ''), COALESCE(r.diff_html, ''),
	p.auto_publish, p.status, p.check_status, COALESCE(r.check_summary, ''),
	p.created_at, p.updated_at
FROM posts p
LEFT JOIN post_revisions r ON r.id = p.latest_revision_id
WHERE p.status <> $1
ORDER BY p.updated_at DESC, p.id DESC
`, StatusArchived)
	if err != nil {
		return nil, fmt.Errorf("list posts: %w", err)
	}
	defer func() {
		_ = rows.Close()
	}()

	var result []Post
	for rows.Next() {
		var post Post
		var tags string
		if err := rows.Scan(
			&post.ID,
			&post.RevisionID,
			&post.Title,
			&tags,
			&post.Source,
			&post.Suggested,
			&post.DiffHTML,
			&post.AutoPublish,
			&post.Status,
			&post.CheckStatus,
			&post.CheckSummary,
			&post.CreatedAt,
			&post.UpdatedAt,
		); err != nil {
			return nil, fmt.Errorf("scan post: %w", err)
		}
		post.Tags = ParseTags(tags)
		result = append(result, post)
	}
	if err := rows.Err(); err != nil {
		return nil, fmt.Errorf("iterate posts: %w", err)
	}
	return result, nil
}

// Get returns one author-facing post with its latest revision artifacts.
func (repo *Repository) Get(ctx context.Context, id int64) (Post, error) {
	var post Post
	var tags string
	err := repo.db.QueryRowContext(ctx, `
SELECT p.id, COALESCE(r.id, 0), p.title, p.tags, COALESCE(r.source, p.source),
	COALESCE(r.suggested_source, ''), COALESCE(r.diff_html, ''),
	p.auto_publish, p.status, p.check_status, COALESCE(r.check_summary, ''),
	p.created_at, p.updated_at
FROM posts p
LEFT JOIN post_revisions r ON r.id = p.latest_revision_id
WHERE p.id = $1
`, id).Scan(
		&post.ID,
		&post.RevisionID,
		&post.Title,
		&tags,
		&post.Source,
		&post.Suggested,
		&post.DiffHTML,
		&post.AutoPublish,
		&post.Status,
		&post.CheckStatus,
		&post.CheckSummary,
		&post.CreatedAt,
		&post.UpdatedAt,
	)
	if err != nil {
		return Post{}, fmt.Errorf("get post: %w", err)
	}
	post.Tags = ParseTags(tags)
	return post, nil
}

// LeaseGrammarJob atomically claims one due grammar-check job for a worker.
func (repo *Repository) LeaseGrammarJob(ctx context.Context, lease time.Duration) (Job, bool, error) {
	tx, err := repo.db.BeginTx(ctx, nil)
	if err != nil {
		return Job{}, false, fmt.Errorf("begin lease job: %w", err)
	}
	defer func() {
		_ = tx.Rollback()
	}()

	var job Job
	err = tx.QueryRowContext(ctx, `
WITH next AS (
	SELECT id
	FROM jobs
	WHERE kind = $1
		AND (
			status = $2
			OR (status = $3 AND lease_until < now())
		)
	ORDER BY created_at, id
	LIMIT 1
	FOR UPDATE SKIP LOCKED
), claimed AS (
	UPDATE jobs
	SET status = $3,
		attempts = attempts + 1,
		lease_until = now() + ($4 || ' seconds')::interval,
		updated_at = now()
	FROM next
	WHERE jobs.id = next.id
	RETURNING jobs.id, jobs.kind, jobs.post_id, jobs.revision_id, jobs.attempts
)
SELECT claimed.id, claimed.kind, claimed.post_id, claimed.revision_id,
	claimed.attempts, post_revisions.source
FROM claimed
JOIN post_revisions ON post_revisions.id = claimed.revision_id
`, JobKindGrammarCheck, JobStatusPending, JobStatusRunning, int(lease.Seconds())).Scan(
		&job.ID,
		&job.Kind,
		&job.PostID,
		&job.RevisionID,
		&job.Attempts,
		&job.Source,
	)
	if errors.Is(err, sql.ErrNoRows) {
		return Job{}, false, nil
	}
	if err != nil {
		return Job{}, false, fmt.Errorf("lease grammar job: %w", err)
	}
	if err := tx.Commit(); err != nil {
		return Job{}, false, fmt.Errorf("commit lease job: %w", err)
	}
	return job, true, nil
}

// CompleteGrammarJob stores checker output and updates author-facing status.
func (repo *Repository) CompleteGrammarJob(ctx context.Context, job Job, result GrammarResult) error {
	tx, err := repo.db.BeginTx(ctx, nil)
	if err != nil {
		return fmt.Errorf("begin complete job: %w", err)
	}
	defer func() {
		_ = tx.Rollback()
	}()

	suggested := strings.TrimSpace(result.SuggestedSource)
	source := strings.TrimSpace(job.Source)
	if suggested == "" {
		suggested = source
	}
	status := StatusReady
	checkStatus := CheckStatusSuccess
	diffHTML := ""
	suggestedForStorage := ""
	if suggested != source {
		status = StatusNeedsChanges
		checkStatus = CheckStatusFailed
		suggestedForStorage = suggested
		diffHTML = renderDiffHTML(source, suggested)
	}

	if _, err := tx.ExecContext(ctx, `
UPDATE post_revisions
SET suggested_source = $1,
	diff_html = $2,
	check_status = $3,
	check_summary = $4
WHERE id = $5
`,
		suggestedForStorage,
		diffHTML,
		checkStatus,
		strings.TrimSpace(result.Summary),
		job.RevisionID,
	); err != nil {
		return fmt.Errorf("store grammar result: %w", err)
	}
	if _, err := tx.ExecContext(ctx, `
UPDATE posts
SET status = $1,
	check_status = $2,
	updated_at = now()
WHERE id = $3 AND latest_revision_id = $4
`, status, checkStatus, job.PostID, job.RevisionID); err != nil {
		return fmt.Errorf("update post check status: %w", err)
	}
	if _, err := tx.ExecContext(ctx, `
UPDATE jobs
SET status = $1,
	lease_until = NULL,
	updated_at = now()
WHERE id = $2
`, JobStatusSucceeded, job.ID); err != nil {
		return fmt.Errorf("mark job succeeded: %w", err)
	}
	if err := tx.Commit(); err != nil {
		return fmt.Errorf("commit complete job: %w", err)
	}
	return nil
}

// FailGrammarJob records a technical checker failure.
func (repo *Repository) FailGrammarJob(ctx context.Context, job Job, cause error) error {
	tx, err := repo.db.BeginTx(ctx, nil)
	if err != nil {
		return fmt.Errorf("begin fail job: %w", err)
	}
	defer func() {
		_ = tx.Rollback()
	}()

	message := ""
	if cause != nil {
		message = cause.Error()
	}
	if _, err := tx.ExecContext(ctx, `
UPDATE post_revisions
SET check_status = $1,
	check_summary = $2
WHERE id = $3
`, CheckStatusFailed, message, job.RevisionID); err != nil {
		return fmt.Errorf("store failure result: %w", err)
	}
	if _, err := tx.ExecContext(ctx, `
UPDATE posts
SET status = $1,
	check_status = $2,
	updated_at = now()
WHERE id = $3 AND latest_revision_id = $4
`, StatusError, CheckStatusFailed, job.PostID, job.RevisionID); err != nil {
		return fmt.Errorf("update post failure status: %w", err)
	}
	if _, err := tx.ExecContext(ctx, `
UPDATE jobs
SET status = $1,
	lease_until = NULL,
	last_error = $2,
	updated_at = now()
WHERE id = $3
`, JobStatusFailed, message, job.ID); err != nil {
		return fmt.Errorf("mark job failed: %w", err)
	}
	if err := tx.Commit(); err != nil {
		return fmt.Errorf("commit fail job: %w", err)
	}
	return nil
}

func enqueueGrammarCheck(ctx context.Context, tx *sql.Tx, postID int64, revisionID int64) error {
	if _, err := tx.ExecContext(ctx, `
INSERT INTO jobs (kind, post_id, revision_id, status)
VALUES ($1, $2, $3, $4)
`, JobKindGrammarCheck, postID, revisionID, JobStatusPending); err != nil {
		return fmt.Errorf("enqueue grammar check: %w", err)
	}
	return nil
}

func renderDiffHTML(original string, suggested string) string {
	var builder strings.Builder
	builder.WriteString(`<pre class="diff">`)
	builder.WriteString(`<span class="diff-line del">- `)
	builder.WriteString(html.EscapeString(original))
	builder.WriteString(`</span>`)
	builder.WriteString("\n")
	builder.WriteString(`<span class="diff-line add">+ `)
	builder.WriteString(html.EscapeString(suggested))
	builder.WriteString(`</span>`)
	builder.WriteString(`</pre>`)
	return builder.String()
}
