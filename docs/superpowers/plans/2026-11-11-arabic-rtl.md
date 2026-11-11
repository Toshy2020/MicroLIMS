# Arabic and RTL (F4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Per-user English/Arabic interface with full RTL layout, LTR-isolated identifiers and numbers, coded server messages, and English-only printed records.

**Architecture:** `i18next` + `react-i18next` with namespace JSON per module (`frontend/src/locales/{en,ar}/{module}.json`), a glossary (`frontend/src/locales/glossary.json`), MUI theme `direction` + an RTL emotion cache (`stylis-plugin-rtl`), an `<Ltr>` isolation component, and an ESLint rule (`i18next/no-literal-string`, configured for JSX text and visible props) with a ratchet file. Backend: `CodedException` support on the existing exception types and an `ApiError { code, params, message }` list in `ApiResponse`, with a ratchet test on uncoded throw sites.

**Tech Stack:** React 19, TypeScript, MUI 9, i18next, react-i18next, stylis-plugin-rtl, eslint-plugin-i18next, Vitest; .NET 10.

**Spec:** `docs/superpowers/specs/2026-11-11-arabic-rtl-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (no new `any`, `userEvent`, lint rules never weakened, WCAG 2.2 AA in both directions, design tokens). Package versions pinned exactly in `frontend/package.json`; check each package's licence (MIT expected) and that the chosen version is at least two weeks old.
- Always LTR and Latin digits: identifiers, codes, numbers, results, units, dates, times, user names, emails, scanner input — via `<Ltr>` (`<bdi dir="ltr">`) or `dir="ltr"` on inputs.
- `Intl.DateTimeFormat` with `ar-u-nu-latn` / `en-GB`, Gregorian, time zone shown as today through `formatLabDate` / `formatLabDateTime`. Numbers never pass through locale formatting that changes separators.
- Printed and exported output English only: PDF/Word/CSV builders on the server are untouched; the frontend print preview pages render with `lng="en"`.
- Signature dialog: English meaning statement (from the server) first, Arabic translation below, note "The English statement is recorded."
- Ratchets: `frontend/i18n-debt-baseline.json` (hard-coded strings per module) and `backend/MicroLIMS.Tests/Architecture/uncoded-messages-baseline.txt` (count) — may only go down.
- Glossary: every Arabic string containing a glossary English term must use the glossary Arabic term (unit test).

## Review Focus

1. A sample number inside an Arabic sentence renders reversed (`FP0107026` → mixed order) → `Identifiers_stay_ltr_inside_arabic_sentence` (Task 2).
2. A translation drops a placeholder (`{{count}}`) → `Placeholders_match_between_languages` (Task 1).
3. MUI components that position by `left` (drawers, popovers, tooltips) misplace in RTL → visual check list + `Drawer_opens_from_right_in_arabic` (Task 2).
4. An Arabic user prints a certificate and gets Arabic → `Print_pages_render_english_for_arabic_user` (Task 2).
5. Server returns a code with parameters the Arabic template does not use → `Server_message_templates_cover_all_params` (Task 4).

## File Structure

| File | Responsibility |
|---|---|
| `frontend/src/i18n/index.ts` | i18next setup, lazy namespaces |
| `frontend/src/i18n/LanguageProvider.tsx` | Profile language, `dir`/`lang` on `<html>`, theme direction, emotion cache |
| `frontend/src/components/Ltr.tsx` | Isolation component |
| `frontend/src/locales/{en,ar}/*.json`, `locales/glossary.json` | Texts |
| `frontend/eslint.config.js` (add rule), `frontend/scripts/i18n-debt.mjs`, `frontend/i18n-debt-baseline.json` | Ratchet |
| `backend/MicroLIMS.Shared/Exceptions/*` (modify), `Responses/ApiResponse.cs` (add `ErrorDetails`) | Coded messages |
| `backend/MicroLIMS.Shared/Messages/MessageCodes.cs` | Code constants |
| `frontend/src/locales/{en,ar}/serverMessages.json` | Server message templates |

---

### Task 1: Foundations

- [ ] **Step 1:** Failing tests: `LanguageProvider` sets `<html lang dir>` and theme direction from the profile (C6 state; browser language before sign-in); switching re-renders without reload; missing Arabic key falls back to English; `Placeholders_match_between_languages`; glossary consistency test; `i18n-debt.mjs` counts literals per module and fails on increase.
- [ ] **Step 2:** FAIL → add packages, implement → `npm run lint`, `npx tsc -b`, `npm run test`, `npm run debt:check`, `npm run build` PASS. Add `i18n:check` to CI next to `debt:check`.
- [ ] **Step 3:** Commit `feat(i18n): language provider, RTL theme, glossary and ratchet`.

### Task 2: Shared components and layout

**Files:** `components/Ltr.tsx`; `formatDate.ts` (locale-aware month names, Latin digits); `MainLayout`, sidebar (`menuConfig.ts` labels to keys), `PageHeader`, `RegisterTable`, `DataTable`, `FormDialog`, `ConfirmationDialog`, `SignatureDialog`, `EmptyState`, `PartialOutageBanner`, `AppBreadcrumbs`, search dialog; replace `left`/`right` styles with logical properties in shared components; direction-aware icons (`ChevronLeft/Right`, arrows) via a small `DirectionalIcon`; print preview routes forced to English.

- [ ] **Step 1:** Failing tests: `Identifiers_stay_ltr_inside_arabic_sentence`; `Drawer_opens_from_right_in_arabic`; table column order reversed and sort icons correct; date shows Arabic month with Latin digits; signature dialog English then Arabic with the note; `Print_pages_render_english_for_arabic_user`.
- [ ] **Step 2:** FAIL → implement → full frontend checks PASS. Commit `feat(i18n): shared components in Arabic and RTL`.

### Task 3: Modules (one commit per module, in this order)

My Work (C1) → testing workspace → receiving → review and approval → pathogen steps → EM / water / after cleaning → media and solutions → inventory and equipment → laboratory configuration → OOS / quality records → reports and KPIs → Document Control → administration.

For each module:
- [ ] Extract every visible string to `locales/en/<module>.json`; write `locales/ar/<module>.json` using the glossary; wrap identifiers and numbers in `<Ltr>`; replace physical left/right styles.
- [ ] Module tests: existing tests updated to query by translated text in English (default test language) plus one Arabic smoke test per page (direction, a key heading, an identifier isolated).
- [ ] `npm run i18n:check` shows the module at zero; lower the baseline; full frontend checks PASS. Commit `feat(i18n): <module> in Arabic`.

### Task 4: Server message codes

**Interfaces:** `BusinessRuleException(string code, string englishMessage, IReadOnlyDictionary<string, string>? parameters = null)` (existing constructor kept); same for `NotFoundException`, plus a coded variant for access refusals; exception middleware fills `ApiResponse.ErrorDetails: List<ApiError { Code, Params, Message }>` beside `Errors` (unchanged for compatibility). Codes `Module.Reason` in `MessageCodes`. Frontend `apiClient` error mapper: translate `ErrorDetails` by code via `serverMessages` namespace, fallback to `Message`. Ratchet test: counts `throw new (BusinessRule|NotFound)Exception(` without a code argument in Application and API; must not exceed the baseline.

- [ ] **Step 1:** Failing tests: coded exception serialised with code, params and English; uncoded unchanged; frontend shows Arabic for a coded message and English for uncoded; `Server_message_templates_cover_all_params` (every code in `MessageCodes` has en and ar templates using exactly its parameter names); ratchet.
- [ ] **Step 2:** FAIL → implement → PASS. Then convert modules in the Task 3 order (one commit each), lowering the baseline each time. Commit `feat(i18n): coded server messages for <module>`.

### Task 5: Screenshots and documents

- [ ] **Step 1:** Extend A3's screenshot tool with a language option; capture the main screens in Arabic; inspect for mirroring and truncation; fix.
- [ ] **Step 2:** Deliver `frontend/src/locales/glossary.json` as the terminology reference; A1 entries if present; manual note on language choice and English-only printed records.
- [ ] **Step 3:** Full verification (backend and frontend per `CLAUDE.md`); report the remaining counts of the two ratchets.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Task 3's module list also includes every module added by packages built before F4 (identification, emAnalytics, photos, retention, report builder, review exceptions, quality, stability, competency, OOS investigation).
2. From the moment F4 Task 4 is merged, every new `BusinessRuleException` / `NotFoundException` in any package carries a message code, and every new screen uses translation keys (the ratchets enforce it).
