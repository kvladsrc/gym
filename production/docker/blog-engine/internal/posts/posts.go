package posts

import (
	"context"
	"database/sql"
	"encoding/json"
	"errors"
	"fmt"
	"html"
	"strings"
	"time"

	"github.com/jackc/pgx/v5/stdlib"
	"github.com/pmezard/go-difflib/difflib"
)

var _ = stdlib.GetDefaultDriver

const (
	// StatusPending means a revision is waiting for checks.
	StatusPending = "pending"
	// StatusChecking means a revision is being checked.
	StatusChecking = "checking"
	// StatusNeedsChanges means checks found author-visible grammar issues.
	StatusNeedsChanges = "needs_changes"
	// StatusReady means checks passed and the post can be published.
	StatusReady = "ready"
	// StatusPendingPublish means a revision is waiting to be published.
	StatusPendingPublish = "pending_publish"
	// StatusPublishing means a revision is being published.
	StatusPublishing = "publishing"
	// StatusPublished means a revision is currently published.
	StatusPublished = "published"
	// StatusError means the latest revision failed for a technical reason.
	StatusError = "error"

	// CheckKindGrammar identifies the initial OpenAI grammar check.
	CheckKindGrammar = "grammar"
	// CheckStatusRunning means a check is currently executing.
	CheckStatusRunning = "running"
	// CheckStatusSuccess means a check completed successfully.
	CheckStatusSuccess = "success"
	// CheckStatusFailed means a check found author-visible issues.
	CheckStatusFailed = "failed"
	// CheckStatusError means a check failed for a technical reason.
	CheckStatusError = "error"
)

// Schema creates or updates the blog post schema without deleting existing data.
const Schema = `
CREATE TABLE IF NOT EXISTS posts (
	id BIGSERIAL PRIMARY KEY,
	latest_revision_id BIGINT,
	published_revision_id BIGINT,
	published_path TEXT NOT NULL DEFAULT '',
	created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
	updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE posts ADD COLUMN IF NOT EXISTS latest_revision_id BIGINT;
ALTER TABLE posts ADD COLUMN IF NOT EXISTS published_revision_id BIGINT;
ALTER TABLE posts ADD COLUMN IF NOT EXISTS published_path TEXT NOT NULL DEFAULT '';
ALTER TABLE posts ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE posts ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT now();

CREATE TABLE IF NOT EXISTS revisions (
	id BIGSERIAL PRIMARY KEY,
	post_id BIGINT NOT NULL REFERENCES posts(id) ON DELETE CASCADE,
	latest_check_id BIGINT,
	title TEXT NOT NULL,
	tags TEXT NOT NULL DEFAULT '',
	source TEXT NOT NULL,
	auto_publish BOOLEAN NOT NULL DEFAULT false,
	status TEXT NOT NULL,
	created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
	updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE revisions ADD COLUMN IF NOT EXISTS post_id BIGINT;
ALTER TABLE revisions ADD COLUMN IF NOT EXISTS latest_check_id BIGINT;
ALTER TABLE revisions ADD COLUMN IF NOT EXISTS title TEXT NOT NULL DEFAULT '';
ALTER TABLE revisions ADD COLUMN IF NOT EXISTS tags TEXT NOT NULL DEFAULT '';
ALTER TABLE revisions ADD COLUMN IF NOT EXISTS source TEXT NOT NULL DEFAULT '';
ALTER TABLE revisions ADD COLUMN IF NOT EXISTS auto_publish BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE revisions ADD COLUMN IF NOT EXISTS status TEXT NOT NULL DEFAULT '';
ALTER TABLE revisions ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE revisions ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ NOT NULL DEFAULT now();

CREATE TABLE IF NOT EXISTS checks (
	id BIGSERIAL PRIMARY KEY,
	revision_id BIGINT NOT NULL REFERENCES revisions(id) ON DELETE CASCADE,
	kind TEXT NOT NULL,
	status TEXT NOT NULL,
	request_json JSONB NOT NULL DEFAULT '{}'::jsonb,
	response_json JSONB NOT NULL DEFAULT '{}'::jsonb,
	error TEXT NOT NULL DEFAULT '',
	created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
	finished_at TIMESTAMPTZ
);

ALTER TABLE checks ADD COLUMN IF NOT EXISTS revision_id BIGINT;
ALTER TABLE checks ADD COLUMN IF NOT EXISTS kind TEXT NOT NULL DEFAULT '';
ALTER TABLE checks ADD COLUMN IF NOT EXISTS status TEXT NOT NULL DEFAULT '';
ALTER TABLE checks ADD COLUMN IF NOT EXISTS request_json JSONB NOT NULL DEFAULT '{}'::jsonb;
ALTER TABLE checks ADD COLUMN IF NOT EXISTS response_json JSONB NOT NULL DEFAULT '{}'::jsonb;
ALTER TABLE checks ADD COLUMN IF NOT EXISTS error TEXT NOT NULL DEFAULT '';
ALTER TABLE checks ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT now();
ALTER TABLE checks ADD COLUMN IF NOT EXISTS finished_at TIMESTAMPTZ;

DO $$
BEGIN
	IF NOT EXISTS (
		SELECT 1 FROM pg_constraint WHERE conname = 'posts_latest_revision_id_fkey'
	) THEN
		ALTER TABLE posts
			ADD CONSTRAINT posts_latest_revision_id_fkey
			FOREIGN KEY (latest_revision_id) REFERENCES revisions(id);
	END IF;
	IF NOT EXISTS (
		SELECT 1 FROM pg_constraint WHERE conname = 'posts_published_revision_id_fkey'
	) THEN
		ALTER TABLE posts
			ADD CONSTRAINT posts_published_revision_id_fkey
			FOREIGN KEY (published_revision_id) REFERENCES revisions(id);
	END IF;
	IF NOT EXISTS (
		SELECT 1 FROM pg_constraint WHERE conname = 'revisions_post_id_fkey'
	) THEN
		ALTER TABLE revisions
			ADD CONSTRAINT revisions_post_id_fkey
			FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE CASCADE;
	END IF;
	IF NOT EXISTS (
		SELECT 1 FROM pg_constraint WHERE conname = 'revisions_latest_check_id_fkey'
	) THEN
		ALTER TABLE revisions
			ADD CONSTRAINT revisions_latest_check_id_fkey
			FOREIGN KEY (latest_check_id) REFERENCES checks(id);
	END IF;
	IF NOT EXISTS (
		SELECT 1 FROM pg_constraint WHERE conname = 'checks_revision_id_fkey'
	) THEN
		ALTER TABLE checks
			ADD CONSTRAINT checks_revision_id_fkey
			FOREIGN KEY (revision_id) REFERENCES revisions(id) ON DELETE CASCADE;
	END IF;
END $$;

CREATE INDEX IF NOT EXISTS posts_updated_at_idx ON posts (updated_at DESC);
CREATE INDEX IF NOT EXISTS revisions_post_id_created_at_idx
	ON revisions (post_id, created_at DESC, id DESC);
CREATE INDEX IF NOT EXISTS revisions_pending_idx
	ON revisions (status, updated_at, id);
CREATE INDEX IF NOT EXISTS checks_revision_id_created_at_idx
	ON checks (revision_id, created_at DESC, id DESC);
`

// Post is the latest author-facing revision stored for a blog post.
type Post struct {
	ID                  int64
	RevisionID          int64
	PublishedRevisionID int64
	PublishedPath       string
	Title               string
	Tags                []string
	Source              string
	Suggested           string
	DiffHTML            string
	AutoPublish         bool
	Status              string
	CheckStatus         string
	CheckSummary        string
	CreatedAt           time.Time
	UpdatedAt           time.Time
}

// GrammarResult is the persisted output of the grammar checker.
type GrammarResult struct {
	SuggestedSource string `json:"suggested_source"`
	Summary         string `json:"summary"`
	DiffHTML        string `json:"diff_html,omitempty"`
}

// GrammarRequest is the persisted logical request for a grammar check.
type GrammarRequest struct {
	Title  string   `json:"title"`
	Tags   []string `json:"tags"`
	Source string   `json:"source"`
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
INSERT INTO posts DEFAULT VALUES
RETURNING id
`).Scan(&postID)
	if err != nil {
		return Post{}, fmt.Errorf("create post: %w", err)
	}

	var revisionID int64
	err = tx.QueryRowContext(ctx, `
INSERT INTO revisions (post_id, title, tags, source, auto_publish, status)
VALUES ($1, $2, $3, $4, $5, $6)
RETURNING id
`,
		postID,
		strings.TrimSpace(input.Title),
		FormatTags(input.Tags),
		strings.TrimSpace(input.Source),
		input.AutoPublish,
		StatusPending,
	).Scan(&revisionID)
	if err != nil {
		return Post{}, fmt.Errorf("create post revision: %w", err)
	}

	if _, err := tx.ExecContext(ctx, `
UPDATE posts
SET latest_revision_id = $1,
	updated_at = now()
WHERE id = $2
`, revisionID, postID); err != nil {
		return Post{}, fmt.Errorf("set latest revision: %w", err)
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
INSERT INTO revisions (post_id, title, tags, source, auto_publish, status)
VALUES ($1, $2, $3, $4, $5, $6)
RETURNING id
`,
		id,
		strings.TrimSpace(input.Title),
		FormatTags(input.Tags),
		strings.TrimSpace(input.Source),
		input.AutoPublish,
		StatusPending,
	).Scan(&revisionID)
	if err != nil {
		return Post{}, fmt.Errorf("create post revision: %w", err)
	}

	result, err := tx.ExecContext(ctx, `
UPDATE posts
SET latest_revision_id = $1,
	updated_at = now()
WHERE id = $2
`, revisionID, id)
	if err != nil {
		return Post{}, fmt.Errorf("update post: %w", err)
	}
	if rows, err := result.RowsAffected(); err != nil {
		return Post{}, fmt.Errorf("count updated posts: %w", err)
	} else if rows == 0 {
		return Post{}, sql.ErrNoRows
	}
	if err := tx.Commit(); err != nil {
		return Post{}, fmt.Errorf("commit update post: %w", err)
	}

	return repo.Get(ctx, id)
}

// Recheck marks the latest immutable revision for another grammar check.
func (repo *Repository) Recheck(ctx context.Context, id int64) (Post, error) {
	tx, err := repo.db.BeginTx(ctx, nil)
	if err != nil {
		return Post{}, fmt.Errorf("begin recheck post: %w", err)
	}
	defer func() {
		_ = tx.Rollback()
	}()

	var revisionID sql.NullInt64
	err = tx.QueryRowContext(ctx, `
SELECT latest_revision_id
FROM posts
WHERE id = $1
FOR UPDATE
`, id).Scan(&revisionID)
	if err != nil {
		return Post{}, fmt.Errorf("load post revision for recheck: %w", err)
	}
	if !revisionID.Valid {
		return Post{}, fmt.Errorf("post %d has no latest revision", id)
	}

	if _, err := tx.ExecContext(ctx, `
UPDATE revisions
SET latest_check_id = NULL,
	status = $3,
	updated_at = now()
WHERE id = $1 AND post_id = $2
`, revisionID.Int64, id, StatusPending); err != nil {
		return Post{}, fmt.Errorf("reset post check status: %w", err)
	}
	if _, err := tx.ExecContext(ctx, `
UPDATE posts
SET updated_at = now()
WHERE id = $1
`, id); err != nil {
		return Post{}, fmt.Errorf("touch post: %w", err)
	}
	if err := tx.Commit(); err != nil {
		return Post{}, fmt.Errorf("commit recheck post: %w", err)
	}

	return repo.Get(ctx, id)
}

// Publish queues the latest immutable revision for publishing.
func (repo *Repository) Publish(ctx context.Context, id int64) (Post, error) {
	tx, err := repo.db.BeginTx(ctx, nil)
	if err != nil {
		return Post{}, fmt.Errorf("begin publish post: %w", err)
	}
	defer func() {
		_ = tx.Rollback()
	}()

	var revisionID sql.NullInt64
	var status string
	err = tx.QueryRowContext(ctx, `
SELECT p.latest_revision_id, r.status
FROM posts p
JOIN revisions r ON r.id = p.latest_revision_id
WHERE p.id = $1
FOR UPDATE
`, id).Scan(&revisionID, &status)
	if err != nil {
		return Post{}, fmt.Errorf("load post revision for publish: %w", err)
	}
	if !revisionID.Valid {
		return Post{}, fmt.Errorf("post %d has no latest revision", id)
	}
	if status == StatusChecking || status == StatusPublishing {
		return Post{}, fmt.Errorf("revision %d status is %q and cannot be published yet", revisionID.Int64, status)
	}

	if _, err := tx.ExecContext(ctx, `
UPDATE revisions
SET status = $1,
	updated_at = now()
WHERE id = $2 AND post_id = $3
`, StatusPendingPublish, revisionID.Int64, id); err != nil {
		return Post{}, fmt.Errorf("queue revision publish: %w", err)
	}
	if _, err := tx.ExecContext(ctx, `
UPDATE posts
SET updated_at = now()
WHERE id = $1
`, id); err != nil {
		return Post{}, fmt.Errorf("touch post: %w", err)
	}
	if err := tx.Commit(); err != nil {
		return Post{}, fmt.Errorf("commit publish post: %w", err)
	}

	return repo.Get(ctx, id)
}

// ListLatest returns posts ordered for the author table.
func (repo *Repository) ListLatest(ctx context.Context) ([]Post, error) {
	rows, err := repo.db.QueryContext(ctx, `
SELECT p.id, r.id, COALESCE(p.published_revision_id, 0), p.published_path,
	r.title, r.tags, r.source,
	CASE WHEN c.status = 'failed'
		THEN COALESCE(c.response_json->>'suggested_source', '')
		ELSE ''
	END,
	COALESCE(c.response_json->>'diff_html', ''),
	r.auto_publish, r.status, COALESCE(c.status, ''),
	COALESCE(c.response_json->>'summary', ''),
	p.created_at, r.updated_at
FROM posts p
JOIN revisions r ON r.id = p.latest_revision_id
LEFT JOIN checks c ON c.id = r.latest_check_id
ORDER BY r.updated_at DESC, p.id DESC
`)
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
			&post.PublishedRevisionID,
			&post.PublishedPath,
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
SELECT p.id, r.id, COALESCE(p.published_revision_id, 0), p.published_path,
	r.title, r.tags, r.source,
	CASE WHEN c.status = 'failed'
		THEN COALESCE(c.response_json->>'suggested_source', '')
		ELSE ''
	END,
	COALESCE(c.response_json->>'diff_html', ''),
	r.auto_publish, r.status, COALESCE(c.status, ''),
	COALESCE(c.response_json->>'summary', ''),
	p.created_at, r.updated_at
FROM posts p
JOIN revisions r ON r.id = p.latest_revision_id
LEFT JOIN checks c ON c.id = r.latest_check_id
WHERE p.id = $1
`, id).Scan(
		&post.ID,
		&post.RevisionID,
		&post.PublishedRevisionID,
		&post.PublishedPath,
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

// NextPendingCheck returns the next post whose latest revision needs checking.
func (repo *Repository) NextPendingCheck(ctx context.Context) (Post, bool, error) {
	var post Post
	var tags string
	err := repo.db.QueryRowContext(ctx, `
SELECT p.id, r.id, COALESCE(p.published_revision_id, 0), p.published_path,
	r.title, r.tags, r.source,
	CASE WHEN c.status = 'failed'
		THEN COALESCE(c.response_json->>'suggested_source', '')
		ELSE ''
	END,
	COALESCE(c.response_json->>'diff_html', ''),
	r.auto_publish, r.status, COALESCE(c.status, ''),
	COALESCE(c.response_json->>'summary', ''),
	p.created_at, r.updated_at
FROM posts p
JOIN revisions r ON r.id = p.latest_revision_id
LEFT JOIN checks c ON c.id = r.latest_check_id
WHERE r.status = $1
ORDER BY r.updated_at, p.id
LIMIT 1
`, StatusPending).Scan(
		&post.ID,
		&post.RevisionID,
		&post.PublishedRevisionID,
		&post.PublishedPath,
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
	if errors.Is(err, sql.ErrNoRows) {
		return Post{}, false, nil
	}
	if err != nil {
		return Post{}, false, fmt.Errorf("get next pending check: %w", err)
	}
	post.Tags = ParseTags(tags)
	return post, true, nil
}

// StartGrammarCheck records a running grammar check for a pending revision.
func (repo *Repository) StartGrammarCheck(ctx context.Context, post Post, request GrammarRequest) (int64, error) {
	encoded, err := json.Marshal(request)
	if err != nil {
		return 0, fmt.Errorf("encode grammar check request: %w", err)
	}

	tx, err := repo.db.BeginTx(ctx, nil)
	if err != nil {
		return 0, fmt.Errorf("begin start grammar check: %w", err)
	}
	defer func() {
		_ = tx.Rollback()
	}()

	var status string
	err = tx.QueryRowContext(ctx, `
SELECT status
FROM revisions
WHERE id = $1 AND post_id = $2
FOR UPDATE
`, post.RevisionID, post.ID).Scan(&status)
	if err != nil {
		return 0, fmt.Errorf("lock revision for grammar check: %w", err)
	}
	if status != StatusPending {
		return 0, fmt.Errorf("revision %d status is %q, want %q", post.RevisionID, status, StatusPending)
	}

	var checkID int64
	err = tx.QueryRowContext(ctx, `
INSERT INTO checks (revision_id, kind, status, request_json)
VALUES ($1, $2, $3, $4::jsonb)
RETURNING id
`, post.RevisionID, CheckKindGrammar, CheckStatusRunning, string(encoded)).Scan(&checkID)
	if err != nil {
		return 0, fmt.Errorf("create grammar check: %w", err)
	}
	if _, err := tx.ExecContext(ctx, `
UPDATE revisions
SET latest_check_id = $1,
	status = $2,
	updated_at = now()
WHERE id = $3 AND post_id = $4
`, checkID, StatusChecking, post.RevisionID, post.ID); err != nil {
		return 0, fmt.Errorf("mark revision checking: %w", err)
	}
	if err := tx.Commit(); err != nil {
		return 0, fmt.Errorf("commit start grammar check: %w", err)
	}
	return checkID, nil
}

// CompleteGrammarCheck stores checker output and updates author-facing status.
func (repo *Repository) CompleteGrammarCheck(ctx context.Context, post Post, checkID int64, result GrammarResult) error {
	tx, err := repo.db.BeginTx(ctx, nil)
	if err != nil {
		return fmt.Errorf("begin complete grammar check: %w", err)
	}
	defer func() {
		_ = tx.Rollback()
	}()

	suggested := normalizeLineEndings(strings.TrimSpace(result.SuggestedSource))
	source := normalizeLineEndings(strings.TrimSpace(post.Source))
	if suggested == "" {
		suggested = source
	}
	status := StatusReady
	checkStatus := CheckStatusSuccess
	diffHTML := ""
	if suggested != source {
		status = StatusNeedsChanges
		checkStatus = CheckStatusFailed
		diffHTML = renderDiffHTML(source, suggested)
	} else if post.AutoPublish {
		status = StatusPendingPublish
	}
	result.SuggestedSource = suggested
	result.DiffHTML = diffHTML
	encoded, err := json.Marshal(result)
	if err != nil {
		return fmt.Errorf("encode grammar check response: %w", err)
	}

	if _, err := tx.ExecContext(ctx, `
UPDATE checks
SET status = $1,
	response_json = $2::jsonb,
	finished_at = now()
WHERE id = $3 AND revision_id = $4
`,
		checkStatus,
		string(encoded),
		checkID,
		post.RevisionID,
	); err != nil {
		return fmt.Errorf("store grammar result: %w", err)
	}
	if _, err := tx.ExecContext(ctx, `
UPDATE revisions
SET status = $1,
	updated_at = now()
WHERE id = $2 AND post_id = $3 AND latest_check_id = $4
`, status, post.RevisionID, post.ID, checkID); err != nil {
		return fmt.Errorf("update revision check status: %w", err)
	}
	if err := tx.Commit(); err != nil {
		return fmt.Errorf("commit complete grammar check: %w", err)
	}
	return nil
}

// FailGrammarCheck records a technical checker failure.
func (repo *Repository) FailGrammarCheck(ctx context.Context, post Post, checkID int64, cause error) error {
	tx, err := repo.db.BeginTx(ctx, nil)
	if err != nil {
		return fmt.Errorf("begin fail grammar check: %w", err)
	}
	defer func() {
		_ = tx.Rollback()
	}()

	message := ""
	if cause != nil {
		message = cause.Error()
	}
	if _, err := tx.ExecContext(ctx, `
UPDATE checks
SET status = $1,
	error = $2,
	finished_at = now()
WHERE id = $3 AND revision_id = $4
`, CheckStatusError, message, checkID, post.RevisionID); err != nil {
		return fmt.Errorf("store grammar failure: %w", err)
	}
	if _, err := tx.ExecContext(ctx, `
UPDATE revisions
SET status = $1,
	updated_at = now()
WHERE id = $2 AND post_id = $3 AND latest_check_id = $4
`, StatusError, post.RevisionID, post.ID, checkID); err != nil {
		return fmt.Errorf("update revision failure status: %w", err)
	}
	if err := tx.Commit(); err != nil {
		return fmt.Errorf("commit fail grammar check: %w", err)
	}
	return nil
}

// NextPendingPublish returns the next post whose latest revision needs publishing.
func (repo *Repository) NextPendingPublish(ctx context.Context) (Post, bool, error) {
	var post Post
	var tags string
	err := repo.db.QueryRowContext(ctx, `
SELECT p.id, r.id, COALESCE(p.published_revision_id, 0), p.published_path,
	r.title, r.tags, r.source,
	CASE WHEN c.status = 'failed'
		THEN COALESCE(c.response_json->>'suggested_source', '')
		ELSE ''
	END,
	COALESCE(c.response_json->>'diff_html', ''),
	r.auto_publish, r.status, COALESCE(c.status, ''),
	COALESCE(c.response_json->>'summary', ''),
	p.created_at, r.updated_at
FROM posts p
JOIN revisions r ON r.id = p.latest_revision_id
LEFT JOIN checks c ON c.id = r.latest_check_id
WHERE r.status = $1
ORDER BY r.updated_at, p.id
LIMIT 1
`, StatusPendingPublish).Scan(
		&post.ID,
		&post.RevisionID,
		&post.PublishedRevisionID,
		&post.PublishedPath,
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
	if errors.Is(err, sql.ErrNoRows) {
		return Post{}, false, nil
	}
	if err != nil {
		return Post{}, false, fmt.Errorf("get next pending publish: %w", err)
	}
	post.Tags = ParseTags(tags)
	return post, true, nil
}

// StartPublish marks a pending publish revision as owned by a worker.
func (repo *Repository) StartPublish(ctx context.Context, post Post) error {
	result, err := repo.db.ExecContext(ctx, `
UPDATE revisions
SET status = $1,
	updated_at = now()
WHERE id = $2
	AND post_id = $3
	AND status = $4
`, StatusPublishing, post.RevisionID, post.ID, StatusPendingPublish)
	if err != nil {
		return fmt.Errorf("start publish: %w", err)
	}
	if rows, err := result.RowsAffected(); err != nil {
		return fmt.Errorf("count publishing revisions: %w", err)
	} else if rows == 0 {
		return sql.ErrNoRows
	}
	return nil
}

// CompletePublish records the latest revision as published.
func (repo *Repository) CompletePublish(ctx context.Context, post Post, publishedPath string) error {
	tx, err := repo.db.BeginTx(ctx, nil)
	if err != nil {
		return fmt.Errorf("begin complete publish: %w", err)
	}
	defer func() {
		_ = tx.Rollback()
	}()

	result, err := tx.ExecContext(ctx, `
UPDATE posts
SET published_revision_id = $1,
	published_path = $3,
	updated_at = now()
WHERE id = $2
	AND latest_revision_id = $1
`, post.RevisionID, post.ID, strings.TrimSpace(publishedPath))
	if err != nil {
		return fmt.Errorf("set published revision: %w", err)
	}
	if rows, err := result.RowsAffected(); err != nil {
		return fmt.Errorf("count published posts: %w", err)
	} else if rows == 0 {
		return sql.ErrNoRows
	}
	result, err = tx.ExecContext(ctx, `
UPDATE revisions
SET status = $1,
	updated_at = now()
WHERE id = $2
	AND post_id = $3
	AND status = $4
`, StatusPublished, post.RevisionID, post.ID, StatusPublishing)
	if err != nil {
		return fmt.Errorf("mark revision published: %w", err)
	}
	if rows, err := result.RowsAffected(); err != nil {
		return fmt.Errorf("count published revisions: %w", err)
	} else if rows == 0 {
		return sql.ErrNoRows
	}
	if err := tx.Commit(); err != nil {
		return fmt.Errorf("commit complete publish: %w", err)
	}
	return nil
}

// FailPublish records a technical publishing failure.
func (repo *Repository) FailPublish(ctx context.Context, post Post, cause error) error {
	if _, err := repo.db.ExecContext(ctx, `
UPDATE revisions
SET status = $1,
	updated_at = now()
WHERE id = $2
	AND post_id = $3
	AND status = $4
`, StatusError, post.RevisionID, post.ID, StatusPublishing); err != nil {
		return fmt.Errorf("mark publish failed: %w", err)
	}
	return nil
}

func renderDiffHTML(original string, suggested string) string {
	original = normalizeLineEndings(original)
	suggested = normalizeLineEndings(suggested)
	if original == suggested {
		return ""
	}

	diff, err := difflib.GetUnifiedDiffString(difflib.UnifiedDiff{
		A:       difflib.SplitLines(original),
		B:       difflib.SplitLines(suggested),
		Context: 3,
	})
	if err != nil || diff == "" {
		return renderWholeTextDiffHTML(original, suggested)
	}

	var builder strings.Builder
	builder.WriteString(`<pre class="diff">`)
	for _, line := range strings.Split(strings.TrimSuffix(diff, "\n"), "\n") {
		builder.WriteString(`<span class="diff-line`)
		builder.WriteString(diffLineClass(line))
		builder.WriteString(`">`)
		builder.WriteString(html.EscapeString(line))
		builder.WriteString(`</span>`)
	}
	builder.WriteString(`</pre>`)
	return builder.String()
}

func normalizeLineEndings(text string) string {
	text = strings.ReplaceAll(text, "\r\n", "\n")
	return strings.ReplaceAll(text, "\r", "\n")
}

func renderWholeTextDiffHTML(original string, suggested string) string {
	var builder strings.Builder
	builder.WriteString(`<pre class="diff">`)
	builder.WriteString(`<span class="diff-line del">- `)
	builder.WriteString(html.EscapeString(original))
	builder.WriteString(`</span>`)
	builder.WriteString(`<span class="diff-line add">+ `)
	builder.WriteString(html.EscapeString(suggested))
	builder.WriteString(`</span>`)
	builder.WriteString(`</pre>`)
	return builder.String()
}

func diffLineClass(line string) string {
	switch {
	case strings.HasPrefix(line, "@@"):
		return " meta"
	case strings.HasPrefix(line, "+"):
		return " add"
	case strings.HasPrefix(line, "-"):
		return " del"
	default:
		return ""
	}
}
