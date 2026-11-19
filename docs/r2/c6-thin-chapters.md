# C6 in-app help: guide chapters that are thin or out of date

Status: working list, written 2026-11-17 for the C6 in-app help package (plan `docs/superpowers/plans/2026-11-11-in-app-help.md`, Task 8, step 4). Nothing in the guide has been changed. This list is the input for the guide owner.

## How this was checked

- The map is `frontend/src/modules/help/content/helpMap.ts` (`HELP_MAP`). It is keyed by menu path, and its values name the chapter. `chapterForPath` falls back to `01-introduction` when a path has no entry.
- Word counts come from `wc -w` on `documentation/guide/NN-*.md`.
- Each mapped chapter was compared with the screens its map entries name, using the page components under `frontend/src/modules` and the features that exist in the code. Terms were searched in each chapter (for example "draft", "unsaved", "saved view", "bulk", "export", "preference", "remember", "SOP").
- Chapter 01 and chapter 41 were read in full. Chapters 09, 35 and 34 were checked by their headings and by the searches above. The other chapters were checked by search only, so a chapter marked out of date may be wrong in more detail than is listed here.

## Counts

- Chapters that the map points to: 26 (`01-introduction` is also the fallback).
- Thin (under 1,000 words, and the screen has more than the chapter covers): **2** (chapter 01 and chapter 41).
- Out of date for features that now exist on the mapped screens: **7** (chapters 08, 09, 11, 12, 14, 34, 35).
- Guide chapter count: 45 files (`01` to `45`). The C6 plan says 44; the test `helpContent.test.ts` expects 45. The plan number is out of date.

## Thin chapters

| Chapter file | Words | Mapped screen(s) | What is missing |
|---|---|---|---|
| `documentation/guide/01-introduction.md` | 484 (29 lines) | No map entry of its own. It is the fallback for every path without an entry, so any page without an entry opens this chapter. | Only two sections (1.1 About This User Guide, 1.2 System Purpose). No list of the areas, no guide to where to start, and no mention of the help panel, F1 or the Full guide page. |
| `documentation/guide/41-integrations.md` | 163 (16 lines) | `/admin/settings` (Settings) and `/admin/jobs` (Jobs) | The chapter only says what is not available (no instrument import, no ERP link, no AI assistant). It describes none of the two screens it is mapped to. Missing: the Settings page (`frontend/src/modules/admin/SettingsPage.tsx`: settings grouped by category, with value and source, including the bench draft retention `bench.draftRetentionHours` and the notification retention), the Jobs page (`JobsPage.tsx`, `MaintenanceSection.tsx`), and the Email delivery card (`EmailDeliveryCard.tsx`). Which of the admin pages shows the card was not checked. |

Chapter 40 (`40-error-monitoring.md`, 803 words, mapped to `/admin/error-log`) is short but covers its one screen in full, so it is not listed as thin.

## Out of date for mapped screens

| Chapter file | Words | Mapped screen(s) | Out of date: what the chapter does not say |
|---|---|---|---|
| `documentation/guide/09-notifications-messages.md` | 1,731 | `/notifications`, `/messages`, `/discussions` | Section 9.1 describes the notifications list. It does not mention notification preferences (`frontend/src/modules/notifications/NotificationPreferences.tsx`: for each e-mailable type, at once, in a daily digest, or not at all), mandatory e-mail types set by a section (`NotificationMandatoryPanel.tsx`), or the "Live updates paused" status. A search for "preference", "digest" and "escalat" finds nothing. |
| `documentation/guide/35-audit-trail.md` | 1,146 | `/admin/audit` and `/audit-trail-reviews` | The chapter covers the audit trail search (35.2) and entity history (35.3). It does not describe the audit trail reviews screen (`AuditTrailReviewsPage.tsx`, `AuditTrailReviewDetailPage.tsx`, `StartReviewDialog.tsx`, `FindingDialog.tsx`): starting a review and its period and scope, marking and closing findings, signing with the password, or exporting the signed review as PDF. The QA role guide (`documents/UserManual/roles/qa.md`, section 3) has these steps; the in-app guide does not. |
| `documentation/guide/08-dashboard.md` | 1,326 | `/dashboard` | The dashboard now remembers the laboratory last chosen (`usePersistedLab`, interface setting `nav.lastLab`, and the `lab` address parameter; FS-NTF-024). The chapter does not mention this. |
| `documentation/guide/11-sample-management.md` | 2,773 | `/receiving/tracking` | Describes the default columns of the sample table, but not the saved views (`ViewsMenu`), the Columns menu, the density control, the audited **Export selected** and the export log (`ExportsController`). |
| `documentation/guide/12-sample-receiving.md` | 2,656 | `/receiving` | The new-sample dialog (`NewSampleDialog.tsx`) keeps unsaved entries on the bench tablet and offers to restore them (`useBenchDraft`). The chapter does not mention unsaved entries, restoring them, or discarding them. |
| `documentation/guide/14-testing-workspace.md` | 4,236 | `/microbiology/workspace`, `/physicochemical/workspace`, and the section pages | The count reading, qualitative, pathogen step and location grid dialogs keep unsaved entries on the bench tablet (`useBenchDraft`). The chapter has no mention of unsaved entries. It mentions "SOP" once, so the **Open SOP** action in the test workflow dialog (`OpenSopAction.tsx`; FS-MDM-047) is not described. |
| `documentation/guide/34-reports-kpis.md` | 5,411 | `/reports` | The selection bar (**Select All … Matching Records**, **Export Selected CSV**) is described. Missing: saved views of the results table, the Columns menu, and the export log header (the lines "Exported by", "Exported at", "Export ID" and the rest; FS-RPT-009). |

## Not in the list, and why

- The other mapped chapters (`20`, `23`, `24`, `25`, `26`, `27`, `28`, `29`, `30`, `31`, `32`, `37`, `40`, `45`, `05`) were not searched for the new features and were not compared with their screens. Their word counts are above 1,000 except `40`, which is covered above. A later pass should check them.
- Pages with no table options yet (Items, OOS tracking, equipment master) are a code gap, not a chapter gap, so no chapter change is listed for them.

## Notes for the guide owner

- The new user-manual chapters `documents/UserManual/features/bench-mode.md` and `documents/UserManual/features/tables.md` are in `documents/`, not in `documentation/guide/`. The in-app help loads only `documentation/guide/`, so these two chapters are not visible in the application until someone adds them there. That was out of scope for this task.
- Adding a chapter to `documentation/guide/` changes the chapter count that `helpContent.test.ts` checks (45). The test must be updated with it.
