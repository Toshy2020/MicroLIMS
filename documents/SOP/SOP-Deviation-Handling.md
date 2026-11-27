# SOP Template: Deviation handling

> **Draft template — not approved.** Generated from the coded deviation workflow (FS-QLT-006, FS-QLT-007; BusinessRules section 15) for completion by the laboratory and approval by Quality Assurance. Text in square brackets `[SITE: …]` must be set by the site. Do not use this document as a procedure until it is approved.

| Field | Value |
|---|---|
| Document number | [SITE: document number] |
| Title | Deviation handling (template) |
| Version | 0.1 |
| Status | Draft — pending approval |
| Author | Claude (AI agent), draft from code |
| Reviewer | [SITE: reviewer] |
| Approver | [SITE: QA approver] |
| Effective date | Set when approved |

## 1. Purpose and scope

- Describes how a laboratory deviation is raised, assessed, investigated, approved, closed or cancelled in MicroLIMS, and which steps are signed.
- Applies to every deviation raised in the laboratory, including deviations raised from an OOS investigation's deviation request, a monitoring deviation, or an audit trail review finding.
- Out of scope: CAPA (see SOP-CAPA template), change control (SOP-Change-Control template), complaints (SOP-Complaint-Handling template), and the software's own error log.

## 2. Responsibilities

- **Raiser (any laboratory role with the raise permission, and QA):** records what happened, when, where, and the immediate action taken.
- **QA (Quality Assurance role, `Quality.Approve`):** assesses or rejects the deviation, classifies it, names the owner and due date, approves or returns the investigation, decides whether a CAPA is required, closes, cancels, and decides extension requests. QA never investigates or owns a deviation.
- **Owner (laboratory user with the raise permission, not QA, a member of the section):** completes the investigation and proposed actions and requests extensions.
- **Section Head or QC Manager (`Quality.LabConclude`):** signs the laboratory conclusion. The owner cannot sign it.
- **Analyst:** must not own a deviation that arises from a result the analyst produced.

## 3. Raising a deviation

1. The raiser opens a new deviation and enters: title (1 to 200 characters), description (1 to 4000 characters), the occurrence date and time in UTC (not in the future), location (optional, up to 200 characters), the immediate action taken (1 to 4000 characters), and the section.
2. Links to the affected records (samples, test orders, OOS investigations, monitoring deviations, equipment, media lots, stock lots, items, specifications, tests, controlled documents, audit trail findings, other quality records) are chosen from the system's search. A link cannot be removed after the first signed step.
3. The deviation is created with status Raised. Nothing is signed at this step.
4. [SITE: state whether a raise from a sample, a test, an equipment record or an OOS investigation is the required route.]

## 4. Assessment (QA)

1. QA opens the Raised deviation and chooses **Accept** or **Reject**.
2. **Accept:** QA selects an active classification (default names Minor, Major, Critical; [SITE: confirm names]), names the owner, and optionally sets a due date. The owner must be an active laboratory user with the raise permission, not a QA user, a member of the deviation's section, and must not have performed a test linked to the deviation. A due date cannot be before the laboratory date. If no date is entered, the site due rule is used ([SITE: due rule in working days per classification]). QA signs the step with the meaning **Deviation assessed**. Status becomes Investigation.
3. **Reject:** QA gives a reason (1 to 4000 characters) and signs the step with the meaning **Deviation rejected**. Status becomes Rejected and the record is closed to further steps.
4. [SITE: decide whether the raiser may be QA and assess their own deviation. The system does not refuse it; see open conflict CONF-228.]

## 5. Investigation (owner)

1. The owner records: the impact assessment (free text, 1 to 4000 characters), the root cause category (from the site cause list, shared with OOS investigations), the root cause description, and the proposed actions. These steps are not signed and can be saved again while the status is Investigation.
2. The impact must be linked as records from the links panel. The impact text does not select records itself (CONF-240).
3. The owner adds corrective or preventive actions if needed. Each action has a responsible person and a due date.

## 6. Laboratory conclusion (Section Head or QC Manager)

1. The signer opens the deviation. The system refuses the signature if the signer is the owner, or if any of the four investigation fields is empty.
2. The signer enters the password and signs the step **Laboratory conclusion signed**. Status becomes LabConcluded. The conclusion, and the linked records at that time, are kept in the signed snapshot.
3. [SITE: name the Section Head or QC Manager who signs for each laboratory. The owner is never the signer.]

## 7. QA approval or return

1. QA reviews the investigation and the laboratory conclusion.
2. **Approve:** QA states whether a CAPA is required, adds an optional comment, and signs **Quality record approved**. Status becomes Approved. QA cannot be the raiser or the owner.
3. **Return:** QA gives a comment (1 to 2000 characters). The status goes back to Investigation. The earlier laboratory conclusion stays in the timeline, and a new conclusion must be signed after the investigation is updated.
4. If a CAPA is required, the CAPA must be created and its plan approved before closure (SOP-CAPA template).

## 8. Closure (QA)

1. QA closes the deviation only when: no action of the deviation is unfinished, and, if a CAPA is required, a non-cancelled linked CAPA has an approved plan (status PlanApproved, CompletionVerified or Closed). A cancelled CAPA never satisfies this.
2. QA enters the password and signs **Quality record closed**. Status becomes Closed. The step is final.
3. QA cannot close a deviation they raised or own.

## 9. Cancellation (QA)

1. QA may cancel a deviation that is Raised, Investigation, LabConcluded or Approved. A closed or rejected deviation cannot be cancelled.
2. QA gives a reason and signs **Quality record cancelled**. The deviation stays visible with status Cancelled.
3. [SITE: decide whether the raiser or the owner may cancel; the system does not refuse it; see CONF-229.]

## 10. Extensions of the due date

1. The owner requests an extension of the deviation's due date with a reason (1 to 4000 characters) and a later date. An extension of an action's due date is requested by the action's responsible person (CONF-237).
2. QA approves or refuses the request, gives an optional comment, and signs **Due-date extension decided**. A request cannot be decided by the person who requested it.
3. On approval the new date replaces the due date. Every request and decision stays in the record's history.

## 11. Sources of a deviation

- **OOS deviation request:** QA raises a deviation from an OOS deviation request. The deviation is pre-filled and linked to the OOS investigation, and the request is marked addressed in the same action. One non-cancelled deviation is allowed per addressed request.
- **Monitoring deviation:** QA or the Section Head escalates the monitoring deviation. The monitoring deviation is not changed.
- **Audit trail review finding:** QA raises a deviation from the finding. One non-cancelled deviation is allowed per finding.

## 12. Records and evidence

- Each signed step is kept as an append-only step with the signer, the time in UTC, the meaning and a snapshot of the content. Signed content cannot be edited afterwards; later information is added as a new entry.
- Attachments are never deleted. A voided attachment keeps its row, its reason, the person and the time.
- [SITE: state where the printed or PDF record is kept and who files it in Document Control.]

## 13. Open points for the site

- CONF-228: raiser and owner exclusion for assessment and rejection.
- CONF-229: raiser and owner exclusion for cancellation.
- CONF-223 and CONF-224: the CAPA-required rule and source raise rules are decided and apply as written in section 8 and section 11.
- CONF-240: impact assessment is free text with linked records.
- [SITE: due rules, classification names, cause categories, QA users, laboratory sections and owners.]
