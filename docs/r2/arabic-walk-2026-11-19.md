# Arabic screenshot walk (F4 Task 5, 2026-11-19)

Build: `r2/walk3-snapshot` at ba117dcd (before the terminology decisions a7611741). Release API on a fresh disposable database `microlims_walk` (127.0.0.1:54329), Vite dev server, headless Brave at 1366×768 and 768×1024. The browser locale was ar-EG; the language menu was also switched to Arabic. About 35 screenshots kept outside the repository (`I:\walk-r2\shots`).

Covered: sign-in, My Work, tour, dashboard, Receive register, New Sample (both steps and the grid), sample summary, qualitative panel, signature dialog (opened, not signed), monitoring plan, media, solution preparation, materials, equipment, document library, reports (overview and control chart), help, a filter drawer, a row menu, the account menu, users, roles, audit search, the printed sample report, the in-page printed materials and equipment tables, and three tablet screens.

Not covered: microbiology screens (no microbiology tests configured), an approved certificate of analysis, a real print to PDF (print media was emulated), the tablet navigation drawer.

What worked: mirroring of the sidebar, breadcrumbs, chevrons and dialog arrows; the dashboard, monitoring plan, media, solution preparation, materials, document library, roles, signature dialog, filter popover, account menu and the tablet media and materials screens; the printed sample report (English, left to right, English dates). No missing-key warnings, no `{{placeholder}}` or raw key text anywhere.

## Findings and triage

| # | Finding | Severity | Triage |
|---|---|---|---|
| W1 | In-page printed tables (materials, equipment) print right to left with Arabic month names and the Arabic MFA banner; printed records must be English | Major | Fix (lane A: PrintableTable English/LTR, English print columns, print only `.print-only` content) |
| W2 | Dates shown inside a left-to-right isolate read in the wrong word order ("نوفمبر 2026، 08:06 غرينتش+2 19") | Major | Fix centrally (lane A: Arabic date text wrapped in a right-to-left isolate) |
| W3 | Reports page title, tabs and KPI labels English; KPI digits Arabic-Indic; "of 2 approved 0 0%" scrambled | Major | Fix (lane A: Reports shell and KPI cards; Latin digits for all counts) |
| W4 | Sign-in page overflows horizontally (also in English); in Arabic the hero panel is off screen | Major | Fix (lane B) |
| W5 | Product tour popover clipped at the right edge in Arabic | Major | Fix (lane B) |
| W6 | Sample summary: raw category "FinishedProduct", "Print label" English, colon misplaced in "Appearance :APPEARANCE" | Minor | Fix (lane B) |
| W7 | My Work: group title and row titles ("Perform Appearance") English; favourite star label English | Minor | Fix (lane B) |
| W8 | Receive register row meta English and cryptic abbreviations | Minor | Fix (lane B) |
| W9 | New Sample step 2 "From your last entry", "Clear", "Choose date" English; grid column clipped | Minor | Fix (lane B) |
| W10 | Qualitative panel date field in US format without time zone | Minor | Fix (lane B) |
| W11 | Help chapter list English; numbering ".1 Introduction" | Minor | Fix (lane B) |
| W12 | Users tab strip clipped, scroll arrow reversed; equipment status chip clipped; tablet header label cut; letter-spacing on Arabic text; stray "·" before date headers | Minor | Fix (lane B) |
| W13 | Audit diff field names ("Last Login At") English | Minor | As designed for R2: field names come from the audit record (server data) |
| W14 | Console: SignalR negotiation errors on the dev hub; React DOM-nesting warnings (button in button in the monitoring plan due panel; div in p in a table) | Minor | DOM nesting to fix with lane B if small; hub errors are the dev setup |
