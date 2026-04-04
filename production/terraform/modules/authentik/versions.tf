terraform {
  required_providers {
    authentik = {
      source  = "goauthentik/authentik"
      version = "~> 2025.0"
    }
  }

  required_version = ">= 1.6"
}
