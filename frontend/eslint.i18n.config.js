import tseslint from "typescript-eslint";
import i18next from "eslint-plugin-i18next";

// Used only by scripts/i18n-debt.mjs (npm run i18n:check / i18n:update).
//
// It finds hard-coded, user-visible English in screen files so the count per
// module can be pinned in i18n-debt-baseline.json and only ever go down. It
// is deliberately NOT part of eslint.config.js: about 1,800 fragments exist
// today, and `npm run lint` must not fail on them. As a module reaches zero
// it is protected here by the ratchet; new screens use translation keys.
//
// What counts: text written between JSX tags, and string values of the props
// that reach the screen as words (see VISIBLE_PROPS). Styling, routing, test
// ids, event handlers and comparisons are not counted.
const VISIBLE_PROPS = [
  "label",
  "placeholder",
  "title",
  "aria-label",
  "aria-description",
  "aria-placeholder",
  "alt",
  "helperText",
  "subheader",
  "primary",
  "secondary",
  "tooltip",
  "message",
  "description",
  "heading",
  "emptyMessage"
];

export default [
  {
    files: ["src/**/*.tsx"],
    ignores: ["src/**/*.test.tsx"],
    languageOptions: {
      parser: tseslint.parser,
      parserOptions: { ecmaFeatures: { jsx: true } }
    },
    plugins: { i18next },
    rules: {
      "i18next/no-literal-string": [
        "error",
        {
          mode: "jsx-only",
          "should-validate-template": true,
          // Only the visible props are looked at: everything else on an element
          // matches the catch-all exclude.
          "jsx-attributes": { include: VISIBLE_PROPS, exclude: [".*"] },
          message: "Hard-coded text: use a translation key"
        }
      ]
    }
  }
];
