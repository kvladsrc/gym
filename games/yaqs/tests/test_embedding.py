import json
import threading

import pytest
from helpers import ATTACK, CharacterEncoder
from tokenizers import Tokenizer, models, pre_tokenizers
from transformers import PreTrainedTokenizerFast

from yaqs.embedding import PROFILE, QwenEncoder
from yaqs.ingest import import_book


def test_shared_checkpoint_accepts_the_two_pinned_model_shards(tmp_path, monkeypatch):
    import yaqs.embedding as embedding

    checkpoint = tmp_path / "shared"
    checkpoint.mkdir()
    for name in embedding.MODEL_FILES:
        (checkpoint / name).write_bytes(b"fixture")
    config = tmp_path / "embedding-model.json"
    config.write_text(
        json.dumps(
            {"model": embedding.REPO, "revision": embedding.REVISION, "path": str(checkpoint)}
        )
    )
    monkeypatch.delenv("YAQS_MODEL_PATH", raising=False)
    monkeypatch.setattr(embedding, "MODEL_CONFIG", config)
    status = embedding.cache_status()
    assert status["ready"]
    assert status["shared_checkpoint"] == str(checkpoint)
    (checkpoint / "model-00002-of-00002.safetensors").unlink()
    assert embedding.cache_status()["missing"] == ["model-00002-of-00002.safetensors"]


def test_shared_checkpoint_rejects_a_different_revision(tmp_path, monkeypatch):
    import yaqs.embedding as embedding

    config = tmp_path / "embedding-model.json"
    config.write_text(
        json.dumps({"model": embedding.REPO, "revision": "another-revision", "path": str(tmp_path)})
    )
    monkeypatch.delenv("YAQS_MODEL_PATH", raising=False)
    monkeypatch.setattr(embedding, "MODEL_CONFIG", config)
    with pytest.raises(ValueError, match="differs from the pinned YAQS model"):
        embedding.cache_status()


def test_cuda_model_keeps_vocabulary_on_cpu(monkeypatch):
    from types import SimpleNamespace

    import torch
    import transformers

    import yaqs.embedding as embedding

    moves = []

    class Part:
        def __init__(self, name):
            self.name = name

        def to(self, device):
            moves.append((self.name, device))
            return self

    class Model:
        embed_tokens = Part("embedding")
        layers = Part("layers")
        norm = Part("norm")
        rotary_emb = Part("rotary")
        config = SimpleNamespace(use_cache=True)

        def eval(self):
            return self

    monkeypatch.setattr(embedding, "require_cache", lambda **kwargs: None)
    monkeypatch.setattr(embedding, "_model_directory", lambda: None)
    monkeypatch.setattr(torch.cuda, "is_available", lambda: True)
    monkeypatch.setattr(transformers.AutoModel, "from_pretrained", lambda *args, **kwargs: Model())
    encoder = embedding.QwenEncoder("cuda")
    encoder._load()
    assert moves == [("layers", "cuda"), ("norm", "cuda"), ("rotary", "cuda")]
    assert encoder._model.config.use_cache is False


def test_cuda_microbatches_keep_all_rows_within_the_token_budget(monkeypatch):
    from types import SimpleNamespace

    import numpy as np
    import torch

    import yaqs.embedding as embedding

    shapes = []
    original_to = torch.Tensor.to

    def simulated_to(tensor, *args, **kwargs):
        if args and args[0] == "cuda":
            return tensor
        return original_to(tensor, *args, **kwargs)

    class Model:
        def get_input_embeddings(self):
            return lambda ids: torch.ones(*ids.shape, embedding.DIMENSIONS)

        def __call__(self, *, inputs_embeds, **kwargs):
            shapes.append(inputs_embeds.shape[:2])
            return SimpleNamespace(last_hidden_state=inputs_embeds)

    monkeypatch.setattr(embedding, "MAX_CUDA_TOKENS", 8)
    monkeypatch.setattr(torch.Tensor, "to", simulated_to)
    encoder = embedding.QwenEncoder("cuda")
    encoder._tokenizer = fast_tokenizer()
    encoder._model = Model()
    result = encoder.encode(["w0 w1 w2 w3"] * 3)
    assert result.shape == (3, embedding.DIMENSIONS)
    assert sum(shape[0] for shape in shapes) == 3
    assert max(shape[0] * shape[1] for shape in shapes) <= 8
    np.testing.assert_allclose(np.linalg.norm(result, axis=1), 1, atol=1e-5)


WORDS = [f"w{i}" for i in range(50)]
TEXT = " ".join(WORDS * 200)


def fast_tokenizer():
    vocab = {"[UNK]": 0, "[PAD]": 1, **{word: i for i, word in enumerate(WORDS, 2)}}
    backend = Tokenizer(models.WordLevel(vocab, unk_token="[UNK]"))
    backend.pre_tokenizer = pre_tokenizers.Whitespace()
    return PreTrainedTokenizerFast(tokenizer_object=backend, pad_token="[PAD]", padding_side="left")


def test_padded_queries_and_token_counts_from_mcp_threads_do_not_collide():
    """Unserialized, this raises "RuntimeError: Already borrowed" within milliseconds."""
    encoder = QwenEncoder()
    encoder._tokenizer = fast_tokenizer()
    errors: list[Exception] = []
    stop = threading.Event()

    def repeat(call):
        while not stop.is_set():
            try:
                call()
            except Exception as error:
                errors.append(error)
                stop.set()

    def query():
        encoder.tokenizer([TEXT, TEXT[:40]], padding=True, truncation=False)

    def count():
        encoder.tokenizer(TEXT, add_special_tokens=False, return_offsets_mapping=True)

    threads = [threading.Thread(target=repeat, args=(call,)) for call in (query, count, count)]
    for thread in threads:
        thread.start()
    stop.wait(1)
    stop.set()
    for thread in threads:
        thread.join()
    assert not errors


def test_chunks_are_embedded_with_the_recorded_document_template(tmp_path):
    seen = []

    class Recorder(CharacterEncoder):
        def encode(self, texts, **kwargs):
            seen.extend(texts)
            return super().encode(texts, **kwargs)

    source = tmp_path / "book.txt"
    source.write_text(f"# Combat\n\n{ATTACK}")
    import_book(tmp_path / "library.sqlite3", source, Recorder(), title="Game reference")
    assert PROFILE["document_template"] == "{title}\n{chapter}\n\n{text}"
    # The heading line stays in the section text; the template adds it as the chapter too.
    assert seen == [f"Game reference\nCombat\n\n# Combat\n\n{ATTACK}"]


def test_idle_model_is_unloaded_and_loaded_again_by_the_next_search(monkeypatch, capsys):
    from types import SimpleNamespace

    import torch

    import yaqs.embedding as embedding

    class Model:
        def __call__(self, *, input_ids, **kwargs):
            hidden = torch.ones(*input_ids.shape, embedding.DIMENSIONS)
            return SimpleNamespace(last_hidden_state=hidden)

    encoder = embedding.QwenEncoder(idle_s=300)
    encoder._tokenizer = fast_tokenizer()
    loads = []

    def load():
        if encoder._model is None:
            loads.append(Model())
            encoder._model = loads[-1]

    monkeypatch.setattr(encoder, "_load", load)
    encoder.encode(["w0 w1"], query=True)
    assert len(loads) == 1
    assert not encoder.unload_if_idle()  # just used
    encoder._used -= 301
    assert encoder.unload_if_idle()
    assert encoder._model is None
    event = json.loads(capsys.readouterr().err.splitlines()[-1])
    assert event == {"model": "unloaded", "idle_seconds": 301}
    encoder.encode(["w0 w1"], query=True)
    assert len(loads) == 2


def test_idle_unload_runs_by_itself_and_only_for_servers():
    import time

    from yaqs.embedding import QwenEncoder

    server = QwenEncoder(idle_s=0.05)
    command = QwenEncoder()
    server._model = command._model = object()
    deadline = time.monotonic() + 5
    while server._model is not None and time.monotonic() < deadline:
        time.sleep(0.02)
    assert server._model is None
    command._used -= 10**6
    assert not command.unload_if_idle()
    assert command._model is not None
