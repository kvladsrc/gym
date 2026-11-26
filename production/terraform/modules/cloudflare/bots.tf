# Free bot protection. The zone settings reach only proxied hostnames; today
# that is shotcall alone, the cluster records are DNS-only.

resource "cloudflare_bot_management" "bonfirelit" {
  zone_id = var.cloudflare_zone_id

  # Bot Fight Mode challenges traffic that matches known bots. No rule can
  # skip it, and it brings JavaScript detections: a script in HTML pages.
  fight_mode = true

  # AI scrapers and crawlers are blocked; the managed robots.txt tells them.
  ai_bots_protection    = "block"
  is_robots_txt_managed = true
}

# The Free plan allows one rate limiting rule: it may match only the path,
# counts per IP (and data centre), over 10 s, and blocks for 10 s. A player
# makes one API request per round or statistics screen; a flood from one IP
# is cut to 10 requests in 20 s.
resource "cloudflare_ruleset" "ratelimit" {
  zone_id = var.cloudflare_zone_id
  name    = "default"
  kind    = "zone"
  phase   = "http_ratelimit"

  rules = [{
    ref         = "api_flood"
    description = "API: more than 10 requests in 10 s from one IP"
    expression  = "starts_with(http.request.uri.path, \"/api/\")"
    action      = "block"
    ratelimit = {
      characteristics     = ["cf.colo.id", "ip.src"]
      period              = 10
      requests_per_period = 10
      mitigation_timeout  = 10
    }
  }]
}

# <project>.pages.dev serves the same site, API and database but is outside
# the zone, so none of the above applies there. Bulk Redirects send it and
# its deployment subdomains to the custom domain, as Pages recommends.
resource "cloudflare_list" "pages_dev" {
  account_id  = local.account_id
  name        = "pages_dev"
  kind        = "redirect"
  description = "pages.dev addresses to their custom domains"

  items = [{
    redirect = {
      source_url            = "${cloudflare_pages_project.shotcall.subdomain}/"
      target_url            = "https://${cloudflare_pages_domain.shotcall.name}"
      status_code           = 301
      include_subdomains    = true
      subpath_matching      = true
      preserve_path_suffix  = true
      preserve_query_string = true
    }
  }]
}

resource "cloudflare_ruleset" "redirects" {
  account_id = local.account_id
  name       = "default"
  kind       = "root"
  phase      = "http_request_redirect"

  rules = [{
    ref         = "pages_dev"
    description = "pages.dev to the custom domains"
    expression  = format("http.request.full_uri in $%s", cloudflare_list.pages_dev.name)
    action      = "redirect"
    action_parameters = {
      from_list = {
        name = cloudflare_list.pages_dev.name
        key  = "http.request.full_uri"
      }
    }
  }]
}
