terraform {
  required_providers {
    minio = {
      source  = "aminueza/minio"
      version = "3.40.1"
    }
  }

  required_version = ">= 1.6"
}
