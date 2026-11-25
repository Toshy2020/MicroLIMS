# SOP Template: Change control

> **Draft template — not approved.** Generated from the coded change-control workflow (FS-QLT-009, FS-QLT-013; BusinessRules section 15) for completion by the laboratory and approval by Quality Assurance. Text in square brackets `[SITE: …]` must be set by the site. Do not use this document as a procedure until it is approved.

| Field | Value |
|---|---|
| Document number | [SITE: document number] |
| Title | Change control (template) |
| Version | 0.1 |
| Status | Draft — pending approval |
| Author | Claude (AI agent), draft from code |
| Reviewer | [SITE: reviewer] |
| Approver | [SITE: QA approver] |
| Effective date | Set when approved |

## 1. Purpose and scope

- Describes how a planned change to a laboratory item, specification, test, equipment or controlled document is proposed, assessed, approved for implementation, carried out, closed or rejected in MicroLIMS.
- Covers the change control record and its signed steps. It does not yet cover the master-data change-control gate, which is a later part of the D2 package. [SITE: state the interim rule for master-data changes; see section 8.]
- Out of scope: emergency changes made first and approved afterwards (not supported by the system), and the deviation and CAPA processes.

## 2. Responsibilities

- **Proposer (Section Head, QC Manager or QA, holding the raise permission):** proposes the change.
- **Owner (active Section Head or QC Manager in the change control's section):** named at proposal; a QA proposer must name one. The owner cannot approve or close the change control.
- **Assessor (holder of the investigation permission; Section Head by default):** writes the impact assessment, answers the checklist, and signs the assessment.
- **Task owner (active laboratory user in the section, not QA):** completes an implementation task with evidence.
- **QA (`Quality.Approve`):** approves or rejects the change for implementation, closes it, and cancels it. QA cannot be the raiser or the owner of the change control it approves or closes.

## 3. Proposal

1. The proposer enters a title (1 to 200 characters), a description (1 to 4000 characters), the reason (1 to 4000 characters), the section, and the owner.
2. The proposer chooses each affected record from the system: an item, a specification, a test definition, equipment, or a controlled document. A new specification or test that does not yet exist is identified by a record code (up to 100 characters). Duplicates are refused.
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
3. [SITE: describe how each change is made in the system (for example, how a specification or test is edited), which reason is entered, and how the change control number is recorded. The merged system does not link a master-data change to the change control; see section 8.]

## 7. Closure (QA)

1. QA closes the change control only when:
   - every task is complete;
   - a post-change review is written (1 to 4000 characters);
   - for every listed specification or test definition, either the record has been changed under this change control, or an unchanged justification (1 to 4000 characters) is written.
2. In the merged build, no record is marked as changed automatically, so an unchanged justification is required for every listed specification or test definition (CONF-232). [SITE: decide the closure evidence until the gate is merged.]
3. QA enters the password and signs **Quality record closed**. Status becomes Closed.
4. QA cannot close a change control the QA user raised or owns.

## 8. Master-data changes (interim rule)

- The system does not refuse a save of a specification, test definition or workflow step because no approved change control exists. The change control gate is planned as a later part of D2 (D2 Task 9).
- [SITE: until the gate is in use, state the rule that applies to changes of specifications, tests and workflow steps, who approves them, and how the change control number is recorded on the change.]
- Writes to test workflow steps and to test freeze or unfreeze check laboratory scope and the record version. Users outside the test's section are refused.

## 9. Cancellation (QA)

1. QA may cancel a change control that is not Closed, Rejected or Cancelled. QA gives a reason and signs **Quality record cancelled**. The change control stays visible with status Cancelled.

## 10. Records and evidence

- Each signed step is kept with the signer, the UTC time, the meaning and a snapshot of the content. A signed step cannot be edited afterwards.
- [SITE: state where the post-change review and the approved change control are filed in Document Control.]

## 11. Open points for the site

- Interim rule for master-data changes (section 8).
- Checklist items and task rules (section 4).
- Affected types: the system does not allow a workflow step or a document revision to be selected as an affected record (CONF-235).
- Owner rights: the owner of a change control who is a QC Manager does not hold the investigation permission by default, so cannot save or sign the assessment (CONF-236).
- Affected-record text: the English and Arabic refusal texts say "after approval" while the rule freezes the list at assessment (CONF-231).
