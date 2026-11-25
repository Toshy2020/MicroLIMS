# SOP Template: Corrective and preventive action (CAPA)

> **Draft template — not approved.** Generated from the coded CAPA workflow (FS-QLT-008; BusinessRules section 15) for completion by the laboratory and approval by Quality Assurance. Text in square brackets `[SITE: …]` must be set by the site. Do not use this document as a procedure until it is approved.

| Field | Value |
|---|---|
| Document number | [SITE: document number] |
| Title | Corrective and preventive action, CAPA (template) |
| Version | 0.1 |
| Status | Draft — pending approval |
| Author | Claude (AI agent), draft from code |
| Reviewer | [SITE: reviewer] |
| Approver | [SITE: QA approver] |
| Effective date | Set when approved |

## 1. Purpose and scope

- Describes how a CAPA is created, planned, approved by QA, carried out, verified and checked for effectiveness in MicroLIMS.
- A CAPA may be raised from a deviation, a complaint, an OOS investigation, an audit trail review finding, or on its own. A deviation that requires a CAPA cannot close until the CAPA's plan is approved (SOP-Deviation-Handling, section 8).
- Out of scope: the deviation itself, change control, and the complaint process.

## 2. Responsibilities

- **Creator (Section Head, or QA):** creates the CAPA and names its owner. The creator needs the investigation or approval permission.
- **Owner (active laboratory user in the CAPA's section, not QA):** writes the plan and the effectiveness check. Only the owner saves the plan.
- **Responsible person (named in each action; active laboratory user in the section, not QA):** completes the action with evidence.
- **QA (`Quality.Approve`):** approves or returns the plan, verifies completion, signs the effectiveness check, and cancels. QA cannot be the raiser or the owner of the CAPA it approves or closes.

## 3. Creating the CAPA

1. The creator enters a title (1 to 200 characters), a description (1 to 4000 characters), the section, and the owner.
2. A source record may be named. The source must be a quality record the creator can read. Links to laboratory records (OOS investigation, monitoring deviation and so on) are chosen from the system's search.
3. The CAPA is created with status Plan. The due date comes from the site due rule ([SITE: due rule for CAPA]). Nothing is signed.

## 4. Writing the plan (owner)

1. The owner adds at least one action. Each action has:
   - a kind: **Corrective** or **Preventive**;
   - a description (1 to 4000 characters);
   - a responsible person who is an active laboratory user in the section;
   - a due date not before the laboratory date.
2. The owner enters the effectiveness check: criteria (1 to 4000 characters), method (1 to 4000 characters), and the planned date. The planned date must be after the latest action due date.
3. The owner saves the plan. Actions can be changed while the plan is in Plan, but existing actions cannot be removed.
4. [SITE: state how many actions and which checks are expected for each CAPA type.]

## 5. Approving the plan (QA)

1. QA checks that the plan has actions and a complete effectiveness check.
2. QA approves and signs **CAPA plan approved**. Status becomes PlanApproved. From this point the planned fields of each action (kind, description, responsible person, due date) cannot be changed, by the application or by the database.
3. QA may return the plan with a comment (1 to 2000 characters) instead. The plan stays in Plan for the owner to revise.
4. QA cannot approve a plan the QA user raised or owns.

## 6. Carrying out the actions (responsible persons)

1. Only the responsible person named on an action completes it. Other users cannot complete it.
2. The responsible person records the evidence (1 to 4000 characters) and, if available, a link to a system record that belongs to this CAPA.
3. An action cannot be completed twice. Completion fields cannot be cleared after they are recorded.
4. Completion is not signed.

## 7. Verifying completion (QA)

1. QA verifies completion only when every action is complete. The system names the first unfinished action and refuses otherwise.
2. QA signs **CAPA completion verified**. Status becomes CompletionVerified.

## 8. Effectiveness check (QA)

1. QA may sign the effectiveness check only on or after the planned date, by the laboratory's date.
2. QA selects the result, **Effective** or **Not effective**, and enters the findings (1 to 4000 characters).
3. **Effective:** QA signs **CAPA effectiveness checked**. Status becomes Closed.
4. **Not effective:** QA must name a new CAPA that was created with this CAPA as its source and is not cancelled. The system refuses the signature without it. QA signs the same step; status becomes Closed. The new CAPA carries the follow-up.

## 9. Cancellation (QA)

1. QA may cancel a CAPA that is not Closed or Cancelled. QA gives a reason and signs **Quality record cancelled**. The CAPA stays visible with status Cancelled.
2. [SITE: decide whether the owner or the creator may cancel; the system does not refuse it; see CONF-229.]

## 10. Due dates and extensions

1. The owner, or the responsible person for an action, may request an extension with a reason and a later date. The requester cannot decide the request.
2. QA approves or refuses the extension and signs **Due-date extension decided**. On approval the new date replaces the record's or the action's due date. Every request stays in the history.
3. [SITE: due rule for CAPA in working days; see CONF-237 for who may request an action extension.]

## 11. Records and evidence

- Each signed step is kept with the signer, the UTC time, the meaning and a snapshot of the content. A signed step cannot be edited afterwards.
- Evidence attachments are never deleted; a voided attachment keeps its reason.
- [SITE: state where effectiveness evidence is kept and how long.]

## 12. Open points for the site

- Effectiveness method and the time after which the check is due [SITE].
- Who may cancel a CAPA (CONF-229).
- Whether an OOS investigation or an audit trail finding is linked by a link or by a source record (the system accepts both; the template assumes a link).
