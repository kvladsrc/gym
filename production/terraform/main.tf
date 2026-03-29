terraform {
  required_providers {
    authentik = {
      source  = "goauthentik/authentik"
      version = "~> 2024.2"
    }
    pihole = {
      source  = "iolave/pihole"
      version = "0.2.2"
    }
    postgresql = {
      source  = "cyrilgdn/postgresql"
      version = "~> 1.25"
    }
  }

  required_version = ">= 1.6"
}

module "authentik" {
  source = "./modules/authentik"

  # OAuth2 secrets
  hedgedoc_oauth2_client_secret = var.hedgedoc_oauth2_client_secret
  minio_oauth2_client_secret    = var.minio_oauth2_client_secret
}

module "pihole" {
  source = "./modules/pihole"
}

module "postgres" {
  source = "./modules/postgres"

  # Per-service passwords
  postgres_authentik_password = var.postgres_authentik_password
  postgres_hedgedoc_password  = var.postgres_hedgedoc_password
  postgres_miniflux_password  = var.postgres_miniflux_password
  postgres_zuul_password      = var.postgres_zuul_password
}
