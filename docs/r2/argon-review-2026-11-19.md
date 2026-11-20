# R2 review against the plans (Gemini 4 Argon, 2026-11-19)

Gemini 4 Argon (agy) reviewed branch `r2/integration` against the nine R2 plans in three read-only passes: C2/C4/C7, C3/C6/C1, and C5/C8/F4. It marked every plan task Done or Partial; none was Missing. Each finding was then checked in the code before it was accepted.

**Decision (user, 2026-11-19):** fix all confirmed findings on the R2 pull request before it merges. Argon (agy) fixes the backend items and Hydrogen (agy) the frontend items. The rest waits until after R2.

## Confirmed: fixed before R2 merges

| # | Finding | Area | Lane |
|---|---|---|---|
| B1 | Calibration-due notifications go to `Equipment.WorkRecord` holders in every section, not only the equipment's section | Notifications | Backend |
| B2 | My Work ignores the physicochemical area (FP vs RM/PM); taking a test does not check the area | My Work | Backend |
| B3 | Live-update `WatchRecord` only checks that a configuration record exists, not the read scope or permission of its screen; record groups are not dropped when section membership is removed | Live updates | Backend |
| B4 | Bulk assign returns the reference number of a sample in a section the user cannot read | Tables | Backend |
| B5 | The change-reason rule counts open work only in the editor's own sections | Configuration | Backend |
| B6 | Recently opened equipment is resolved against the equipment master table, but the page tracks equipment inventory IDs | Navigation | Backend |
| F1 | Reports tables cut 50 or 100 rows per page down to 25 and show a second pager | Tables | Frontend |
| F2 | "Select all that match" is not cleared when the filter or search changes (Receive register, physicochemical workspace) | Tables | Frontend |
| F3 | After a 409 conflict, "Review and save again" resends the old version, so every retry fails (Equipment and Material wizards do not reload at all) | Configuration | Frontend |
| F4 | A restored configuration draft does not keep the version it started from, so it can overwrite a later save without a conflict | Bench / Configuration | Frontend |
| F5 | Four receiving signature dialogs show no Arabic translation of the meaning statement | Arabic | Frontend |

## How the fixes were made

| # | Commit | By |
|---|---|---|
| B1 | 956864ca | Argon (agy) |
| B2 | e4271c4e | Argon (agy) |
| B4 | 7301d166 | Argon (agy) |
| B5 | 1ca1d809 | Sonnet (agy credits ran out) |
| B6 | 98cc721b | Sonnet |
| B3 | 69889dab | Sonnet: one shared read rule (`LiveRecordReadAuthorizer`) for watching a record, applied again when access changes |
| F1 | 710e15ad | Hydrogen (agy) |
| F2 | 2643b1b8, 51c31015 | Hydrogen; then the workspace keeps samples ticked by hand and reports them as hidden, while "all that match" always clears |
| F3 | a8e8042d | Hydrogen |
| F4 | 3e350e1a, 639acc14 | Hydrogen |
| F5 | 2d02398d | Hydrogen |

## Partly correct or not confirmed

- **Retired specification fallback (C7):** not confirmed. `SpecificationLookup.ThrowIfRetiredAsync` refuses result entry when any retired row matches the sample's grade or stage, including ungraded rows, so evaluation never silently falls back to other limits. Two small points do hold and wait until after R2:
  - The sample summary does not hide retired rows, so a retired row and its replacement can both be listed.
  - HPLC and ICP report "no specification" before "retired". Both still refuse; only the message differs.
- **Selected-rows export of "all matching" includes unapproved report rows:** it is the same set as the existing "Export all" button, which the user may already run. After R2: align the selection rule.
- **Exported CSV omits selected retest child rows:** plausible. After R2.

## Follow-ups checked before merging (2026-11-19)

- **Live updates for `equipment` use the wrong table:** not a defect. The live `equipment` type is the equipment master record throughout: the signal is raised for `Equipment`, the read rule checks `Equipment`, and no screen watches equipment by inventory ID (only the specification wizard watches a record).
- **Bulk assign reports a sample with only superseded orders in the user's section as "not found":** not a defect. The check is the same as the system's sample-access rule (`EnsureSampleAccessAsync`), which also ignores superseded orders, so bulk assign behaves exactly like opening the sample.
- **No positive test for watching a role record:** added. A System Administrator (holds Roles.Manage) may watch a role record; a section head may not. Mutation-checked.

## After R2 (not checked in detail or low risk)


- My Work sign-off queue loads sample entities with their test orders into memory instead of projecting (performance).
- Configuration paste: the upfront check does not run every domain rule, so a batch can stop at the first failing row instead of listing all; the Solution master screen has no paste.
- The media preparation dialog does not discard a bench draft on close.
- The notification catalogue test does not scan `new NotificationRequest(...)` call sites.
- "Changed by someone else" banner is not mounted on the sample summary, test workflow and a few configuration dialogs.
- Notification preference writes do not set the audit user outside the request pipeline; digest e-mails may repeat mandatory immediate types.
- `SampleSummaryDialog.test.tsx` still mocks project components and uses `fireEvent` (legacy test changed during R2).
- Interface-state keys reject hyphens: not a defect, decided 2026-11-20 to keep. FS-NTF-038 fixes the key pattern at `^[a-z][a-zA-Z0-9.]{1,63}$`; a caller that needs a separator uses a dot. The Notification Centre has no section filter.
- 765 hard-coded interface strings remain (the ratchet only goes down).
