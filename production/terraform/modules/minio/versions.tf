terraform {
  required_providers {
    minio = {
      source  = "aminueza/minio"
      version = "3.28.1"
    }
  }

  required_version = ">= 1.6"
}
