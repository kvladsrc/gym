# Authentik module - manages SSO, proxy providers, and applications
# Provider configuration is inherited from the root module
data "authentik_flow" "default_authorization_flow" {
  slug = "default-provider-authorization-implicit-consent"
}

data "authentik_flow" "default_invalidation_flow" {
  slug = "default-provider-invalidation-flow"

}
