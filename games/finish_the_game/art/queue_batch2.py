"""The second set of concepts: enemies and townsfolk (rig-ready), items,
weapons, animals, birds, insects, decor and relief, in the set's look
(queue_concepts.TAIL). Their ids go to art/batch2.json.

    python3 art/queue_batch2.py
"""

import json
import sys
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from queue_concepts import CHARACTER, TAIL

ANIMAL = (
    "Full body single {who}, standing on all four legs on flat ground, side three-quarter view at eye level, "
    "the whole body and all legs clearly visible, legs apart, tail close to the body. {body} One cohesive figure. "
    "Stylized hand-painted game creature art in the style of classic open-world role-playing games, strong chunky "
    "silhouette, broad clean color areas, muted natural palette. Neutral matte diffuse studio lighting, no harsh cast "
    "shadows. Entire figure visible with generous margins, centered on plain white background. No text, no ground, no saddle."
)

ITEM = (
    "Single {what} as a game item prop, {how} Seen from the front three-quarter view, slightly above, the whole object "
    "visible, chunky readable shape, hand-painted texture with broad clean color areas, classic fantasy role-playing game "
    "style, warm muted palette. Neutral matte diffuse studio lighting, no harsh cast shadows. Centered on plain white "
    "background with generous margins. No text, no letters, no hands, no table, no floating parts, no thin fragile details."
)

C = {}
# Enemies: rig-ready humanoids (empty hands; weapons are separate models).
for name, who, body in [
    (
        "bandit",
        "rough highway bandit, a lean scarred man with a dark stubble",
        (
            "A patched dark brown leather jerkin over a dirty red shirt, a black cloth mask over the lower face, worn grey trousers, "
            "tall scuffed boots, leather bracers; colors: dark brown, faded red, grey."
        ),
    ),
    (
        "skeleton",
        "undead skeleton warrior, bare yellowed bones",
        (
            "Wearing only a dented rusty iron helmet, a rusty breastplate strapped over the rib cage and bits of rusted greaves; "
            "colors: bone ivory, rust orange-brown, dark iron."
        ),
    ),
    (
        "goblin",
        "small wiry goblin raider with long pointed ears and a big hooked nose, green-grey skin",
        (
            "A crude patchwork leather vest, a rope belt with small pouches, ragged brown short trousers, bare clawed feet; "
            "colors: olive green skin, brown leather, dusty ochre."
        ),
    ),
    (
        "orc",
        "huge hulking orc brute with tusks, broad shoulders, grey-green skin",
        (
            "Heavy spiked dark iron shoulder plates, a thick leather harness across the bare chest, a wide belt, dark brown "
            "leather trousers, fur-lined boots; colors: grey-green skin, dark iron, brown leather."
        ),
    ),
    (
        "cultist",
        "sinister cultist, a gaunt pale man with a shaved head and dark painted runes on his face",
        (
            "A tight dark crimson leather tunic with black straps, a black sash, black fitted trousers and tall black boots; "
            "colors: deep crimson, black, pale skin."
        ),
    ),
    (
        "wraith_knight",
        "fallen dark knight in heavy blackened plate armor, a closed horned helmet with a glowing blue slit",
        (
            "Black scorched plate armor with dull silver trim, a tattered dark blue tabard no longer than the waist, plated "
            "gauntlets and greaves; colors: black, dull silver, dark blue, cold blue glow."
        ),
    ),
]:
    C[f"enemy_{name}"] = (CHARACTER.format(who=who, body=body), "832x1216", "character")
# Townsfolk.
for name, who, body in [
    (
        "blacksmith",
        "burly village blacksmith, a bald man with a thick red beard and strong arms",
        (
            "A heavy brown leather apron over a grey linen shirt with rolled sleeves, thick dark trousers, sturdy boots, leather "
            "gloves; colors: brown leather, grey linen, red beard."
        ),
    ),
    (
        "farmer",
        "sturdy farm woman with a sun-browned face and braided chestnut hair",
        (
            "A short-sleeved green linen blouse, a brown laced bodice, a short apron to the hips over brown fitted trousers, "
            "tall work boots; colors: green, brown, cream."
        ),
    ),
    (
        "merchant",
        "plump travelling merchant with a curly black moustache and a round face",
        (
            "A rich burgundy padded doublet with brass buttons, a wide belt with coin purses, fitted mustard yellow trousers, "
            "soft brown boots, a small burgundy cap; colors: burgundy, mustard, brass."
        ),
    ),
    (
        "guard",
        "town guard, a broad middle-aged man with a short grey beard",
        (
            "A steel kettle helmet, a blue padded gambeson under a steel breastplate with the town's gold emblem, steel "
            "vambraces, dark trousers, steel-capped boots; colors: blue, steel, gold."
        ),
    ),
    (
        "hermit",
        "old forest hermit with a long white beard and wild white hair",
        (
            "A patched mossy green woollen tunic belted with rope, a fur mantle over the shoulders down to the elbows, "
            "brown wrapped trousers, worn leather boots, bead necklaces; colors: moss green, grey fur, brown."
        ),
    ),
    (
        "innkeeper",
        "cheerful innkeeper woman with rosy cheeks and a dark hair bun",
        (
            "A cream linen shirt, a dark green laced vest, a short white apron to the hips over dark brown fitted trousers, "
            "low boots; colors: cream, dark green, white."
        ),
    ),
]:
    C[f"npc_{name}"] = (CHARACTER.format(who=who, body=body), "832x1216", "character")
# Animals.
for name, who, body in [
    (
        "wolf",
        "grey forest wolf",
        "Thick grey and cream fur, a bushy tail, alert pointed ears, amber eyes.",
    ),
    (
        "deer",
        "red deer stag",
        "Warm red-brown coat, a cream belly, big branching antlers, slender legs.",
    ),
    (
        "boar",
        "wild boar",
        "Coarse dark brown bristly fur, a ridge of bristles along the back, small curved tusks.",
    ),
    (
        "horse",
        "sturdy farm horse",
        "A chestnut coat, a cream mane and tail, white socks, a calm look.",
    ),
    (
        "cow",
        "dairy cow",
        "A white coat with large brown patches, short horns, a pink nose.",
    ),
    (
        "sheep",
        "woolly sheep",
        "Thick creamy white wool, a dark grey face and dark legs.",
    ),
    (
        "dog",
        "shaggy village dog",
        "Brown and cream shaggy fur, floppy ears, a friendly face.",
    ),
    (
        "bear",
        "big brown bear",
        "Thick dark brown fur, a heavy hump on the shoulders, small round ears.",
    ),
]:
    C[f"animal_{name}"] = (ANIMAL.format(who=who, body=body), "1216x832", "animal")
# Birds and insects (as props: perched or still, chunky).
for name, what, how in [
    (
        "crow",
        "black crow perched on a small mossy stone",
        "glossy black feathers with a blue sheen, the stone flat-bottomed.",
    ),
    (
        "owl",
        "brown owl perched on a short thick branch stump",
        "mottled brown and cream feathers, big round amber eyes, the stump flat-bottomed.",
    ),
    (
        "eagle",
        "golden eagle with wings folded perched on a rock",
        "dark brown and gold feathers, a yellow hooked beak, the rock flat-bottomed.",
    ),
    (
        "duck",
        "mallard duck standing",
        "a green head, a white collar, brown chest, grey body, orange feet.",
    ),
    (
        "chicken",
        "brown farm hen standing",
        "red-brown feathers, a red comb, yellow feet.",
    ),
    (
        "butterfly",
        "large butterfly resting on a flower",
        "wings open, orange and black wing patterns, a thick green stem on a flat base.",
    ),
    (
        "beetle",
        "big stag beetle",
        "glossy dark bronze shell, large branching mandibles, six legs, a flat underside.",
    ),
    (
        "dragonfly",
        "dragonfly resting on a reed",
        "iridescent teal body, broad translucent wings drawn as solid pale panels, the reed on a flat base.",
    ),
]:
    C[f"wild_{name}"] = (ITEM.format(what=what, how=how), "1024x1024", "creature")
# Items.
for name, what, how in [
    (
        "potion_red",
        "round glass healing potion bottle",
        "filled with glowing red liquid, a cork stopper and a twine wrap.",
    ),
    (
        "potion_blue",
        "tall glass mana potion flask",
        "filled with glowing blue liquid, a silver cap.",
    ),
    (
        "scroll",
        "rolled parchment scroll tied with a red ribbon and a wax seal",
        "aged cream paper.",
    ),
    (
        "key",
        "large ornate iron key",
        "a ring bow with a scroll pattern and chunky teeth.",
    ),
    (
        "coins",
        "leather coin pouch with gold coins spilling",
        "tied with a cord, a few thick gold coins beside it.",
    ),
    (
        "book",
        "thick leather-bound spell book",
        "dark red leather cover with brass corners and a brass clasp.",
    ),
    (
        "lantern",
        "iron travel lantern",
        "a warm glow inside four glass panes, a top ring handle.",
    ),
    (
        "bread",
        "loaf of rustic bread with a wedge of cheese",
        "golden crust, yellow cheese, on a small wooden board.",
    ),
    (
        "chest_gold",
        "small iron-bound treasure chest, open",
        "full of gold coins and a few gems.",
    ),
    (
        "map",
        "rolled up old map",
        "half unrolled showing a painted coastline, tied with string.",
    ),
]:
    C[f"item_{name}"] = (ITEM.format(what=what, how=how), "1024x1024", "item")
# Weapons (to hold in a hand bone).
for name, what, how in [
    (
        "sword",
        "straight longsword",
        "a broad steel blade, a leather-wrapped grip, a simple brass crossguard and round pommel; shown upright, point up.",
    ),
    (
        "axe",
        "single-bladed battle axe",
        "a wide steel head, a thick wooden haft wrapped in leather; shown upright.",
    ),
    (
        "bow",
        "wooden longbow",
        "a thick recurved stave of dark wood with leather grip, the string shown as a thick cord; shown upright.",
    ),
    (
        "staff",
        "wizard's staff",
        "a gnarled thick wooden staff topped with a glowing blue crystal held by roots; shown upright.",
    ),
    (
        "shield",
        "round wooden shield",
        "painted blue and cream with an iron rim and a central iron boss; seen from the front.",
    ),
    (
        "mace",
        "flanged iron mace",
        "a heavy flanged head on a thick wooden handle; shown upright.",
    ),
    (
        "spear",
        "hunting spear",
        "a broad leaf-shaped steel head on a thick ash shaft; shown upright.",
    ),
    (
        "dagger",
        "curved dagger",
        "a broad curved steel blade, a bone handle; shown upright.",
    ),
]:
    C[f"weapon_{name}"] = (ITEM.format(what=what, how=how), "832x1216", "weapon")
# Decor.
for name, paragraph in [
    (
        "market_stall",
        (
            "One wooden market stall: a sturdy timber counter with a striped red and cream cloth awning on four thick posts, "
            "baskets of apples and bread on the counter. Flat stable bottom. Colors: brown wood, red and cream cloth."
        ),
    ),
    (
        "windmill",
        (
            "One village windmill: a round stone tower with a conical thatched cap and four broad lattice sails drawn as "
            "solid wooden panels. Flat stable bottom. Colors: grey stone, golden thatch, brown wood."
        ),
    ),
    (
        "bridge",
        (
            "One short arched wooden footbridge: thick planks on a gentle arch with chunky wooden railings, stone footings "
            "at both ends. Flat stable bottom. Colors: weathered brown wood, grey stone."
        ),
    ),
    (
        "table",
        (
            "One rustic wooden table with two benches, a jug and two mugs on it, all one connected piece. Flat stable "
            "bottom. Colors: brown wood, grey pottery."
        ),
    ),
    (
        "anvil",
        "One blacksmith's anvil on a thick tree stump, a hammer resting on it. Flat stable bottom. Colors: dark iron, brown wood.",
    ),
    (
        "scarecrow",
        (
            "One field scarecrow: a straw-stuffed figure in a patched shirt and a floppy hat on a thick wooden post, arms "
            "spread on a crossbar. Flat stable bottom. Colors: straw yellow, patched blue and brown cloth."
        ),
    ),
    (
        "beehives",
        "Three round straw skep beehives on a low wooden bench, all one connected piece. Flat stable bottom. Colors: straw gold, brown wood.",
    ),
    (
        "statue",
        (
            "One weathered stone statue of a hooded knight leaning on a sword, on a square plinth, moss in the folds. Flat "
            "stable bottom. Colors: grey stone, olive moss."
        ),
    ),
    (
        "standing_stones",
        (
            "A small ring of five tall rough standing stones on a low grassy mound, all one connected piece, faint "
            "carved spirals on them. Flat stable bottom. Colors: blue-grey stone, olive moss."
        ),
    ),
    (
        "gravestones",
        (
            "A group of three old leaning gravestones in a patch of earth, all one connected piece. Flat stable bottom. "
            "Colors: grey stone, dark earth, moss."
        ),
    ),
    (
        "wagon",
        (
            "One covered wooden wagon with a canvas top, four big spoked wheels. Flat stable bottom, both wheels attached. "
            "Colors: brown wood, cream canvas."
        ),
    ),
    (
        "shrine",
        (
            "One small roadside shrine: a stone pillar with a little roof sheltering a carved figure, candles and flowers at "
            "its foot. Flat stable bottom. Colors: grey stone, red roof tiles, warm candle light."
        ),
    ),
]:
    C[f"decor_{name}"] = (paragraph + " " + TAIL, "1024x1024", "decor")
# Relief.
for name, paragraph in [
    (
        "cliff",
        (
            "One tall rugged cliff face segment: a single massive wall of layered grey-brown rock, two broad strata, three "
            "large fractured planes, about twice as wide as tall, olive moss on the ledges. Flat stable bottom."
        ),
    ),
    (
        "crag",
        (
            "One jagged rocky crag: a single tall pointed mass of grey rock with sharp broken faces and a few ledges, olive "
            "moss and ochre lichen. Flat stable bottom."
        ),
    ),
    (
        "rock_cluster",
        (
            "One cluster of four mossy boulders of different sizes pressed together into a single mass, grey stone, "
            "olive moss, ochre lichen. Flat stable bottom."
        ),
    ),
    (
        "fallen_log",
        (
            "One big fallen mossy tree log lying on its side, a broken end and a few short branch stubs, bracket "
            "mushrooms. Flat underside. Colors: grey-brown bark, olive moss, cream mushrooms."
        ),
    ),
    (
        "mound",
        (
            "One low grassy earth mound with a few rocks poking out and tufts of tall grass, broad and gentle. Flat underside. "
            "Colors: olive green grass, brown earth, grey rocks."
        ),
    ),
    (
        "river_stones",
        (
            "One flat group of smooth rounded river stones of many sizes set together, a little moss between them, "
            "broad and low. Flat underside. Colors: blue-grey and warm grey stone, olive moss."
        ),
    ),
]:
    C[f"relief_{name}"] = (paragraph + " " + TAIL, "1024x1024", "relief")


def main() -> None:
    jobs = {}
    for name, (prompt, resolution, kind) in C.items():
        body = {
            "server": "qwen_image",
            "task": "text-to-image",
            "prompt": prompt,
            "params": {"resolution": resolution, "steps": 40},
            "count": 1,
        }
        request = urllib.request.Request(
            "http://127.0.0.1:9000/api/jobs",
            data=json.dumps(body).encode(),
            headers={"Content-Type": "application/json"},
        )
        jobs[name] = {
            "job": json.load(urllib.request.urlopen(request))["id"],
            "kind": kind,
        }
    (HERE / "batch2.json").write_text(json.dumps(jobs, indent=2) + "\n")
    print(len(jobs))


if __name__ == "__main__":
    main()
