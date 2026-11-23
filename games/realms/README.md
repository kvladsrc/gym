# Hexcrown

A calm, turn-based fantasy strategy game on a hex map, made to be played
together with a child; a game fits into one evening. Single player, mouse and
keyboard, English and Russian, Windows and Linux.

Six kingdoms (four on a small map) share a generated land of meadows, forests,
hills and coasts under the fog of war. Your towns grow on their own: the bigger
a town, the farther its borders reach. Put a stake on the map to show a town
where to grow next.

**Build.** Farms, fisheries, pastures, lumber camps, quarries and gold mines on
the land; a market, granary, tavern, barracks, archery range, stables, walls and
a castle in the towns. Send settlers out to found new towns and name them
yourself.

**Explore and fight.** Heroes lead spearmen, archers and riders into the fog.
Goblins, bandits, boars, wolves, skeletons, spiders and trolls each fight in
their own way in turn-based battles on a small hex field. Command every move or
let the troops fight on their own; retreat when it goes badly. Beaten enemies
may give up: let them go or finish them. Lairs send raiding bands against
nearby towns, and walls beat them off.

**Win the crown.** Every kingdom has a crown with six gems: Capital, Great
Build (a bell tower your capital raises in three stages), Towns, Hunter, Hero
and Lands. The first to set three wins, usually in 35–45 turns.
The neighbouring kingdoms never attack you, but they race for their own crown
at the pace you choose: Calm, Normal or Race.

**Your flag.** Draw your kingdom's flag from patterns, colours and emblems; it
flies over your towns and on your heroes' banners.

## Playing

The game opens on the main menu. **Continue** goes back into the last game,
**New game** opens a window of choices: your kingdom's flag, the map (a small
picture of what your kingdom knows at the start, another map, or one by its
number), the map's size and the neighbours' pace. **Play** draws the map under
the loading screen. A click on the kingdom's shield in the top bar opens the
flag editor again during a game. **About** on the main menu shows the version,
how the art and sound were made, the licences, and where the saves and the log
are kept, to attach to a report of a fault.

The game saves itself after each turn, before each battle, before a new game
and when you quit (**Saved** shows beside the turn counter). **Menu** in the
top bar (or Esc) can also save into six slots of your own and load any save.

Each house and town building takes a place in its town. When a town is full,
click an empty cell of your kingdom beside it and press **Expand town**. A
town's hiring tab gathers a settlers' wagon; the hero who takes it can
**Found a town** where he stands.

The hero card exchanges units with a hero on a neighbouring cell or with the
town the hero stands in. In a battle, click a cell to walk the current stack
there and an enemy to strike or shoot it; D defends, A lets the troops fight on
their own.

## Controls

| Keys | Action |
| --- | --- |
| W A S D, arrows | move the map (Shift is faster) |
| Right mouse button | drag the map |
| Q and E (or Z and X), middle mouse button | turn the map (a click on the compass by the minimap faces north) |
| Backspace | turn the map back to its usual angle |
| Alt (held) | the badges of resources, places and monster groups over all seen land |
| Mouse wheel, + and − | zoom (the wheel zooms towards the cursor) |
| Left mouse button | select a cell, town, monster group or your hero; a selected cell shows what can be built on it |
| T, double click on your town | the town screen: buildings, hiring, the garrison |
| R, double click on your hero | the hero screen: level, attack, defence, the army |
| Right mouse button (click, not drag) | lay the chosen hero's way; a second right click on its end (or G) walks it, into a fight if monsters stand there |
| Right click on another of your heroes | walk next to him and exchange units |
| F | next hero |
| Tab | next town |
| Space, Home | back to the capital |
| Enter | end the turn |
| F1, H | how to play |
| Esc | clear the way and the selection, close a screen; with nothing left, the menu |
| Enter, Esc in a dialog | the first or the last answer |

Clicking or dragging on the minimap moves the view.

## Building it

From the monorepo root, `nix develop -c just realms build` builds the Linux
player and `nix develop -c just realms play` plays it; `just realms build
windows` builds the Windows one. [DEVELOPMENT.md](DEVELOPMENT.md) has the
commands, the code's layout and how the art is made; [DESIGN.md](DESIGN.md)
has the design and its plans.

The 3D models, textures, icons and menu pictures were made with generative AI
tools (local image-to-3D models and image generation) and picked by hand.
