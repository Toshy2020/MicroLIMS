# After R2: status and follow-ups (2026-11-20)

R2 was merged on 2026-11-19 (#149). The after-R2 work then ran as one pull request per batch, each gated locally and merged when GitHub CI was green.

## Done

| PR | What |
|---|---|
| #150 | Drafts header ignores storage answers after it unmounts (main CI teardown error after R2) |
| #151 | 24-hour date and time entry in every result panel, the destruction and audit-review dialogs and working hours; media preparation Cancel discards its bench draft |
| #152–#162, #164, #165, #167 | Every refusal thrown in Application and API carries a message code: the uncoded baseline went from 1,609 to 0 (2,088 codes, English unchanged, Arabic templates in `serverMessages.json`) |
| #153 | Notification catalogue test finds `NotificationRequest` types; preference changes are audited to the acting user |
| #163 | Page-time scaling test made stable (ANALYZE after seeding, best of 7; budget unchanged) |
| #166 | `useTrackRecent` test waits for the post instead of racing it |

## Still open

### Messages Arabic users still see in English
These refusals pass a runtime list through one `{{message}}` code instead of coding each line at its source:
- Password policy rules (`PasswordPolicy.Validate`, code `User.PasswordPolicyFailed`): five fixed rules, easy to code individually.
- Workflow template validator errors (`WorkflowTemplateValidator`).
- Frequency rule errors (`FrequencyRules.Validate`, two codes: `Monitoring.FrequencyValidationFailed`, `Monitoring.FrequencyRuleInvalid`; can be merged into one).
- File validator errors for material and equipment documents, and discussion attachments.
- SSO switch-on blockers (`SsoModeService.GetSwitchOnBlockersAsync`).
- Master data workbook reader errors, multi-error label print requests, and the multi-error fallback in `TestStepNavigator`.

### Not counted by the ratchet
`ActionCheck.No(reason)` and `ActionCheck.Denied(reason)` without a code still produce uncoded refusals; the ratchet counts only `throw new` sites. A follow-up ratchet can count those calls the same way.

### From the R2 decisions (2026-11-19)
- Translate stored English values shown in data tables (status chips are already translated).
- Redesign the New Sample grid column widths so the date and delete columns are visible without sideways scrolling.

### From the Argon review (see `argon-review-2026-11-19.md`)
The low-risk items listed under "After R2" there, except those fixed above.
