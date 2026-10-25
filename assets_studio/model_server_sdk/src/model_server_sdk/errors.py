"""Helpers for classifying errors raised by model libraries (no torch import)."""

_OUT_OF_MEMORY_MARKERS = (
    "out of memory",
    "CUBLAS_STATUS_ALLOC_FAILED",
    "CUDNN_STATUS_ALLOC_FAILED",
)


def is_out_of_memory(error: BaseException) -> bool:
    """True for GPU allocation failures.

    PyTorch raises ``torch.cuda.OutOfMemoryError``, but cuBLAS and cuDNN
    failures arrive as plain ``RuntimeError`` with a telling message. Report
    them as retryable ``GenerationError``: freeing GPU memory can help.
    """
    if type(error).__name__ == "OutOfMemoryError":
        return True
    message = str(error)
    return any(marker in message for marker in _OUT_OF_MEMORY_MARKERS)


# A server that exits with this code broke its GPU state, not its setup: start
# it again at once (the launcher does). EX_TEMPFAIL of sysexits.h.
EXIT_GPU_BROKEN = 75

# CUDA errors that poison the process's CUDA context: every later call fails
# with them too, until the process exits.
_GPU_BROKEN_MARKERS = (
    "illegal memory access",
    "device-side assert",
    "unspecified launch failure",
    "misaligned address",
    "illegal instruction",
)


def gpu_broken_cause(error: BaseException) -> BaseException | None:
    """The error, or one it came from, that broke the process's CUDA context
    (e.g. a native library writing past its buffer); None if none did. After
    it the model cannot run again in this process.
    """
    seen: set[int] = set()
    current: BaseException | None = error
    while current is not None and id(current) not in seen:
        seen.add(id(current))
        if any(marker in str(current) for marker in _GPU_BROKEN_MARKERS):
            return current
        current = current.__cause__ or current.__context__
    return None


def is_gpu_broken(error: BaseException) -> bool:
    """True when the error comes from a broken CUDA context (gpu_broken_cause)."""
    return gpu_broken_cause(error) is not None


def explain_child_failure(output: str, returncode: int, lines: int = 6) -> str:
    """Why a child process failed, for a job's error message.

    Its last exception if it printed one (a traceback's last line), else the
    end of its output; a negative return code (killed by a signal, e.g. a
    segmentation fault in native code) is named first, since such a crash
    prints nothing of its own and the output ends in unrelated warnings.
    """
    import signal

    text = output.strip().splitlines()
    error = next(
        (line for line in reversed(text) if line and not line[0].isspace() and "Error" in line),
        None,
    )
    detail = error if error is not None else "\n".join(text[-lines:])
    if returncode < 0:
        try:
            name = signal.Signals(-returncode).name
        except ValueError:
            name = f"signal {-returncode}"
        return f"the process crashed ({name})" + (f"; last error: {error}" if error else "")
    return detail
