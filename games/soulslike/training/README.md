# Training the duellists

<!-- markdownlint-disable MD013 -->

Two duellists of one body and one network learn against each other's past
selves (ML-Agents symmetric self-play: one behaviour, `Fighter`, teams 0
and 1) in a simplified arena (`Soulslike → Build Arena`: four walled
30 × 30 m arenas). Both share one body: the game's rank-and-file enemy,
`EnemySultryVampireHuntress`. The aim is a network that knows how to fight,
to fine-tune for the game's bosses later.

- **The arena** is 30 m a side, its walls far off, as the game's creatures
  have none (at 24 m once, then 14, the agents ran in circles; the time-out
  costs -2 now). **Each episode** lays it out anew: its floor bent into
  hills (from flat to ±0.6 m, 6–14 m wide: the game's ground is not flat),
  0–12 trunks 0.4–1.6 m thick
  and 0–4 wall pieces (as thick on the floor as 0–3 and 0–1 were on 14 m). It draws each fighter's height (1.4–2.2 m) and
  weapon (sword, hammer or bare hands) apart. They start on clear ground,
  at least 5 m apart, facing anywhere.
- **Actions** (`Scripts/Training/Fighter.cs`): 55 melee attacks
  (`CharacterAnimator.DuelAttacks`), 3 quick moves (`DuelMoves`: an aerial
  evade, a dodge back, an esquiva; no blow), and the roll.
  - Common, open to all: 8 kicks and a punch (six Mixamo kicks in the
    boxing's places; punching_1, the quickest action, and kicking_1, the one
    blow stepping in, kept), and the quick moves and the roll.
  - Bare hands only: the 15 of capoeira.
  - Sword: 21, three of them kicks that hold the sword through them.
  - Hammer: 11, two of them kicks that hold the haft.
  - The zombie blows are left out. The network knows an action by its place
    in the list: a clip replaced keeps the place. The clips are checked to be
    distinct by their motion (the Mixamo package's Mixamo → Find Duplicate
    Clips). `./balance.sh` → "actions table" lists them by section (common, sword,
    hammer, bare-handed): how often each weapon takes each and lands it, and
    its balance. The clips' videos are the package's page
    (`animations/mixamo/Gallery~/index.html`).
  Every action's clip carries the body the way its root went (the clip
  plays in place, the body moves its line; the bones, and so the
  hurtboxes, keep the clip's own timing). Walking and running come from
  the speeds.
  - The common ones are open to all, capoeira to the bare-handed only.
  - A sword's or a hammer's need that weapon.
  - What strikes is swept against the other's bone hurtboxes (the torso's
    ends at the neck) in the clip's middle three fifths: the blade
    (5 cm round), the hammer's head across the handle (10 cm) and the half of
    the handle next to it (6 cm), or bare, the
    fist (hand to knuckles) or the foot and the shin (7 cm). A bare-handed
    blow also sweeps its other fast limb: a kick's swinging arm, a punch's
    step. After a hit it touches nothing for 0.5 s, then may land again (a
    combo's clip strikes more than once; each hit continues the combo).
  - A blow that lands plays the rest of its clip, its recovery, 1.5 times
    as fast: a hit opens a combo, a miss pays in full. (The editor's
    Soulslike → Combo Demo shows every blow so, against a standing target,
    with its hurtboxes and the blows' paths, and compares no speed-up and
    cancelling the recovery.)
  - Damage, speed and stagger: from the balance (below). Damage 80–120 %
    of the action's by the striking point's speed on contact, against 200
    health; then the combo's factor: 20 % more for each hit of it, with no
    end (100, 120, 140, 160 %…); then the adrenaline's (below). A hit
    staggers the other for the action's stagger, and the other's action is
    lost. Nothing is invulnerable: a roll or a quick move saves only by
    taking the body out of the blow's way.
- **Balance** (`balance.json`, Balance.cs). Each action's damage, speed
  and stagger. `base`: a hit's damage and stagger by what strikes (`sword`,
  `hammer`, `limb`). `rules`: factors (`damage`, `speed`, `stagger`, 1 if
  left out), each for what it names: the weapon in hand (`held`: `sword`,
  `hammer`, `hands`) and the animation (`blow`: a clip's name as
  `actions.sh` prints it, or `roll`), either, both or neither (every
  action); every rule naming an action multiplies it. Speed plays the whole
  animation that much faster, its damage (by the striking point's speed)
  and energy (by its length) following; walking is not an action. Now: sword
  16 / 0.3 s, hammer 30 / 0.5 s, limb 10 / 0.3 s; bare-handed ×1.3 damage,
  ×1.4 speed, 0.5 s stagger; sword_and_shield_slash_5 ×0.85 speed (each
  rule's `note` says why). The players (train.sh, play.sh, record.sh set
  `SOULSLIKE_BALANCE`) read it again as a fight begins when it has changed:
  a change needs no rebuild nor restart, and takes at the next fights. A
  file that does not read leaves the balance as it was (the player's log
  says so). Its hash goes to TensorBoard (`Balance/version`): weapons.sh and
  actions.sh count from its last change. The level builders put a copy in
  their scenes, for the game. `./balance.sh` opens a panel in the browser
  (balance.py, 127.0.0.1:8765): the base and the rules as tables, every
  action's resulting numbers per weapon against the saved ones, the
  players' logs (which balance each took, and any complaint), weapons.sh and
  actions.sh on a click, and the versions before (balance_history/). It
  checks before saving: the names against actions.json (each action, what it
  needs in hand and what strikes in it, written by Build Arena), the
  factors positive; an error keeps it from saving.
- **Eyes.** A 32 × 32 grid of 0.25 m cells, 8 m across, centred 2 m before
  oneself (6 m ahead, 2 m behind) and turned with one, in metres whatever
  one's size: the other's body (its hurtboxes: where it really is, a kick's
  leg and all, not its controller's capsule, which a clip's spin leaves
  behind) and obstacles, through `resnet` convolutions (a cell is marked if
  anything touches it; at 0.5 m a body filled up to 3 × 3). The editor's
  Soulslike → Grid Demo draws it on the floor.
  Plus 408 numbers:
  - the other's hips, head, left and right hand (knuckles), left and right
    foot (toes), all alike, in one's own frame (right, up, ahead; from the
    ground under one's hips): where each is, as p / (|p| + one's height)
    (near, nearly metres; far, only the way to it: the hips at 20 m and a
    fist at 0.5 m in one scale), and its speed (over 12 m/s). The action's
    phase alone left the network to learn where every clip's fist or foot
    is at every phase, anew for a clip replaced;
  - one's own six points alike (one's hips' height shows a crouch or a
    roll, their speed where the clip carries one);
  - which way the other faces, relative to the line to one, and its turning;
  - one's own speeds, whether one is focused on the other, and both healths;
  - whether one is busy (staggered, or in an action);
  - the fight's clock, 0 to 1 at the time-out. Without it a time-out came at
    random, and did not press them; fights grew to 80 s;
  - one's own and the other's action (one-hot of 60: none, 55 blows, 3
    moves, the roll) and its phase (one-hot of 5);
  - stagger, for both;
  - the size ratio and both weapons;
  - one's place in the arena;
  - one's own last 3 actions, one-hot each;
  - one's combo, and the time since its last hit;
  - both adrenalines, 0–1 each: the level (0 to S) with the points towards
    the next, over 5.
- **Memory.** An LSTM of 256 numbers (128 of them its output, all the action
  heads read) carried from each decision to the next
  (learned on stretches of 32 decisions, 6.4 s): the other's habits within a
  fight, what the grid saw before one turned away. A separate history of the
  other's actions was left to it.
- **Self-play.** Against itself and its last 20 snapshots, one every 50 k
  steps (half the fights against the newest). The snapshots live only in the
  trainer's memory: each `--resume` begins them anew.
- **Actions, once a decision** (5 a second: every 10 academy steps of
  0.02 s): speeds ahead, aside and
  turning, each kept or ordered one way: the way it goes adds a third (up
  to the most); from standing or against the way it goes, it is at once a
  third the new way; turning, against the way it turns stops it first.
  Or turning is left to the focus: the body turns to the other by itself,
  at its most speed (360°/s, between actions), till a turning order either
  way takes it back.
  Any action (a blow, a quick move, the roll) stops all three: that is the
  only way to stand. The roll turns the body the way it rolls. So a turn about, or a roll any way
  (the way the speeds then point), takes one decision. A blow or a roll
  carries the body its own way meanwhile. The most
  pace is 4 m/s ahead, 2.6 aside, 2.4 back (at 1.8 m tall), ahead and aside
  together no faster than either. Plus one
  action: none, an attack by index, or the roll.
- **Rewards, by a curriculum** (`duel.yaml`, `environment_parameters.stage`):
  1. *Face*, to 300 k steps: turning towards the other, as the change of
     the angle since the last decision.
  2. *Fight*: damage dealt minus damage taken, as shares of health; +1 for
     a win, nothing more for a loss (its lost health is −1 already; only
     −0.001, so that the self-play ELO, reading the last step's sign, takes
     it for a loss and not a draw); a time-out (90 s; it was 180, and with fights of 80 s
     it was too far to press) −2 to both, each
     added to the last step's damage and energy. So losing a fight, energy and all, is
     always better than running from it to the time-out (with a loss at −1,
     the losing side gained by running, and they ran). Not zero-sum: a
     decided fight is +1 to the pair, a time-out −4. A combo is hits landing
     one after another, each a different blow from the last, each within 2.5 s;
     a miss, a blow taken or the time ends it. Nothing else is rewarded:
     combos, adrenaline and evasions (which raise adrenaline) pay through
     the damage they add. Rewards for repeating a blow, for evasions and for
     standing apart were tried and dropped, as steering the fight by hand.
     (`Duel/Apart share` still shows the time spent at adrenaline 0.)

  Always, every action spends energy, paid for in reward (×8), by the
  length of its clip alone: 0.08 a second, whatever strikes (the roll and
  the quick moves too); from adrenaline level A on, a twentieth of that
  (at a fifth, a fight's blows cost more than standing to the time-out, and
  they stopped meeting). Moving and turning are free. That is the only cost
  of a blow into the air.
- **Adrenaline**, a level 0, A, B, C, D, S (`Fighter.Level`):
  - +1 while the other is within 3 m (between the bodies, hips to hips less
    their capsules' radii, as evasion's 1 m), at once in and
    out.
  - +1 for each whole point gathered: a hit given (not taken) gives damage ÷ 30
    points, a blow evaded 0.7. Gathered points hold for 1 s after the
    last one, then fall 0.35 a second.
  - Its level multiplies the damage one does: 1, 1.1, 1.25, 1.4, 1.6, 2.
    Both sides see both levels.

```sh
uv sync                        # once: Python 3.10.12, mlagents 1.1.0, PyTorch
./train.sh duel --build        # rebuild the arena and its player (Unity running), train
./train.sh duel --resume       # go on with a run
./train.sh duel --hours 8      # stop (and save) after 8 hours; 4 by default, 0 for none
./record.sh duel               # film a minute of the newest model to results/duel/videos
./watch.sh duel                # watch it in a window, live, until closed
./play.sh duel                 # play against the newest model in a window
./weapons.sh duel              # how often each weapon beats each other, lately (--every 60 to follow)
./actions.sh duel              # which actions each weapon's fighters take, and how often each blow lands
uv run tensorboard --logdir results
```

`play.sh` makes you the near fighter, in the same body as the model's (the
same blows, energy, adrenaline; only your speeds come at once, not by the
agent's inertia); the model, loaded once, is the far one. The mouse turns
the camera and you keep turning to face its way; W/S ahead and back, A/D
aside.
The left button gives the three quickest blows of your weapon in turn (a
combo; from the first again after 1.5 s without a press), the right its
heavy blow, F a quick kick, Shift a quick move the way WASD points, Space
the roll.

Under its table `weapons.sh` gives, of the fights between two weapons, how
often the weapons cross while both blows are open (phases 2–4; counted once
as they meet, nothing yet happens for it), and each weapon's blows a fight
and the share of them a blow taken cut short (TensorBoard: `Clash/`); and,
of a sword against a hammer, the share of the sword's blows the hammer
blocked and the sword's hits a fight (`Guard/`).

The hammer is a shield against the sword: its head and the upper half of
its shaft (what strikes in its blows), always there, in a blow or not. A
sword's blade meeting it in the blow's open phases strikes no more in that
blow (no stagger; the clip plays on, a miss,
and the hammer's holder gains an evasion's adrenaline if it was close). Bare
limbs pass it. The viewer shows a block as a white flash, cold sparks and a
ring of steel; the editor's "Soulslike/Combo Demo/Sword on Hammer" throws the
sword's blows at a still target holding a hammer.

`train.sh` runs `learn.py`, which is `mlagents-learn` with one fix for
`threaded: true` (the update thread kept on the GPU). The config leaves it
off: with Python's GIL the trainer stays on one core anyway, and the steps
came slower. That one core is the ceiling, about 330 steps a second with 4
arena players; more players or arenas do not raise it.

Each run writes to `results/<run-id>`: TensorBoard logs and checkpoints.
On a stop it also writes `Fighter.onnx`. The trainer stops
if free memory drops below 1.5 GB.
