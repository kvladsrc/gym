"""Qwen3.5 in bfloat16 on an 8 GB GPU, with the LoRA applied by forward hooks.

Written for the 9B, whose weights do not fit; the 2B fits whole and nothing is streamed.

The weights are views of the memory-mapped checkpoint shards: whatever is not copied
to the GPU costs page cache, which the kernel can reclaim, instead of resident RAM.
The decoder layers that fit stay on the GPU; each of the others is copied there before
its forward pass and dropped after it. The 248k-row embedding table stays in the mapped
file and its rows are looked up on the CPU. Of the output head only the ten digit rows
are kept: the server reads nothing but the softmax over the digits.
"""

import json
from collections.abc import Callable, Iterable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn.functional as F

LAYERS = "model.language_model.layers."
EMBEDDING = "model.language_model.embed_tokens.weight"
HEAD = "lm_head.weight"
LORA_PREFIX = "base_model.model."
# GPU memory left free while layers are placed: a streamed layer, activations, workspaces.
RESERVE_BYTES = 3 * 2**29  # 1.5 GiB
# Adapter options that change the LoRA computation; the hooks implement plain LoRA only.
_UNSUPPORTED = (
    "use_dora",
    "use_rslora",
    "fan_in_fan_out",
    "rank_pattern",
    "alpha_pattern",
    "modules_to_save",
    "trainable_token_indices",
    "layer_replication",
    "target_parameters",
)


@dataclass(frozen=True)
class Adapter:
    """A plain LoRA adapter as PEFT saves it."""

    weights: Path
    scaling: float


def read_adapter(directory: Path) -> Adapter:
    config_file = directory / "adapter_config.json"
    weights = directory / "adapter_model.safetensors"
    if not (config_file.is_file() and weights.is_file()):
        raise FileNotFoundError(
            f"no LoRA adapter in {directory}:"
            " copy adapter_config.json and adapter_model.safetensors there"
        )
    config = json.loads(config_file.read_text(encoding="utf-8"))
    extras = [key for key in _UNSUPPORTED if config.get(key)]
    if config.get("peft_type") != "LORA" or config.get("bias", "none") != "none" or extras:
        raise ValueError(
            "only a plain LoRA adapter is supported"
            f" (type {config.get('peft_type')}, bias {config.get('bias')}, options {extras})"
        )
    return Adapter(weights, config["lora_alpha"] / config["r"])


class LoRA:
    """Adds B(A(x))·scaling to a linear layer's output, in float32 as PEFT's LoRA layer
    does with float32 adapter weights, and casts the sum back."""

    def __init__(self, a: torch.Tensor, b: torch.Tensor, scaling: float) -> None:
        self.a, self.b, self.scaling = a, b, scaling

    def __call__(self, module: torch.nn.Module, args: tuple, output: torch.Tensor) -> torch.Tensor:
        delta = F.linear(F.linear(args[0].to(self.a.dtype), self.a), self.b)
        return (output + delta * self.scaling).to(output.dtype)


def add_lora(model: torch.nn.Module, adapter: Adapter, device: torch.device) -> int:
    """Hooks the adapter onto its linear layers; returns how many there are."""
    from safetensors.torch import load_file

    parts: dict[str, dict[str, torch.Tensor]] = {}
    for key, tensor in load_file(adapter.weights, device=str(device)).items():
        name, dot, part = key.removeprefix(LORA_PREFIX).rpartition(".lora_")
        if not key.startswith(LORA_PREFIX) or not dot:
            raise ValueError(f"unexpected adapter tensor {key}")
        parts.setdefault(name, {})[part] = tensor
    for name, tensors in parts.items():
        module = model.get_submodule(name)
        if not isinstance(module, torch.nn.Linear) or tensors.keys() != {"A.weight", "B.weight"}:
            raise ValueError(f"unexpected LoRA tensors for {name}: {sorted(tensors)}")
        module.register_forward_hook(
            LoRA(tensors["A.weight"], tensors["B.weight"], adapter.scaling)
        )
    return len(parts)


def put(root: torch.nn.Module, name: str, tensor: torch.Tensor, device: torch.device) -> None:
    """Replaces the parameter ``name`` with ``tensor`` on ``device``, in the parameter's dtype."""
    owner_name, _, attribute = name.rpartition(".")
    owner = root.get_submodule(owner_name)
    dtype = getattr(owner, attribute).dtype
    setattr(
        owner,
        attribute,
        torch.nn.Parameter(tensor.to(device=device, dtype=dtype), requires_grad=False),
    )


class Streamed:
    """Copies a module's weights to ``device`` before each forward pass, drops them after.

    Between passes the module holds its meta placeholders; the weights stay where they
    are given, here the mapped checkpoint.
    """

    def __init__(
        self, module: torch.nn.Module, weights: dict[str, torch.Tensor], device: torch.device
    ) -> None:
        self.device = device
        self.slots = []
        for name, tensor in weights.items():
            owner_name, _, attribute = name.rpartition(".")
            owner = module.get_submodule(owner_name)
            placeholder = getattr(owner, attribute)
            self.slots.append((owner, attribute, placeholder, tensor.to(placeholder.dtype)))
        module.register_forward_pre_hook(self.fetch)
        module.register_forward_hook(self.drop)

    def fetch(self, module: torch.nn.Module, args: tuple) -> None:
        for owner, attribute, _, tensor in self.slots:
            setattr(
                owner, attribute, torch.nn.Parameter(tensor.to(self.device), requires_grad=False)
            )

    def drop(self, module: torch.nn.Module, args: tuple, output: Any) -> None:
        for owner, attribute, placeholder, _ in self.slots:
            setattr(owner, attribute, placeholder)


class MappedEmbedding(torch.nn.Module):
    """The token embedding table left in the mapped file: rows are looked up on the CPU."""

    def __init__(self, weight: torch.Tensor) -> None:
        super().__init__()
        self.weight = weight  # a plain tensor, not a parameter: it never moves

    def forward(self, ids: torch.Tensor) -> torch.Tensor:
        return F.embedding(ids.cpu(), self.weight).to(ids.device)


def digit_head(table: torch.Tensor, digits: Sequence[int], device: torch.device) -> torch.nn.Linear:
    """The output head cut down to the digit rows, in digit order."""
    head = torch.nn.Linear(
        table.shape[1], len(digits), bias=False, device=device, dtype=table.dtype
    )
    with torch.no_grad():
        head.weight.copy_(table[list(digits)])
    return head.requires_grad_(False)


def nbytes(tensors: Iterable[torch.Tensor]) -> int:
    return sum(tensor.numel() * tensor.element_size() for tensor in tensors)


def place_layers(
    layers: Sequence[torch.nn.Module],
    weights: Sequence[dict[str, torch.Tensor]],
    device: torch.device,
    limit: int | None,
    fits: Callable[[int], bool],
) -> int:
    """Puts layers on ``device`` from the first while they fit and ``limit`` allows; the
    rest are streamed. Returns how many stay on the device."""
    resident, streaming = 0, False
    for layer, own in zip(layers, weights, strict=True):
        streaming = (
            streaming or (limit is not None and resident >= limit) or not fits(nbytes(own.values()))
        )
        if streaming:
            Streamed(layer, own, device)
        else:
            for name, tensor in own.items():
                put(layer, name, tensor, device)
            resident += 1
    return resident


def checkpoint_index(base: Path) -> dict[str, str]:
    text = (base / "model.safetensors.index.json").read_text(encoding="utf-8")
    return json.loads(text)["weight_map"]


def mapped_weights(base: Path, index: dict[str, str], names: set[str]) -> dict[str, torch.Tensor]:
    """The checkpoint tensors ``names``, as views of the memory-mapped shards."""
    from safetensors import safe_open

    missing = sorted(names - index.keys())
    if missing:
        raise ValueError(f"{len(missing)} weights are not in the checkpoint, e.g. {missing[:3]}")
    weights: dict[str, torch.Tensor] = {}
    for shard in sorted({index[name] for name in names}):
        # safe_open's CPU tensors share the file mapping and outlive the handle.
        with safe_open(base / shard, framework="pt", device="cpu") as handle:
            weights.update(
                {name: handle.get_tensor(name) for name in names if index[name] == shard}
            )
    return weights


def gpu_fits(device: torch.device) -> Callable[[int], bool]:
    return lambda size: torch.cuda.mem_get_info(device)[0] - size >= RESERVE_BYTES


def build(
    base: Path,
    adapter: Adapter,
    digits: Sequence[int],
    device: torch.device,
    gpu_layers: int | None = None,
) -> tuple[torch.nn.Module, int]:
    """The model ready for digit reads, and how many decoder layers stay on ``device``."""
    from accelerate import init_empty_weights
    from transformers import AutoConfig, Qwen3_5ForConditionalGeneration

    config = AutoConfig.from_pretrained(base)
    with init_empty_weights():  # parameters on the meta device; buffers (rotary tables) real
        model = Qwen3_5ForConditionalGeneration._from_config(
            config, dtype=torch.bfloat16, attn_implementation="sdpa"
        )
    model.eval()
    index = checkpoint_index(base)
    names = {name for name, _ in model.named_parameters()} - {HEAD}
    head = HEAD if HEAD in index else EMBEDDING  # a tied head is the embedding table
    weights = mapped_weights(base, index, names | {head})
    for name, buffer in list(model.named_buffers()):
        owner_name, _, attribute = name.rpartition(".")
        model.get_submodule(owner_name)._buffers[attribute] = buffer.to(device)
    text = model.model.language_model
    model.lm_head = digit_head(weights[head], digits, device)
    text.embed_tokens = MappedEmbedding(weights[EMBEDDING])
    for name in names:
        if name != EMBEDDING and not name.startswith(LAYERS):
            put(model, name, weights[name], device)
    add_lora(model, adapter, device)
    per_layer = [
        {
            name.removeprefix(f"{LAYERS}{i}."): weights[name]
            for name in names
            if name.startswith(f"{LAYERS}{i}.")
        }
        for i in range(len(text.layers))
    ]
    fits = gpu_fits(device) if device.type == "cuda" else (lambda size: True)
    resident = place_layers(text.layers, per_layer, device, gpu_layers, fits)
    stray = [n for n, p in model.named_parameters() if p.is_meta and not n.startswith(LAYERS)]
    if stray:
        raise RuntimeError(f"weights left unloaded: {stray[:3]}")
    return model, resident


def digit_ids(tokenizer: Any) -> list[int]:
    """Token ids of "0".."9"; a class digit and the space after it must stay two tokens."""

    def ids(text: str) -> list[int]:
        return tokenizer(text, add_special_tokens=False)["input_ids"]

    digits = [ids(str(d)) for d in range(10)]
    if any(len(d) != 1 for d in digits):
        raise ValueError(f"the digits are not single tokens: {digits}")
    for d in range(10):
        if ids(f"{d} ")[:1] != digits[d] or len(ids(f"{d} ")) != 2:
            raise ValueError(f"{d!r} and a space merge into one token")
    return [d[0] for d in digits]


@torch.inference_mode()
def digit_probs(
    model: Any, processor: Any, text: str, image: Any, device: torch.device
) -> np.ndarray:
    """The softmax over the ten digits for the token after ``text``, as in evaluation."""
    encoded = processor(text=[text], images=[image], padding=True, return_tensors="pt")
    encoded = {key: value.to(device) for key, value in encoded.items()}
    logits = model(**encoded, logits_to_keep=1, use_cache=False).logits[:, -1].float()
    return torch.softmax(logits, dim=-1)[0].cpu().numpy()
