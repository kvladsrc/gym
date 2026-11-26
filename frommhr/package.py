"""A reader's package as one safetensors file: the tensors under dotted keys, everything else
(numbers, strings, lists, nested dicts) as JSON in the file's metadata, where each tensor stands
as {"$tensor": key}. Loading runs no code, unlike a pickle."""

import json

import numpy as np
import torch
from safetensors import safe_open
from safetensors.torch import save_file


def save(path, package):
    """package: a dict with a "format" string and any nesting of dicts, lists and tensors."""
    tensors = {}

    def flatten(obj, key):
        if torch.is_tensor(obj):
            if key in tensors:
                raise ValueError(f"two tensors at {key}")
            tensors[key] = obj.detach().cpu().contiguous().clone()
            return {"$tensor": key}
        if isinstance(obj, np.ndarray):
            return flatten(torch.from_numpy(obj), key)
        if isinstance(obj, dict):
            if not all(isinstance(k, str) for k in obj):
                raise TypeError(f"non-string keys at {key}")
            return {k: flatten(v, f"{key}.{k}" if key else k) for k, v in obj.items()}
        if isinstance(obj, (list, tuple)):
            return [flatten(v, f"{key}.{i}") for i, v in enumerate(obj)]
        if isinstance(obj, np.generic):
            return obj.item()
        if obj is None or isinstance(obj, (bool, int, float, str)):
            return obj
        raise TypeError(f"{type(obj).__name__} at {key}")

    tree = flatten(package, "")
    save_file(tensors, str(path), metadata={"format": str(package["format"]), "package": json.dumps(tree)})


def metadata(path):
    """The package without its tensors (each still {"$tensor": key}): cheap to read."""
    with safe_open(str(path), framework="pt") as f:
        return json.loads(f.metadata()["package"])


def load(path):
    with safe_open(str(path), framework="pt") as f:
        tree = json.loads(f.metadata()["package"])

        def build(obj):
            if isinstance(obj, dict):
                if set(obj) == {"$tensor"}:
                    return f.get_tensor(obj["$tensor"])
                return {k: build(v) for k, v in obj.items()}
            if isinstance(obj, list):
                return [build(v) for v in obj]
            return obj

        return build(tree)
