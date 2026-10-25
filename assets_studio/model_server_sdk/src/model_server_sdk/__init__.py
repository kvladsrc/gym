"""SDK for asset studio model servers (contract v1, ADR-001 and ADR-003)."""

from model_server_sdk.cli import serve
from model_server_sdk.contract import (
    CONTRACT_VERSION,
    PRIMARY,
    InputSpec,
    KnownTask,
    ModelInfo,
    ui,
)
from model_server_sdk.errors import (
    EXIT_GPU_BROKEN,
    explain_child_failure,
    is_gpu_broken,
    is_out_of_memory,
)
from model_server_sdk.server import (
    GenerationError,
    InputData,
    InvalidInput,
    Job,
    ModelServer,
    Output,
    TaskSpec,
    create_app,
)

__all__ = [
    "CONTRACT_VERSION",
    "EXIT_GPU_BROKEN",
    "PRIMARY",
    "GenerationError",
    "InputData",
    "InputSpec",
    "InvalidInput",
    "Job",
    "KnownTask",
    "ModelInfo",
    "ModelServer",
    "Output",
    "TaskSpec",
    "create_app",
    "explain_child_failure",
    "is_gpu_broken",
    "is_out_of_memory",
    "serve",
    "ui",
]
