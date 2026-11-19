# Interface Language User Guide

> **Draft — not approved.** Written from code on `2026-11-19`. No screenshots were made, and the application was not run to prepare it. Your laboratory's approved SOPs take precedence over this guide.

| Field | Value |
|---|---|
| Title | Interface Language User Guide |
| Version | 0.1 |
| Status | Draft |
| Author | AI coding agent, draft from code |
| Reviewer | |
| Approver | |
| Effective date | |

## 1. Interface language (English / العربية)

- **Choose a language:** open your account menu (the profile menu in the top bar) and choose **Language**, then English or Arabic (العربية).
- **It is saved for you:** your choice is saved to your user account. The screen switches at once, without signing out, and the same language is used the next time you sign in on any computer.
- **Before sign-in:** the sign-in screen uses the first language your browser prefers, if MicroLIMS has it; otherwise English. A language you choose before signing in lasts for that browser tab only and is not saved to an account.
- **Arabic layout:** in Arabic the screen reads from right to left. Menus, tables and forms follow that direction.
- **Left to right in Arabic:** identifiers, codes (sample, batch, order and equipment codes), numbers, units, dates, times, user names and anything you type stay left to right and use Latin digits (0–9). Dates and times still show their time zone, as they do in English.
- **Printed and exported records are always in English:** the certificate of analysis, the sample report, the media and cryovial reports, the equipment history, and the CSV and PDF exports are in English whatever language you use on screen. The English record is the regulated record.
- **Electronic signatures:** the recorded meaning of a signature is the English statement. In Arabic, the signature dialog shows the English statement first, then an Arabic translation under it, with the note "The English statement is recorded." Read the translation to understand the statement; the English text is what is signed and stored.
- **Messages from the server:** when the server sends a coded message (for example a refused save or a missing field), the message appears in your language. Some messages, mainly those from configuration and administration screens, still appear in English.
- **Laboratory terms:** the Arabic terms follow the laboratory glossary (`frontend/src/locales/glossary.json`). If a term looks wrong for your laboratory, tell your Section Head; the glossary is not changed on screen.

## 2. What this draft does not cover

- Arabic texts on every screen have not all been checked by a native-speaking reviewer for this draft. Some screens may still show English.
- The right-to-left layout was not checked on a real tablet in bench mode for this draft.
- Your laboratory's rules on which language to use for training, SOPs and printed labels are set in the approved SOP, not in this guide.
