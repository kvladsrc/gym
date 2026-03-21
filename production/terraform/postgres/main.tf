terraform {
  required_providers {
    postgresql = {
      source  = "cyrilgdn/postgresql"
      version = "~> 1.25"
    }
  }

  required_version = ">= 1.6"
}

provider "postgresql" {
  host     = var.postgres_host
  port     = var.postgres_port
  username = "postgres"
  password = var.postgres_password
  sslmode  = "disable"

  # Allow terraform to create databases and roles
  superuser = false
}
