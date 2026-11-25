# SOP Template: Complaint handling

> **Draft template — not approved.** Generated from the coded complaint workflow (FS-QLT-010; BusinessRules section 15) for completion by the laboratory and approval by Quality Assurance. Text in square brackets `[SITE: …]` must be set by the site. Do not use this document as a procedure until it is approved.

| Field | Value |
|---|---|
| Document number | [SITE: document number] |
| Title | Complaint handling (template) |
| Version | 0.1 |
| Status | Draft — pending approval |
| Author | Claude (AI agent), draft from code |
| Reviewer | [SITE: reviewer] |
| Approver | [SITE: QA approver] |
| Effective date | Set when approved |

## 1. Purpose and scope

- Describes how a complaint about a product, item or lot is received, investigated in the laboratory, reported, concluded and closed in MicroLIMS.
- Complaint samples are received through the normal receiving process and linked to the complaint. The receiving and testing rules do not change.
- Out of scope: regulatory reporting of complaints, recalls, and the deviation and CAPA processes. A follow-up deviation or CAPA is raised through its own SOP; see section 7.

## 2. Responsibilities

- **QA (`Quality.Approve`):** receives the complaint, requests the laboratory investigation, concludes the complaint, closes it, and cancels it. QA cannot investigate or own the complaint.
- **Owner (active laboratory user with the raise permission, not QA, a member of the complaint's section):** runs the investigation, records findings and links the samples.
- **Section Head or QC Manager (`Quality.LabConclude`):** signs the laboratory report. The owner cannot sign it.
- **Laboratory investigation holder (`Quality.Investigate`):** may record findings in place of the owner.

## 3. Receipt (QA)

1. QA enters the source (1 to 200 characters), the date received (not in the future), an external reference (optional, up to 200 characters), an item (optional; the item must be active), a lot as free text (optional, up to 200 characters), the complaint text (1 to 4000 characters) and the section.
2. [SITE: state how the lot is identified. The system takes the lot as free text and does not link it to a stock or media lot; see CONF-233.]
3. The complaint is created with status Received. Nothing is signed at receipt.

## 4. Request for investigation (QA)

1. QA selects an active classification (default names Minor, Major, Critical; [SITE: confirm names]).
2. QA names the owner. The owner must be an active laboratory user with the raise permission, not QA, and a member of the section.
3. QA enters what is to be tested, for example the retained sample (1 to 4000 characters). QA may enter a due date; if not, the site due rule is used ([SITE: due rule for complaints in working days per classification]). A due date cannot be before the laboratory date.
4. The status becomes Investigation. Nothing is signed.

## 5. Investigation (owner)

1. The owner records the findings (1 to 4000 characters). These can be saved again while the status is Investigation.
2. Samples received for the complaint are linked by sample. The link is added only if the owner can read the sample. Linked samples and their results are read from the sample records.
3. [SITE: describe how the retained sample or other test is ordered in the laboratory, and how the results are reviewed. Those steps are the existing test and review processes.]

## 6. Laboratory report (Section Head or QC Manager)

1. The signer opens the complaint. The signature is refused if the signer is the owner, or if the findings are empty.
2. The signer enters the password and signs **Laboratory conclusion signed** (the laboratory report). The status becomes LabReported. The findings and the linked records at that time are kept in the signed snapshot.

## 7. Conclusion (QA)

1. QA concludes the complaint only after the laboratory report is signed. QA cannot conclude a complaint the QA user received or owns.
2. QA selects the outcome: **Justified**, **Not justified** or **Inconclusive**. QA enters a summary (1 to 4000 characters).
3. QA signs **Complaint concluded**. The status becomes Concluded.
4. The system does not record whether a deviation or CAPA is raised from the complaint (CONF-234). [SITE: state how the follow-up deviation or CAPA is raised and linked, and who decides it.]

## 8. Closure (QA)

1. QA closes the complaint when no action of the complaint is unfinished. The system names the first unfinished action and refuses otherwise.
2. QA enters the password and signs **Quality record closed**. The status becomes Closed.

## 9. Cancellation (QA)

1. QA may cancel a complaint that is Received, Investigation, LabReported or Concluded. QA gives a reason and signs **Quality record cancelled**. The complaint stays visible with status Cancelled.
2. A closed complaint cannot be cancelled.

## 10. Due dates and extensions

1. The owner may ask for an extension of the complaint's due date with a reason and a later date. QA approves or refuses it and signs **Due-date extension decided**. A request cannot be decided by the person who requested it.
2. On approval the new date replaces the due date. Every request stays in the history.

## 11. Records and evidence

- Each signed step is kept with the signer, the UTC time, the meaning and a snapshot of the content. A signed step cannot be edited afterwards.
- Attachments are never deleted. A voided attachment keeps its reason, the person and the time. Downloads of attachments are recorded in the audit trail.
- [SITE: state where the complaint report is filed and how the customer is answered. The system does not send a reply to the complainant.]

## 12. Open points for the site

- How the lot is identified (section 3; CONF-233).
- Follow-up deviation or CAPA after the conclusion (section 7; CONF-234).
- Due rule and classification names (sections 4 and 10).
