# Flux service account user
resource "authentik_user" "flux_service" {
  username  = "flux"
  name      = "Flux CD"
  email     = "flux@bonfirelit.dev"
  is_active = true

  # Service accounts typically don't need passwords for OAuth2
  # Password will be managed separately if needed
  attributes = jsonencode({
    "cn"          = "flux"
    "dn"          = "cn=flux,ou=users,dc=bonfire,dc=lit"
    "sn"          = "flux"
    "mail"        = "flux@bonfirelit.dev"
    "displayName" = "FluxCD"
  })
}
