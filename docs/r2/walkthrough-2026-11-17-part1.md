# R2 live walkthrough, part 1 (2026-11-17)

Run on `r2/integration` (HEAD 24a24774 at start) against a disposable database (`microlims_walk`), API on :5000, Vite dev server on :5173, driven with headless Brave through playwright-core scripts. Users: analyst1, analyst2, reviewer1, sectionhead1 (all members of MICRO and FP), admin. About 120 screenshots kept outside the repository.

Covered: C8 navigation (a), C1 My Work (b), C6 help (c), C3 tables (d, partly), Arabic first look (e), bench offer (f). Not covered: a screen reader; an analyst in another section opening a shared view (all users were in both sections); the 5,000-row scroll; CSV export (not reachable, see 4); the Overdue/Now/Today groups and document/equipment/media items in My Work (no data).

## Findings and triage

| # | Finding | Triage |
|---|---|---|
| 1 | "Change in the order of Hooks" in `MyWorkRow` and `SampleSummaryDialog`, once a full-page error; not reproducible on reload | Probably caused by hot reload: `useActionAvailability` gained hooks in a merge at 21:38 while the walk was running, and both components use it. Recheck in part 2 with no merges during the run |
| 2 | Live hub negotiate blocked by CORS; "Live updates paused" always shown | **Fixed** e6ad0cdc (hub client no longer asks for browser credentials; it signs in with the token) |
| 3 | Bulk assign refused nothing: samples under approval and under review were reassigned; with a voided sample selected the analyst list is empty without explanation | To investigate against the single-assign rules |
| 4 | "Export selected" not offered on the workspace for a user holding Data.Export; Receive Sample register has no row selection | To investigate |
| 5 | Voided sample still shows an enabled "Assign responsible analyst" button and a selectable checkbox | To investigate |
| 6 | Row menu button labelled only "Sample actions" (needs the record, CLAUDE.md §8) | To fix |
| 7 | Next-action buttons in the Receive Sample register (Approve, Review) enabled for users who cannot do the action | To gate with the server's answer |
| 8 | Same refusal, two wordings ("You performed a test on this sample" in My Work vs "You cannot review a sample you tested."); My Work counts disagree ("All work 1", "2 items shown") | To investigate |
| 9 | Reviewer signature time and workflow history times shown without a time zone; Exp/Mfg date shown as "01 Oct 2028, 03:00" in the summary and "10/1/2028" elsewhere | **GMP display** (§11.50 manifestation): to fix first |
| 10 | Release and Void dialogs open with focus on the container, not Cancel (CLAUDE.md §9) | To fix |
| 11 | After a keyboard Take or Release, focus falls to the page body | To fix |
| 12 | Favourites and Recent sit between My Work and Dashboard, so Dashboard reads as a favourite | To fix |
| 13 | Help: My Work opens the generic chapter; "{n} unread" placeholder in chapter 6; literal `<-- WF-SAMPLE-001 --!>` in the receiving chapter | To fix (guide content and chapter map) |
| 14 | Status column blank for Received and Voided rows in the FP workspace split layout | To investigate |
| 15 | `GET /api/items/{id}/stm` 404 on every sample screen for an item without an STM; client-cancelled requests logged as unhandled exceptions | To fix (noise) |
| 16 | Appearance step has "Complies" pre-selected, so a pass can be signed without choosing | **Question for the user** |
| 17 | Help page breadcrumb has only the current crumb; hint title in lower case | Low |
| 18 | "Return to Analyst" enabled for the Section Head who tested | To check against the server rule |
| A1 | No in-app control to choose the interface language (only the stored setting or the browser language) | To build (F4) |
| A2 | Arabic: sidebar FAVOURITES/RECENT/Manage/Clear and favourite names in English; recent rows run identifier and title together ("FP1126003pH") | To fix |
| A3 | Arabic: some dates in Arabic-Indic digits (date formatting outside `formatLabDate` follows the browser locale) | To fix with the module stream |
| A4 | Bench offer dismissal is per browser, not per user | As designed (bench mode is a device setting) |

What worked: favourites (keyboard reorder, persisted), recent records, remembered lab and tab after sign-out, breadcrumbs, gated reasons with `aria-describedby` (Submit Review for the tester, Submit Decision for the analyst), My Work flows for three roles at 768 px, the tour, F1 on six pages, help search, field hints, saved and shared view, the bench offer on a coarse pointer, RTL layout and the signature dialog in Arabic.
