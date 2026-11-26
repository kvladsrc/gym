-- Shot Call («Сколько отзывов?») on Cloudflare D1: one row per player (anonymous random id), with running
-- totals kept by triggers so the statistics endpoint reads one row and the histogram.
CREATE TABLE IF NOT EXISTS players (
  rid TEXT PRIMARY KEY,
  n INTEGER NOT NULL,      -- counted pairs (picked a or b)
  h INTEGER NOT NULL,      -- the player's right picks
  m INTEGER NOT NULL,      -- the robot's right picks on the same pairs
  k INTEGER NOT NULL,      -- «Знаю игру»
  s INTEGER NOT NULL,      -- skipped
  cnt INTEGER NOT NULL,    -- all answers
  rw INTEGER NOT NULL DEFAULT 0,  -- finished rounds (10 answers, same r) won against the robot
  rd INTEGER NOT NULL DEFAULT 0,  -- drawn
  rl INTEGER NOT NULL DEFAULT 0,  -- lost
  list TEXT NOT NULL,      -- JSON [{a, b, p, ms, r}]
  iph TEXT NOT NULL,       -- salted hash prefix of the IP, only to limit new players per address
  created INTEGER NOT NULL,
  updated INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS players_iph ON players (iph, created);

CREATE TABLE IF NOT EXISTS agg (
  id INTEGER PRIMARY KEY CHECK (id = 1),
  P INTEGER NOT NULL DEFAULT 0,     -- players with n >= 5
  sh REAL NOT NULL DEFAULT 0,       -- sum of their shares h/n
  sm REAL NOT NULL DEFAULT 0,       -- sum of the robot's shares m/n
  N INTEGER NOT NULL DEFAULT 0,     -- their counted pairs
  beat INTEGER NOT NULL DEFAULT 0,  -- players with h > m over all their answers
  W INTEGER NOT NULL DEFAULT 0,     -- their rounds won / drawn / lost
  D INTEGER NOT NULL DEFAULT 0,
  L INTEGER NOT NULL DEFAULT 0,
  won INTEGER NOT NULL DEFAULT 0,   -- players who won at least one round
  AN INTEGER NOT NULL DEFAULT 0,    -- all players: counted pairs
  AH INTEGER NOT NULL DEFAULT 0,    -- all players: right picks
  AM INTEGER NOT NULL DEFAULT 0     -- the robot's right picks on those pairs
);
INSERT OR IGNORE INTO agg (id) VALUES (1);

-- players with n >= 5 by their share in whole percent
CREATE TABLE IF NOT EXISTS hist (p INTEGER PRIMARY KEY, c INTEGER NOT NULL DEFAULT 0);
WITH RECURSIVE x(p) AS (SELECT 0 UNION ALL SELECT p + 1 FROM x WHERE p < 100)
INSERT OR IGNORE INTO hist (p, c) SELECT p, 0 FROM x;

CREATE TRIGGER IF NOT EXISTS players_ins AFTER INSERT ON players WHEN NEW.n >= 5 BEGIN
  UPDATE agg SET P = P + 1, sh = sh + 1.0 * NEW.h / NEW.n, sm = sm + 1.0 * NEW.m / NEW.n,
    N = N + NEW.n, beat = beat + (NEW.h > NEW.m), W = W + NEW.rw, D = D + NEW.rd,
    L = L + NEW.rl, won = won + (NEW.rw > 0) WHERE id = 1;
  UPDATE hist SET c = c + 1 WHERE p = CAST(ROUND(100.0 * NEW.h / NEW.n) AS INTEGER);
END;
CREATE TRIGGER IF NOT EXISTS players_upd_old AFTER UPDATE ON players WHEN OLD.n >= 5 BEGIN
  UPDATE agg SET P = P - 1, sh = sh - 1.0 * OLD.h / OLD.n, sm = sm - 1.0 * OLD.m / OLD.n,
    N = N - OLD.n, beat = beat - (OLD.h > OLD.m), W = W - OLD.rw, D = D - OLD.rd,
    L = L - OLD.rl, won = won - (OLD.rw > 0) WHERE id = 1;
  UPDATE hist SET c = c - 1 WHERE p = CAST(ROUND(100.0 * OLD.h / OLD.n) AS INTEGER);
END;
CREATE TRIGGER IF NOT EXISTS players_upd_new AFTER UPDATE ON players WHEN NEW.n >= 5 BEGIN
  UPDATE agg SET P = P + 1, sh = sh + 1.0 * NEW.h / NEW.n, sm = sm + 1.0 * NEW.m / NEW.n,
    N = N + NEW.n, beat = beat + (NEW.h > NEW.m), W = W + NEW.rw, D = D + NEW.rd,
    L = L + NEW.rl, won = won + (NEW.rw > 0) WHERE id = 1;
  UPDATE hist SET c = c + 1 WHERE p = CAST(ROUND(100.0 * NEW.h / NEW.n) AS INTEGER);
END;
CREATE TRIGGER IF NOT EXISTS players_del AFTER DELETE ON players WHEN OLD.n >= 5 BEGIN
  UPDATE agg SET P = P - 1, sh = sh - 1.0 * OLD.h / OLD.n, sm = sm - 1.0 * OLD.m / OLD.n,
    N = N - OLD.n, beat = beat - (OLD.h > OLD.m), W = W - OLD.rw, D = D - OLD.rd,
    L = L - OLD.rl, won = won - (OLD.rw > 0) WHERE id = 1;
  UPDATE hist SET c = c - 1 WHERE p = CAST(ROUND(100.0 * OLD.h / OLD.n) AS INTEGER);
END;
-- the global counter takes every player, also those under 5 pairs
CREATE TRIGGER IF NOT EXISTS players_all_ins AFTER INSERT ON players BEGIN
  UPDATE agg SET AN = AN + NEW.n, AH = AH + NEW.h, AM = AM + NEW.m WHERE id = 1;
END;
CREATE TRIGGER IF NOT EXISTS players_all_upd AFTER UPDATE ON players BEGIN
  UPDATE agg SET AN = AN - OLD.n + NEW.n, AH = AH - OLD.h + NEW.h, AM = AM - OLD.m + NEW.m WHERE id = 1;
END;
CREATE TRIGGER IF NOT EXISTS players_all_del AFTER DELETE ON players BEGIN
  UPDATE agg SET AN = AN - OLD.n, AH = AH - OLD.h, AM = AM - OLD.m WHERE id = 1;
END;
