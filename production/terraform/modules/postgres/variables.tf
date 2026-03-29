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
