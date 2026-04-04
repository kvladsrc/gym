# outputs.tf (main)
output "registry_access_key" {
  value = module.minio.minio_access_keys["registry"]
}

output "registry_secret_key" {
  value     = module.minio.minio_secret_keys["registry"]
  sensitive = true
}

output "bb_access_key" {
  value = module.minio.minio_access_keys["buildbuddy"]
}

output "bb_secret_key" {
  value     = module.minio.minio_secret_keys["buildbuddy"]
  sensitive = true
}

output "zuul_access_key" {
  value = module.minio.minio_access_keys["zuul"]
}

output "zuul_secret_key" {
  value     = module.minio.minio_secret_keys["zuul"]
  sensitive = true
}
