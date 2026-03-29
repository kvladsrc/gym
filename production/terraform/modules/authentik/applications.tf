# Create authentik applications for each service
resource "authentik_application" "apps" {
  for_each = var.applications

  name              = each.value.name
  slug              = each.value.slug
  protocol_provider = authentik_provider_proxy.proxy_providers[each.key].id
}
