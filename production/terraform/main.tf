terraform {
  required_providers {
    authentik = {
      source  = "goauthentik/authentik"
      version = "~> 2025.0"
    }
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 5.0"
    }
    postgresql = {
      source  = "cyrilgdn/postgresql"
      version = "~> 1.25"
    }
    minio = {
      source  = "aminueza/minio"
      version = "3.32.1"
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

module "postgres" {
  source = "./modules/postgres"

  # Per-service passwords
  postgres_authentik_password = var.postgres_authentik_password
  postgres_hedgedoc_password  = var.postgres_hedgedoc_password
  postgres_miniflux_password  = var.postgres_miniflux_password
  postgres_zuul_password      = var.postgres_zuul_password
}

module "minio" {
  source = "./modules/minio"
}

module "cloudflare" {
  source = "./modules/cloudflare"

  cloudflare_zone_id = var.cloudflare_zone_id
}
