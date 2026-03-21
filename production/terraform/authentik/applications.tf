# Data source to reference the default authorization flow
data "authentik_flow" "default_authorization_flow" {
  slug = "default-provider-authorization-explicit-consent"
}

# Data source to reference the default invalidation flow
data "authentik_flow" "default_invalidation_flow" {
  slug = "default-provider-invalidation-flow"
}

# Create authentik applications for each service
resource "authentik_application" "apps" {
  for_each = var.applications

  name              = each.value.name
  slug              = each.value.slug
  protocol_provider = authentik_provider_proxy.proxy_providers[each.key].id
}
