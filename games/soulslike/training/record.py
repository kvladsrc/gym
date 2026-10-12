"""Film the trained duellists fighting in the arena (record.sh runs this).

The arena viewer (unity/soulslike/Builds/ArenaView, "Build Arena Viewer")
runs with graphics and ``-record <folder>``: it films the first arena. Here
each decision is taken by the run's newest exported models (.onnx, as the
trainer writes them at its checkpoints), with the same inputs and masks as in
training and the same sampling. The training goes on meanwhile: another
player, another port.
"""

import argparse
import random
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import uuid
from pathlib import Path

import numpy as np
import onnxruntime as ort
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.exception import UnityException
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.side_channel import SideChannel

HERE = Path(__file__).resolve().parent
VIEWER = HERE.parent / "unity/soulslike/Builds/ArenaView/Arena.x86_64"
# A decision every 10 academy steps of 0.02 s (ML-Agents steps with the physics).
STEPS_PER_SECOND = 5


def newest(folder: Path, behavior: str) -> Path:
    """The checkpoint with the most steps, or the final export."""
    found = []
    for path in (folder / behavior).glob(f"{behavior}-*.onnx"):
        match = re.fullmatch(rf"{behavior}-(\d+)\.onnx", path.name)
        if match:
            found.append((int(match.group(1)), path))
    if found:
        return max(found)[1]
    final = folder / f"{behavior}.onnx"
    if final.exists():
        return final
    sys.exit(f"no {behavior} model in {folder}")


class Ignored(SideChannel):
    """The arena's statistics (DuelEpisode's, for the trainer's TensorBoard):
    taken and dropped, not warned of at every step."""

    def __init__(self):
        super().__init__(uuid.UUID("a1d8f7b7-cec8-50f9-b78b-d3e165a78520"))

    def on_message_received(self, msg) -> None:
        pass


class Stop:
    """Ctrl+C asks the loop to end between steps: an interrupt inside the
    model's or the connection's native code crashed the interpreter
    (a segmentation fault) instead of closing the player."""

    def __init__(self):
        self.asked = False
        signal.signal(signal.SIGINT, self.ask)

    def ask(self, *_) -> None:
        if self.asked:
            raise KeyboardInterrupt
        self.asked = True


class Policy:
    """A model's decisions; with a memory (LSTM), each agent's carried from one
    decision to its next, and forgotten as its episode ends (as ML-Agents does
    in training)."""

    def __init__(self, path: Path, deterministic: bool):
        # One thread, sleeping between calls: the default, a thread a core
        # spinning while it waits, took a core and more beside the player
        # for a network this small.
        options = ort.SessionOptions()
        options.intra_op_num_threads = 1
        options.inter_op_num_threads = 1
        options.add_session_config_entry("session.intra_op.allow_spinning", "0")
        self.session = ort.InferenceSession(str(path), options, providers=["CPUExecutionProvider"])
        self.inputs = {i.name: i for i in self.session.get_inputs()}
        self.output = "deterministic_discrete_actions" if deterministic else "discrete_actions"
        self.memory_size = 0
        if "recurrent_in" in self.inputs:
            size = self.inputs["recurrent_in"].shape[-1]
            self.memory_size = size if isinstance(size, int) else int(np.asarray(self.session.run(["memory_size"], {})[0]).ravel()[0])
        self.memories: dict[int, np.ndarray] = {}

    def act(self, steps) -> np.ndarray:
        feed = {f"obs_{i}": obs.astype(np.float32) for i, obs in enumerate(steps.obs)}
        # ML-Agents masks: True = not allowed; the model wants 1 = allowed.
        masks = np.concatenate(steps.action_mask, axis=1) if steps.action_mask else None
        feed["action_masks"] = (1 - masks).astype(np.float32) if masks is not None else None
        outputs = [self.output]
        if self.memory_size:
            blank = np.zeros((1, self.memory_size), dtype=np.float32)
            feed["recurrent_in"] = np.stack([self.memories.get(int(a), blank) for a in steps.agent_id])
            outputs.append("recurrent_out")
        feed = {k: v for k, v in feed.items() if k in self.inputs and v is not None}
        results = self.session.run(outputs, feed)
        if self.memory_size:
            for agent, memory in zip(steps.agent_id, results[1]):
                self.memories[int(agent)] = np.asarray(memory, dtype=np.float32).reshape(1, self.memory_size)
        return np.asarray(results[0], dtype=np.int32)

    def forget(self, agents) -> None:
        """Agents whose episode ended: their next fight begins with no memory."""
        for agent in agents:
            self.memories.pop(int(agent), None)


def newest_models(results: Path) -> dict[str, Path]:
    """The newest model of each behaviour the run trained (the duel: one,
    "Fighter", for both sides)."""
    behaviors = sorted(d.name for d in results.iterdir()
                       if d.is_dir() and any(d.glob(f"{d.name}-*.onnx")))
    if not behaviors:
        sys.exit(f"no models in {results}")
    return {name: newest(results, name) for name in behaviors}


def load(results: Path, deterministic: bool) -> tuple[dict[str, Path], dict[str, "Policy"]]:
    models = newest_models(results)
    return models, {name: Policy(path, deterministic) for name, path in models.items()}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("run", help="the run id (results/<run>)")
    parser.add_argument("--seconds", type=float, default=60.0, help="game time filmed")
    parser.add_argument("--deterministic", action="store_true", help="always the likeliest action")
    parser.add_argument("--watch", action="store_true",
                        help="show the fight in the window, in real time, until it is closed "
                             "(or Ctrl+C); nothing is written; a newer model is taken up each minute")
    parser.add_argument("--port", type=int, default=6005)
    parser.add_argument("--seed", type=int, default=None,
                        help="Unity's random seed (sizes, weapons, the arena's layout); a new one by default")
    args = parser.parse_args()

    results = HERE / "results" / args.run
    models, policies = load(results, args.deterministic)
    step = min(int(re.search(r"-(\d+)", p.name).group(1)) if re.search(r"-(\d+)", p.name) else 0
               for p in models.values())
    for name, path in models.items():
        print(f"{name}: {path.relative_to(HERE)}")

    frames = None if args.watch else Path(tempfile.mkdtemp(prefix="arena-frames-"))
    engine = EngineConfigurationChannel()
    env = UnityEnvironment(
        file_name=str(VIEWER),
        base_port=args.port,
        # ML-Agents seeds Unity's random from this; its default, 0, gave every
        # film and watch the same fighters, weapons and arena.
        seed=args.seed if args.seed is not None else random.randrange(1 << 30),
        no_graphics=False,
        side_channels=[engine, Ignored()],
        # Drawing and writing frames is slow beside training; wait long.
        timeout_wait=600,
        additional_args=(["-watch"] if args.watch else ["-record", str(frames)])
        + ["-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720"],
    )
    if args.watch:
        # Real time, as a game runs.
        engine.set_configuration_parameters(time_scale=1.0, capture_frame_rate=0, target_frame_rate=60)
    else:
        engine.set_configuration_parameters(time_scale=1.0, capture_frame_rate=60, target_frame_rate=-1)
    stop = Stop()
    try:
        env.reset()
        steps = 0
        reloaded = time.monotonic()
        stepped = 0
        while not stop.asked and (args.watch or steps < int(args.seconds * STEPS_PER_SECOND)):
            for behavior in env.behavior_specs:
                decisions, ended = env.get_steps(behavior)
                policy = policies[behavior.split("?")[0]]
                policy.forget(ended.agent_id)
                if len(decisions) == 0:
                    continue
                env.set_actions(behavior, ActionTuple(discrete=policy.act(decisions)))
            env.step()
            steps += 1
            if args.watch and time.monotonic() - reloaded > 60:
                # The game's pace over the minute, in decisions a second.
                pace = (steps - stepped) / (time.monotonic() - reloaded)
                print(f"{pace:.1f} decisions/s (real time is {STEPS_PER_SECOND})")
                reloaded, stepped = time.monotonic(), steps
                if newest_models(results) != models:
                    models, policies = load(results, args.deterministic)
                    for name, path in models.items():
                        print(f"{name}: {path.relative_to(HERE)}")
    except KeyboardInterrupt:
        pass
    except UnityException:
        # Watching, the window was closed; filming, a real failure.
        if not args.watch:
            raise
    finally:
        env.close()
    if args.watch:
        return

    videos = results / "videos"
    videos.mkdir(exist_ok=True)
    out = videos / f"step-{step}.mp4"
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", "-framerate", "30",
                    "-i", str(frames / "frame%05d.jpg"), "-c:v", "libx264", "-pix_fmt", "yuv420p",
                    "-crf", "20", str(out)], check=True)
    shutil.rmtree(frames)
    print(f"written {out.relative_to(HERE)}")


if __name__ == "__main__":
    main()
