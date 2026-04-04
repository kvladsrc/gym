provider "authentik" {
  url      = var.authentik_url
  token    = var.authentik_token
  insecure = var.authentik_insecure
}

provider "pihole" {
  url      = var.pihole_endpoint
  password = var.pihole_password
}

provider "postgresql" {
  host     = var.postgres_host
  port     = var.postgres_port
  username = "postgres"
  password = var.postgres_admin_password
  sslmode  = "disable"

  # Allow terraform to create databases and roles
  superuser = false
}

provider "minio" {
  minio_server     = var.minio_endpoint
  minio_access_key = var.minio_access_key
  minio_secret_key = var.minio_secret_key
  minio_insecure   = true
}
