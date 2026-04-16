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
  description = "Authentik instance URL"
  type        = string
  default     = "http://authentik-server.authentik.svc.cluster.local"
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

# MinIO
variable "minio_endpoint" {
  description = "MinIO endpoint"
  type        = string
}

variable "minio_access_key" {
  description = "MinIO access key"
  type        = string
  sensitive   = true
}

variable "minio_secret_key" {
  description = "MinIO secret key"
  type        = string
  sensitive   = true
}

# Cloudflare
variable "cloudflare_api_token" {
  description = "Cloudflare API Token"
  type        = string
  sensitive   = true
}

variable "cloudflare_zone_id" {
  description = "Cloudflare Zone ID for your.domain"
  type        = string
}
