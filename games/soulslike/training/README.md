# How the fighters learn

<!-- markdownlint-disable MD013 -->

Every enemy in the game is driven by one neural network. It was trained
from scratch with reinforcement learning, by fighting copies of itself.

## LSTM

Originally, movement and camera turning were given to the model as actions.
Focusing on the opponent therefore took a long sequence of actions, and that
horizon was too long for the network to learn. After several network
configurations I added a single action: auto-focus. While it's on, the
character keeps turning towards the opponent automatically, at a fixed
speed. The network can still steer the turning "by hand".

Movement had the same problem. It took many hours, 5+ million steps of
self-play, just to learn to get around. Even then, the network had no
memory and started from a blank slate at every decision, so once it set off
somewhere it forgot what it had been going for. I had planned to bundle
movement and pathfinding into a single action. In the end, short-term
memory solved it instead. Even a small one, trained on the last ~6 seconds,
made the agents move much better. Doubling the memory's size let the agents
adapt to their opponent's style. Even in deterministic mode the model is a
less predictable opponent, and it learns not to repeat the same mistakes
over and over.

## Training setup

- PPO with symmetric self-play in Unity ML-Agents (`duel.yaml`): one
  behaviour, `Fighter`, both sides one network.
- The network fights its current self and its last 20 snapshots (one every
  50 k steps, half the fights against the newest), so it has to beat old
  styles as well as new ones.
- 16 arenas run headless on a laptop: 4 players (`Soulslike → Build Arena
  Player`) of 4 arenas each, 30 × 30 m, walls far off as the game's
  creatures have none.
- Each fight randomises the layout: 0–12 trunks, 0–4 wall pieces, gently
  bent ground (hills up to ±0.6 m), each fighter's height (1.4–2.2 m) and
  weapon (sword, hammer or bare hands). Both share one body, the game's
  rank-and-file enemy. A fight lasts at most 90 s.
- A short opening stage (to 300 k steps) teaches the fighters to face each
  other; after that everything is the fight.

## Network

~1.6M parameters in the policy; ~3.2M including the critic used only in
training. Three 512-wide layers, then the LSTM.

- **Vision:** a 32×32 grid of 25 cm cells (8 m across, centred 2 m ahead,
  turned with the fighter), read by a small ResNet-style CNN. It shows
  obstacles and the opponent's body parts (its hurtboxes, so a kick's leg
  shows where it really is).
- **Body points:** the network sees both its own skeleton and the
  opponent's as six points: hips, head, both hands, both feet. Each point is
  a 3D position in the fighter's own frame, as `p / (|p| + height)`: almost
  metres up close, only the direction far away. Each point also gets a 3D
  velocity (over 12 m/s). A blow is read from where the fist or foot
  actually is and how it moves, not from the animation's name.
- **Animation phases:** the fighter's own action and the opponent's action
  are given as one-hots. Each action is split into 5 phases, so the network
  knows whether a swing is winding up, striking or recovering.
- **Also:** both healths, both adrenalines (0–1), both weapons, the size
  ratio, stagger, whether it is busy, its own speeds and focus, which way
  the opponent faces and turns, its place in the arena, its last 3 actions,
  its combo and the time since its last hit, and the fight's clock (without
  it the time-out came at random and fights grew to 80 s).
- **Memory:** an LSTM with 256 numbers of state carried between decisions,
  learned on stretches of 32 decisions (6.4 s). It holds the opponent's
  habits within a fight and what the grid saw before the fighter turned
  away.
- **Decisions:** 5 per second, about human reaction time. Three movement
  branches (ahead, aside, turning or auto-focus), plus one of 60 actions:
  none, 55 attacks, 3 quick evades and a roll. Masking hides whatever the
  current weapon can't do. Any action stops the walk; that is the only way
  to stand.

## Actions

The 55 attacks are Mixamo clips (`Editor/CharacterAnimator.cs`); the
network knows an action by its place in the list.

- Common, open to all: 8 kicks and a punch, the quick evades and the roll.
- Bare hands only: 15 capoeira moves.
- Sword: 21, three of them kicks. Hammer: 11, two of them kicks.

A blow lands where its striking part is swept against the opponent's bone
hurtboxes, in the clip's middle three fifths: the sword's blade, the
hammer's head and the upper half of its shaft, or a fist, foot or shin.
Damage scales 80–120 % with the striking point's speed on contact. A hit
staggers the opponent and cuts its action short; the blow that landed plays
its recovery 1.5 times as fast, so a hit opens a combo and a miss pays in
full. Nothing is invulnerable: a roll or an evade saves only by taking the
body out of the blow's way.

- **The hammer is a shield** against the sword: its head and the upper
  half of its shaft stop a blade that meets them, always, in a blow or not.
- **Forearms and shins** take no damage from the sword's blade (armour);
  other blows land on them.

## Adrenaline

The network kept finding strategies that worked well for reward but made
for very dull fights: spamming the same attack, ignoring defence entirely
and simply trading blows. So I added a parameter that the network sees as
one of its inputs and that is tied to reward, which I can tune by hand to
"direct" the fights. Really it's a way to pull the agents out of the local
minima they settle into. That's how the adrenaline mechanic came about.
Adrenaline scales up damage, and the network quickly learns that it's
"good". It's earned by, for example, combos that chain different attacks,
or last-moment evades.

- Six levels: 0, A, B, C, D, S.
- One level comes simply from standing within 3 m of the opponent.
- Earned by: dealing damage (a point per 30 damage) and evading blows (0.7
  a point). Taking hits earns nothing, so trading blows doesn't pump both
  fighters.
- Earned points hold for 1 s, then decay (0.35 a second).
- Higher levels multiply your damage: ×1, 1.1, 1.25, 1.4, 1.6, 2 at S. From
  level A on, actions cost only 5% of their normal energy.
- Separately, hits landed within 2.5 s of each other chain into a combo,
  +20% damage per hit. Each hit must be a different attack from the last.

## Rewards

- **Damage:** dealt and taken, as a share of the opponent's health (+) and
  your own (−).
- **Win:** +1. A loss adds nothing beyond the health already lost.
- **Time-out:** −2 for both, so stalling never pays.
- **Energy:** a small cost for every action, proportional to its duration
  (0.08 a second of clip).

Nothing else is rewarded: combos, adrenaline and evades pay only through the
damage they add. Extra rewards (on a repeated blow, for evades, for
standing apart) were tried and dropped.

## Balance

`balance.json` sets each action's damage, speed and stagger: a base by what
strikes (`sword`, `hammer`, `limb`), then rules that multiply it for a
weapon in hand (`held`), an animation (`blow`), or both. Speed plays the
whole clip faster, and its damage and energy follow. The training's
players read the file again as each fight begins, so a change needs no
rebuild or restart. `./balance.sh` opens a panel in the browser that edits
it with checks, shows every action's resulting numbers, which balance each
player took, and the actions table (how often each weapon takes each action
and lands it).

## Running

```sh
uv sync                        # once: Python 3.10.12, mlagents 1.1.0, PyTorch
./train.sh duel --build        # rebuild the arena and its player (Unity running), train
./train.sh duel --resume       # go on with a run
./train.sh duel --hours 8      # stop (and save) after 8 hours; 4 by default, 0 for none
./record.sh duel               # film a minute of the newest model to results/duel/videos
./watch.sh duel                # watch it in a window, live, until closed
./play.sh duel                 # play against the newest model in a window
./weapons.sh duel              # how often each weapon beats each other, lately
./actions.sh duel              # which actions each weapon takes, and how often each lands
./balance.sh                   # the balance panel
uv run tensorboard --logdir results
```

In `play.sh` you are the near fighter, in the same body as the model's. The
mouse turns the camera; W/S ahead and back, A/D aside. The left button gives
three blows of your weapon in turn (a combo), the right its heavy blow, F a
quick kick, Shift a quick evade the way WASD points, Space the roll.

Each run writes TensorBoard logs and checkpoints to `results/<run-id>`, and
`Fighter.onnx` on a stop; the game's level build takes the newest. The
trainer stops if free memory drops below 1.5 GB. It runs on one CPU core
(Python's GIL), about 330 steps a second with 4 players.
