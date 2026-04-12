# Local DNS records for internal services
# Ingress-nginx (.220)
resource "pihole_dns_record" "api_bonfire" {
  domain = "api.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "b_bonfire" {
  domain = "b.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "ci_bonfire" {
  domain = "ci.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "cl_bonfire" {
  domain = "review.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "cs_bonfire" {
  domain = "search.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "dns_bonfire" {
  domain = "dns.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "feed_bonfire" {
  domain = "feed.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "hedgedoc_bonfire" {
  domain = "hedgedoc.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "logs_bonfire" {
  domain = "logs.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "read_bonfire" {
  domain = "read.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "registry_bonfire" {
  domain = "registry.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "s3_bonfire" {
  domain = "s3.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "store_bonfire" {
  domain = "store.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "sql_bonfire" {
  domain = "sql.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "sso_bonfire" {
  domain = "sso.your.domain"
  ip     = "192.168.1.220"
}

resource "pihole_dns_record" "zz_bonfire" {
  domain = "zz.your.domain"
  ip     = "192.168.1.220"
}

# BuildBuddy (.221)
resource "pihole_dns_record" "bb_grpc_bonfire" {
  domain = "bb-grpc.your.domain"
  ip     = "192.168.1.221"
}

resource "pihole_dns_record" "bb_bonfire" {
  domain = "bb.your.domain"
  ip     = "192.168.1.221"
}
