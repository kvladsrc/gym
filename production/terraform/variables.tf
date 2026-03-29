# Pihole
variable "pihole_password" {
  description = "Admin password for Pi-hole web interface"
  type        = string
  sensitive   = true
}

variable "pihole_endpoint" {
  description = "Pi-hole API endpoint"
  type        = string
  default     = "https://dns.your.domain"
}

# Postgresql
variable "postgres_host" {
  description = "PostgreSQL host (use kubectl port-forward or cluster DNS)"
  type        = string
  default     = "postgresql.postgres.svc.cluster.local"
}

variable "postgres_port" {
  description = "PostgreSQL port"
  type        = number
  default     = 5432
}

variable "postgres_admin_password" {
  description = "Password for the 'postgres' superuser (from postgresql-wrapper secret)"
  type        = string
  sensitive   = true
}

variable "postgres_authentik_password" {
  description = "Password for authentik PostgreSQL role"
  type        = string
  sensitive   = true
  default     = "mockpassword"
}

variable "postgres_hedgedoc_password" {
  description = "Password for hedgedoc PostgreSQL role"
  type        = string
  sensitive   = true
  default     = "mockpassword"
}

variable "postgres_miniflux_password" {
  description = "Password for miniflux PostgreSQL role"
  type        = string
  sensitive   = true
  default     = "mockpassword"
}

variable "postgres_zuul_password" {
  description = "Password for zuul PostgreSQL role"
  type        = string
  sensitive   = true
  default     = "mockpassword"
}

# Authentik
variable "authentik_url" {
  description = "Authentik instance URL (e.g., https://sso.your.domain)"
  type        = string
  default     = "https://sso.your.domain"
}

variable "authentik_token" {
  description = "Authentik API token (from authentik admin UI)"
  type        = string
  sensitive   = true
}

variable "authentik_insecure" {
  description = "Allow insecure TLS connections (self-signed certificates)"
  type        = bool
  default     = true
}

variable "hedgedoc_oauth2_client_secret" {
  description = "OAuth2 client secret for HedgeDoc"
  type        = string
  sensitive   = true
}

variable "minio_oauth2_client_secret" {
  description = "OAuth2 client secret for MinIO"
  type        = string
  sensitive   = true
}
