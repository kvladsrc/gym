-- rebuild agg and hist from players (exact; safe to run any time)
UPDATE agg SET
  P = (SELECT COUNT(*) FROM players WHERE n >= 5),
  sh = COALESCE((SELECT SUM(1.0 * h / n) FROM players WHERE n >= 5), 0),
  sm = COALESCE((SELECT SUM(1.0 * m / n) FROM players WHERE n >= 5), 0),
  N = COALESCE((SELECT SUM(n) FROM players WHERE n >= 5), 0),
  beat = COALESCE((SELECT SUM(h > m) FROM players WHERE n >= 5), 0),
  W = COALESCE((SELECT SUM(rw) FROM players WHERE n >= 5), 0),
  D = COALESCE((SELECT SUM(rd) FROM players WHERE n >= 5), 0),
  L = COALESCE((SELECT SUM(rl) FROM players WHERE n >= 5), 0),
  won = COALESCE((SELECT SUM(rw > 0) FROM players WHERE n >= 5), 0),
  AN = COALESCE((SELECT SUM(n) FROM players), 0),
  AH = COALESCE((SELECT SUM(h) FROM players), 0),
  AM = COALESCE((SELECT SUM(m) FROM players), 0)
WHERE id = 1;
UPDATE hist SET c = (SELECT COUNT(*) FROM players WHERE n >= 5 AND CAST(ROUND(100.0 * h / n) AS INTEGER) = hist.p);
