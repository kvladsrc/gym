terraform {
  required_providers {
    minio = {
      source  = "aminueza/minio"
      version = "3.46.4"
    }
  }

  required_version = ">= 1.6"
}
