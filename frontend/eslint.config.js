import js from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";
import reactHooks from "eslint-plugin-react-hooks";
import unusedImports from "eslint-plugin-unused-imports";

// Flat config for eslint 10. The repository had no eslint.config.* at all,
// so `npm run lint` exited 2 on a configuration error before reading a
// single file, and ci.yml had to leave the lint step out entirely.
//
// The severities below are deliberate. Turning a full recommended set on
// over a codebase this size surfaces ~760 pre-existing violations, and a
// gate that fails on day one is a gate nobody can adopt - it would block
// every pull request for reasons unrelated to the change under review.
// So anything that is *accumulated debt* is a warning (visible, counted,
// fixable incrementally) and anything that indicates a genuine defect
// stays an error. Tighten these back to "error" as each backlog is
// cleared; the counts in the comments are the baseline to work down from.
export default tseslint.config(
  { ignores: ["dist/**", "coverage/**", "*.config.js", "*.config.ts"] },

  js.configs.recommended,
  tseslint.configs.recommended,

  {
    files: ["**/*.{ts,tsx}"],
    languageOptions: {
      ecmaVersion: 2022,
      sourceType: "module",
      globals: { ...globals.browser },
      parserOptions: { ecmaFeatures: { jsx: true } }
    },
    plugins: { "react-hooks": reactHooks, "unused-imports": unusedImports },
    rules: {
      // Breaking these produces a genuinely broken component, not untidy
      // code, and there are currently zero violations - so it is the one
      // rule that can be an error without grandfathering anything in.
      "react-hooks/rules-of-hooks": "error",

      // 35 violations. Each one is a judgement call about whether the
      // omitted dep is deliberate, and the codebase already annotates the
      // deliberate ones with disable comments.
      "react-hooks/exhaustive-deps": "warn",

      // 463 violations. This codebase models API payloads as `any` almost
      // everywhere (useState<any[]>, form records). Typing those properly
      // is real work, not a lint fix.
      "@typescript-eslint/no-explicit-any": "warn",

      // 167 and 119 violations. Both are safe to clear - the import one is
      // entirely `--fix`-able - but doing so touches ~100 files, which is
      // its own commit rather than a side effect of adding a config.
      "@typescript-eslint/no-unused-vars": [
        "warn",
        { argsIgnorePattern: "^_", varsIgnorePattern: "^_" }
      ],
      "unused-imports/no-unused-imports": "warn"
    }
  },

  {
    // Remaining core-recommended rules with small violation counts, kept
    // separate so each can be promoted independently: no-useless-assignment
    // (6), preserve-caught-error (3), no-empty-object-type (2),
    // no-useless-escape (1).
    files: ["**/*.{ts,tsx}"],
    rules: {
      "no-useless-assignment": "warn",
      "preserve-caught-error": "warn",
      "no-useless-escape": "warn",
      "@typescript-eslint/no-empty-object-type": "warn"
    }
  }
);
