import threading

from helpers import ATTACK, CharacterEncoder
from tokenizers import Tokenizer, models, pre_tokenizers
from transformers import PreTrainedTokenizerFast

from yaqs.embedding import PROFILE, QwenEncoder
from yaqs.ingest import import_book

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
