"""Queues the game's concepts in the asset studio (Qwen-Image): one paragraph
a thing, the set's common tail, two or one variants. Their ids go to
art/concepts.json; the 3D comes after review (art/queue_models.py).

    python3 art/queue_concepts.py [--studio http://127.0.0.1:9000]
"""

import argparse
import json
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent

# The set's look: old open-world RPGs (Gothic, Neverwinter Nights 2, Dragon
# Age), a quiet storybook valley at the edge of the world.
TAIL = (
    "Single isolated environment game asset for a quiet medieval fantasy valley in the style of classic "
    "open-world role-playing games. Stylized hand-painted art matching weathered timber villages and old "
    "grey stone walls, strong chunky silhouette, broad clean color areas, warm muted earthy palette, "
    "restrained sculpted details. Neutral matte diffuse studio lighting, no harsh cast shadows. "
    "Orthographic front three-quarter view at eye level. Entire object visible with generous margins, "
    "centered on plain white background. No surrounding terrain, no diorama, no text, no letters, "
    "no people, no floating parts, no thin fragile details."
)

CHARACTER = (
    "Full body single {who}, orthographic FRONT view at eye level, perfectly symmetrical neutral A-pose, "
    "arms straight diagonally down 45 degrees away from the body, open empty hands, legs apart with a clear "
    "gap between the thighs down to the crotch, feet flat on the ground. {body} One cohesive figure. "
    "No tail, no wings, no cape, no skirt, no robe, no weapons, no loose straps, nothing floating. "
    "Stylized hand-painted game character art in the style of classic open-world role-playing games, "
    "strong chunky silhouette, broad clean color areas. Neutral matte diffuse studio lighting, no harsh cast "
    "shadows. Entire figure visible with generous margins, centered on plain white background. No text."
)

# name: (prompt, resolution, variants, category)
CONCEPTS = {
    "hero": (
        CHARACTER.format(
            who="young wandering adventurer, a lean man of twenty-five with short tousled brown hair and a short beard",
            body="A sleeveless dark leather jerkin laced over a grey chain mail shirt, a muted forest-green wool tunic "
            "showing at the hips, brown leather trousers tucked into tall worn brown boots, plain leather bracers, "
            "a wide belt with two small pouches; colors: dark brown leather, steel grey, forest green.",
        ),
        "832x1216",
        2,
        "character",
    ),
    "house": (
        (
            "One timber-framed village house: a single coherent building on a flat stable stone footing. "
            "Whitewashed plaster walls between dark brown oak beams, one wooden door at the front and two small "
            "shuttered windows, a steep thick straw thatch roof with rounded eaves overhanging the walls, a short "
            "stone chimney at one end. Colors: warm off-white plaster, dark oak brown, golden ochre thatch."
        ),
        "1024x1024",
        2,
        "building",
    ),
    "watchtower": (
        (
            "One tall village watchtower, a single coherent building: a square base of rough grey fieldstone "
            "two storeys high, flat stable bottom, topped by a timber lookout room with dark oak beams and a "
            "steep pyramid roof of dark red-brown wooden shingles. A small arched wooden door at the foot. "
            "Colors: blue-grey stone, dark oak, rust red roof."
        ),
        "832x1216",
        1,
        "building",
    ),
    "well": (
        (
            "One round village well: a low thick ring wall of rough grey fieldstone with a flat stable bottom, "
            "two thick wooden posts carrying a small steep shingle roof and a wooden crank beam with a hanging "
            "bucket. Colors: blue-grey stone, weathered brown wood, muted red roof."
        ),
        "1024x1024",
        1,
        "prop",
    ),
    "wall": (
        (
            "One straight segment of a thick medieval town wall: a single solid block of large squared grey stone "
            "blocks, about three times as long as it is tall, flat stable bottom, a walkway on top behind a row of "
            "square crenellations with clear gaps between them, a few darker weathered stones and patches of olive "
            "moss at the foot. Seen from the front, the long face filling the view. Colors: blue-grey stone, "
            "olive moss."
        ),
        "1376x768",
        1,
        "building",
    ),
    "gate": (
        (
            "One town gatehouse: two thick square stone towers with crenellated tops joined by a wall over a "
            "single broad round gateway. The gateway is completely empty, wide enough for a cart, plain white "
            "background visible through it all the way down to the ground; no door, no portcullis, no bars across "
            "it. A faded blue banner hangs flat against each tower. Flat stable bottom. Colors: blue-grey stone, "
            "faded blue banners, dark wood trim."
        ),
        "1024x1024",
        2,
        "building",
    ),
    "keep": (
        (
            "One tall square stone keep of a small town, a single coherent building: thick grey stone walls with "
            "a few narrow arched windows, a crenellated top with a small square turret on one corner, a large "
            "faded blue banner with a simple gold emblem hanging flat on the front. Flat stable bottom. Colors: "
            "blue-grey stone, faded blue and gold."
        ),
        "832x1216",
        1,
        "building",
    ),
    "townhouse": (
        (
            "One narrow two-storey town house of grey stone below and dark timber framing with ochre plaster "
            "above, the upper floor jutting out over the lower one, a steep dark slate roof, one wooden door "
            "and small leaded windows. A single coherent building on a flat stable footing. Colors: blue-grey "
            "stone, ochre plaster, dark slate."
        ),
        "832x1216",
        1,
        "building",
    ),
    "cave": (
        (
            "One large rugged rock outcrop with a cave entrance: a single massive formation of layered grey-brown "
            "cliff stone, two broad strata and three large fractured planes, its front face opening into a tall "
            "dark cave mouth at ground level, the mouth deep black inside. Patches of olive moss on the top ledges. "
            "Flat stable bottom. Three-quarter view clearly showing the cave mouth and both rock faces. Colors: "
            "grey-brown stone, olive moss, black shadow in the mouth."
        ),
        "1024x1024",
        2,
        "terrain",
    ),
    "boulder": (
        (
            "One large weathered boulder: a single rounded mass of grey granite with two broad fractured faces, "
            "patches of ochre lichen and olive moss on top, flat stable bottom."
        ),
        "1024x1024",
        1,
        "terrain",
    ),
    "barn": (
        (
            "One large wooden farm barn: a single coherent building of dark weathered vertical planks on a low "
            "stone footing, a broad steep thatched roof, two large closed wooden doors at the front. Flat stable "
            "bottom. Colors: dark brown wood, golden thatch, grey stone."
        ),
        "1024x1024",
        1,
        "building",
    ),
    "haystack": (
        (
            "One round haystack: a single tall dome of golden dry hay with a slightly rounded top, the straw in "
            "broad clean bundles, flat stable bottom. Colors: golden ochre, warm straw yellow."
        ),
        "1024x1024",
        1,
        "prop",
    ),
    "fence": (
        (
            "One straight segment of a rustic wooden fence: three thick rough-hewn posts joined by two thick "
            "horizontal split rails, all one connected piece, flat stable bottom, about three times as long as "
            "it is tall. Seen from the front. Colors: weathered grey-brown wood."
        ),
        "1376x768",
        1,
        "prop",
    ),
    "oak": (
        (
            "One old broad oak tree: a single thick gnarled trunk splitting into a few heavy limbs, the crown made of "
            "a few large rounded clumps of dense dark green leaves, the trunk going below the ground line into a "
            "deep tapering mass of a few thick roots about a third of the trunk's height, as if the tree were pulled "
            "out of the earth; no flat base, no ground disc, no soil. Colors: dark grey-brown bark, deep green and "
            "olive leaves."
        ),
        "1024x1024",
        2,
        "tree",
    ),
    "pine": (
        (
            "One tall pine tree: a single straight dark trunk carrying a narrow crown of a few large stacked "
            "rounded clumps of dark blue-green needles, the trunk going below the ground line into a deep tapering "
            "mass of a few thick roots; no flat base, no ground disc, no soil. Colors: red-brown bark, dark "
            "blue-green needles."
        ),
        "832x1216",
        1,
        "tree",
    ),
}

# Ground tiles: images only (no 3D), seen straight down, made tileable in the game's import.
TILES = {
    "grass": "Seamless top-down texture of meadow ground for a hand-painted fantasy role-playing game: short "
    "green grass in broad soft painted strokes with a few tiny clover patches and small yellow wild "
    "flowers, even lighting, no shadows, no horizon, no objects, uniform density over the whole image.",
    "road": "Seamless top-down texture of a packed dirt road for a hand-painted fantasy role-playing game: warm "
    "ochre-brown earth with faint cart ruts and a few scattered small pebbles in broad soft painted "
    "strokes, even lighting, no shadows, no horizon, no grass, uniform over the whole image.",
}


def post(studio: str, body: dict) -> dict:
    request = urllib.request.Request(
        f"{studio}/api/jobs",
        data=json.dumps(body).encode(),
        headers={"Content-Type": "application/json"},
    )
    return json.load(urllib.request.urlopen(request))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--studio", default="http://127.0.0.1:9000")
    args = parser.parse_args()
    jobs = {}
    for name, (paragraph, resolution, count, _) in CONCEPTS.items():
        prompt = paragraph if name == "hero" else f"{paragraph} {TAIL}"
        job = post(
            args.studio,
            {
                "server": "qwen_image",
                "task": "text-to-image",
                "prompt": prompt,
                "params": {"resolution": resolution, "steps": 40},
                "count": count,
            },
        )
        jobs[name] = job["id"]
    for name, prompt in TILES.items():
        job = post(
            args.studio,
            {
                "server": "qwen_image",
                "task": "text-to-image",
                "prompt": prompt,
                "params": {"resolution": "1024x1024", "steps": 40},
                "count": 1,
            },
        )
        jobs[f"tile:{name}"] = job["id"]
    (HERE / "concepts.json").write_text(json.dumps(jobs, indent=2) + "\n")
    print(json.dumps(jobs, indent=2))


if __name__ == "__main__":
    main()
