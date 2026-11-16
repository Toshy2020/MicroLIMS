# Standard Operating Procedures

SOPs governing use of MicroLIMS: sample receipt, result entry, review,
approval, deviation handling, user account management, backup/restore.

## SOP notes from functional packages

These notes are inputs for the SOP authors. They are not approved procedures. Site-specific values are for the laboratory and QA to set.

### Notifications, escalation and working hours (package C5; FS-NTF-012 to FS-NTF-021)

- **Notifications are aids, not records.** A notification, a notification e-mail, the bell count, the "Live updates paused" status and the "record changed" banner help a person to find work. They are not part of the laboratory record, they are not audited, and they are removed after the retention period (`notifications.retentionDays`). The SOP must not rely on a notification or an e-mail as evidence that a task was assigned, seen or done, and must say that the review, approval and result records remain the record. A person who has no e-mail address, or whose e-mail is not delivered, is still bound by the same duties; the Integrations page ("Email delivery") shows failed e-mails and users without an address.
- **Escalation rules and working hours are controlled configuration.** They are changed only by a user with the notification-configuration permission (Section Head or QC Manager by default) for a section the user belongs to. Each change (new rule, replacement, retirement, change of working hours) is recorded in the audit trail. A rule is replaced by retiring it and creating a new one; it is not edited in place. No rule and no working hours are supplied with the system: the laboratory decides the thresholds (in working hours) and the roles, QA approves them, and the SOP should record the approved values and who approved them. An escalation message that says no one holds the role in the section goes to the system administrators; the SOP should say who then acts.
- **Mandatory e-mail types** for a section are a decision of the Section Head; the SOP should record which types are mandatory and why.
- **Live updates** are a convenience. When "Live updates paused" is shown the screens still refresh by themselves, more slowly; the SOP should not depend on instant updates. A "record changed" banner means someone else saved the record: the user decides whether to reload and must check what was entered before saving over it.

### Configuration changes: reasons and retirement (package C7; FS-MDM-038 to FS-MDM-045)

- **Change reasons.** A reason of at least 10 characters is required when a specification, a Test Master test, an item or equipment is edited while open work (open test orders, open samples, incubations or runs in progress) would be affected, and always when a specification is deleted, retired or reinstated, when a test or item is frozen or unfrozen, and when an item is deleted. The reason is stored on the audit entries of that change. The SOP should say what a good reason contains (what changed and why, with a reference to the approving document or change control) and that "see above" or a single word is not one.
- **Retirement instead of deletion.** A specification that a recorded result refers to is retired, not deleted; recorded results are not changed. A retired specification is not used for new samples, and a sample in testing that depended on it cannot receive results until the specification is reinstated or a new one is configured. The SOP should require the Section Head to check the where-used summary (open work and history) before retiring, deleting, freezing or changing a record, to decide what happens to samples in testing, and to record the decision. Reinstating needs a reason.
- **Copying.** "New from existing" and "Copy specifications" create new records that the user must review and confirm; codes, names, SOP numbers, serial numbers, calibration dates and documents are not copied. The SOP should say that a copied specification is a new specification that needs the same review and approval as one entered by hand.
- **Limits stay as configured at the time of entry.** The system does not version a specification or give it an effective date (see CONF-150 and CONF-207). Until that is decided, the SOP should rely on the audit trail and the reason for change to show which limits applied when.
