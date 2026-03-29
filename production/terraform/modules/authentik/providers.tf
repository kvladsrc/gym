# Create proxy providers for each application
resource "authentik_provider_proxy" "proxy_providers" {
  for_each = var.applications

  name               = "Provider for ${each.value.slug}"
  authorization_flow = data.authentik_flow.default_authorization_flow.id
  invalidation_flow  = data.authentik_flow.default_invalidation_flow.id

  external_host                = each.value.external_host
  internal_host                = "http://${each.value.internal_host}:${each.value.internal_port}"
  internal_host_ssl_validation = each.value.internal_host_ssl_validation

  mode = each.value.mode

  basic_auth_enabled            = true
  basic_auth_username_attribute = "cn"
  basic_auth_password_attribute = "cn" # pragma: allowlist secret

  # Token validity
  access_token_validity = each.value.access_token_validity

  # Skip path regex for health checks and public endpoints
  skip_path_regex = each.value.skip_path_regex
}
