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
