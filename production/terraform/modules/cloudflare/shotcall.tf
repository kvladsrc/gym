# Shot Call (https://shotcall.bonfirelit.dev): a Cloudflare Pages site with
# Pages Functions and a D1 database of the players' answers. Wrangler deploys
# the site together with its bindings from wrangler.toml; Terraform owns the
# project, the database, the domain and its DNS record. The names stay
# "skolko-otzyvov": Pages and D1 cannot rename them.

data "cloudflare_zone" "bonfirelit" {
  zone_id = var.cloudflare_zone_id
}

locals {
  account_id = data.cloudflare_zone.bonfirelit.account.id
}

resource "cloudflare_d1_database" "shotcall" {
  account_id = local.account_id
  name       = "skolko-otzyvov"

  read_replication = {
    mode = "disabled"
  }

  # The players' answers live only here.
  lifecycle {
    prevent_destroy = true
  }
}

resource "cloudflare_pages_project" "shotcall" {
  account_id        = local.account_id
  name              = "skolko-otzyvov"
  production_branch = "main"

  lifecycle {
    prevent_destroy = true
    # Each wrangler deploy sets the bindings and the compatibility date.
    ignore_changes = [build_config, deployment_configs]
  }
}

resource "cloudflare_pages_domain" "shotcall" {
  account_id   = local.account_id
  project_name = cloudflare_pages_project.shotcall.name
  name         = "shotcall.bonfirelit.dev"
}

resource "cloudflare_dns_record" "shotcall_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "shotcall"
  content = cloudflare_pages_project.shotcall.subdomain
  type    = "CNAME"
  proxied = true
  ttl     = 1
}
