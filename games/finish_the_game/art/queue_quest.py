"""Queues the demo quest's assets in the asset studio: the farm woman's
spirit's concept (Qwen-Image, an edit of her own; its 3D and rig after
review, with art/studio.py), the keys' concept (its 3D likewise), every spoken line (XTTS, English) and the sounds (Stable
Audio Open). The jobs' ids go to art/quest.json; art/fetch_quest.py brings
their outputs into the Unity project.

    python3 art/queue_quest.py [--only voices|images|sounds] [--studio http://127.0.0.1:9000]

The lines are the quest's own (QuestScript.cs has the same ids): the hero,
the farm woman Hilda, and her spirit (her own voice, made strange in Unity).
"""

import argparse
import json
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE / "quest.json"

# The farm woman's spirit: herself, recognisably, but the finest version of
# her, a noble lady of a fairy-tale court (an edit of her own concept, so the
# face, the braids and the A-pose stay; legs apart, for the rig).
FARMER = "01a13c6efc42aee6fdf310"  # pragma: allowlist secret
NOBLE = (
    "The same young woman, the same face, the same sun-browned skin and freckles, the same two thick chestnut braids, "
    "the same perfectly symmetrical neutral A-pose with arms straight diagonally down and open empty hands, legs apart, "
    "front view; but now dressed as a radiant noble lady of a fairy-tale court, the finest version of herself: a fitted "
    "emerald velvet bodice richly embroidered with golden wheat ears, puffed sleeves of ivory silk slashed with gold, a "
    "jewelled golden belt, an ankle-length emerald and gold brocade gown split at the front over fitted ivory breeches "
    "and soft golden boots so that a clear gap between the legs stays visible, a delicate golden circlet of wheat ears "
    "with small amber jewels on her head, golden ribbons and pearls braided into her braids, amber earrings, a golden "
    "necklace with an amber pendant. Proud, kind, noble expression. Stylized hand-painted game character art in the "
    "style of classic open-world role-playing games, strong chunky silhouette, broad clean color areas. Neutral matte "
    "diffuse studio lighting. Entire figure visible with generous margins, centered on plain white background. No text."
)

KEYS = (
    "One ring of two old iron barn keys: a large heavy key with a round bow and chunky square teeth and a smaller "
    "key, both threaded on one thick round iron ring, the three parts joined into one solid chunky object, lying "
    "flat. Dark blackened iron with worn brighter edges and a little rust. Single isolated game asset for a "
    "medieval fantasy valley in the style of classic open-world role-playing games, stylized hand-painted art, "
    "strong chunky silhouette, broad clean color areas. Neutral matte diffuse studio lighting, no harsh cast "
    "shadows. Seen from above at a three-quarter angle, entire object visible with generous margins, centered on "
    "plain white background. No text, no hands, no table."
)

# Who speaks with which voice (XTTS's own speakers); the spirit speaks with Hilda's.
VOICES = {"hero": "Damien Black", "hilda": "Gitta Nikolina"}

# id: (speaker, line)
LINES = {
    # On the road: the spirit, and what the hero asks it.
    "spirit_hello": (
        "hilda",
        "The wheel that goes nowhere... it keeps them. It keeps them all day long.",
    ),
    "hero_huh": ("hero", "What?"),
    "hero_who": ("hero", "Who are you?"),
    "spirit_who": (
        "hilda",
        "Nine cows... and one bell. Nine cows, one bell. And the wheel keeps them.",
    ),
    "spirit_what": (
        "hilda",
        "The one that goes nowhere. Under. Under it, where it is warm. It keeps them.",
    ),
    # At the farm: Hilda.
    "hilda_busy": (
        "hilda",
        "Not now, stranger. I've lost my keys, and the cows won't wait.",
    ),
    "hero_keys": ("hero", "What keys?"),
    "hilda_keys": (
        "hilda",
        "The barn keys. Two iron keys on a ring. I had them this morning, I know I did.",
    ),
    "hilda_cows": (
        "hilda",
        "If I don't find them, the cows stay locked in. By sundown the whole valley will hear them.",
    ),
    "hero_wheel": ("hero", "A wheel that goes nowhere keeps them."),
    "hilda_wheel": ("hilda", "A wheel that... what kind of talk is that?"),
    "hilda_cart": (
        "hilda",
        "Wait. The old cart! That cart hasn't moved in ten years. I sat on it at noon to rest my legs.",
    ),
    "hilda_found": ("hilda", "There they are! Right under the wheel."),
    "hilda_how": ("hilda", "How did you know that?"),
    "hero_guess": ("hero", "Lucky guess."),
    "hilda_guess": ("hilda", "Some guess."),
    "hilda_thanks": (
        "hilda",
        "Thank you, all the same. Here, take this bread. I baked it this morning.",
    ),
    "hero_leave": ("hero", "I'll leave you to it."),
    "hilda_leave": ("hilda", "Mm. Mind the cows."),
}

# id: (prompt, seconds)
SOUNDS = {
    "flash": (
        (
            "A bright magical shimmer burst: a glassy chime swelling quickly and fading out, fantasy spell sound "
            "effect, clean, isolated, no music, no voice."
        ),
        3.0,
    ),
    "hop": (
        (
            "A single soft magical hop: a tiny glass bell pling with a short airy whoosh, cute and light, sound "
            "effect, clean, isolated, no music."
        ),
        1.0,
    ),
    "vanish": (
        (
            "A soft magical whoosh rising and fading into silence with an airy glittering shimmer, fantasy sound "
            "effect, clean, isolated, no music."
        ),
        3.0,
    ),
    "keys": (
        (
            "A ring of heavy old iron keys picked up from the grass and jingling briefly in a hand, close foley "
            "sound effect, clean, isolated, no music."
        ),
        2.0,
    ),
    "entry": (
        (
            "A short gentle medieval jingle on lute and wooden flute, two bars, warm and calm, like a quest log "
            "entry in an old role-playing game."
        ),
        4.0,
    ),
    "dream": (
        (
            "A deep soft swelling whoosh with a distant choir pad, a dreamy transition, fading out, sound effect, "
            "no beat."
        ),
        4.0,
    ),
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
    parser.add_argument("--only", choices=["voices", "images", "sounds"])
    args = parser.parse_args()
    jobs = json.loads(OUT.read_text()) if OUT.exists() else {}
    if args.only in (None, "images"):
        jobs["image:noble"] = post(
            args.studio,
            {
                "server": "qwen_image",
                "task": "image-to-image",
                "prompt": NOBLE,
                "inputs": {"image": FARMER},
                "params": {"size": "1K", "steps": 50},
                "count": 4,
            },
        )["id"]
        jobs["image:keys"] = post(
            args.studio,
            {
                "server": "qwen_image",
                "task": "text-to-image",
                "prompt": KEYS,
                "params": {"resolution": "1024x1024", "steps": 50},
                "count": 2,
            },
        )["id"]
    if args.only in (None, "voices"):
        for name, (who, line) in LINES.items():
            jobs[f"line:{name}"] = post(
                args.studio,
                {
                    "server": "voice",
                    "task": "text-to-speech",
                    "prompt": line,
                    "params": {"language": "en", "speaker": VOICES[who]},
                },
            )["id"]
    if args.only in (None, "sounds"):
        for name, (prompt, seconds) in SOUNDS.items():
            jobs[f"sound:{name}"] = post(
                args.studio,
                {
                    "server": "sound",
                    "task": "text-to-audio",
                    "prompt": prompt,
                    "params": {"seconds": seconds, "steps": 150},
                },
            )["id"]
    OUT.write_text(json.dumps(jobs, indent=2) + "\n")
    print(json.dumps(jobs, indent=2))


if __name__ == "__main__":
    main()
