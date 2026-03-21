resource "postgresql_database" "authentik" {
  name  = "authentikdb"
  owner = postgresql_role.authentik.name

  depends_on = [postgresql_role.authentik]
}

resource "postgresql_database" "hedgedoc" {
  name  = "hedgedocdb"
  owner = postgresql_role.hedgedoc.name

  depends_on = [postgresql_role.hedgedoc]
}

resource "postgresql_database" "miniflux" {
  name  = "minifluxdb"
  owner = postgresql_role.miniflux.name

  depends_on = [postgresql_role.miniflux]
}

resource "postgresql_database" "zuul" {
  name  = "zuuldb"
  owner = postgresql_role.zuul.name

  depends_on = [postgresql_role.zuul]
}
