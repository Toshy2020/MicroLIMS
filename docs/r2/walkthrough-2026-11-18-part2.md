# R2 live walkthrough, part 2 (2026-11-18)

Frozen app: worktree `r2/walk2-snapshot` at 7b7cb22b, Release build, disposable database `microlims_walk`, headless Brave driven by playwright-core scripts. Users analyst1–3, reviewer1, sectionhead1, admin.

## Covered

- **C5 live updates:**
  - The hub connects; the CORS fix holds.
  - A sample submitted for review appears in the reviewer's My Work and bell within about 1 s.
  - With the hub down, the paused chip and polling take over; it recovers after a restart.
  - The working calendar and an escalation rule fired at the threshold.
  - The record-changed banner shows on an item and on a sample, keeps unsaved input, and Reload asks first.
  - Not covered: e-mail; no mail server was used.
- **C4 data entry:**
  - Receive grid paste with a bad row first: blocked, nothing entered.
  - Corrected paste, Ctrl+D, draft note, Save All.
  - Keyboard entry with inline required-field errors.
  - 20 water points pasted, with unknown-system rows blocked.
  - Not covered: EM paste and incubation with a pre-filled lot (no EM or media configuration in this database); timing against the old dialog.
- **C7 configuration:**
  - Item from an existing one; test from an existing one, with preview.
  - Specification edit: a reason is asked with open samples and not without.
  - Unused specification deleted; used one retired and reinstated.
  - Keyboard pass on a wizard.
- **C2 bench:**
  - Touch contexts at 1280×800, 1024×768 and 768×1024.
  - Offline draft: kept, then restored after reconnecting, then saved.
  - Not covered: EM/water grids and pathogen screens (no data); offline reload (dev server has no service worker); the audit source context (not visible through the API); a real tablet.
- **Rechecks:**
  - No hook-order errors anywhere (part 1 finding 1 was the hot reload).
  - CSV export works for a user with Data.Export.
  - A shared view opened by a single-section analyst shows only that section's views and rows.
  - Qualitative rows start unchosen.
  - Reassignment of a submitted test is refused, with the reason in the UI and the API.
  - Return to Analyst is refused for the tester and allowed for another reviewer.
  - The language menu switches and persists.

## Findings and triage

| # | Finding | Triage |
|---|---|---|
| D1 | Sidebar My Work badge does not update on the live signal (refetches only on focus) | Fix |
| D2 | Bell badge shows 0 for 2–5 s on page load | Low |
| D3 | Notification list times without a zone | Fix (small) |
| D4 | Paused chip appears after ~48 s; polling every ~60 s | As designed; no figure in the plan |
| D5 | "1 working hours" in the escalation text | Fix |
| D6 | Receive grid: a prefilled "Sampled by" cell loses the first typed character and keeps focus on Tab, so later keyboard values land one column off | **Fix first** (data lands in the wrong field) |
| D7 | Paste fills by visible column position; pasted rows get no item, stage or lab | As designed (the screen says choice columns are left to the user) |
| D8 | Configuration wizard opens with focus on the container; typing triggers global shortcuts | Fix |
| D9 | Bench mode: many targets under 48 px (breadcrumb links, take/release links, sort buttons, header search, rows-per-page, toggles, list rows, some dialog inputs) | Fix in the bench theme |
| D10 | Offline draft restored the observation and choice but not the comment | Check: the signature comment is excluded from drafts by design (plan Review Focus 1); confirm this is that field |
| D11 | Number keypad shown in the qualitative dialog (no numeric fields) and covers the form; not shown for the pH replicate field | Fix |
| D12 | CSV "Due" is a raw epoch | Fix |
| D13 | CSV "Assigned To", "Tests", "Next Action" empty | Fix |
| D14 | CSV "Exported at …Z (Africa/Cairo)" mixes UTC and a local zone name | Fix |
| D15 | CSV item rows have no sample reference number; status is the raw enum | Fix |
| D16 | Assign dialog offers Retry on a permanent refusal | Fix (small) |
| D17 | Arabic: account menu items, header labels and most page bodies still English | F4 Task 3 stream (Header and account menu next) |
