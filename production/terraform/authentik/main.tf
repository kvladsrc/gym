terraform {
  required_providers {
    authentik = {
      source  = "goauthentik/authentik"
      version = "~> 2024.2"
    }
  }

  required_version = ">= 1.6"
}

provider "authentik" {
  url   = var.authentik_url
  token = var.authentik_token

  # Set to true if using self-signed certificates
  insecure = var.authentik_insecure
}
