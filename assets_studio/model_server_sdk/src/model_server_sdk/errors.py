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
