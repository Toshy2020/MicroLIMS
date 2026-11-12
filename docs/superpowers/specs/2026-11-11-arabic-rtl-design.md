# F4 — Arabic and Right-to-Left Interface: Design Specification

**Roadmap package:** F4 (`improvement-roadmap.md`)
**Status:** Draft for user review
**Date:** 2026-11-11

## 1. Purpose

Let every user work in Arabic or English, with a correct right-to-left layout in Arabic, while identifiers, numbers, printed certificates and signed records stay exactly as validated in English.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Coverage | Every screen; each user chooses English or Arabic |
| 2 | Printed records (D-9) | Certificates, reports and signature manifestations stay English only |
| 3 | User data | Stored and shown as typed; master data and comments are not translated |
| 4 | Translation | Done entirely in this package (no separate translation step at the site) |

Roadmap decision D-9 is closed by 2.

## 3. Out of scope

- Arabic printed certificates, reports or exports.
- Translating master data, comments or documents in Document Control.
- Arabic-Indic digits. Numbers stay Latin digits everywhere.
- Hijri calendar. Dates use the Gregorian calendar in both languages.
- In-app help chapters in Arabic (the guide stays English; C6's panel shows it as is).

## 4. Current state (from code and counts, not run)

- About 525 screen files with about 1,800 visible English text fragments written directly in them. No translation library, no right-to-left support.
- Dates are formatted through shared helpers in most places, but about 100 files format numbers or dates directly.
- The server sends error messages as English sentences: about 2,170 business-rule messages, 320 not-found messages and 120 access messages. There are no message codes.
- The theme is built in one place, which makes direction switching feasible.

## 5. Design

### 5.1 Language choice

- Each user chooses English or Arabic in their profile; the choice is stored on the server (C6's per-user interface state) and applied on any computer. The sign-in page follows the browser language until the user signs in.
- Switching language takes effect at once without signing out.

### 5.2 Interface text

- All interface text moves into translation files, one per module, in English and Arabic. Screens refer to keys.
- A lint rule blocks new hard-coded visible text in screen files; existing ones are removed module by module, tracked by a count that can only go down (like the existing test-debt ratchet).
- The Arabic text is produced in this package. Terms used in GMP and microbiology follow a **glossary file** (English term → Arabic term) so each term is translated the same way everywhere. Codes, abbreviations and units (TAMC, CFU, pH, mg, °C, test codes) are not translated.
- A key missing in Arabic shows the English text; a test lists missing keys and fails the build when a module claimed as complete has any.

### 5.3 Layout

- In Arabic the whole layout mirrors: navigation on the right, reading order right to left, icons that show direction (arrows, chevrons) flipped, others not.
- Styles use start/end instead of left/right so both directions work from the same code.
- **Always left to right, in Latin digits, even inside Arabic text:** sample and batch numbers, test codes, document codes, numbers and results, units, dates and times, user names typed in Latin letters, email addresses, and anything a scanner or instrument produces. These are wrapped so they never reorder.
- Tables keep the same column meaning; in Arabic, columns run right to left.
- Charts keep their axes left to right (time grows to the right) with Arabic labels.

### 5.4 Dates and numbers

- Dates use the Gregorian calendar with Arabic month names in Arabic (for example "11 نوفمبر 2026"), Latin digits, and the time zone shown as today.
- Decimal separator is always the point; no thousands grouping changes; results appear exactly as the server sends them.

### 5.5 Server messages

- The server's error and refusal messages get **message codes with parameters**, alongside the English sentence. The screen shows the Arabic version of a code when the user works in Arabic, and the English sentence otherwise.
- Messages are converted module by module; a count of messages without a code can only go down. Until a message has a code, it is shown in English inside the Arabic screen.
- Messages that explain why an action is unavailable (C8) follow the same mechanism.

### 5.6 Electronic signatures

- In the signature dialog the meaning statement is shown in **English first, with the Arabic translation below**, and the dialog states that the English statement is the one recorded. What is stored and printed is the English meaning, unchanged.

### 5.7 Printed and exported output

- Certificates, sample reports, audit exports, CSV exports and PDFs stay English, whatever the user's language (D-9). The print dialog says so in Arabic.

### 5.8 Accessibility

- The page declares its language and direction so screen readers switch voice; mixed-direction text is marked so it is read correctly.
- Contrast, focus and keyboard order are checked in both directions.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Missing Arabic key | English text shown; listed by the test |
| Server message without code | English sentence shown |
| Unknown language in profile | English |

## 7. Testing

- **Unit:** every key present in both languages for completed modules; glossary terms used consistently (a check that the Arabic for each glossary term is the glossary's); placeholders in each translation match the English ones; no hard-coded text in converted modules (lint).
- **Frontend (`userEvent`):** switching language changes direction and text without reload; identifiers stay left to right inside Arabic sentences; dates and numbers in Latin digits; signature dialog shows English then Arabic; server message by code in Arabic, fallback in English.
- **Backend:** message codes and parameters returned with the English sentence; ratchet counting uncoded messages.
- **Visual:** screenshots of the main screens in Arabic (A3's screenshot tool) reviewed for mirroring problems.
- **Manual:** a full analyst day in Arabic on the bench tablet.

## 8. Delivery stages

1. Library, language choice, direction switching, glossary, lint and ratchets.
2. Shared components and layout (navigation, tables, dialogs, signature dialog).
3. Modules in order of use: My Work, testing workspace, receiving, review and approval, then the rest.
4. Server message codes, same order.
5. Screenshots and documents.

## 9. Note on translation quality

The Arabic is produced in this package without a review step at the site, as decided. Terminology is kept consistent through the glossary, which is delivered as a separate file so it can be checked or changed in one place if anyone at the site later wants to.
