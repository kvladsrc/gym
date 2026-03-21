# ── Data Sources ──────────────────────────────────────────────────────────────

data "authentik_certificate_key_pair" "default" {
  name = "authentik Self-signed Certificate"
}

data "authentik_property_mapping_provider_scope" "oauth2_scopes" {
  managed_list = [
    "goauthentik.io/providers/oauth2/scope-openid",
    "goauthentik.io/providers/oauth2/scope-email",
    "goauthentik.io/providers/oauth2/scope-profile",
  ]
}

# ── HedgeDoc OAuth2 Provider ─────────────────────────────────────────────────

resource "authentik_provider_oauth2" "hedgedoc" {
  name               = "HedgeDoc OAuth2"
  client_id          = "271wmrr5X5pPDMWB7WG4a3LUrRAaUUaM8ILHiPba"
  client_secret      = var.hedgedoc_oauth2_client_secret
  authorization_flow = data.authentik_flow.default_authorization_flow.id
  invalidation_flow  = data.authentik_flow.default_invalidation_flow.id
  signing_key        = data.authentik_certificate_key_pair.default.id

  property_mappings = data.authentik_property_mapping_provider_scope.oauth2_scopes.ids

  allowed_redirect_uris = [
    {
      matching_mode = "strict"
      url           = "https://hedgedoc.your.domain/auth/oauth2/callback"
    }
  ]
}

# ── HedgeDoc Application ─────────────────────────────────────────────────────

resource "authentik_application" "hedgedoc" {
  name              = "hedgedoc"
  slug              = "hedgedoc"
  protocol_provider = authentik_provider_oauth2.hedgedoc.id
  meta_launch_url   = "https://hedgedoc.your.domain"
}
