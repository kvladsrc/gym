# Shot Call

A browser mini-game at <https://shotcall.bonfirelit.dev/>: two screenshots of
Steam games of one genre, both released in 2026; pick the one with more
reviews. A round is ten pairs against the robot, the
[`qwen_review`](../../qwen_review) forecaster, and the statistics screen
compares all players with it.

- Pairs are drawn on the page from 3,052 screenshots of the 763 held-out
  2026 games with more than 30 reviews: same genre group, review classes at
  least two apart, no game pair twice. 18+ games are off by default.
- English by default, Russian on the switch. Pixel style.
- No review counts are shown, so players cannot learn the answers.

## How it works

A Cloudflare Pages site with Pages Functions and a D1 database:

- `POST /api/answers` takes a player's whole answer list after each round.
  The server scores it from its own key (`lib/key.js`, never served), counts
  only valid pairs, keeps the longest list and lets 30 new players a day
  through per IP hash.
- `GET /api/stats` reads running totals (`agg`, `hist`) that database
  triggers keep up to date; `sql/recompute.sql` rebuilds them from
  `players` at any time.

Terraform owns the Pages project, the database, the domain and its DNS
record ([`shotcall.tf`](../../production/terraform/modules/cloudflare/shotcall.tf)),
and the free bot protection in front of them
([`bots.tf`](../../production/terraform/modules/cloudflare/bots.tf)): Bot
Fight Mode, AI crawlers blocked, at most 10 `/api/` requests in 10 s from
one IP. `skolko-otzyvov.pages.dev` and its deployment addresses redirect to
the domain, so nothing reaches the site past these checks.
`wrangler` deploys the site with its bindings from `wrangler.toml`. The
project and the database keep their first name, `skolko-otzyvov`.

## Commands

Run from the repository root. `wrangler` comes through `npx` and needs a
Cloudflare account token with Pages and D1 write in `CLOUDFLARE_API_KEY`.

```sh
just shotcall data     # answer key, page data, screenshot links
just shotcall build    # public/ and lib/key.js
just shotcall dev      # local site and database at http://127.0.0.1:8788/
just shotcall deploy   # publish
just shotcall backup   # live database to data/backups/
```

`make.py` reads `qwen_review/data`, which is not in the repository. The
screenshot ids must not change: the players' answers refer to them.

## Files

| Path | Holds |
| --- | --- |
| `page.src.html` | the game: markup, styles, logic, texts in both languages |
| `make.py` | the shot base from `qwen_review/data` into `data/` |
| `build.py` | `public/` and the server key `lib/key.js` |
| `functions/api/` | the answers and statistics endpoints |
| `sql/` | the database schema and the statistics rebuild |
| `art/` | the two portraits of the statistics screen |
| `fonts/` | IBM Plex Mono and Press Start 2P, under the OFL |
