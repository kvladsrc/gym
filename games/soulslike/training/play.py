"""Play against the trained duellist in the arena viewer (play.sh runs this).

The viewer (unity/soulslike/Builds/ArenaView, "Build Arena Viewer") runs in a
window with ``-play``: the first fighter is yours (HumanFighter: WASD, the
mouse, its buttons, F, Shift, Space), the second takes its decisions from
the run's newest exported model, loaded once at the start, with the same
inputs, masks and sampling as in training. Until the window is closed (or
Ctrl+C).
"""

import argparse
import random
import sys

from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.exception import UnityException
from mlagents_envs.side_channel.engine_configuration_channel import (
    EngineConfigurationChannel,
)
from record import HERE, VIEWER, Ignored, Stop, load


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("run", help="the run id (results/<run>)")
    parser.add_argument(
        "--deterministic",
        action="store_true",
        help="the model always takes its likeliest action",
    )
    parser.add_argument("--port", type=int, default=6015)
    args = parser.parse_args()

    models, policies = load(HERE / "results" / args.run, args.deterministic)
    for name, path in models.items():
        print(f"{name}: {path.relative_to(HERE)}")

    engine = EngineConfigurationChannel()
    env = UnityEnvironment(
        file_name=str(VIEWER),
        base_port=args.port,
        seed=random.randrange(1 << 30),
        no_graphics=False,
        side_channels=[engine, Ignored()],
        timeout_wait=600,
        additional_args=[
            "-play",
            "-screen-fullscreen",
            "0",
            "-screen-width",
            "1280",
            "-screen-height",
            "720",
        ],
    )
    # Real time, as a game runs.
    engine.set_configuration_parameters(
        time_scale=1.0, capture_frame_rate=0, target_frame_rate=60
    )
    stop = Stop()
    try:
        env.reset()
        while not stop.asked:
            for behavior in env.behavior_specs:
                decisions, ended = env.get_steps(behavior)
                policy = policies[behavior.split("?")[0]]
                policy.forget(ended.agent_id)
                if len(decisions) == 0:
                    continue
                env.set_actions(behavior, ActionTuple(discrete=policy.act(decisions)))
            env.step()
    except (KeyboardInterrupt, UnityException):
        # Ctrl+C, or the window closed.
        pass
    finally:
        env.close()


if __name__ == "__main__":
    sys.exit(main())
