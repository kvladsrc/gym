terraform {
  required_providers {
    minio = {
      source  = "aminueza/minio"
      version = "3.43.0"
    }
  }

  required_version = ">= 1.6"
}
