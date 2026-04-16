# Ingress-nginx (.220)
resource "cloudflare_record" "api_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "api"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "b_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "b"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "ci_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "ci"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "cl_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "cl"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "cs_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "cs"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "dns_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "dns"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "feed_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "feed"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "hedgedoc_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "hedgedoc"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "read_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "read"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "registry_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "registry"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "s3_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "s3"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "store_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "store"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "sql_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "sql"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "sso_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "sso"
  content = "192.168.1.220"
  type    = "A"
  proxied = false
}

# BuildBuddy (.221)
resource "cloudflare_record" "bb_grpc_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "bb-grpc"
  content = "192.168.1.221"
  type    = "A"
  proxied = false
}

resource "cloudflare_record" "bb_bonfire" {
  zone_id = var.cloudflare_zone_id
  name    = "bb"
  content = "192.168.1.221"
  type    = "A"
  proxied = false
}
