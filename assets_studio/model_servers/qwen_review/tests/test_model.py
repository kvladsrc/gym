"""The offload pieces on tiny modules: streaming, placement, LoRA hooks, embedding, head."""

import copy
import json
from pathlib import Path
from typing import Any

import pytest
import torch
from qwen_review_model_server.model import (
    LoRA,
    MappedEmbedding,
    Streamed,
    add_lora,
    digit_head,
    digit_ids,
    mapped_weights,
    place_layers,
    read_adapter,
)
from safetensors.torch import save_file

CPU = torch.device("cpu")


def tiny(seed: int = 0) -> torch.nn.Sequential:
    torch.manual_seed(seed)
    return torch.nn.Sequential(torch.nn.Linear(4, 8), torch.nn.ReLU(), torch.nn.Linear(8, 3))


def weights_of(module: torch.nn.Module) -> dict[str, torch.Tensor]:
    return {name: p.detach().clone() for name, p in module.named_parameters()}


def test_streamed_module_computes_like_a_resident_one() -> None:
    resident = tiny()
    streamed = copy.deepcopy(resident).to("meta")
    Streamed(streamed, weights_of(resident), CPU)
    x = torch.randn(2, 4)
    with torch.inference_mode():
        assert torch.equal(streamed(x), resident(x))
        assert torch.equal(streamed(x), resident(x))  # fetched again on the next pass
    assert all(p.is_meta for p in streamed.parameters())  # dropped after each pass


def test_layers_stay_on_the_device_until_one_does_not_fit() -> None:
    layers = [tiny().to("meta") for _ in range(4)]
    weights = [weights_of(tiny(seed)) for seed in range(4)]
    room = iter([True, True, False])
    assert place_layers(layers, weights, CPU, None, lambda size: next(room)) == 2
    assert not any(p.is_meta for layer in layers[:2] for p in layer.parameters())
    assert all(p.is_meta for layer in layers[2:] for p in layer.parameters())  # streamed


def test_layer_limit_caps_the_resident_layers() -> None:
    layers = [tiny().to("meta") for _ in range(3)]
    weights = [weights_of(tiny(seed)) for seed in range(3)]
    assert place_layers(layers, weights, CPU, 1, lambda size: True) == 1


def test_lora_hook_adds_the_float32_update_and_keeps_the_dtype() -> None:
    torch.manual_seed(0)
    linear = torch.nn.Linear(6, 5, bias=False).to(torch.bfloat16)
    a, b = torch.randn(2, 6), torch.randn(5, 2)
    x = torch.randn(3, 6, dtype=torch.bfloat16)
    expected = (linear(x).float() + (x.float() @ a.T @ b.T) * 2.0).to(torch.bfloat16)
    linear.register_forward_hook(LoRA(a, b, 2.0))
    out = linear(x)
    assert out.dtype == torch.bfloat16
    torch.testing.assert_close(out, expected)


def adapter_files(directory: Path, tensors: dict[str, torch.Tensor], **config: Any) -> None:
    save_file(tensors, directory / "adapter_model.safetensors")
    config = {"peft_type": "LORA", "r": 2, "lora_alpha": 4, "bias": "none", **config}
    (directory / "adapter_config.json").write_text(json.dumps(config))


def test_adapter_is_hooked_onto_the_modules_its_keys_name(tmp_path: Path) -> None:
    model = torch.nn.Module()
    model.model = torch.nn.Module()
    model.model.proj = torch.nn.Linear(4, 3, bias=False)
    a, b = torch.randn(2, 4), torch.randn(3, 2)
    adapter_files(
        tmp_path,
        {
            "base_model.model.model.proj.lora_A.weight": a,
            "base_model.model.model.proj.lora_B.weight": b,
        },
    )
    adapter = read_adapter(tmp_path)
    assert adapter.scaling == 2.0
    x = torch.randn(1, 4)
    base = model.model.proj(x)
    assert add_lora(model, adapter, CPU) == 1
    torch.testing.assert_close(model.model.proj(x), base + x @ a.T @ b.T * 2.0)


def test_adapter_options_that_change_the_math_are_refused(tmp_path: Path) -> None:
    adapter_files(tmp_path, {"x": torch.zeros(1)}, use_dora=True)
    with pytest.raises(ValueError, match="plain LoRA"):
        read_adapter(tmp_path)


def test_missing_adapter_names_its_directory(tmp_path: Path) -> None:
    with pytest.raises(FileNotFoundError, match=str(tmp_path)):
        read_adapter(tmp_path)


def test_mapped_weights_come_from_their_shards(tmp_path: Path) -> None:
    save_file({"a.weight": torch.arange(6.0).reshape(2, 3)}, tmp_path / "one.safetensors")
    save_file({"b.weight": torch.ones(2)}, tmp_path / "two.safetensors")
    index = {"a.weight": "one.safetensors", "b.weight": "two.safetensors"}
    weights = mapped_weights(tmp_path, index, {"a.weight", "b.weight"})
    assert torch.equal(weights["a.weight"], torch.arange(6.0).reshape(2, 3))
    assert torch.equal(weights["b.weight"], torch.ones(2))
    with pytest.raises(ValueError, match="not in the checkpoint"):
        mapped_weights(tmp_path, index, {"c.weight"})


def test_embedding_rows_are_looked_up_in_the_table() -> None:
    table = torch.randn(10, 4)
    ids = torch.tensor([[3, 1, 3]])
    assert torch.equal(MappedEmbedding(table)(ids), table[ids])


def test_digit_head_keeps_the_digit_rows_in_order() -> None:
    table = torch.randn(20, 4)
    head = digit_head(table, [15, 3, 7], CPU)
    h = torch.randn(1, 4)
    torch.testing.assert_close(head(h), h @ table[[15, 3, 7]].T)


class CharTokenizer:
    """One token per character, or ``merged`` text as a single token."""

    def __init__(self, merged: str = "") -> None:
        self.merged = merged

    def __call__(self, text: str, add_special_tokens: bool = False) -> dict[str, list[int]]:
        if text == self.merged:
            return {"input_ids": [1000]}
        return {"input_ids": [ord(c) for c in text]}


def test_digit_ids_are_single_tokens_apart_from_the_space() -> None:
    assert digit_ids(CharTokenizer()) == [ord(str(d)) for d in range(10)]
    with pytest.raises(ValueError, match="merge"):
        digit_ids(CharTokenizer(merged="5 "))
