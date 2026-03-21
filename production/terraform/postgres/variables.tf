# ── Connection ────────────────────────────────────────────────────────────────

variable "postgres_host" {
  description = "PostgreSQL host (use kubectl port-forward or cluster DNS)"
  type        = string
  default     = "localhost"
}

variable "postgres_port" {
  description = "PostgreSQL port"
  type        = number
  default     = 5432
}

variable "postgres_password" {
  description = "Password for the 'postgres' superuser (from postgresql-wrapper secret)"
  type        = string
  sensitive   = true
  default     = "mockpassword"
}

# ── Per-service passwords ──────────────────────────────────────────────────────

variable "authentik_password" {
  description = "Password for authentik PostgreSQL role"
  type        = string
  sensitive   = true
  default     = "mockpassword"
}

variable "hedgedoc_password" {
  description = "Password for hedgedoc PostgreSQL role"
  type        = string
  sensitive   = true
  default     = "mockpassword"
}

variable "miniflux_password" {
  description = "Password for miniflux PostgreSQL role"
  type        = string
  sensitive   = true
  default     = "mockpassword"
}

variable "zuul_password" {
  description = "Password for zuul PostgreSQL role"
  type        = string
  sensitive   = true
  default     = "mockpassword"
}
