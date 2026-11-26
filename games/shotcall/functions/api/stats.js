// GET /api/stats: everyone's statistics from the running totals the database triggers keep.
// total: every counted pair of every player (also under 5 pairs): right picks of people and robot.
// rounds: everyone's finished rounds against the robot; won: players with a won round.
// people/robot are means of per-player shares over players with at least 5 counted pairs.
export async function onRequestGet({ env }) {
  const [agg, hist] = await env.DB.batch([
    env.DB.prepare(
      "SELECT P, sh, sm, N, beat, W, D, L, won, AN, AH, AM FROM agg WHERE id = 1",
    ),
    env.DB.prepare("SELECT p, c FROM hist WHERE c > 0"),
  ]);
  const a = agg.results[0] || {
    P: 0,
    sh: 0,
    sm: 0,
    N: 0,
    beat: 0,
    W: 0,
    D: 0,
    L: 0,
    won: 0,
    AN: 0,
    AH: 0,
    AM: 0,
  };
  const h = {};
  for (const r of hist.results) h[r.p] = r.c;
  const body = {
    players: a.P,
    people: a.P ? a.sh / a.P : null,
    robot: a.P ? a.sm / a.P : null,
    pairs: a.N,
    beat: a.beat,
    rounds: { w: a.W, d: a.D, l: a.L },
    won: a.won,
    total: { n: a.AN, h: a.AH, m: a.AM },
    hist: h,
    updated: new Date().toISOString(),
  };
  return new Response(JSON.stringify(body), {
    headers: {
      "content-type": "application/json",
      "cache-control": "public, max-age=15",
    },
  });
}
