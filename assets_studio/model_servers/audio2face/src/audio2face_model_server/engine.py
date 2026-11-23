"""Build a model's TensorRT engine from its published ONNX network.

    python -m audio2face_model_server.engine MODEL_DIRECTORY MAX_BATCH OPT_BATCH REVISION

The server runs this as a child process, so that its own process never holds
a CUDA context; the runner, another child, uses the engine. The engine is
built as upstream's tools build it (trtexec with the shapes and the shared
memory limit of the model's trt_info.json, full precision) and lands next to
the model's model.json as network.trt, with a stamp naming the TensorRT
version, the GPU, the model's revision and the batch sizes. A stamp that
still matches means there is nothing to do.
"""

import ctypes
import json
import sys
import time
from pathlib import Path

ENGINE = "network.trt"
STAMP = "network.trt.json"
# trt_info.json's "cuda_in_graphics" argument, which upstream passes on
# every build: --memPoolSize=tacticSharedMem:0.046875 (MiB).
TACTIC_SHARED_MEMORY = int(0.046875 * 2**20)
WORKSPACE = 2 * 2**30


def gpu() -> str:
    """The first visible GPU's name and compute capability, from the driver."""
    cuda = ctypes.CDLL("libcuda.so.1")
    device, major, minor = ctypes.c_int(), ctypes.c_int(), ctypes.c_int()
    name = ctypes.create_string_buffer(256)
    for status in (
        cuda.cuInit(0),
        cuda.cuDeviceGet(ctypes.byref(device), 0),
        cuda.cuDeviceGetName(name, len(name), device),
        cuda.cuDeviceGetAttribute(ctypes.byref(major), 75, device),  # compute capability major
        cuda.cuDeviceGetAttribute(ctypes.byref(minor), 76, device),  # and minor
    ):
        if status != 0:
            raise SystemExit(f"cannot query the GPU (CUDA driver error {status})")
    return f"{name.value.decode()} (sm_{major.value}{minor.value})"


def shapes(
    directory: Path, max_batch: int, opt_batch: int
) -> dict[str, tuple[tuple[int, ...], ...]]:
    """Input name -> (min, opt, max) shapes, from trt_info.json's trtexec arguments."""
    info = json.loads((directory / "trt_info.json").read_text())
    table: dict[str, list[tuple[int, ...]]] = {}
    order = {"--minShapes": 0, "--optShapes": 1, "--maxShapes": 2}
    for argument in info["trt_build_param"]["batch"]:
        option, _, value = argument.format(
            MAX_BATCH_SIZE=max_batch, OPT_BATCH_SIZE=opt_batch
        ).partition("=")
        for item in value.split(","):
            name, _, dims = item.partition(":")
            table.setdefault(name, [(), (), ()])[order[option]] = tuple(
                int(dim) for dim in dims.split("x")
            )
    return {name: (low, optimum, high) for name, (low, optimum, high) in table.items()}


def stamp(max_batch: int, opt_batch: int, revision: str) -> dict[str, object]:
    import tensorrt_bindings as trt

    return {
        "tensorrt": trt.__version__,
        "gpu": gpu(),
        "revision": revision,
        "batch": [max_batch, opt_batch],
    }


def build(directory: Path, max_batch: int, opt_batch: int) -> None:
    # The tensorrt-cu12-bindings wheel names its module tensorrt_bindings;
    # only NVIDIA's tensorrt-cu12 front-end package re-exports it as tensorrt.
    import tensorrt_bindings as trt

    logger = trt.Logger(trt.Logger.WARNING)
    builder = trt.Builder(logger)
    network = builder.create_network(0)
    parser = trt.OnnxParser(network, logger)
    if not parser.parse_from_file(str(directory / "network.onnx")):
        errors = "; ".join(str(parser.get_error(index)) for index in range(parser.num_errors))
        raise SystemExit(f"cannot read the ONNX network: {errors}")
    config = builder.create_builder_config()
    config.set_memory_pool_limit(trt.MemoryPoolType.WORKSPACE, WORKSPACE)
    config.set_memory_pool_limit(trt.MemoryPoolType.TACTIC_SHARED_MEMORY, TACTIC_SHARED_MEMORY)
    profile = builder.create_optimization_profile()
    for name, (low, optimum, high) in shapes(directory, max_batch, opt_batch).items():
        profile.set_shape(name, low, optimum, high)
    config.add_optimization_profile(profile)
    plan = builder.build_serialized_network(network, config)
    if plan is None:
        raise SystemExit("TensorRT could not build the engine")
    partial = directory / f"{ENGINE}.part"
    partial.write_bytes(memoryview(plan))
    partial.replace(directory / ENGINE)


def main() -> None:
    if len(sys.argv) != 5:
        raise SystemExit(__doc__)
    directory, revision = Path(sys.argv[1]), sys.argv[4]
    max_batch, opt_batch = int(sys.argv[2]), int(sys.argv[3])
    wanted = stamp(max_batch, opt_batch, revision)
    stamp_path = directory / STAMP
    if (
        (directory / ENGINE).is_file()
        and stamp_path.is_file()
        and json.loads(stamp_path.read_text()) == wanted
    ):
        print("engine up to date", flush=True)
        return
    stamp_path.unlink(missing_ok=True)
    print(f"building {directory / ENGINE} for {wanted['gpu']}", flush=True)
    started = time.monotonic()
    build(directory, max_batch, opt_batch)
    stamp_path.write_text(json.dumps(wanted, indent=2) + "\n")
    print(f"engine built in {time.monotonic() - started:.0f} s", flush=True)


if __name__ == "__main__":
    main()
