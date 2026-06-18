import { LAYOUT } from "./config.js";

const OPENAI_API_URL = "https://api.openai.com/v1/responses";
const STORAGE_KEY = "zooreader.openaiApiKey";

/**
 * @param {unknown} value
 * @returns {string}
 */
function normalizeSpeech(value) {
  return String(value || "")
    .replace(/\s+/g, " ")
    .trim()
    .split(" ")
    .slice(0, LAYOUT.SPEECH_MAX_WORDS)
    .join(" ");
}

/**
 * @param {unknown} data
 * @returns {string}
 */
function extractResponseText(data) {
  if (typeof data?.output_text === "string") {
    return normalizeSpeech(data.output_text);
  }

  const output = Array.isArray(data?.output) ? data.output : [];
  for (const item of output) {
    const content = Array.isArray(item?.content) ? item.content : [];
    for (const part of content) {
      if (typeof part?.text === "string") {
        return normalizeSpeech(part.text);
      }
    }
  }

  return "";
}

/**
 * @param {string} apiKey
 * @param {string} pageText
 * @returns {Promise<string>}
 */
async function requestHeroLine(apiKey, pageText) {
  const response = await fetch(OPENAI_API_URL, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      model: LAYOUT.OPENAI_MODEL,
      max_output_tokens: 60,
      input: [
        {
          role: "system",
          content:
            "You write short original action hero quips for a PDF reader game. " +
            "Do not quote or imitate existing characters directly. " +
            "Return one punchy line, at most 20 words.",
        },
        {
          role: "user",
          content:
            "Write a short battle-hero comment inspired by this page:\n\n" +
            pageText.slice(0, LAYOUT.SPEECH_PAGE_TEXT_LIMIT),
        },
      ],
    }),
  });

  if (!response.ok) {
    const details = await response.text();
    throw new Error(`OpenAI request failed: ${response.status} ${details}`);
  }

  return extractResponseText(await response.json());
}

/**
 * Keeps at most one pending or visible hero line.
 */
export class HeroSpeech {
  /**
   * @param {HTMLInputElement} keyInput
   */
  constructor(keyInput) {
    this.keyInput = keyInput;
    this.visible = null;
    this.inFlight = false;
    this.requestedPages = new Set();

    this.keyInput.value = sessionStorage.getItem(STORAGE_KEY) || "";
    this.keyInput.addEventListener("change", () => {
      sessionStorage.setItem(STORAGE_KEY, this.keyInput.value.trim());
    });
  }

  /**
   * @param {number} pageNum
   * @param {string} pageText
   */
  maybeRequest(pageNum, pageText) {
    if (this.inFlight || this.visible) return;
    if (this.requestedPages.has(pageNum)) return;
    if (!pageText.trim()) return;
    if (Math.random() >= LAYOUT.SPEECH_REQUEST_CHANCE) return;

    const apiKey = this.keyInput.value.trim();
    if (!apiKey) return;

    this.requestedPages.add(pageNum);
    this.inFlight = true;

    requestHeroLine(apiKey, pageText)
      .then((text) => {
        if (!text) return;
        this.visible = {
          text,
          expiresAt: performance.now() + LAYOUT.SPEECH_VISIBLE_MS,
        };
      })
      .catch((err) => {
        console.warn(err);
      })
      .finally(() => {
        this.inFlight = false;
      });
  }

  /**
   * @param {number} now
   */
  update(now) {
    if (this.visible && now >= this.visible.expiresAt) {
      this.visible = null;
    }
  }

  /** @returns {string | null} */
  currentText() {
    return this.visible?.text || null;
  }
}
