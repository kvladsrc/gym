terraform {
  required_providers {
    minio = {
      source  = "aminueza/minio"
      version = "3.38.5"
    }
  }

  required_version = ">= 1.6"
}
