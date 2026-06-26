package objectstore

import (
	"bytes"
	"context"
	"crypto/hmac"
	"crypto/sha256"
	"encoding/hex"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"sort"
	"strings"
	"time"

	"src/production/docker/blog-engine/internal/config"
)

// ErrNotFound means the requested object does not exist.
var ErrNotFound = errors.New("object not found")

// Object is an object store response body plus basic metadata.
type Object struct {
	Body        []byte
	ContentType string
}

// Client talks to an S3-compatible object store.
type Client struct {
	Endpoint       string
	Region         string
	AccessKey      string
	SecretKey      string
	ForcePathStyle bool
	HTTPClient     *http.Client
	Now            func() time.Time
}

// New returns a client configured from the blog engine config.
func New(cfg config.Config) Client {
	return Client{
		Endpoint:       cfg.S3.Endpoint,
		Region:         cfg.S3.Region,
		AccessKey:      cfg.S3.AccessKey,
		SecretKey:      cfg.S3.SecretKey,
		ForcePathStyle: cfg.S3.ForcePathStyle,
		HTTPClient:     http.DefaultClient,
		Now:            time.Now,
	}
}

// Put writes one object.
func (client Client) Put(ctx context.Context, bucket string, key string, contentType string, body []byte) error {
	req, err := client.request(ctx, http.MethodPut, bucket, key, body)
	if err != nil {
		return err
	}
	if contentType != "" {
		req.Header.Set("Content-Type", contentType)
	}
	resp, err := client.do(req)
	if err != nil {
		return err
	}
	defer func() {
		_ = resp.Body.Close()
	}()
	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		raw, _ := io.ReadAll(io.LimitReader(resp.Body, 4096))
		return fmt.Errorf("put object %s/%s returned %s: %s", bucket, key, resp.Status, strings.TrimSpace(string(raw)))
	}
	return nil
}

// Get reads one object.
func (client Client) Get(ctx context.Context, bucket string, key string) (Object, error) {
	req, err := client.request(ctx, http.MethodGet, bucket, key, nil)
	if err != nil {
		return Object{}, err
	}
	resp, err := client.do(req)
	if err != nil {
		return Object{}, err
	}
	defer func() {
		_ = resp.Body.Close()
	}()
	if resp.StatusCode == http.StatusNotFound {
		return Object{}, ErrNotFound
	}
	if resp.StatusCode < 200 || resp.StatusCode >= 300 {
		raw, _ := io.ReadAll(io.LimitReader(resp.Body, 4096))
		return Object{}, fmt.Errorf("get object %s/%s returned %s: %s", bucket, key, resp.Status, strings.TrimSpace(string(raw)))
	}
	body, err := io.ReadAll(io.LimitReader(resp.Body, 8<<20))
	if err != nil {
		return Object{}, fmt.Errorf("read object %s/%s: %w", bucket, key, err)
	}
	return Object{Body: body, ContentType: resp.Header.Get("Content-Type")}, nil
}

func (client Client) request(ctx context.Context, method string, bucket string, key string, body []byte) (*http.Request, error) {
	if strings.TrimSpace(bucket) == "" {
		return nil, errors.New("bucket is required")
	}
	if strings.TrimSpace(key) == "" {
		return nil, errors.New("object key is required")
	}
	objectURL, err := client.objectURL(bucket, key)
	if err != nil {
		return nil, err
	}
	req, err := http.NewRequestWithContext(ctx, method, objectURL, bytes.NewReader(body))
	if err != nil {
		return nil, fmt.Errorf("create object request: %w", err)
	}
	if err := client.sign(req, body); err != nil {
		return nil, err
	}
	return req, nil
}

func (client Client) objectURL(bucket string, key string) (string, error) {
	endpoint, err := url.Parse(client.Endpoint)
	if err != nil {
		return "", fmt.Errorf("parse object store endpoint: %w", err)
	}
	if endpoint.Scheme == "" || endpoint.Host == "" {
		return "", fmt.Errorf("object store endpoint must include scheme and host")
	}
	cleanKey := strings.TrimLeft(key, "/")
	if cleanKey == "" || strings.Contains(cleanKey, "..") {
		return "", fmt.Errorf("invalid object key %q", key)
	}
	if client.ForcePathStyle {
		endpoint.Path = joinEscapedPath(endpoint.Path, bucket, cleanKey)
		return endpoint.String(), nil
	}
	endpoint.Host = bucket + "." + endpoint.Host
	endpoint.Path = joinEscapedPath(endpoint.Path, cleanKey)
	return endpoint.String(), nil
}

func (client Client) sign(req *http.Request, body []byte) error {
	if client.Region == "" {
		return errors.New("object store region is required")
	}
	if client.AccessKey == "" {
		return errors.New("object store access key is required")
	}
	if client.SecretKey == "" {
		return errors.New("object store secret key is required")
	}

	now := time.Now
	if client.Now != nil {
		now = client.Now
	}
	requestTime := now().UTC()
	date := requestTime.Format("20060102")
	amzDate := requestTime.Format("20060102T150405Z")
	payloadHash := sha256Hex(body)

	req.Header.Set("X-Amz-Date", amzDate)
	req.Header.Set("X-Amz-Content-Sha256", payloadHash)

	headers := map[string]string{
		"host":                 req.URL.Host,
		"x-amz-content-sha256": payloadHash,
		"x-amz-date":           amzDate,
	}
	signedHeaderNames := make([]string, 0, len(headers))
	for name := range headers {
		signedHeaderNames = append(signedHeaderNames, name)
	}
	sort.Strings(signedHeaderNames)

	var canonicalHeaders strings.Builder
	for _, name := range signedHeaderNames {
		canonicalHeaders.WriteString(name)
		canonicalHeaders.WriteByte(':')
		canonicalHeaders.WriteString(strings.TrimSpace(headers[name]))
		canonicalHeaders.WriteByte('\n')
	}
	signedHeaders := strings.Join(signedHeaderNames, ";")
	canonicalRequest := strings.Join([]string{
		req.Method,
		req.URL.EscapedPath(),
		req.URL.RawQuery,
		canonicalHeaders.String(),
		signedHeaders,
		payloadHash,
	}, "\n")

	scope := strings.Join([]string{date, client.Region, "s3", "aws4_request"}, "/")
	stringToSign := strings.Join([]string{
		"AWS4-HMAC-SHA256",
		amzDate,
		scope,
		sha256Hex([]byte(canonicalRequest)),
	}, "\n")
	signature := hex.EncodeToString(hmacSHA256(signingKey(client.SecretKey, date, client.Region), stringToSign))
	req.Header.Set("Authorization", fmt.Sprintf(
		"AWS4-HMAC-SHA256 Credential=%s/%s, SignedHeaders=%s, Signature=%s",
		client.AccessKey,
		scope,
		signedHeaders,
		signature,
	))
	return nil
}

func (client Client) do(req *http.Request) (*http.Response, error) {
	httpClient := client.HTTPClient
	if httpClient == nil {
		httpClient = http.DefaultClient
	}
	resp, err := httpClient.Do(req)
	if err != nil {
		return nil, fmt.Errorf("object store request: %w", err)
	}
	return resp, nil
}

func joinEscapedPath(base string, parts ...string) string {
	var segments []string
	for _, segment := range strings.Split(strings.Trim(base, "/"), "/") {
		if segment != "" {
			segments = append(segments, url.PathEscape(segment))
		}
	}
	for _, part := range parts {
		for _, segment := range strings.Split(strings.Trim(part, "/"), "/") {
			if segment != "" {
				segments = append(segments, url.PathEscape(segment))
			}
		}
	}
	return "/" + strings.Join(segments, "/")
}

func signingKey(secret string, date string, region string) []byte {
	dateKey := hmacSHA256([]byte("AWS4"+secret), date)
	dateRegionKey := hmacSHA256(dateKey, region)
	dateRegionServiceKey := hmacSHA256(dateRegionKey, "s3")
	return hmacSHA256(dateRegionServiceKey, "aws4_request")
}

func hmacSHA256(key []byte, value string) []byte {
	mac := hmac.New(sha256.New, key)
	_, _ = mac.Write([]byte(value))
	return mac.Sum(nil)
}

func sha256Hex(value []byte) string {
	sum := sha256.Sum256(value)
	return hex.EncodeToString(sum[:])
}
