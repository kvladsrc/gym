# Shot Call was first deployed with wrangler; these blocks adopt its
# resources. Remove them once an apply has imported them.

import {
  to = module.cloudflare.cloudflare_d1_database.shotcall
  id = "${module.cloudflare.account_id}/b262b5f0-6d44-4ff7-99bd-e0da91d19dae"
}

import {
  to = module.cloudflare.cloudflare_pages_project.shotcall
  id = "${module.cloudflare.account_id}/skolko-otzyvov"
}

import {
  to = module.cloudflare.cloudflare_pages_domain.shotcall
  id = "${module.cloudflare.account_id}/skolko-otzyvov/shotcall.bonfirelit.dev"
}
