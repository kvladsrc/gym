terraform {
  required_providers {
    pihole = {
      source  = "iolave/pihole"
      version = "0.2.2"
    }
  }

  required_version = ">= 1.6"
}
