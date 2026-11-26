// POST /api/answers {v, rid, list:[{a, b, p, ms, r, f?}]}: the player's whole answer list after a round.
// The server scores it itself from the key (never trusts the page's numbers) and keeps the longest list.
import { T, E, G, SALT, V, MIN_GAP } from "../../lib/key.js";

const MAX_BODY = 400000,
  MAX_LIST = 5000,
  NEW_PER_IP_DAY = 30,
  ROUND = 10;

const json = (obj, status = 200) => {
  if (status >= 400) console.log("reject", JSON.stringify(obj));
  return new Response(JSON.stringify(obj), {
    status,
    headers: {
      "content-type": "application/json",
      "cache-control": "no-store",
    },
  });
};

async function ipHash(ip) {
  const d = await crypto.subtle.digest(
    "SHA-256",
    new TextEncoder().encode(SALT + ip),
  );
  return [...new Uint8Array(d)]
    .slice(0, 8)
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

export async function onRequestPost({ request, env }) {
  const text = await request.text();
  if (text.length > MAX_BODY) return json({ error: "too_large" }, 413);
  let body;
  try {
    body = JSON.parse(text);
  } catch {
    return json({ error: "bad_json" }, 400);
  }
  // a page with another shot base (an old open tab) would send indices into other screenshots
  if (!body || body.v !== V) return json({ error: "old_version" }, 409);
  const rid = body && body.rid;
  if (typeof rid !== "string" || !/^r[0-9a-z]{4,24}$/.test(rid))
    return json({ error: "bad_rid" }, 400);
  const list = body.list;
  if (!Array.isArray(list) || !list.length || list.length > MAX_LIST)
    return json({ error: "bad_list" }, 400);

  const seen = new Set(),
    clean = [];
  let n = 0,
    h = 0,
    m = 0,
    k = 0,
    s = 0;
  for (const e of list) {
    const a = e && e.a,
      b = e && e.b,
      p = e && e.p;
    if (
      !Number.isInteger(a) ||
      !Number.isInteger(b) ||
      a < 0 ||
      b < 0 ||
      a >= T.length ||
      b >= T.length ||
      a === b
    )
      return json({ error: "bad_pair" }, 400);
    if (G[a] < 0 || G[a] !== G[b] || Math.abs(T[a] - T[b]) < 2)
      return json({ error: "bad_pair" }, 400);
    if (p !== "a" && p !== "b" && p !== "k" && p !== "s")
      return json({ error: "bad_pick" }, 400);
    const key = a < b ? a * 1000 + b : b * 1000 + a;
    if (seen.has(key)) return json({ error: "repeat" }, 400);
    seen.add(key);
    const ms =
      Number.isInteger(e.ms) && e.ms >= 0 && e.ms <= 3600000 ? e.ms : null;
    const r = Number.isInteger(e.r) && e.r >= 0 && e.r <= 100000 ? e.r : 0;
    const f = e.f === 1 ? 1 : undefined; // the answer was revealed right away («Сразу показывать ответ»)
    clean.push(f ? { a, b, p, ms, r, f } : { a, b, p, ms, r });
    // pairs closer than MIN_GAP classes (made by pages before the rule went from 2 to 3) are kept, not counted
    if (p === "k") k++;
    else if (p === "s") s++;
    else if (Math.abs(T[a] - T[b]) >= MIN_GAP) {
      const aWins = T[a] > T[b];
      n++;
      if ((p === "a") === aWins) h++;
      if (E[a] > E[b] === aWins) m++;
    }
  }

  // rounds: the page tags each answer with its round number r; a finished round has ROUND answers.
  // Won / drawn / lost exactly as the page's end screen says (only rounds with counted pairs).
  const rounds = new Map();
  for (const e of clean) {
    let g = rounds.get(e.r);
    if (!g) rounds.set(e.r, (g = { c: 0, n: 0, h: 0, m: 0 }));
    g.c++;
    if ((e.p === "a" || e.p === "b") && Math.abs(T[e.a] - T[e.b]) >= MIN_GAP) {
      const aWins = T[e.a] > T[e.b];
      g.n++;
      if ((e.p === "a") === aWins) g.h++;
      if (E[e.a] > E[e.b] === aWins) g.m++;
    }
  }
  let rw = 0,
    rd = 0,
    rl = 0;
  for (const g of rounds.values()) {
    if (g.c < ROUND || !g.n) continue;
    if (g.h > g.m) rw++;
    else if (g.h < g.m) rl++;
    else rd++;
  }

  const now = Date.now();
  const old = await env.DB.prepare("SELECT cnt FROM players WHERE rid = ?1")
    .bind(rid)
    .first();
  if (old && old.cnt >= clean.length) return json({ ok: true, kept: old.cnt });
  const iph = await ipHash(request.headers.get("cf-connecting-ip") || "");
  if (!old) {
    const c = await env.DB.prepare(
      "SELECT COUNT(*) AS c FROM players WHERE iph = ?1 AND created > ?2",
    )
      .bind(iph, now - 86400000)
      .first();
    if (c && c.c >= NEW_PER_IP_DAY) return json({ error: "rate" }, 429);
  }
  await env.DB.prepare(
    `INSERT INTO players (rid, n, h, m, k, s, cnt, rw, rd, rl, list, iph, created, updated)
     VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?13)
     ON CONFLICT(rid) DO UPDATE SET n = excluded.n, h = excluded.h, m = excluded.m, k = excluded.k, s = excluded.s,
       cnt = excluded.cnt, rw = excluded.rw, rd = excluded.rd, rl = excluded.rl, list = excluded.list,
       updated = excluded.updated
     WHERE excluded.cnt > players.cnt`,
  )
    .bind(
      rid,
      n,
      h,
      m,
      k,
      s,
      clean.length,
      rw,
      rd,
      rl,
      JSON.stringify(clean),
      iph,
      now,
    )
    .run();
  return json({ ok: true, n, h, m, rw, rd, rl });
}
