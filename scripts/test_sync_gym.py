"""Regression checks for game exports; never touches the real public repo."""

import pathlib
import shutil
import subprocess
import tempfile
import unittest


class SyncGymTest(unittest.TestCase):
    def test_games_preview_and_binary_assets(self):
        repository = pathlib.Path(__file__).resolve().parents[1]
        with tempfile.TemporaryDirectory(prefix="sync-gym-test-") as directory:
            source = pathlib.Path(directory) / "source"
            destination = pathlib.Path(directory) / "destination"
            destination.mkdir()
            (source / "scripts").mkdir(parents=True)
            shutil.copy2(repository / "scripts/sync_gym.sh", source / "scripts")
            projects = (
                "games/cothic",
                "games/zooreader",
                "games/ripples_cli",
                "games/vibe_jakubovich_mvp",
                "games/future_game",
            )
            for project in projects:
                (source / project / "assets").mkdir(parents=True)
                ignore = repository / project / ".gitignore"
                if ignore.exists():
                    shutil.copy2(ignore, source / project)
                else:
                    (source / project / ".gitignore").write_text("/build/\n")
                (source / project / "README.md").write_text(
                    "Created by myuser at your.domain\n", encoding="utf-8"
                )
                (destination / project).mkdir(parents=True)
                (destination / project / "stale.txt").write_text("old source")

            # The public justfile must work without the unpublished draft.
            game_modules = [
                line
                for line in (repository / "justfile").read_text().splitlines()
                if line.startswith("mod") and '"games/' in line
            ]
            (source / "justfile").write_text("\n".join(game_modules) + "\n")
            (source / "games/vibe_jakubovich_mvp/justfile").write_text(
                "probe:\n    @echo mvp\n"
            )

            for project in ("cothic", "zooreader"):
                (source / "games" / project / "justfile").write_text(
                    f"probe:\n    @echo {project}\n"
                )

            # Simulate a public checkout made with the old directory layout.
            legacy = {
                "cothic": "games/cothic",
                "production/docker/zooreader": "games/zooreader",
                "ripples_cli": "games/ripples_cli",
                "vibe_jakubovich_mvp": "games/vibe_jakubovich_mvp",
            }
            for old, new in legacy.items():
                (destination / old).parent.mkdir(parents=True, exist_ok=True)
                (destination / new).rename(destination / old)

            excluded = [
                "games/cothic/.godot/imported/cache.bin",
                "games/cothic/tmp/frame.png",
                "games/cothic/build/game.pck",
                "games/zooreader/asset-work/concept.png",
                "games/future_game/build/output.bin",
                "games/vibe_jakubovich_mvp/.godot/imported/cache.bin",
                "games/vibe_jakubovich_mvp/build/game.pck",
                "games/vibe_jakubovich_mvp/tmp/frame.png",
                "games/vibe_jakubovich/references/media/episode.mp4",
                "games/vibe_jakubovich/references/frames/frame.png",
                "games/vibe_jakubovich/art/work/model.blend",
                "games/vibe_jakubovich/unreal/Saved/state.bin",
                "games/vibe_jakubovich/unreal/Intermediate/data.bin",
                "games/vibe_jakubovich/tools/__pycache__/pipeline.pyc",
                "games/vibe_jakubovich/.env",
            ]
            for relative in excluded:
                path = source / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(b"local-only")

            binaries = [
                "games/cothic/assets/hero.glb",
                "games/vibe_jakubovich_mvp/assets/spin.wav",
                "games/vibe_jakubovich_mvp/assets/font.ttf",
                "games/vibe_jakubovich_mvp/assets/font.otf",
                "games/vibe_jakubovich_mvp/assets/host.png",
                "games/vibe_jakubovich_mvp/assets/previews/wheel.gif",
                "static/vibe_jakubovich.gif",
            ]
            payload = b"\x00\xffmyuser your.domain\x00"
            for relative in binaries:
                path = source / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(payload)
            (source / "README.md").write_text(
                "![Preview](static/"
                "vibe_jakubovich.gif)\n",
                encoding="utf-8",
            )

            (source / "games/README.md").write_text(
                "![Preview](../static/vibe_jakubovich.gif)\n"
                "[Game](vibe_jakubovich_mvp)\n"
            )

            result = subprocess.run(
                ["bash", str(source / "scripts/sync_gym.sh"), str(destination)],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertFalse((destination / "games/vibe_jakubovich").exists())
            for old in legacy:
                self.assertFalse((destination / old).exists(), old)
            self.assertEqual(
                (destination / "games/README.md").read_text(),
                "![Preview](../static/vibe_jakubovich.gif)\n"
                "[Game](vibe_jakubovich_mvp)\n",
            )
            public_just = subprocess.run(
                ["just", "--justfile", str(destination / "justfile"),
                 "vibe_jakubovich_mvp", "probe"],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(public_just.returncode, 0, public_just.stderr)
            self.assertEqual(public_just.stdout.strip(), "mvp")
            for project in projects:
                with self.subTest(project=project):
                    self.assertEqual(
                        (destination / project / "README.md").read_text(),
                        "Created by myuser at your.domain\n",
                    )
                    self.assertTrue((destination / project / ".gitignore").is_file())
                    self.assertFalse((destination / project / "stale.txt").exists())
            for relative in excluded:
                with self.subTest(excluded=relative):
                    self.assertFalse((destination / relative).exists())
            for relative in binaries:
                with self.subTest(binary=relative):
                    self.assertEqual((destination / relative).read_bytes(), payload)
            self.assertEqual(
                (destination / "README.md").read_text(),
                "![Preview](static/vibe_jakubovich.gif)\n",
            )

    def test_migration_refuses_conflicting_copies(self):
        repository = pathlib.Path(__file__).resolve().parents[1]
        with tempfile.TemporaryDirectory(prefix="sync-gym-conflict-") as directory:
            source = pathlib.Path(directory) / "source"
            destination = pathlib.Path(directory) / "destination"
            (source / "scripts").mkdir(parents=True)
            (source / "games/cothic").mkdir(parents=True)
            shutil.copy2(repository / "scripts/sync_gym.sh", source / "scripts")
            for path in ("cothic", "games/cothic"):
                (destination / path).mkdir(parents=True)
                (destination / path / "local.txt").write_text(path)
            result = subprocess.run(
                ["bash", str(source / "scripts/sync_gym.sh"), str(destination)],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("reconcile them before syncing", result.stderr)
            for path in ("cothic", "games/cothic"):
                self.assertEqual(
                    (destination / path / "local.txt").read_text(), path
                )


if __name__ == "__main__":
    unittest.main()
