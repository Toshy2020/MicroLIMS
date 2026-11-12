# C2 — Bench and Tablet Mode: Design Specification

**Roadmap package:** C2 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-10 (late-save note and 24-hour retention confirmed)
**Date:** 2026-11-10

## 1. Purpose

Make the screens analysts use at the bench comfortable on a 10–11 inch tablet with gloved hands, and make sure a short connection drop does not lose what they typed, without ever treating something as recorded before the server has accepted it.

## 2. Decisions (confirmed by the user, 2026-11-10)

| # | Decision | Answer |
|---|---|---|
| 1 | Connection loss (roadmap D-7) | Keep an unsaved draft on the tablet. The record, time stamp and audit entry exist only when the server accepts it |
| 2 | Devices | 10–11 inch tablets, landscape first |
| 3 | Shared tablets | Normal sign out / sign in, made faster |
| 4 | First screens | All four groups: count reading and incubation steps; pathogen observation steps; EM and water location grids; media and solution preparation |

Roadmap decision D-7 is closed by row 1.

## 3. Out of scope

- Phones (below 768 px wide).
- True offline recording, offline login, or keeping any server data on the tablet.
- A native app.
- Screen lock with password resume; PIN login.
- Voice entry, push notifications.
- Other screens (configuration, reports, document control) beyond what the shared changes give them for free.

## 4. Current state (from code, not run)

- Only 7 `useMediaQuery` calls in 6 files. Below `md` the sidebar becomes a drawer; dialogs go full-screen only below `sm`.
- No density or size setting in the theme. Screens pass `size="small"` by hand; table cells use 6 × 12 px padding.
- Bench entry is spread over many panels in `frontend/src/modules/testingWorkspace/` with inline `type="number"` fields and fixed widths of 90–120 px. There is no shared numeric input.
- Colony counts have no client-side format check; one entry uses `parseFloat`.
- No manifest, no service worker.
- Idle logout listens to `mousemove`, `keydown`, `click`, `scroll` only.
- Sign out is inside the profile menu.
- Unsaved input is lost when a dialog closes or a request fails.

## 5. Design

### 5.1 Bench mode switch

- A switch in the header, "Bench mode", stored **per device** (`localStorage`), not per user: a bench tablet stays in bench mode whoever signs in; an office PC stays normal.
- On first use on a touch device (`pointer: coarse`) the app offers it once.
- Bench mode changes presentation only. Every rule, calculation and permission stays on the backend.

What it changes everywhere:

| Aspect | Bench mode |
|---|---|
| Touch targets | At least 48 × 48 px, with at least 8 px between neighbours |
| Controls | Medium/large sizes; inputs with 18 px text |
| Tables | Comfortable row height; no hover-only actions (row actions always visible) |
| Dialogs | Full-screen for bench entry, with a fixed bottom bar: Cancel, Save, "Save and next" |
| Sidebar | Closed by default (drawer), giving the full width to the task |
| Gestures | No drag, double-tap or long-press is ever required |

Implemented as a theme variant (component default sizes and spacing in `frontend/src/theme/`), so screens do not carry their own size logic. The hand-written `size="small"` on the bench screens is removed in favour of theme defaults.

### 5.2 Number entry

One shared component, `NumericEntryField`, replaces the inline number fields on the bench screens.

- `inputMode="numeric"` (or `"decimal"` where the field's configuration allows decimals), large text, tabular numerals, select-all on focus.
- In bench mode an on-screen keypad (digits, backspace, Next) appears in the bottom bar, so the tablet's own keyboard does not cover half the screen. Next moves to the next field in reading order. A hardware keyboard keeps working.
- Format-only checks on the client: whole number where the field is a count, not negative, length limit. A wrong format is shown under the field at once.
- **All laboratory rules stay on the backend** (countable range, averaging, special results). The component offers only the special entries the existing screens already offer; it adds none.
- The `parseFloat` count entry in `PrimaryObservationMatrixPanel` is corrected to the whole-number rule the backend already enforces.

### 5.3 The four screen groups

Each gets a tablet layout at 1024–1280 px landscape and stays usable at 768 px portrait and on a PC.

| Group | Main changes |
|---|---|
| Count reading and incubation | Count dialog full-screen; plates as large rows with `NumericEntryField`; Start incubation and Read actions as full-width buttons; countdown readable from a distance |
| Pathogen observation | Step panels in one column of large choices (growth / no growth, colony description) instead of dense tables; observation matrix cells become rows per plate |
| EM and water location grids | One location per row with its count field, auto-advance with Next, a progress line "12 of 40 entered", sticky location name; the 90 px fixed fields are removed |
| Media and solution preparation | Step-by-step layout with one section per screen height; scan fields from B1 for lots and equipment; large confirm buttons |

No workflow step, field or validation is added or removed.

### 5.4 Unsaved drafts

**Rule:** a draft is not a record. Nothing in a draft is attributed, time-stamped or audited by the system until the analyst submits it and the server accepts it.

- While a bench entry dialog is open, its values are saved on the tablet after each change (IndexedDB), keyed by user, record and step, with the record version the form was opened with.
- This also covers accidents while online: a closed dialog, a reload, a flat battery.
- A banner appears when the connection is lost: "No connection. Your entries are **not saved** to MicroLIMS. They are kept on this tablet until you can save." Save is replaced by "Keep draft".
- Connection state comes from failed requests and a light check of the existing health endpoint every 15 seconds, not from the browser's online flag alone.
- When the connection returns: "Connection restored. You have 2 unsaved entries." Nothing is sent automatically. The analyst opens each draft, sees the values, and presses Save. The server validates it exactly like a normal entry.
- If the record changed meanwhile (version mismatch) or the step is no longer open, the server rejects it as today; the draft stays, marked "Could not be saved: the record has changed", and the analyst can copy the values or discard.
- **A draft never holds a password or signature.** Forms that sign at the same time as saving (for example the qualitative result) store only the result fields; the password is typed again at save.
- Drafts belong to the user who typed them. Another user on the same tablet cannot open or see their values; they see only "Another user has unsaved entries on this tablet".
- The header shows a draft counter. Signing out with drafts asks for confirmation and keeps them for that user.
- Drafts are deleted when saved, discarded, or older than `bench.draftRetentionHours` (default 24, 1–72). An expired draft is removed at the next start and the user is told.
- Discarding a draft needs a confirmation.

**Transparency about timing (please confirm, 9.1).** When an entry is saved from a draft that waited more than 5 minutes, the request carries the time the draft was started, taken from the tablet's clock. The server stores it in the audit entry as additional information ("Submitted from a device draft started at 10:02, device clock"), clearly separate from the authoritative server time stamp. This makes a late save visible to reviewers instead of hiding it.

### 5.5 Installable app

- A web app manifest (name, icons, standalone display, landscape preferred) so the tablet has a home-screen icon and no browser bars.
- A service worker that caches **only the application's own files** (scripts, styles, fonts, icons). It never caches API responses: no sample, result or user data is kept by the service worker.
- When a new version is deployed the app shows "A new version is available. Reload" and waits for the user; it never reloads during an entry.
- Without a connection the app opens to its shell, shows the connection banner, and lets a signed-in user reach their drafts. Signing in needs the server.
- Library: `vite-plugin-pwa` (MIT), build-time only; qualified like the B1 libraries.

### 5.6 Shared tablets and session

- In bench mode the header shows a large "Switch user" button: it signs the current user out (drafts kept for them) and opens the login screen. Each analyst signs in with their own credentials, and second factor per B2.
- The idle logout also listens to `pointerdown` and `touchstart`, so touch-only work counts as activity.
- The screen is kept awake while a bench entry dialog is open (Screen Wake Lock, where the browser supports it), and released when it closes.

### 5.7 Accessibility

Bench mode keeps WCAG 2.2 AA: target size, visible focus, contrast in both themes, text labels on every status, `aria-live` for the connection banner and draft messages, and full keyboard operation with a hardware keyboard.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Save fails for a network reason | Values kept as a draft; banner; no success message |
| Save rejected by a rule | The server's message is shown on the field or form, as today; draft kept |
| Version conflict on a draft | Draft marked "could not be saved"; never overwrites the newer record |
| Tablet storage unavailable or full | Bench mode still works; a warning says drafts cannot be kept on this device |
| Session expired while offline | After sign-in the user returns to their drafts |
| New app version while a dialog is open | Update waits until the dialog is closed and the user agrees |

## 7. Testing

- **Unit / component (Vitest, `userEvent`):** `NumericEntryField` format rules and keypad navigation; draft store (save, restore, per-user isolation, expiry with a fake clock, version kept); connection banner states; "Keep draft" versus Save; restored draft is never auto-submitted; conflict keeps the draft; Switch user keeps drafts; idle hook counts touch events; bench-mode switch persists per device.
- **Per screen group:** the real panel renders in bench mode, all controls reachable in reading order, Save sends the same payload as before the change (the backend contract does not move).
- **Backend `[PostgresFact]`:** the draft-started time is stored as additional audit information and cannot replace the server time stamp; a submission with a stale version is rejected.
- **Service worker:** a test asserts that no route under `/api/` is cached.
- **Browser checks during verification:** each screen at 1280×800, 1024×768 and 768×1024 with touch emulation; target sizes measured; airplane-mode walk-through of the draft flow.
- **Manual (site):** real tablet with gloves on each of the four screen groups; a Wi-Fi drop in the middle of a count entry.

## 8. Delivery stages

1. Bench-mode switch and theme variant; idle and Switch-user changes.
2. `NumericEntryField` and keypad; count reading and incubation screens.
3. Draft store, connection banner, submit-from-draft flow.
4. EM and water grids.
5. Pathogen observation screens.
6. Media and solution preparation screens.
7. Manifest and service worker.
8. Documents: requirements, user guide, SOP notes (drafts are not records; tablet care; glove use).

## 9. Open items

1. **Tablet model and browser** (Safari on iPad or Chrome on Android), which decides wake-lock and install behaviour to test.
2. **Tablet management:** the SOP should require lab-owned, locked-down tablets, since drafts sit on the device until saved.
