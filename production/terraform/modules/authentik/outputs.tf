output "headlamp_oauth2_client_secret" {
  value     = authentik_provider_oauth2.headlamp.client_secret
  sensitive = true
}
