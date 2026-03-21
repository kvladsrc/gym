# ── Connection ────────────────────────────────────────────────────────────────

variable "authentik_url" {
  description = "Authentik instance URL (e.g., https://sso.your.domain)"
  type        = string
  default     = "https://sso.your.domain"
}

variable "authentik_token" {
  description = "Authentik API token (from authentik admin UI)"
  type        = string
  sensitive   = true
  default     = "mocktoken"
}

variable "authentik_insecure" {
  description = "Allow insecure TLS connections (self-signed certificates)"
  type        = bool
  default     = true
}

# ── OAuth2 Applications ───────────────────────────────────────────────────────

variable "hedgedoc_oauth2_client_secret" {
  description = "OAuth2 client secret for HedgeDoc"
  type        = string
  sensitive   = true
  default     = ""
}

# ── Applications ──────────────────────────────────────────────────────────────

variable "applications" {
  description = "Map of applications to manage with authentik proxy."
  type = map(object({
    name                         = string
    slug                         = string
    external_host                = string
    internal_host                = string
    internal_port                = number
    mode                         = optional(string, "proxy")
    skip_path_regex              = optional(string, "")
    access_token_validity        = optional(string, "hours=16")
    internal_host_ssl_validation = optional(bool, false)
  }))
  default = {
    "zuul-web" = {
      name                  = "zuul-web"
      slug                  = "zuul-web"
      external_host         = "https://zz.your.domain"
      internal_host         = "zuul-web-minimal.zuul.svc.cluster.local"
      internal_port         = 80
      access_token_validity = "hours=24"
    }
    "miniflux" = {
      name          = "miniflux"
      slug          = "miniflux"
      external_host = "https://feed.your.domain"
      internal_host = "miniflux.miniflux.svc.cluster.local"
      internal_port = 8080
    }
    "dns" = {
      name          = "dns"
      slug          = "dns"
      external_host = "https://dns.your.domain"
      internal_host = "pihole-web.pihole.svc.cluster.local"
      internal_port = 80
    }
    "gerrit" = {
      name            = "gerrit"
      slug            = "gerrit"
      external_host   = "https://review.your.domain"
      internal_host   = "gerrit-service.gerrit-cluster.svc.cluster.local"
      internal_port   = 80
      skip_path_regex = "^/a/plugins/checks/.*"
    }
    "kanboard" = {
      name          = "kanboard"
      slug          = "kanboard"
      external_host = "https://b.your.domain"
      internal_host = "kanboard.kanboard.svc.cluster.local"
      internal_port = 80
    }
    "zuul-logs" = {
      name            = "zuul-logs"
      slug            = "zuul-logs"
      external_host   = "https://logs.your.domain"
      internal_host   = "zuul-logs-svc.zuul.svc.cluster.local"
      internal_port   = 80
      skip_path_regex = "^/.*$"
    }
    "opengrok" = {
      name          = "opengrok"
      slug          = "opengrok"
      external_host = "https://search.your.domain/"
      internal_host = "opengrok.opengrok.svc.cluster.local"
      internal_port = 8080
    }
    "sql" = {
      name          = "sql"
      slug          = "sql"
      external_host = "https://sql.your.domain"
      internal_host = "postgres-ui.postgres.svc.cluster.local"
      internal_port = 8080
    }
    "zooreader" = {
      name          = "zooreader"
      slug          = "zooreader"
      external_host = "https://read.your.domain"
      internal_host = "zooreader.zooreader.svc.cluster.local"
      internal_port = 80
    }
    "zuul" = {
      name                  = "zuul"
      slug                  = "zuul"
      external_host         = "https://ci.your.domain"
      internal_host         = "zuul-web.zuul.svc.cluster.local"
      internal_port         = 9000
      skip_path_regex       = "^/api/.*"
      access_token_validity = "hours=24"
    }
  }
}
