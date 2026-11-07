"""Small stand-ins for the Qwen tokenizer and encoder, so tests need no model download."""

import numpy as np

from yaqs.embedding import DIMENSIONS

# Pages need MIN_CHUNK_TOKENS characters to be indexed with the character tokenizer.
ATTACK = "Enemy attack anticipation tells the player what comes next."
ECONOMY = "Currency sources and sinks keep the economy in balance."


class CharacterTokenizer:
    """One token per character, answered in the shape of a Hugging Face fast tokenizer."""

    def __call__(self, text, **kwargs):
        return {
            "input_ids": list(range(len(text))),
            "offset_mapping": [(i, i + 1) for i in range(len(text))],
        }


class CharacterEncoder:
    """Character tokenizer and the same unit vector for every text."""

    tokenizer = CharacterTokenizer()

    def encode(self, texts, **kwargs):
        vectors = np.zeros((len(texts), DIMENSIONS), dtype=np.float32)
        vectors[:, 0] = 1
        return vectors
