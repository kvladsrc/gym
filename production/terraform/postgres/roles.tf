resource "postgresql_role" "authentik" {
  name     = "authentik"
  login    = true
  password = var.authentik_password

  lifecycle {
    ignore_changes = [roles]
  }
}

resource "postgresql_role" "hedgedoc" {
  name     = "hedgedoc"
  login    = true
  password = var.hedgedoc_password

  lifecycle {
    ignore_changes = [roles]
  }
}

resource "postgresql_role" "miniflux" {
  name     = "miniflux"
  login    = true
  password = var.miniflux_password

  lifecycle {
    ignore_changes = [roles]
  }
}

resource "postgresql_role" "zuul" {
  name     = "zuul"
  login    = true
  password = var.zuul_password

  lifecycle {
    ignore_changes = [roles]
  }
}
