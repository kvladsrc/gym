# Realms: game design

Status: a draft for discussion, 2026-11-09. The map exists (iteration 1, see
[README](README.md)) and building (iteration 2) has its first build with its own
models; iteration 3 (heroes, monsters, fog of war and battles) has its first
build with its own models too, and a second one after the players' notes
(controls, the exchange of units, the fog's and the ground's look, a calmer
look with a dark interface). This
document plans everything after the map: goals, mechanics, the economy and the
order of iterations. The questions in [Open questions](#open-questions) are for
the players to answer before the rules of the iteration they affect are fixed;
the answers so far are listed there too. Numbers are first guesses that playing
will change.

## The idea

A colourful turn-based map strategy for a father and son who rule one kingdom
together at one computer. The kingdom lies on an island of hexagons: you
build up your towns, open farms, mines and workshops, clear the lairs of
trolls, wolves and boars, and race five other kingdoms to a goal chosen at
the start. A game takes 30–60 minutes, about 40 turns.

The son likes global strategy with a map where you build cities. Building is
therefore the centre of the game. After playing the first build the players
chose adventure as its second half: a hero leads an army across an island
hidden in fog, fights monsters in a battle mode of its own and brings home
treasure, as in hero-led strategy games. There is no war between the
kingdoms for now.

## Pillars

1. **Everything you build is on the map.** A new farm appears on its wheat
   field, a workshop beside the town hall, a road between towns, a bigger town
   gets more houses. The map is the scoreboard.
2. **Few numbers, all of them visible.** Four basic goods and a few crafted
   ones, always shown as the same icon chips. Before any action the game
   shows what it costs and what it gives.
3. **Mistakes slow you down, they do not end the game.** No starvation that
   kills towns, no bankruptcy that disbands the army in one turn. A shortage
   stops growth and says why.
4. **Short turns.** A turn is a few decisions: what to build, where to send
   the army, end the turn. The other kingdoms move quickly and show what they
   did in a few lines.
5. **Clear goals.** The game always says what you are working towards: the
   victory goal and a few small tasks ("build a farm on the wheat").

## What already exists

| Part | State |
| --- | --- |
| Map | 70 × 42 hexes, 38–50 % land, biomes (meadow, plains, forest, hills, mountains, beach, shallows, ocean), rivers along the hex edges, lakes; fog of war over everything the players have not seen |
| Kingdoms | 6, each 12+ connected land cells with 2–4 towns, the capital joined to every town by road; no war between them |
| Resources | wheat, fish, sheep, wood, stone, gold; every kingdom has wheat, wood, stone and gold |
| Building | field buildings on resources, town buildings (market, granary, tavern, barracks, archery range, stables), walls and a castle, town growth and levels up to the royal city, borders that grow from the towns, settlers and new towns, small tasks, the turn report |
| Heroes | one at the start, up to three from taverns; paths with the turn each cell is reached, experience, levels with an attack or defence card; recruits hired in towns join the hero there or wait for him |
| Monsters | seven kinds in groups that grow deeper in the wild, lairs of troll, wolves and boars; chests, piles, shrines and watchtowers as places |
| Battles | tactical, on an 11 × 7 hex field: walking, striking, shooting, defending, strike-backs, auto battle and retreat; a quick battle and the odds before each fight |
| Turn | the Enter key: income goes to the treasury, recruits come, heroes get their moves back |
| Art | town hall, tavern, spinnery, weaver, tailor, hunter's lodge, tannery, smelter, five field buildings, market, granary, three cottages, troll, wolf, boar, sheep, hero, spearman, archer, rider, goblin, bandit, skeleton, spider, barracks, archery range, stables, chest, two shrines, watchtower, town wall and tower; icons for the goods, crown, swords, lair, hammer, kingdom shields |

## The turn

1. **Income.** Every town and every worked cell adds its goods to the
   treasury; armies and buildings take their upkeep. A short report shows the
   difference ("+12 gold, +5 food…").
2. **Decisions.** In any order:
   - in a town: build or upgrade a building, train a unit;
   - on a cell of your land: build a field building (farm, mine…) or a road;
   - next to your land: found a new town with settlers (the land itself grows
     from the towns by itself);
   - with a hero: move, fight a monster group, visit a chest, shrine or
     tower, take the recruits waiting in a town.
3. **End of turn.** The other kingdoms and the monsters act; the game shows
   the important things (a town grew, wolves attacked a farm, a neighbour
   cleared a lair, a goal is close).

There is no limit on the number of actions except the goods. A building is
ready at once and counts in the income at the end of the turn; build times
come only if playing shows they are needed. The player does not have to plan
many turns ahead.

## Land and towns

**Territory.** Land belongs to a kingdom cell by cell. The players chose
"it grows by itself, no buying" (2026-11-11); the rule for the child is «Города
сами берут землю вокруг. Чем больше город, тем быстрее и дальше». It grows in
three ways:

- *the towns' influence* (`Core/Influence.cs`, below);
- *clearing a lair or a group*: its cell joins the winners' land at once if it
  touches that land or lies within one of their towns' reach;
- *founding a town* with settlers on wild land at least three cells from any
  other town: the town takes the cells around it and grows from there.

**Influence.** Every turn each town gains influence: its level (1–4), one more
for a capital and one for a market. For 6 influence it takes the cheapest wild
cell beside the kingdom's land within its reach, which is 5 + level in the cost
of the walk over its own and wild land (meadow and plains 1, beach 1.2, forest
1.5, hills 2, mountains 4, a river crossed +1). A resource cell counts 1.5
cheaper, so it comes first. No town takes the sea, a cell with monsters, a
lair's neighbours, or a cell two steps or nearer to another kingdom's town; a
cell two kingdoms want goes to the cheaper way, then to the kingdom with less
land. A cell once taken never changes hands. A town with nothing to take keeps
at most 6 influence and the turn report says «некуда расти» once; the cell card
says which town takes a wild cell in how many turns, or why none will. All six
kingdoms grow by the same rule.

What the players see of it (`Scripts/BorderMarks.cs`): the cell each of their
towns takes next is outlined with dashes in the kingdom's colour, with the
kingdom's small flag in it; while Alt is held, the other kingdoms' next cells
show as dashes without a flag; a chosen town of theirs puts a dot on every wild
cell it reaches; at the turn's end the cells just taken flash and settle as
their border appears. The kingdoms' list shows «+N» beside each land bar for
the cells taken last turn, and its rows scroll above the bottom panel.

**Stake** (вешка; the territory plan's П3, without buying). A kingdom may set
one free stake on a wild cell one of its towns reaches and could take (open
land, not beside a lair, not near another kingdom's town). The town with the
cheapest way there then spends its influence on that way's wild cells first,
the stake's cell last; the other towns grow as before. The cell card of a wild
cell says when the border would reach it with and without a stake («С вешкой —
примерно через N ходов») and sets, moves or removes it; the stake shows as a
tall banner in the kingdom's colours with dots along the way. The stake goes
when the cell is taken: the turn report says the border reached it, or that a
neighbour took the cell first. A task asks for the first stake (5 wood). The
neighbours, and the measurer's players, stake the nearest wild resource their
towns reach. Measured on 60 maps at «Обычно», it steers rather than
strengthens: the players' first wild resource inside their land comes on turn
5.0 instead of 5.7, 4.6 such resources by turn 20 instead of 3.9, the crown on
turn 41 instead of 43, and the land gained is the same.

The kingdoms start with what their towns reach less three (`Influence.Trim`)
plus the cheapest way from each town to the capital, so every town has a ring
to fill; the roads between capitals still follow the land before the cut. The
measurer (`GameReport`, "Land") checks the territory plan's targets: on 20 maps
a kingdom starts with about 60 cells, takes about one a turn early on, the
players' bot reaches +60 cells (the Lands gem) on turn 39 on average, and the
passive kingdoms stall after turn 45 until they found towns.

**Settlers** (exist). A town's hiring tab gathers a settlers' cart for 5 gold
and 8 food for every town the kingdom has, so each new town makes the next one
dearer. A hero in the town takes the cart at once, otherwise it waits there for
the next hero who comes or is hired. The hero card then has a "found a town"
button: on any land but mountains, with no resource or place, wild or the
kingdom's own, three steps or more from every town. The players name the town (a
free name is offered); it starts as a village of two houses and takes the wild
cells around it, but not open sea, monster cells or another kingdom's land. The
map is drawn again, as the ground levels under the new town, and the new town's
screen opens.

**Towns** have levels, shown by the number of houses on their cells:

| Level | Name | Houses | Taxes a turn |
| --- | --- | --- | --- |
| 1 | village (деревня) | 2–4 | 2 gold |
| 2 | town (город) | 5–7 | 3 gold |
| 3 | city (большой город) | 8–10 | 4 gold |
| 4 | royal city (королевский город), with a castle | 11–13 | 5 gold |

The capital starts with 5 houses (level 2), other towns with 2 or 3. Besides
its taxes every town gives 1 food a turn, the capital 2 food, 1 gold and 1
wood more.

A town grows with **food**. Every turn each town takes up to 2 food from the
treasury into its growth bar, 4 with a granary. The next house needs twice as
much food as the town has houses: a village of 3 houses needs 6 food for its
fourth. When the treasury runs out of food, the towns further down the
list wait for the next turn; nothing is lost. A town grows past 7 houses
(level 3) only with a **tavern**, and past 10 only with a **castle**, to a
royal city of 13 houses. The town panel always says why a town is not
growing.

**Places and quarters.** A town's centre cell has 5 places round its square,
with the capital's hall, the castle or a well in the middle. Every house and
every town building takes a place. When they are all taken, the town stops
growing and building until the players grow it onto a neighbouring cell. That
cell becomes a quarter with 6 more places and costs 6 gold and 8 wood. The
button is on the card of an empty cell of the kingdom beside the town's
centre: land that is not mountains, with no resource, place or monster group.
It shows only while the town is full.

The capital starts with one quarter, as its 5 houses fill its centre. A
building keeps the place it was built on, and houses fill the free places in
order, so nothing moves as the town grows. Walls go round the centre only. A
royal city with every building needs 19 places: the centre and three
quarters.

The players asked for quarters on 2026-11-09, as the buildings of big towns
overlapped. The quarters replace the old building slots per level; the level
still decides what can be built. The other kingdoms grow their towns onto
quarters too, in their own turns (see "The other kingdoms").

**Flags.** Every kingdom has a flag: a pattern of two colours (plain, two
halves side by side or one above the other, quarters, halved across, a cross,
a saltire, a chevron, a band, a border or a triangle at the pole) and an
emblem in a third colour (18 heraldic figures, or none), each colour one of
ten. A new map gives each kingdom one from the seed, in the kingdom's colour
and with a metal (white, yellow) on a colour or the other way round. The
players draw their own in the flag editor, which opens when a game starts and
on a click on the shield in the top bar; it flies on the towns' and heroes'
banners and on the shields of the interface, and new games keep it. The
players asked for the editor, as founding towns and drawing flags are what the
younger player likes most.

The field is the kingdom's colour: its borders, roofs, banners, plates and
minimap, and its name (Алое королевство for red, Лазурное царство for blue,
one for each of the ten colours). No two kingdoms share a field; a kingdom
whose colour the players take gets their old one and a flag drawn again on
it, with the same pattern and emblem.

**Fortifications.** Walls and a castle take no place. Walls (from
level 2) beat off the lairs' bands (see Raids below) and take a small loot
from them. They first stopped monster groups within three steps from
growing, which did nothing: groups stand three steps or more from the towns
anyway (the fun plan of 2026-11-11). The castle (from level 3, inside walls) lets the town grow into
a royal city. With no war, walls had to do something now; the players asked
for walls and castles after the settlers build, and a siege can use them
later (iteration 7).

**Worked cells.** A resource gives its goods only when it lies on the
kingdom's land and has its field building; a resource without one gives
nothing. Rivers help: a farm on a cell beside a river gives one more food.

**Coast.** The shallows go with the land beside them: each wild shallow cell
belongs to the kingdom that holds most of its land neighbours. Open sea belongs
to nobody.

## Economy

### Goods

| Good | Icon | Comes from | Spent on |
| --- | --- | --- | --- |
| Gold | coins | towns (taxes), gold mines, trade, lairs | almost everything, army upkeep |
| Food | wheat, fish | farms, fisheries, pastures | town growth, settlers, army upkeep |
| Wood | logs | lumber camps | buildings, roads, ships, bows |
| Stone | stone | quarries | buildings from level 2, walls, the wonder |
| Wool | sheep | pastures | spinnery |
| Cloth | (new icon) | spinnery + weaver | town growth from level 3, tailor |
| Clothes | (new icon) | tailor | royal city, festivals, trade |
| Hides → leather | (new icons) | hunter's lodge → tannery | armour, scouts |
| Ore → iron | ingot | iron mine → smelter | soldiers, knights, tools |

The four basic goods are enough for the first iterations. Crafted goods come
in when towns reach level 3 and the army needs iron (see
[Iterations](#iterations)); this matches the workshops the Studio already
made.

### Buildings

Field buildings stand on a resource cell of the kingdom's land, one on a
cell:

| Building | Cell | Cost | Gives a turn | Model |
| --- | --- | --- | --- | --- |
| Farm | wheat on plains or meadow | 5 wood | +2 food (+1 by a river) | needed |
| Fishery | fish on the coast or a lake | 5 wood | +2 food | needed |
| Pasture | sheep | 4 wood | +1 food, +1 gold (+1 wool instead of gold once crafts exist) | sheep (exists) + fence |
| Lumber camp | wood in a forest | 3 gold | +2 wood | needed |
| Quarry | stone on hills or mountains | 5 wood | +2 stone | needed |
| Gold mine | gold in the hills | 8 wood, 4 stone | +3 gold | needed |
| Iron mine | iron (a new resource) in the hills | 8 wood, 4 stone | +2 ore | needed |
| Hunter's lodge | any forest cell | 4 wood | +1 food, +1 hides | exists |

Town buildings take a place of the town (see places and quarters above):

| Building | From level | Cost | Does | Model |
| --- | --- | --- | --- | --- |
| Market | 1 | 10 gold, 5 wood | +2 gold; trade with other kingdoms | needed |
| Granary | 1 | 8 wood, 2 stone | the town takes 4 food a turn for growth instead of 2 | needed |
| Tavern | 2 | 10 gold, 8 wood | the town can grow to level 3; hires heroes | exists |
| Spinnery | 2 | 8 wood, 4 stone | 2 wool → 1 yarn | exists |
| Weaver | 2 | 8 wood, 4 stone | 1 yarn → 1 cloth | exists |
| Tailor | 3 | 10 wood, 6 stone | 2 cloth → 1 clothes | exists |
| Tannery | 2 | 8 wood, 4 stone | 2 hides → 1 leather | exists |
| Smelter | 2 | 6 wood, 10 stone | 2 ore → 1 iron | exists |
| Barracks | 1 | 6 wood, 2 stone | 4 spearmen a turn to hire | needed |
| Archery range | 1 | 4 gold, 6 wood | 3 archers a turn to hire | needed |
| Stables | 2 | 8 gold, 6 wood, 4 stone | 2 riders a turn to hire | needed |
| Walls | 2 | 4 wood, 12 stone | no place; beats off the lairs' bands for a small loot | wall, tower |
| Castle | 3, inside walls | 10 gold, 6 wood, 20 stone | no place; the town grows to a royal city of 13 houses | castle |
| Harbour | 1, on the coast | 15 wood | fishing boats, ships to other islands later | needed |

The yarn step can be dropped if three steps for clothes are too long (see the
questions); then the spinnery is a decoration of the weaver.

### Balance targets

- A game takes 30–60 minutes, about 40 turns: the first turns take seconds,
  the later ones a minute or two.
- The start (20 gold, 10 food, 10 wood, 5 stone) pays for two or three field
  buildings in the first turns. The towns' food (4 a turn with two towns)
  does not feed their growth (6 a turn), so the first farms matter at once.
- A kingdom's income doubles in about ten turns of sensible building.
- The first village becomes a town around turn 6, the first level-3 town
  comes around turn 15, the victory goal is reachable around turn 35–45. A
  test plays every kingdom with a simple builder on twelve maps: the first
  town comes at turns 5–9, the first level-3 town at turns 10–18.
- Upkeep never turns the treasury negative by itself: units that cannot be
  paid stop and wait at home; buildings without inputs just stand idle.
- Seen in the first build: a simple player that builds every field building
  it can had 143 food after 14 turns, gaining 15 a turn, because a town
  takes at most 2 food a turn (4 with a granary). Past the first few farms,
  food has no use until armies and settlers need it (iteration 3). Building
  in towns first instead gives 60 gold (+20), 50 food (+10) and 4 wood (+3)
  after 14 turns: wood is the bottleneck, the tavern (8 wood) waits for
  turns while gold piles up.
- Played on 2026-11-08: the look and the building were liked, but everything
  was built quickly and then there was nothing to do; what to do about the
  lairs and the other kingdoms was unclear. The gold mine task could not be
  done: on about one map in seven the nearest gold lay behind a lair or a
  neighbour, so every kingdom now gets gold of its own.

## Heroes, armies and monsters

### Heroes

A **hero** is the kingdom's piece on the map: a rider under the kingdom's
banner who leads an army of up to five **stacks** (a stack is one kind of
unit with a count, "12 spearmen"). The kingdom starts with one hero in the
capital with 10 spearmen and 4 archers; a town with a tavern hires another
for 15 gold with 5 spearmen, up to three heroes.

| | Start | Grows |
| --- | --- | --- |
| Attack | 1 | +1 at a level-up if chosen, shrines |
| Defence | 1 | +1 at a level-up if chosen, shrines |
| Moves a turn | 16 | — |
| Level | 1 | experience: 100 for level 2, 300 for 3, 600 for 4, 1000 for 5… |

The hero's attack and defence add to every stack of his army. Experience is
the health of the monsters the army killed. At a level-up the players choose
one of two cards (attack or defence); skills come later (3b). The hero does
not fight himself and never dies: beaten, he goes back to the nearest town
of the kingdom without an army and waits there for the next turn.

**Moving** costs moves per cell entered: a road 1, plains, meadow and beach
2, forest and hills 3, mountains 4; crossing a river where no road crosses
it 2 more. Water stops heroes (ships later). Heroes may cross other
kingdoms' land but not enter their towns. The left button only selects
(a cell, a town, a hero), so looking at a town never sends a hero there; the
right button gives orders: a right click draws the chosen hero's path with
the cell where each turn ends, a second one on its end (or G) goes. Entering a
monster group's cell starts a fight; entering a place visits it and the
hero walks on. A path may end on another hero of the kingdom: the hero walks
to the cell beside him and the two exchange units.

### Units

Towns hire units from their buildings. Each building adds recruits to the
town's pool every turn (up to three turns' worth); hiring takes them into the
army of a hero in the town, or into the town's garrison, which the next hero
to enter takes along. The army is split between heroes by exchanging: two
heroes on neighbouring cells, or a hero and the garrison of the town he
stands in, pass units across in a dialog (all of a kind, one, or half). A
hero left without units cannot fight, so the dialog warns about it.

| Unit | Building | Recruits a turn | Cost each | Health | Attack | Defence | Damage | Speed | Special |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Spearmen (копейщики) | barracks | 4 | 4 gold, 1 food | 10 | 4 | 5 | 1–3 | 4 | — |
| Archers (лучники) | archery range | 3 | 6 gold, 1 wood | 8 | 5 | 3 | 2–3 | 4 | shoot |
| Riders (всадники) | stables | 2 | 14 gold, 2 food | 22 | 7 | 6 | 3–6 | 7 | — |

Food and gold, which piled up in the first build, now buy the army. There is
no upkeep yet.

### Monsters

Monsters stand on wild land in **groups** of one kind; a group never moves,
but it grows by a sixth every year (four turns), up to twice its first size.
A group's size follows how deep in the wild it stands: the steps over land
from the player's kingdom, counted from the first unowned cell, so a kingdom
hemmed in by its neighbours still has the wild at its door. At the edge a
group is under half the first hero's army (three small groups are placed
there first, so every map starts with easy fights), even at six steps,
three times as strong at twenty. Lairs hold the group of their kind. Most
places (below) beyond three steps have a group on them. The player's capital
always stands on the biggest landmass, so the heroes can walk to the wild.

| Monster | Health | Attack | Defence | Damage | Speed | Special | Where |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Goblins (гоблины) | 5 | 3 | 1 | 1–2 | 5 | — | near, many |
| Boars (кабаны) | 8 | 4 | 2 | 1–3 | 6 | — | near; boars' lairs |
| Wolves (волки) | 12 | 5 | 3 | 2–4 | 8 | bite without a strike back | middle; wolves' lairs |
| Bandits (разбойники) | 10 | 5 | 4 | 2–3 | 4 | shoot | middle |
| Skeletons (скелеты) | 9 | 5 | 5 | 1–3 | 4 | arrows do half damage | middle |
| Giant spiders (пауки) | 16 | 6 | 4 | 2–5 | 6 | web: the stack they hit cannot walk in its next turn | far, in forests |
| Troll (тролль) | 60 | 10 | 8 | 8–14 | 4 | heals a third of his health at the start of each round | far; troll's lairs |

Beating a group gives its experience and a reward in gold (and goods for
lairs). A beaten lair or group leaves its cell to the winners' land when it touches
that land or lies within their towns' reach; the towns then grow around it.

### Raids

The lairs send bands (ватаги) against the towns: the fun plan's Ф3 (2026-11-11),
so that the world does something and walls have a use. From turn 12-15 a
kingdom meets one band at a time and at most one every 12 turns; it comes from
a lair within nine steps whose nearest town is the kingdom's, the one that
rested longest. The band is a third of its lair's group and stands on the map
as a group, so a hero can fight it on its way. It walks three steps a turn to
the town; a line over the map says «Волки идут к городу Овсянка — 2 хода», and
the band's card says where it goes. A walled town beats it off and takes the
reward of a group its size. At a town without walls it stands three turns on
one of the town's built fields: the town does not grow and that field gives
nothing. Nothing is destroyed, and then it goes back. A cleared lair sends no
more bands, nor does a lair a neighbour's troop besieges, and bands do not grow.

The first rule, every lair raiding every eight turns, kept a kingdom with many
lairs nearby under siege all game: the players' bot lost 8.5 town-turns a game
(up to 47), against the economy plan's 5. With the kingdom's clock, on 60 maps
the bot meets 3.3 bands in 80 turns and loses 3.0 town-turns (2.9 by turn 45;
more than 5 on 14 maps); the crown comes as before.

### Places

| Place | A visit gives | Model |
| --- | --- | --- |
| Chest | a choice: gold (8–15) or experience (80–150) | chest |
| Pile | 5–8 wood, 4–6 stone or 8–12 gold | the resource props, smaller |
| Shrine | once per hero: +1 attack (the red shrine) or +1 defence (the blue one) | shrine |
| Watchtower | opens the fog seven cells around it | watchtower |

### Fog of war

The island starts hidden under clouds except the kingdom's land and two cells
around it. A hero opens three cells around him (four from hills and
mountains), a cell the kingdom takes two around it, a watchtower seven. What has been seen
stays open: monsters do not walk, so the second, "not seen right now" level
of fog that real-time games use (*Game Interface Design*, p. 178) would hide
nothing. Hidden cells show no resources, places, monsters or names, also on
the minimap. The clouds are a dark grey bank about a cell high with rounded,
billowing edges, darker than the land so the explored island stays the
brightest part of the picture; towns, monsters, places and mountain peaks of
unseen cells are hidden until a hero sees them.

### Battles

**Pillars** (as *Game Balance*, ch. 10 asks: time, pacing, feel,
participants, progression, volume): turn-based; short, three to six rounds
and a couple of minutes; bold and readable, with the odds known before;
the hero's army against one monster group; the hero's levels and bigger
armies as progression; about a third of the play, beside building and
exploring.

**The battlefield** is a field of 11 × 7 hexes in a mode of its own, with
the ground of the cell where the fight happens and up to four obstacles from
its biome (trees in a forest, rocks on hills). The hero's stacks stand on
the left, the monsters' on the right; a monster group splits into up to four
stacks.

**A round:** every stack acts once, the fastest first, the players' first at
equal speed. A stack walks up to its speed (around obstacles and stacks) and
may strike a neighbour; a shooter shoots any stack instead, unless an enemy
stands next to it; any stack may defend instead (+50 % defence until its
next turn).

**Damage** = count × a random number from the damage range, times
`1 + 0.1 × (attack − defence)` (at most ×3) when the attack is higher, or
divided by `1 + 0.05 × (defence − attack)` when it is lower. Shooting beyond
six cells, and shooters striking in melee, do half. The stack struck back
once a round (not at shooters). Damage goes into the top unit first; the
others are whole.

**The end:** a side without stacks loses. The players may retreat at any
time: a loss, but the hero leads the living away, each stack leaving a
quarter behind (rounded), and goes back to the nearest town out of moves. A
hero beaten to the last unit finds a militia of 5 spearmen in that town (the
fun plan of 2026-11-11: a loss took the whole army, so only easy fights were
worth taking). A beaten group keeps its losses: measured over the auto
battles, two passes (one army retreating at half its worth, then its
survivors with a second army) lose about twice what both armies lose in one
pass, and win less often, so coming back is no trick. The **auto battle** plays the players' side: shooters shoot
the stack they would hurt most, the others go for the stack they can reach
and hurt most, shooters first.

**Tempers:** each monster kind goes for its own target, told on its card, so
that every group sets the players a different problem and they can plan
against it (the battle plan of 2026-11-10: every fight felt the same, the
monsters always running at the archers). Goblins go for the nearest stack,
and the next goblins pile onto a stack goblins struck this round; boars for
the stack in the row nearest their own; wolves for the weakest (the least
health in all); bandits shoot the nearest; skeletons go for the nearest stack
that does not shoot, the shooters last; spiders for the fastest not caught in
a web yet; the troll for the biggest (the most health in all). Between
stacks the temper likes as much, the nearer comes first, then the one they
would hurt more. A monster strikes its pick when it can reach it this turn,
or else walks towards it.

**Surrender:** the first time a group is down to a third of its health while
the players' stacks have twice what it has left, its kind rolls once to offer
to give up (the players asked for surrender by the monster's kind and chance,
not in every fight, 2026-11-10). Goblins offer in about half of such fights,
bandits 40%, wolves 30%, boars 25%, spiders 20%, the troll 15%; skeletons
never. A question asks whether to let them go (Enter: the fight is won now,
with no more losses, and they run off the field) or finish them off (Esc:
more experience, which comes only from monsters killed). The reward is the
whole group's either way. The quick battle asks the same question; the odds
count the offer as taken. Measured over the auto battles: 9% of the start
army's fights, 19% of the middle army's and 25% of the late army's end so.

**Odds:** before a fight the game plays it 200 times with the auto battle on
both sides and shows the chance in words (easy above 80 %, even from 40 %,
dangerous below) with the expected losses and the reward. A won fight pays
the group's reward once when it looked easy, twice when even and three
times when dangerous (the fun plan of 2026-11-11: the reward was smaller
than the losses); the factor is the one shown before the fight.

**Later: a real-time battle.** The players wished for battles led from the
hero's shoulder in real time. That needs animated models for every unit and
monster, steering for dozens of units at once and new controls: more work
than the whole game so far. The battle is therefore a mode with a fixed input
(the two armies and the cell) and output (the survivors), so a real-time
battle can be tried in its place later without changing the rest.

## The other kingdoms

**Their turns** (exist, since 2026-11-11; the economy plan's step 2). The five
other kingdoms play their own turns at the end of each turn, before the
income. Each buys at most what the pace allows, chosen in the «Новая игра?»
question and kept between games: at «Спокойно» one thing in three turns, at
«Обычно» two in three, at «Гонка» three in two. They follow the players' rules
and costs: lumber camps, a tavern, a quarter for a full town, walls and a
castle, food while their towns want more, then a granary, quarries, gold mines
and markets. Each has a temper, an order of the gems Столица, Города and
Земли and Охотник picked by the map's seed, and puts first what its current
gem wants: the capital, the smallest towns, markets early for their influence,
or a troop to a lair. The kingdoms' list shows beside each crown the gem it is
after. They found no towns and have no heroes; their land grows by itself like
everybody's, and their towns' new quarters are drawn at the turn's end.

**Their troops** (exist, since 2026-11-11; the economy plan's step 4). A
kingdom without a hero buys a troop for a lair: spearmen worth 1.2 times the
lair's group at the spearmen's price. It has one out at a time and sends it to
the nearest lair within nine steps of its towns that it can pay for, as one of
its turn's purchases: first when it is after the Охотник gem, otherwise with
the gold left at the end. The troop stands three turns at the lair under the
kingdom's flag; then the lair is cleared for that kingdom as a hero's win would
be (the reward, the Охотник count, the cell into its land). A lair under a
troop sends no bands. If the players' hero clears the lair first, the troop
goes home with half its price. The players see the flag on a lair they know,
a line over the map («Янтарный берег осаждает логово: возьмёт его через 2
хода»), the turns on the lair's badge, a «Осада» line on its card and the
turn report's lines. A kingdom saves for a troop only if its income pays for
it within six turns: before that rule a kingdom after the Охотник gem saved
all game for a troll lair it could never pay for and built nothing else.

The lairs' groups grow, so on 60 maps at «Обычно» the first troop leaves
around turn 38; by turn 45 the neighbours together hold 1.2 lairs and the
players' bot 0.5, by turn 80 4.6 and 1.5. The bot's Охотник gem came on 15 of
60 maps, as before. The measurer (`GameReport`) set the paces on 60 maps:
its players' bot, which founds no towns, sets its crown first on 95%, 78% and
53% of them (the plan wants 70–80% at «Обычно»). The lead still changes
seldom, 0.4 times a game at «Обычно» against the plan's once in every other
game; the raids and the neighbours' troops did not change it (players first on
51 of 60 maps with the troops).

Later the AI may get a fuller personality from the kingdom's name and colour:
the builder (towns and wonders), the farmer (food, many towns), the warrior
(army, lairs, later war), the merchant (markets, trade). It follows the same
rules and costs as the players and shows its intentions on the map. It needs
no look-ahead search: a few rules per concern (economy, expansion, army) weigh
simple measures such as free land nearby, the danger of lairs and the strength
of neighbours, kept on influence maps over the hexes. The same maps give the
players' advisor its hints.

**Relations** with each kingdom: peace, alliance or war.

- *Peace* (the start): borders are closed to armies, trade is open.
- *Alliance*: armies may cross each other's land; allies share their view and
  help against monsters.
- *War*: armies fight on contact, towns can be besieged and taken (a taken
  town keeps its buildings and changes colour).

The players chose no war between the kingdoms for now (2026-11-08): there is
peace only, heroes may cross other kingdoms' land but not enter their towns,
and the race is for wild land, lairs, treasure and the goal.

**Trade** goes through markets: offers of the kind "6 wood for 4 stone" from
the AI kingdoms each turn, and a market price for selling goods for gold.

## Victory

**The crown** (exists, since 2026-11-11; the economy and fun plans of that
day). Every kingdom has a crown with five sockets, one gem for each great
feat, won in any order and never lost:

| Gem | Feat |
|---|---|
| Столица | the capital grows to 11 houses, a royal city (level 4) |
| Города | four towns of 8 houses (level 3) |
| Охотник | three lairs cleared |
| Герой | a hero of the fifth level |
| Земли | the kingdom's land 60 cells bigger than at the start |

The first kingdom to set three gems has won; the players chose this short
game, about 35–45 turns, as the main one, and the numbers are first guesses
for the measurer (`GameReport`) to tune. The top bar's crown shows the
sockets and opens the feats with their progress («логова: 2 из 3») over the
kingdoms' list, where each kingdom shows its own count. A gem gets a line in
the turn report or a message at once; the first crown of the game, anyone's,
ends it with a screen of the gems by turn and the game's counts, and the
players may play on: their own crown, if it comes later, is shown once more
with its place. Enter and Esc play on; a new game needs a click.

Further goals, for a longer game once its middle has more to do:

- **Wonder.** Build the Great Castle (большой замок) in your capital: four
  stages, each needing stone, gold, iron and clothes. The first kingdom to
  finish wins. Every stage changes the capital's model, so everybody sees who
  is ahead.
- **Monster hunter.** Clear every lair on the island, then the dragon's lair
  that wakes up in the mountains after turn 25.
- **Conquest.** Hold every capital (only with war, which is not planned).
- **Prosperity.** The best score at turn 40: towns' levels, land, buildings,
  goods.

The interface always shows the progress of each kingdom towards the goal.

**Small tasks** (задания) lead through the game: "build a farm", "grow a town
to level 2", "clear the boars' lair", "found a new town". Each gives a small
reward and teaches one rule; together they replace a tutorial. A task shows
the need before the means: first "the boars eat the harvest", then "train
spearmen". An advisor warns about what will happen in the next turns (food
runs short, wolves come, a rival is about to finish a wonder stage).

**Keeping the race open.** Growth feeds itself: more land gives more income,
which buys more land. Against that, each new town makes the next settlers
and every bought cell dearer, the AI kingdoms trade worse with the leader,
and wild land far from towns stays dangerous. At the end the opposite is
wanted: once a kingdom starts the last wonder stage, everybody sees it and
the game ends in a few turns rather than dragging on. A game has three
phases: building at home (turns 1–10), lairs and rivals (10–25), the race
for the goal (25 and later).

## Interface

The map's interface is two panels attached to the screen's edges, not
windows floating over the map (since 2026-11-09; before, seven separate cards
stood over the map at the start). Stronghold is the reference for the layout
only, not for its art: an information bar along the top and a panel along the
bottom with what is chosen. Lists open from the bar under it, the town and
hero screens are side panels at the left and right edge, and only dialogs
(a fight, a level-up, an exchange, the help) are framed windows.

- **Top bar** (exists): the kingdom's shield (its flag; a click opens the
  flag editor) and name, the treasury as icon
  chips with the income per turn, the season and year, the end-turn button,
  the menu (Esc), the crown with the kingdoms' list (K) and the help.
- **Menu** (exists, since 2026-11-11): a column along the left edge, as dark
  and square as the town screen, with the live map beside it. The bar's
  «Меню» or Esc, once nothing is left to close, opens it: back to the game,
  save, load, a new game, how to play, to the main menu, quit, and the
  version at the bottom. It took the place of the bar's «Новая карта» and
  the N key, with which one slip could end a game. Quitting asks nothing, as
  the game saves itself. In a battle it has only back, help and «Выйти (бой
  начнётся заново)», and the battle waits. Add: settings (sound).
- **Main menu** (exists, since 2026-11-11; the interface plan's 3.2): the
  game opens on it at once, over the game's picture (a castle on a hill,
  groves and a boar, drawn for it), with no panel: the logo a little above
  the middle, the crown with six faces over the name lettered in the same
  picture, Hexcrown (the family's choice of 2026-11-11, a crown of six faces
  like the map's hexagons; no motto), and the buttons centred under it.
  «Продолжить» with the last game's kingdom and turn (gold, Enter), «Новая
  игра» (gold when there is nothing to continue), «Загрузить», «Как играть»
  and «Выход». Nothing is drawn behind it: «Продолжить» names the newest
  save from its header alone, and a game is drawn only when the players go
  into it (the players found the start hanging while the last game loaded
  behind the menu). Its return report shows once they are in.
  «Новая игра» opens the new game's screen: the map it would start on
  beside the column, its number, «Другая карта», a map by its number, the
  neighbours' pace, «Начать» (Enter) and «Назад» (Esc), which brings back
  the game it replaced. The preview is never saved; «Начать» starts it and
  opens the flag editor. A map asked by its number (`-seed`) starts without
  the main menu.
- **Loading screen** (exists, since 2026-11-11): «Продолжить», a save from
  «Загрузить», a new game's map and «Назад» to a kept game draw their map
  under a screen of the menu's picture with the logo, «Загрузка…», the game
  or the map's number and a gold bar. The bar follows the map's drawing: the
  ground, then each cell of its own and each block of plants and decor, a
  frame every 60 ms or so. A map that fails to draw brings the main menu
  back with the reason.
- **Languages** (exist, since 2026-11-11; the interface plan's 3.6): Russian
  and English. «Русский / English» stand at the foot of the main menu and of
  the menu, in their own words; a choice is kept. The game opens in the
  system's language (Russian on a Russian system, English elsewhere), but
  players from before the English version keep Russian. Another language
  starts the scene again, and a game being played saves itself and opens
  again under the loading screen. Every phrase is written in place as
  `T("…", "…")` (Scripts/Lang.cs), so one without its English does not
  compile; English counts with `En(n, one, many)`. The rules keep the
  Russian names of kingdoms, towns and heroes as keys, so a save opens in
  either language; their English stands beside them in Core/Names.cs: towns
  and kingdoms of the same spirit (Овсянка is Oatfield), heroes in Latin
  letters (Ratibor). LanguageTests catch Russian written without its
  English and keep the names' tables in step. `-lang ru|en|long` chooses a
  language for one run; long is English stretched by 40 %, to see where a
  line will not fit.
- **Saves** (exist, since 2026-11-11; the interface plan's 3.1): the game
  writes itself after each turn's end, before each battle, before a new game
  and on quitting, into three autosaves of the game in turn; the autosaves
  of the last three games are kept. A battle is never saved: quitting in one
  goes back to the save before it, whose kept chance gives the same battle.
  The players have six slots in the menu's «Сохранить» list; an occupied one
  asks before it is replaced, the first empty one is gold. «Загрузить» lists
  this game's autosaves, the newest of each earlier game and the own slots;
  a row loads at once, as loading another game saves this one first. A file
  that does not open says why (another game's, damaged, too old, too new)
  and takes no click. The main menu's «Продолжить» opens the newest save.
  «Сохранено» shows beside the turn counter for two seconds. The format
  (Core/Save.cs) is the whole world and game state with the random numbers,
  the camera, the chosen hero and the last turn's report, behind a header
  and a hash; a census test fails when a field is added without a place in
  it.
- **Bottom panel** (exists): the chosen hero (portrait, a tab a hero, level,
  attack, defence, moves, the army and the hero's buttons), the cell under
  the pointer or chosen (what it is and whose on the left; the town, the
  actions and the hint on the right) and the minimap. Over the panel the
  last cell stays shown, so its card can be read.
- **Cell card** (exists): what the cell is, gives and costs, and the actions
  for the cell: build here, buy, enter the town.
- **Map camera** (exists): moves with W A S D or a right drag, zooms to the
  pointer, turns with Q and E (or Z and X) or a middle drag. The minimap
  shows the view as a quad and a compass beside it; a click on the compass
  faces north.
- **Town card** (exists): on the map, the town's level, growth and income,
  how many buildings it can afford and the way into its screen.
- **Hero card** (exists): the hero's level and experience, attack, defence,
  moves left and the army's stacks with their counts; the path is drawn on
  the map while choosing a target, with the cell where each turn ends; the
  odds and the enemy's army before a fight.
- **Town and hero screens** (exist): the game's modes besides the map and the
  battle. A double click, T or R (or the card's button) brings the camera
  down beside the town or the hero, from a side where no mountain is in the
  way, swaying slowly round it; the land beyond blurs and darkens, the map's
  panels give way to one tall panel. The town
  screen has the houses, growth and income, and three tabs: buildings (the
  places taken by houses and buildings and the free ones, the build list
  with costs as chips, unaffordable entries pale with what is missing),
  hiring, and the army (the garrison, exchanged with a
  hero standing in the town). The hero screen has the level, attack,
  defence and moves with what they do, the level-up button and the army
  with each unit's numbers. Esc goes back to the map where it was; Tab and
  F or the arrows step to the next town or hero. Few modes that never
  overlap, each told apart by a big change of view (Schell, *The Art of
  Game Design*, p. 239–240); towns managed on their own screen, apart from
  the units in the field (*Game Interface Design*, p. 249 and 253).
- **Battle screen** (exists): the battlefield with the stacks, the order of
  the round at the top, buttons for defend, auto battle and retreat at the
  bottom, the card of the stack under the pointer with what a blow would do.
- **Turn report** (exists): a few lines after each turn, each one clickable to
  move the camera there.
- **Tasks** (exists) **and goal** (new): the tasks are lines of text over the
  map under the bar, an icon, the task and its reward, with no window; the
  map takes clicks through them.
- **Guiding lines** (exist, since 2026-11-11; the interface plan's 3.3), in
  place of a greeting toast and a page of rules. Six tasks teach a gesture:
  the farm and the lumber camp (building on a cell), the fight (choosing the
  hero and ordering him with the right button), the chest, the stake and the
  barracks (entering a town and building there). The first such task on
  offer that can move on this turn gets a thin gold line under it saying
  what to do now, gold rings set inside the cells that fit (the free fitting
  cells nearest the capital, the group the hero surely beats, the nearest
  seen chest, the best stake, the town), and a breathing gold outline on the
  button to press once the right cell or screen is open. A gesture the
  players did themselves is learned and not shown again, unless they stand
  at its task for 15 seconds without doing anything; the farm teaches
  building with words, the lumber camp repeats it with the rings and outline
  only. The «Следующий ход» button calls with the outline when no task on
  offer can move on this turn (with a line in a newcomer's first three
  turns), and in those turns after 30 seconds without a deed too. Four hints
  come once each, when they are needed: Alt at the third click on a monster,
  the camera after 20 seconds without moving it on the first turn, hiring
  when the hero's army is empty, and Tab at the second town. What was
  learned and seen is kept in the settings (PlayerPrefs), not in the save:
  it belongs to the players, not to a game. The conditions are in
  Core/Guide.cs and its tests; what shows is decided in RealmsGuide.cs.
  Add: a settings switch for the newcomer's hints and «show them again».
- **How to play** (exists, since 2026-11-11; the interface plan's 3.3): the
  help (F1, H, the menu) has three pages in place of one table. «Главное»
  is five lines with pictures: choose, order, build, the turn and the goal.
  «Клавиши» is the table of keys in two columns. «Подсказки» lists every
  guiding line and hint the players have seen, so that a forgotten one can be
  read again; that list is kept in the settings too.
- **Returning** (exists, since 2026-11-11; the interface plan's 3.3): a
  loaded game shows the last turn's report under «Ход N. В прошлый ход:» for
  twenty seconds. When the save is more than three days old, the camera hint
  and the hero's order are shown once more.
- **Game setup** (new): seed, the kingdom (shield), the victory goals, the
  AI's character (peaceful, normal).

All text in Russian, short sentences, icons wherever a word is not needed.

### Look

The players found the second build of iteration 3 too bright, brighter than
the Studio models themselves. The land, trees, rocks and water now use muted,
natural colours (`Palette`, the water and terrain shaders), each tree its own
shade of green; the Studio models get no extra saturation, and the prop shader
calms their loudest paint (a cottage's golden thatch was the brightest spot on
the map). Full colour is kept for what the player has to find at a glance:
the kingdoms' borders and shields, the selection, the paths, monster badges.
The interface is dark: opaque warm charcoal panels with square corners and a
muted gold line where a panel meets the map, parchment text and a muted gold
main button, so it stays calmer than the map; green, orange and red keep
their meaning (income, a shortage, danger). Dialogs keep a painted frame. The
players did not like see-through or rounded windows, and asked for smaller
text than the first panels had.

Trees, the rocks the mountains are built of and the boulders are to be
Studio models (`PropBuilder.ScatterModels`): two conifers and two broadleaf
trees, a spire with and without snow, a crag, a low outcrop and a boulder.
The map scatters them by the thousand, so each copy is merged into one mesh
per model and map chunk (`ModelBatch`), turned any way and, for trees, given
its own shade. A mountain cell stands its snowy spire at the back, crags or
spires at the sides and low outcrops by the front corners, around the pass in
the middle. Only the models the players accepted are in `art.json`: on
2026-11-09 the linden, both spires and the crag. A missing model is replaced
by the other of its kind (oak by linden, outcrop by crag), and by a built-in
low-poly shape when its kind has none yet.

The players looked at the map on 2026-11-09 and put a style pass before
sound, mechanics and content (iteration 3s). What looks bad:

- *Primitives.* The castle, walls and towers, the base town pieces, trees,
  mountains and resources are built from code primitives. They are to be
  Studio models: a reference picture (Codex image generation or the Studio's
  image models), then the Studio 3D chain, rated by the players in the
  Studio; only the accepted ones go into `art.json`.
- *Wind.* It shows as geometric lines moving in waves.
- *Ground.* The land textures look like a flat fill with even grain, and the
  roads too.

Codex reviews the captures independently, against a key frame of the target
style, and makes the reference pictures.

A town on the map is its own cell dressed with its houses and buildings: a
well, the capital's town hall or a castle in the middle, houses round it and
each town building as its model, so a town looks as big as it is. Separate
models for town sizes were planned and dropped after the players saw the
towns. Walls run round the cell a little inside its edges, with a round
tower with a roof in the kingdom's colour on each corner and an open gate
between two taller towers where a road leaves; nothing grows inside them.

Every town also has a base set that the players do not build, so even a
village looks lived in (asked for on 2026-11-09). The players asked that it
always be there and look natural, round the places their buildings take. Its
pieces:

- *Ground.* Trodden earth for a square in the middle and lanes from it to
  the places. The square grows with the level and is paved from level 2.
- *Free places.* Each holds a kitchen garden behind a wattle fence until
  something is built there.
- *Between the places.* Yards with fruit trees, woodpiles, barrels or small
  haystacks.
- *Roads.* By the roads leaving town: lamps from level 2, carts and barrels.
- *Level 3.* The centre's well becomes a fountain with flower beds round the
  square.
- *Quarters.* The same set, smaller.

The pieces are built in code (`PropTown.cs`); the ground is drawn by the
terrain shader from the town's level.

The world moves; the players put a living look before new mechanics on
2026-11-09. One wind blows the way the cloud shadows drift, and it moves:

- the trees, bushes, grass tufts and reeds, which sway more at the top, with
  their shadows;
- waves running over the meadows and the wheat, broad from afar and finer
  close up: the pressed blades turn paler and silvery and lean downwind, and
  spring back darker behind each wave, with gusts sweeping over them (until
  2026-11-09 the gusts were drawn from square-grid noise and read as straight
  lines across the screen);
- the smoke from three cottages in four, which rises, drifts downwind and
  fades.

The shaders move all of it from the time, so nothing is updated per frame,
and a capture shows one moment of it.

### Sound

Planned for iteration 3c; nothing plays yet. Searched in YAQS on 2026-11-09,
mostly in *The Game Audio Tutorial* (Stevens and Raybould).

- **Ambience in layers** (ch. 1, "Ambience: A Painterly Approach", p. 31;
  ch. 2, "Non-repetitive Design", p. 52). A quiet bed of wind under what the
  camera sees, a layer for each kind of land in view (the sea's waves and
  gulls, birds in the forest, a stream by a river, the murmur of a town and
  a smith's hammer near one), and single sounds (a bird, a dog, a cart)
  played at random times with a little random pitch and volume, so nothing
  loops audibly. The listener is the camera (ch. 3, "Where's My Head At?",
  p. 159): the share of sea, forest and town on screen sets each layer's
  volume, and zooming in brings the near layers forward.
- **Music without voices.** Calm instrumental pieces of folk instruments
  (lute, harp, recorder, fiddle, a soft drum), a few long ones with silence
  between them. Music played all the time loses its meaning, so it fades out
  after a while and comes back with something new: a turn after a quiet
  spell, a battle, a new town (ch. 4, "The 'Bored' Switch", p. 203). Battles
  have their own piece. Short stingers mark a victory, a defeat, a founded
  town and a chest; percussive ones fit over any music (ch. 4, "Stingers",
  p. 180–182).
- **Interface sounds as one family.** Wood, parchment and coins, matching
  the warm charcoal and gold interface: variations of one or two sounds
  rather than a new sound for each action, with clear forward, back, yes
  and no (ch. 6, "Information and Feedback", p. 281–284). Every click is
  acknowledged.
- **Feedback and rewards.** Sound makes a motion read as an impact (Swink,
  *Game Feel*, ch. 9, p. 160): blows in a battle, a building finishing, gold
  falling into the treasury at the end of a turn. A reward should please in
  several ways at once (Schell, *The Art of Game Design*, ch. 13, p. 233):
  the coins fly, ring and add up.
- **Variation and mixing.** Several takes of every frequent sound (steps,
  clicks, blows) with random pitch and volume (ch. 2, p. 52 and 70). Four
  volumes (music, ambience, interface, world) and a master; music and
  ambience dip under a stinger or a dialog (ch. 6, "Ducking", p. 311–313);
  levels set against a comfortable system sound, not against the loudest
  one (ch. 6, p. 298).

The sounds come from the Studio: Stable Audio Open (up to 47 s a take) for
effects, ambience beds and the stingers. Its YuE2 server sings lyrics and
returns only the mixed song; the music needs its instrumental track (YuE
generates the voice and the accompaniment as separate tracks), which the
server would have to return, or Stable Audio's music loops. No voices are
planned.

## Iterations

Each iteration ends with a build the players try with `play.sh` and notes
for the next one, as for the map.

| # | Iteration | Contents | New art |
| --- | --- | --- | --- |
| 1 | The map (done) | generation, borders, towns, resources, lairs, rivers, ground textures, income | — |
| 2 | Building (done) | field buildings on resources (undeveloped ones give nothing), the town panel with market, granary and tavern, town growth and levels, buying land, the turn report, the small tasks | farm, fishery, lumber camp, quarry, gold mine, market, granary; cottages for growing towns |
| 3 | Heroes and monsters (first build) | a larger map (70 × 42), fog of war, heroes (one at the start, more from taverns), movement and paths, barracks, archery range and stables with spearmen, archers and riders, seven kinds of monster groups and their lairs, chests, piles, shrines and watchtowers, the tactical battle mode with auto battle and odds, hero levels | hero, spearman, archer, rider, goblin, bandit, skeleton, spider, barracks, archery range, stables, chest, shrine, watchtower |
| 3b | More adventure | settlers and new towns (done), walls and a castle (done), town places and quarters (done), a flag editor (done), wolves raiding farms, monsters coming back, hero skills, items from the troll's treasure | castle, settlers' cart |
| 3s | Style (now) | the players' notes of 2026-11-09 (see Look): Studio models instead of primitives for the castle, walls, base town pieces, trees, mountains and resources; the wind without geometric lines; ground and road textures; Codex's independent review of each build | castle, settlers' cart, trees, rocks, town pieces, resource piles |
| 3c | Alive and audible | the world coming alive (wind in banners and trees, grass, drifting mist); a building rising on its place (scaffolding, dust, then the finished model); the end of the turn shown (the day turns, gold and food fly to the treasury, new houses rise one by one); sounds for clicks, building, the turn, steps, chests and battles; ambience (wind, birds, the sea) and instrumental music (see Sound) | sounds from the Studio's Stable Audio, music from YuE2's instrumental track or Stable Audio |
| 4 | Rivals | AI kingdoms that build, grow, explore with heroes and clear lairs; alliance; trade at markets | caravan |
| 5 | Goals | game setup, saving, the wonder and its stages, the monster hunter and the dragon, prosperity score, end of game screen | Great Castle stages, dragon |
| 6 | Crafts | wool, cloth, clothes, hides, leather, iron; swordsmen and knights; level 3 and 4 towns | swordsman, knight, iron mine; icons for wool, cloth, clothes, leather |
| 7 | War (not planned) | war, sieges, taking towns (walls exist and could defend them): the players chose no war for now | siege camp |

Ships and other islands are a possible later step.

The rules live in `Core` (no Unity), as now, with tests for the economy, the
growth, movement costs and battles; the AI kingdoms can play whole games in
the tests, which catches balance mistakes (a kingdom that never grows, a
goal nobody reaches) without playing.

## Ideas from the library

Searched in YAQS, the local library of game design books, on 2026-11-08.
What changed the rules above:

- **Feedback loops** (Schreiber and Romero, *Game Balance*, ch. 12, p.
  301–314). Positive feedback makes an early lead decisive and the trailing
  player helpless, unless negative feedback balances it; at the end it is
  useful, so a decided game ends quickly. Game length is set by the power
  curves, and phases keep a long game varied. Hence the rising costs of
  growth, the visible last wonder stage and the three phases.
- **Luck** (*Game Balance*, ch. 19, p. 477–487). Random outcomes add
  excitement but give "cheap" losses; exposing the probabilities keeps
  skill in the game, and known risks (input randomness) let players plan.
  Hence the odds before every fight, the hero who cannot die, and raids
  announced a turn ahead.
- **Meaningful choices** (Schell, *The Art of Game Design*, lens 32, p.
  180–181). No dominant strategy; the most interesting choice is a small safe
  reward against a big risky one ("triangularity"). Hence boars, wolves and
  the troll as one choice at three sizes, and building costs to tune until no
  building is always first. *Designing Games* (p. 145): vary the scope of
  decisions (building, moving armies, a big choice now and then) and avoid
  analysis paralysis.
- **Learning by need** (Hodent, *The Gamer's Brain*, 13.3). Show the lock
  before the key: the need first, then the way to meet it; Civilization VII
  added advisor warnings about the next turns. Civilization keeps a constant
  stream of short- and long-term goals in view (*Game Interface Design*, p.
  24, 182). Hence the order of the tasks, the advisor and the goal card.
- **Playing together** (Sylvester, *Designing Games*, p. 23; Schell, p. 186).
  A game is also a reason for a father and son to talk; events that two
  players make or learn together drive that. Joust mixes cooperation and
  competition: a shared fight against the game and a personal score. The
  players chose one kingdom for both: every decision is talked over.
- **Rewards** (*The Gamer's Brain*, 12.2). Specific praise ("the village has
  become a town!") and the world reacting (a feast in a town freed from
  wolves) tell the player what they achieved; bigger tasks deserve bigger
  rewards.
- **Combat** (*Game Balance*, ch. 10, p. 220–222). Before the rules of a
  combat system come its pillars: real time or turns, pacing, feel, who
  fights, how it grows and its share of the whole play. Hence the pillars of
  the battle mode and the tactical battle before a real-time one.
- **Fog** (*Game Interface Design*, p. 178). Strategy games hide the
  unexplored map under a shroud and what no unit sees right now under a
  lighter fog of war. Monsters here stand still, so the shroud alone is
  enough.
- **The look** (Block, *The Visual Story*, p. 120, 130, 144, 146–149;
  searched 2026-11-09). The brightest area draws the eye first, and saturated
  yellow most of all; a picture with only saturated colours has "affinity of
  saturation" and no focus, while a desaturated picture with a few saturated
  accents leads the eye to them. Totten (*Level Design*, p. 310) quotes the
  painters' rule: one principal light, the rest subordinate. Hence the muted
  land, the dark fog and the dark interface around saturated borders, paths
  and badges. For the interface Brewer (*The Pocket Mentor for Video Game
  UX/UI*, p. 112) advises two to four colours with their tints and shades and
  never colour alone as the signal; *Game Interface Design* (p. 188, 200)
  says the closer the interface's colours are to the game world's, the less
  it intrudes, and warm colours raise alertness while cool ones calm.
- **The interface's layout** (searched 2026-11-09). *Game Interface Design*:
  ask whether each part is needed, and remove, minimise or hide what is not
  (p. 218); clutter is an interface that asks for more attention than the
  game (p. 226); turn-based strategy games send the player to other screens
  to manage the empire, real-time ones always show the battlefield (p. 124,
  249). Schell (*The Art of Game Design*, p. 234–238) sorts information by
  how often it is needed and maps it to channels: what is needed all the
  time stays on screen. Sylvester (*Designing Games*, p. 228–230) shows
  everything at once in a visual hierarchy, the important loud and the rest
  quiet, down to hiding it. Hodent (*The Gamer's Brain*, p. 125): every extra
  piece of information is noise to filter out. Hence two edge panels, lists
  on demand, the town and hero screens, and tasks as quiet text.
- **The AI** (Millington, *Artificial Intelligence for Games*, 8.7 and 12.6).
  Turn-based strategy AI is heuristics and influence maps, not game-tree
  search; the same analysis can help the player.

## Open questions

Answered on 2026-11-08:

- **Who plays?** One player: the father and son rule one kingdom together.
- **How long is a game?** 30–60 minutes.
- **War between kingdoms?** Not for now; more kinds of monsters instead, and
  a somewhat larger map.
- **Armies?** A hero leads an army of unit stacks, as in hero-led strategy
  games, and fights in a separate battle mode. A real-time battle led from
  the hero's shoulder was wished for; it comes later as an experiment, after
  the tactical battle (see [Battles](#battles)).
- **Fog of war?** Yes.

For the father and son to answer; they change the rules of the iterations in
brackets.

1. **Which goal first:** answered 2026-11-11: the crown of three gems of five
   as the main, short game.
2. **How long are the chains:** two steps (wool → cloth) or three (wool →
   yarn → cloth → clothes, as the workshops suggest)? (6)
3. **Random events** (a good harvest, a merchant caravan, a storm, a
   festival): fun or annoying? (4)
