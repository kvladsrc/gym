terraform {
  required_providers {
    authentik = {
      source  = "goauthentik/authentik"
      version = "~> 2026.0"
    }
  }

  required_version = ">= 1.6"
}
