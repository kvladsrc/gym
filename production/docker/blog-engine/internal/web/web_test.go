package web

import (
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

func TestPages(t *testing.T) {
	tests := []struct {
		name    string
		path    string
		handler http.HandlerFunc
		want    []string
		forbid  []string
	}{
		{
			name:    "home renders search form",
			path:    "/",
			handler: home,
			want: []string{
				`<form method="get" action="/search">`,
				`<h1>Blog</h1>`,
			},
		},
		{
			name:    "search escapes query",
			path:    `/search?q=%22%3E%3Cscript%3Ealert(1)%3C/script%3E`,
			handler: search,
			want: []string{
				`&#34;&gt;&lt;script&gt;alert(1)&lt;/script&gt;`,
			},
			forbid: []string{
				`<script>alert(1)</script>`,
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			req := httptest.NewRequest(http.MethodGet, tt.path, nil)
			rec := httptest.NewRecorder()

			tt.handler(rec, req)

			if rec.Code != http.StatusOK {
				t.Fatalf("status = %d, want %d", rec.Code, http.StatusOK)
			}
			body := rec.Body.String()
			for _, want := range tt.want {
				if !strings.Contains(body, want) {
					t.Fatalf("body missing %q:\n%s", want, body)
				}
			}
			for _, forbid := range tt.forbid {
				if strings.Contains(body, forbid) {
					t.Fatalf("body contains forbidden %q:\n%s", forbid, body)
				}
			}
		})
	}
}
