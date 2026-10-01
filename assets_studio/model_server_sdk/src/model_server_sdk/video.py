"""Encoding frames into the canonical video of ADR-003.

H.264 (High), yuv420p, ``+faststart``, encoded bit-exactly so that the same
frames always give the same bytes (the contract check compares bytes). Uses
the system ``ffmpeg``; the ``video`` extra installs ``imageio-ffmpeg`` as a
fallback with its own binary.
"""

import contextlib
import shutil
import subprocess
import tempfile
from collections.abc import Iterable
from pathlib import Path

ENCODE_TIMEOUT_S = 600


class VideoEncodingError(RuntimeError):
    pass


def ffmpeg_binary() -> str:
    found = shutil.which("ffmpeg")
    if found is not None:
        return found
    try:
        import imageio_ffmpeg  # pyright: ignore[reportMissingImports]
    except ImportError:
        raise VideoEncodingError(
            "encoding video needs ffmpeg: install it or the SDK's `video` extra"
        ) from None
    # An optional package without type information.
    return str(imageio_ffmpeg.get_ffmpeg_exe())  # pyright: ignore


def canonical_h264_options(crf: int = 18, source_matrix: str | None = None) -> list[str]:
    """ffmpeg output options for the canonical video stream (ADR-003).

    Colour: converted with the BT.709 matrix and tagged so. Without tags,
    browsers take HD video for BT.709 while ffmpeg's default conversion is
    BT.601: reds and greens would shift. The thread count is fixed so the
    same frames give the same bytes on any machine.

    ``source_matrix`` (``bt709``/``bt601``) overrides the input's YUV matrix
    when a YUV source carries no tag: ffmpeg would then assume BT.601.
    """
    source = f"in_color_matrix={source_matrix}:" if source_matrix else ""
    return [
        *("-c:v", "libx264", "-preset", "slow", "-crf", str(crf), "-profile:v", "high"),
        *("-x264-params", "threads=8"),
        *(
            "-vf",
            # yuv420p needs even sides.
            "pad=ceil(iw/2)*2:ceil(ih/2)*2,"
            f"scale={source}out_color_matrix=bt709:out_range=tv,format=yuv420p,"
            "setparams=color_primaries=bt709:color_trc=bt709:colorspace=bt709:range=tv",
        ),
        *("-pix_fmt", "yuv420p", "-movflags", "+faststart"),
    ]


def encode_mp4(
    frames: Iterable[bytes], *, width: int, height: int, fps: float, crf: int = 18
) -> bytes:
    """Encode RGB24 frames (``width * height * 3`` bytes each) as canonical MP4.

    Odd sides are padded to even (yuv420p needs them). A low CRF keeps the
    model's detail: these files are assets, not streams.
    """
    command = [
        ffmpeg_binary(),
        *("-nostdin", "-loglevel", "error", "-y"),
        *("-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{width}x{height}", "-r", f"{fps}"),
        *("-i", "pipe:0"),
        *canonical_h264_options(crf),
        # Same frames, same bytes: no encoder or muxer metadata, no timestamps.
        *("-fflags", "+bitexact", "-flags:v", "+bitexact", "-map_metadata", "-1"),
    ]
    frame_size = width * height * 3
    with tempfile.TemporaryDirectory(prefix="video-") as directory:
        target = Path(directory) / "out.mp4"
        # Messages to a file: a pipe nobody reads while frames are written
        # could fill up and stall both sides.
        log_path = Path(directory) / "ffmpeg.log"
        with log_path.open("wb") as log:
            process = subprocess.Popen(  # noqa: S603 -- ffmpeg with fixed arguments
                [*command, str(target)],
                stdin=subprocess.PIPE,
                stdout=subprocess.DEVNULL,
                stderr=log,
            )
        assert process.stdin is not None
        try:
            for index, frame in enumerate(frames):
                if len(frame) != frame_size:
                    raise VideoEncodingError(
                        f"frame {index} has {len(frame)} bytes, expected {frame_size}"
                    )
                process.stdin.write(frame)
        except BrokenPipeError:
            pass  # ffmpeg failed; its message is read below
        except BaseException:
            process.kill()
            with contextlib.suppress(OSError):  # the pipe to a killed process
                process.stdin.close()
            process.wait()
            raise
        # communicate() flushes and closes stdin (ignoring a broken pipe).
        try:
            process.communicate(timeout=ENCODE_TIMEOUT_S)
        except subprocess.TimeoutExpired as error:
            process.kill()
            process.wait()
            raise VideoEncodingError(f"encoding took over {ENCODE_TIMEOUT_S} s") from error
        stderr = log_path.read_bytes()
        if process.returncode != 0 or not target.is_file():
            message = stderr.decode(errors="replace").strip() or "ffmpeg failed"
            raise VideoEncodingError(f"cannot encode the video: {message}")
        return target.read_bytes()
