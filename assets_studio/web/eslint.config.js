import js from "@eslint/js";
import tseslint from "typescript-eslint";

export default tseslint.config(
  { ignores: ["dist", "src/api/schema.ts"] },
  js.configs.recommended,
  ...tseslint.configs.strictTypeChecked,
  {
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      "@typescript-eslint/restrict-template-expressions": [
        "error",
        { allowNumber: true },
      ],
      // `onClick={() => setX(y)}` is the idiomatic handler form.
      "@typescript-eslint/no-confusing-void-expression": [
        "error",
        { ignoreArrowShorthand: true },
      ],
    },
  },
  {
    files: ["e2e/**/*.mjs", "eslint.config.js"],
    ...tseslint.configs.disableTypeChecked,
  },
  {
    files: ["e2e/**/*.mjs"],
    // Node globals, plus browser globals inside page.evaluate() callbacks.
    languageOptions: {
      globals: {
        process: "readonly",
        console: "readonly",
        fetch: "readonly",
        document: "readonly",
        window: "readonly",
      },
    },
  },
);
