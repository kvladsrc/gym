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
            projects = ("vibe_jakubovich_mvp",)
            for project in projects:
                (source / project / "assets").mkdir(parents=True)
                shutil.copy2(repository / project / ".gitignore", source / project)
                (source / project / "README.md").write_text(
                    "Created by myuser at your.domain\n", encoding="utf-8"
                )
                (destination / project).mkdir()
                (destination / project / "stale.txt").write_text("old source")

            # The public justfile must work without the unpublished draft.
            game_modules = [
                line
                for line in (repository / "justfile").read_text().splitlines()
                if line.startswith("mod") and "vibe_jakubovich" in line
            ]
            (source / "justfile").write_text("\n".join(game_modules) + "\n")
            (source / "vibe_jakubovich_mvp/justfile").write_text(
                "probe:\n    @echo mvp\n"
            )

            excluded = [
                "vibe_jakubovich_mvp/.godot/imported/cache.bin",
                "vibe_jakubovich_mvp/build/game.pck",
                "vibe_jakubovich_mvp/tmp/frame.png",
                "vibe_jakubovich/references/media/episode.mp4",
                "vibe_jakubovich/references/frames/frame.png",
                "vibe_jakubovich/art/work/model.blend",
                "vibe_jakubovich/unreal/Saved/state.bin",
                "vibe_jakubovich/unreal/Intermediate/data.bin",
                "vibe_jakubovich/tools/__pycache__/pipeline.pyc",
                "vibe_jakubovich/.env",
            ]
            for relative in excluded:
                path = source / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(b"local-only")

            binaries = [
                "vibe_jakubovich_mvp/assets/spin.wav",
                "vibe_jakubovich_mvp/assets/font.ttf",
                "vibe_jakubovich_mvp/assets/font.otf",
                "vibe_jakubovich_mvp/assets/host.png",
                "vibe_jakubovich_mvp/assets/previews/wheel.gif",
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

            result = subprocess.run(
                ["bash", str(source / "scripts/sync_gym.sh"), str(destination)],
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertFalse((destination / "vibe_jakubovich").exists())
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


if __name__ == "__main__":
    unittest.main()
