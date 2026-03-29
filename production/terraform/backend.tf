# Terraform state backend configuration
# Uses S3-compatible storage (MinIO) for state file
terraform {
  backend "s3" {
    bucket = "terraform-state"
    endpoints = {
      s3 = "http://minio.minio.svc.cluster.local:9000"
    }

    key = "production/terraform.tfstate"

    region = "us-east-1"

    skip_credentials_validation = true
    skip_metadata_api_check     = true
    skip_region_validation      = true
    use_path_style              = true
    skip_requesting_account_id  = true
  }
}
