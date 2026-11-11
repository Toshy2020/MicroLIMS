# C4 — Faster Data Entry: Design Specification

**Roadmap package:** C4 (`improvement-roadmap.md`)
**Status:** Draft for user review
**Date:** 2026-11-11
**Builds on:** C2 (device drafts, `NumericEntryField`).

## 1. Purpose

Cut the typing and clicking needed to register samples, enter results and maintain configuration, without weakening any check: every value still passes the same server validation, and nothing is recorded that the user did not confirm.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Drafts | Device drafts (from C2) are enough; no drafts stored on the server |
| 2 | Receiving several samples | All or nothing: every row is registered together or none is |
| 3 | "My last choice" defaults | Equipment; media lot / solution lot; sampled by and cause of testing at receiving |
| 4 | Paste from a spreadsheet | Sample receiving grid; EM and water location grids; and the configuration areas, for bulk entry |

The roadmap's "server-side drafts" item is dropped by decision 1.

## 3. Out of scope

- Server-stored drafts.
- Importing a file (CSV or Excel upload). Paste covers the same need with a preview; the existing master-data workbook is not changed.
- Pasting microbiology colony counts anywhere except the EM and water location grids.
- Voice entry; instrument data capture (package E1).
- New shortcuts for review, approval or signing.

## 4. Current state (from code, not run)

- **Receiving:** `NewSampleDialog` + `MultiSampleEntryGrid` (859 lines). Saving loops over the rows and sends one request per row; a failure in the middle leaves earlier rows registered. Adding a row copies three fields from the row above. No Enter or arrow-key handling.
- **Validation:** done by hand per form. The server returns a list of messages with no field names (`ApiResponse.Errors` is a list of strings), so a server error cannot be shown next to its field.
- **Keyboard:** `UnitEntryGrid` (physicochemical) supports Enter / Up / Down and spreadsheet paste. Other grids have none. `FormDialog` submits on Enter. Only one dialog sets initial focus.
- **Pickers:** preloaded lists filtered in the browser; none remembers a previous choice. Count entry starts with empty media and incubator fields.
- **Paste:** only `UnitEntryGrid`.
- **Result preview:** the count dialog computes "count × dilution" in the browser (`TestWorkflowDialog.tsx` L248–257). `CLAUDE.md` Principle 2 says such calculations belong to the backend only.

## 5. Design

### 5.1 Keyboard movement in grids

One shared hook gives every multi-row entry grid the same keys:

| Key | Action |
|---|---|
| Enter, Down | Same column, next row |
| Up | Same column, previous row |
| Left / Right | Previous / next cell when the caret is at the edge of the text |
| Tab / Shift+Tab | Next / previous cell in reading order (browser default, kept) |
| Ctrl+D | Copy the value from the cell above |
| Ctrl+Enter | Save the form |
| Escape | Cancel edit; a second Escape asks before closing a changed form |

Applied to: receiving grid, EM / water / pathogen location grids, the observation matrix, `UnitEntryGrid` (gains Left/Right, Ctrl+D), the HPLC replicate table, and the configuration grids in 5.6. Inside a grid, Enter moves down and does not submit. Each dialog puts focus in its first empty required field when it opens. A "Keyboard shortcuts" entry in the help menu lists the keys.

### 5.2 Receiving several samples together

- New endpoint `POST /api/samples/receive-batch` taking a category and up to 100 rows, with the same fields each category's single endpoint takes today.
- The server validates **every** row first with the existing rules. If any row fails, nothing is saved and the response names each failing row and field.
- If all pass, all samples are created in one transaction, with the normal audit entries, and the response returns the new sample ids and numbers in row order (B1 uses these to offer label printing).
- The single-sample endpoints stay for other callers.
- Supplier certificate files attached to rows are uploaded after the batch succeeds, one per sample; a failed upload is reported per row and can be retried from the sample without re-registering it.
- The dialog shows one progress state and one result. On failure the grid stays open with errors on the cells.

### 5.3 Errors next to the field

- The server's validation response gains an optional list of field errors (field name, row number when it applies, message), in addition to today's message list. Existing clients are unaffected.
- A shared helper puts each message under its control, linked for screen readers, and moves focus to the first one. A summary line states how many problems there are.
- Format problems the browser can see (required, whole number, date order) are shown as the user leaves the field. Everything else is decided by the server.
- Rolled out to the forms this package touches: receiving, count entry, location grids, media and solution preparation, and the configuration grids. Other forms keep working as today.

### 5.4 "My last choice"

- New endpoint `GET /api/entry-defaults` returns, for a given step, what **this user** last chose and whether it is still valid now.
- Covered: incubator, balance and autoclave for the same step and equipment type; media lot for the **same medium**; solution lot for the same solution; "sampled by" and "cause of testing" at receiving for the same category.
- The server offers a value only when it passes the same check the save will apply (equipment in valid calibration / qualification status; lot released and within expiry; value active). Otherwise the field is empty.
- No new table: the value is read from the user's own latest records.
- A pre-filled value is marked "From your last entry" and the field is outlined until the user confirms or changes it. For **lots**, Save stays disabled until each pre-filled lot is confirmed with one tap or replaced by a scan (B1). This is deliberate: the risk is accepting yesterday's lot by habit.
- The save still validates everything; a default is a suggestion, never a bypass.
- A setting, `entry.lastChoiceDefaults.enabled` (default on), lets the Section Head switch the feature off.

### 5.5 Paste: receiving, EM and water

Paste never writes directly. It always opens a preview:

1. The pasted block is read as rows and columns.
2. The user sees it mapped onto the grid's columns, with each cell checked for format.
3. Problems are listed ("Row 4: Exp Date is not a date"). The user fixes the source and pastes again, or accepts only when there are no format errors.
4. Accepted values fill the grid as if typed; saving goes through the normal validation.

- **Receiving grid:** columns are matched by position to the visible text and date columns (batch, control number, quantity, manufacturing and expiry date, sampled at). Choice columns (item, cause, department) are not pasted; they use Ctrl+D.
- **EM and water location grids:** pasting **two columns, location code and count**. Counts are matched to rows **by location code, not by position**, so a missing or reordered line cannot shift every result by one. Unknown codes, duplicate codes and non-whole numbers block acceptance. A single column of counts is refused with an explanation.
- Limit 500 rows per paste.

### 5.6 Paste in configuration (needs your list, 9.1)

You asked for bulk entry in the configuration areas. I propose the same preview-then-fill paste on the configuration screens where rows are added one at a time:

| Screen | Pasted columns |
|---|---|
| Sampling locations (EM and water points) | Code, name, area / room, grade or class |
| Item specifications | Test, limit fields, unit |
| Master testing materials | Name, code, type, supplier |
| Solution master | Name, code, components |
| Equipment master | Code, name, type, location |

Rules: pasted rows are created through the same server rules, approvals and versioning as rows typed by hand; nothing is activated by a paste that typing would not activate; duplicates of existing codes are flagged in the preview; a screen saves all pasted rows or none.

### 5.7 Drafts on PCs

The C2 device draft is switched on outside bench mode for the forms in this package, with the line "Draft saved on this computer at 10:42 — not recorded". Same rules as C2: per user, never a password, never sent without the user pressing Save, removed after 24 hours.

### 5.8 Result preview from the server (a correction)

The count dialog's in-browser "count × dilution" preview is replaced by a server preview: `POST /api/test-workflow/{id}/preview-count-result` runs the same calculation the save will run and returns the figure and any rule message, without saving. The preview is labelled "Preview — not recorded". This removes a calculation from the frontend, as `CLAUDE.md` requires.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Batch receive: any row invalid | Nothing saved; errors on the cells; grid stays open |
| Batch receive: server or network failure | Nothing saved; values kept (device draft) |
| File upload fails after a successful batch | Samples exist; the row shows "Certificate not attached — retry" |
| Paste too large or unreadable | Refused with the reason; grid untouched |
| Location code in paste not in this session | Preview blocks acceptance and lists the codes |
| Last-choice value no longer valid | Not offered |
| Defaults endpoint fails | Fields start empty; no error dialog |
| Preview endpoint fails | "Preview unavailable"; Save still works and the server calculates |

## 7. Testing

- **Backend `[PostgresFact]`:** batch receive (all valid → all created with audit rows and ids in order; one invalid → none created and field errors returned; 101 rows refused; section scope; duplicate batch/control numbers inside the request; rollback when the last row fails at the database). Field-error contract. Entry defaults (returns my last valid choice; not another user's; not an expired lot or out-of-calibration equipment; not across a different medium; setting off). Count preview equals the saved result for the same input across the configured rounding cases and saves nothing.
- **Frontend (`userEvent`, real components):** grid keys on each grid; Enter does not submit inside a grid; Ctrl+Enter saves; focus on open; field errors placed, linked and focused; default marked and lot confirmation required before Save; paste preview for each grid (good block, bad cell, too many rows); location paste matched by code with a shuffled block; single-column count paste refused; draft line and restore on a PC.
- **Mutation checks:** removing the all-or-nothing transaction, or the validity check on defaults, must fail a test.
- **Manual:** timed comparison of registering 12 samples before and after; keyboard-only pass.

## 8. Delivery stages

1. Field-error contract and helper.
2. Batch receive and the receiving grid (keys, Ctrl+D, paste, errors, draft).
3. Grid keys on the other grids; focus on open; shortcut list.
4. Entry defaults.
5. EM and water paste by location code.
6. Server count preview.
7. Configuration paste, one screen at a time.
8. Documents: requirements, user guide, SOP note on pasted data and confirming pre-filled lots.

## 9. Open items

1. **Which configuration screens** get paste first (5.6): confirm or change my list.
2. **Batch size:** 100 samples per receipt and 500 rows per paste.
3. **Pre-filled lots must be confirmed before Save** (5.4): confirm you want this extra tap.
4. Whether the supplier certificate must be attached **before** registration for any category (today it is optional).
