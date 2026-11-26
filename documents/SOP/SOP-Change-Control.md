# SOP Template: Change control

> **Draft template — not approved.** Generated from the coded change-control workflow (FS-QLT-009, FS-QLT-013 and FS-QLT-022 to FS-QLT-024; BusinessRules sections 15.8 and 15.14) for completion by the laboratory and approval by Quality Assurance. Text in square brackets `[SITE: …]` must be set by the site. Do not use this document as a procedure until it is approved.

| Field | Value |
|---|---|
| Document number | [SITE: document number] |
| Title | Change control (template) |
| Version | 0.2 |
| Status | Draft — pending approval |
| Author | Claude (AI agent), draft from code |
| Reviewer | [SITE: reviewer] |
| Approver | [SITE: QA approver] |
| Effective date | Set when approved |

## 1. Purpose and scope

- Describes how a planned change to a laboratory item, specification, test, equipment or controlled document is proposed, assessed, approved for implementation, carried out, closed or rejected in MicroLIMS.
- Covers the change control record and its signed steps, and the master-data change-control gate that links specification, test and workflow changes to an approved change control (section 8).
- Out of scope: emergency changes made first and approved afterwards (not supported by the system), and the deviation and CAPA processes.

## 2. Responsibilities

- **Proposer (Section Head, QC Manager or QA, holding the raise permission):** proposes the change.
- **Owner (active Section Head or QC Manager in the change control's section):** named at proposal; a QA proposer must name one. The owner cannot approve, reject, close or cancel the change control. The owner may save and sign the impact assessment even without the investigation permission (CONF-236).
- **Assessor (the named owner, or a user without the QA role who holds the investigation permission; Section Head by default):** writes the impact assessment, answers the checklist, and signs the assessment.
- **Task owner (active laboratory user in the section, not QA):** completes an implementation task with evidence.
- **QA (`Quality.Approve`):** approves or rejects the change for implementation, closes it, and cancels it. QA cannot be the raiser or the owner of the change control it approves, rejects, closes or cancels.

## 3. Proposal

1. The proposer enters a title (1 to 200 characters), a description (1 to 4000 characters), the reason (1 to 4000 characters), the section, and the owner.
2. The proposer chooses each affected record from the system: an item, a specification, a test definition, equipment, or a controlled document. An item, test definition, equipment or controlled document that does not exist yet can be identified by a planned record code (up to 100 characters). A new specification is not identified by a code: the proposer lists the item or test that the specification belongs to, and the system refuses a specification entry that does not name an existing specification. Duplicates are refused.
3. The change control is created with status Proposed.
4. Affected records can be added while the status is Proposed. They cannot be added after the impact assessment is signed. A change to the scope after that point needs a new change control.

## 4. Impact assessment (assessor)

1. The assessor enters the impact summary (1 to 4000 characters).
2. The assessor answers every active checklist item with Yes, No or Not applicable and may add a comment. [SITE: list the checklist items for the site, for example validation, documents, training, stock and open samples. The system does not supply checklist items as merged; see FS-QLT-013.]
3. The assessor adds implementation tasks. Each task has a description, a responsible person who is an active laboratory user in the section, and a due date not before the laboratory date. Tasks cannot be removed once saved.
4. The assessor signs **Change impact assessed**. The signature is refused if the summary is empty or any active checklist item is unanswered. After signing, the affected list and the checklist answers are frozen. The signed snapshot holds the summary, the affected records, the tasks and the answers.

## 5. Approval or rejection (QA)

1. QA reviews the signed assessment and the affected records.
2. **Approve:** QA enters the password and signs **Change control approved**. Status becomes ApprovedForImplementation. Implementation may start.
3. **Reject:** QA gives a reason (1 to 4000 characters) and signs **Change control rejected**. Rejection is allowed while the change is Proposed or Assessed. Status becomes Rejected.
4. QA cannot approve or reject a change control the QA user raised or owns.

## 6. Implementation (task owners)

1. Each task owner completes the task with evidence (1 to 4000 characters) and, if available, a link to a system record that belongs to this change control. Completion is not signed.
2. A task cannot be completed twice, and only its responsible person completes it.
3. When the gate is on for the laboratory, each specification, test definition, test workflow step or stage replicate change is made under an approved change control. The user enters the change control number in the **Change control needed** dialog before saving, or in **Change control number (optional)** on the Master Data Workbook page before loading the file. The system links the change to the listed record and records the time and the audit entry on that link (section 8). [SITE: describe the reason text entered for each change.]

## 7. Closure (QA)

1. QA closes the change control only when:
   - every task is complete;
   - a post-change review is written (1 to 4000 characters);
   - for every listed specification or test definition, either the record has been changed under this change control, or an unchanged justification (1 to 4000 characters) is written.
2. A listed specification or test definition that a gated write changed under this change control is marked as changed by the system. Any other listed specification or test definition needs an unchanged justification (CONF-232).
3. QA enters the password and signs **Quality record closed**. Status becomes Closed.
4. QA cannot close a change control the QA user raised or owns.

## 8. Master-data changes under the gate

- The gate is set per laboratory. With no setting, or with the gate off, a save of a specification, test definition, test workflow step or stage replicate is not refused for lack of a change control.
- QA switches the gate on or off for a laboratory with a reason, the current settings version and an electronic signature (`Quality.Approve`; FS-QLT-023).
- While the gate is on, each of the 16 gated writes is refused unless the request gives the number of a change control that is approved for implementation, not cancelled, belongs to the same laboratory, and lists the record (FS-QLT-022). The 16 writes are: specification create, update, delete and reinstate; test definition create, update, freeze and unfreeze; test workflow type update and workflow step create, update, move and delete; and stage replicate create, update and delete.
- A new specification is matched by its listed item or test. An existing specification is matched only by its own listing; a listed item or test does not cover edits to existing specifications.
- Deleting an item is refused while any of its specifications belongs to a test in a laboratory where the gate is on (FS-QLT-024). The specifications must be deleted or retired first, each under its own change control.
- Water and environmental limits, chemistry limits, HPLC and ICP methods, and item test assignments are not gated (CONF-227).
- Writes to test workflow steps and to test freeze or unfreeze check laboratory scope and the record version. Users outside the test's section are refused.
- [SITE: state which laboratories have the gate on, and the rule for master-data changes that the gate does not cover.]

## 9. Cancellation (QA)

1. QA may cancel a change control that is not Closed, Rejected or Cancelled. QA gives a reason and signs **Quality record cancelled**. The change control stays visible with status Cancelled.
2. The user who raised the change control and its owner cannot cancel it (CONF-229).

## 10. Records and evidence

- Each signed step is kept with the signer, the UTC time, the meaning and a snapshot of the content. A signed step cannot be edited afterwards.
- [SITE: state where the post-change review and the approved change control are filed in Document Control.]

## 11. Open points for the site

- Laboratories that use the gate, and the rule for changes the gate does not cover (section 8).
- Checklist items and task rules (section 4).
- Affected types: the system does not allow a workflow step or a document revision to be selected as an affected record (CONF-235).
