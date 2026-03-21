# Grant each role full privileges on all objects in their own database.
# "database" object privilege lets the role connect and operate.

resource "postgresql_grant" "authentik_database" {
  database    = postgresql_database.authentik.name
  role        = postgresql_role.authentik.name
  object_type = "database"
  privileges  = ["CONNECT", "CREATE", "TEMPORARY"]

  depends_on = [postgresql_database.authentik]
}

resource "postgresql_grant" "authentik_schema" {
  database    = postgresql_database.authentik.name
  role        = postgresql_role.authentik.name
  schema      = "public"
  object_type = "schema"
  privileges  = ["CREATE", "USAGE"]

  depends_on = [postgresql_database.authentik]
}

# ──────────────────────────────────────────────────────────────────────────────

resource "postgresql_grant" "hedgedoc_database" {
  database    = postgresql_database.hedgedoc.name
  role        = postgresql_role.hedgedoc.name
  object_type = "database"
  privileges  = ["CONNECT", "CREATE", "TEMPORARY"]

  depends_on = [postgresql_database.hedgedoc]
}

resource "postgresql_grant" "hedgedoc_schema" {
  database    = postgresql_database.hedgedoc.name
  role        = postgresql_role.hedgedoc.name
  schema      = "public"
  object_type = "schema"
  privileges  = ["CREATE", "USAGE"]

  depends_on = [postgresql_database.hedgedoc]
}

# ──────────────────────────────────────────────────────────────────────────────

resource "postgresql_grant" "miniflux_database" {
  database    = postgresql_database.miniflux.name
  role        = postgresql_role.miniflux.name
  object_type = "database"
  privileges  = ["CONNECT", "CREATE", "TEMPORARY"]

  depends_on = [postgresql_database.miniflux]
}

resource "postgresql_grant" "miniflux_schema" {
  database    = postgresql_database.miniflux.name
  role        = postgresql_role.miniflux.name
  schema      = "public"
  object_type = "schema"
  privileges  = ["CREATE", "USAGE"]

  depends_on = [postgresql_database.miniflux]
}

# ──────────────────────────────────────────────────────────────────────────────

resource "postgresql_grant" "zuul_database" {
  database    = postgresql_database.zuul.name
  role        = postgresql_role.zuul.name
  object_type = "database"
  privileges  = ["CONNECT", "CREATE", "TEMPORARY"]

  depends_on = [postgresql_database.zuul]
}

resource "postgresql_grant" "zuul_schema" {
  database    = postgresql_database.zuul.name
  role        = postgresql_role.zuul.name
  schema      = "public"
  object_type = "schema"
  privileges  = ["CREATE", "USAGE"]

  depends_on = [postgresql_database.zuul]
}
