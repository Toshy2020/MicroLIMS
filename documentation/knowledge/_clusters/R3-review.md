# R3 Domain and GMP review — Review, approval, reports out (C3)

Reviewed from `C3-review-approval.md` (source of truth) with spot checks against code (SampleReviewService CompleteReview and ReturnToAnalyst, SampleApprovalService DecideCore, SampleReviewController, repository-wide search for "Final Release"). Application not run. Status key: DOCUMENTED / PARTIAL / NOT IMPLEMENTED / REQUIRES CONFIRMATION. Only values present in the cluster file or code are stated.

## Phase 5 domain checklist

### MOD-REVIEW-001 Technical review and return to analyst (Review and approval)
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Reviewer workflow | DOCUMENTED | WF-REVIEW-001, SCR-REVIEW-001, BR-REVIEW-001..006 | Per laboratory section, not per test order. One signature (Reviewed). |
| Automatic submit for review | DOCUMENTED | BR-REVIEW-001, BR-SAMPLE-012 | No password, no signature; event text "All tests completed - automatically submitted for review". |
| Return to Analyst | PARTIAL | WF-REVIEW-002, SCR-REVIEW-002, BR-REVIEW-007..011 | Unsigned, reason optional, no separation-of-duties check. UI offers it for count tests and every physicochemical test (GAP-300) (corrected in Phase 13, RECONCILIATION.md). Not available for location-grid, pathogen or qualitative tests in the UI. |
| Rejection / rework by reviewer | PARTIAL | BR-REVIEW-007 | A reviewer cannot reject. Rework before review is Return to Analyst only; after review only approver Reject/Retest. |
| Quick Table / Detailed review modes | NOT IMPLEMENTED | BR-REVIEW-013, GAP-018 | In the spec, not in the code. |
| Per-test-order review (spec) | NOT IMPLEMENTED | BR-REVIEW-012, GAP-018 | Section-level review replaces it; intended section-level rule REQUIRES CONFIRMATION. |
| Reviewer separation across sections | REQUIRES CONFIRMATION | GAP-312 | Check covers only the section being reviewed. |
| Status transitions | DOCUMENTED | statuses (sample, section, test order) | |

### MOD-APPROVAL-001 Approval, retest, new sample, close testing
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Section Head workflow | DOCUMENTED | WF-APPROVAL-001, SCR-APPROVAL-001 | Four decisions: Approve, Not Conform (Reject), New Sample Required, Retest Retained Sample. |
| Approval requirements | DOCUMENTED | BR-APPROVAL-001..005 | Section UnderApproval; approver not the reviewer; approver did not test; Approve needs passed system suitability where required and active analysis entries. |
| Approval of out-of-specification results | REQUIRES CONFIRMATION | BR-APPROVAL-025, GAP-304 | Screen warns; server does not block. |
| Rejection | PARTIAL | BR-APPROVAL-013, BR-APPROVAL-026, GAP-303 | Rejection sets the deciding section Rejected only; other sections continue. Service header comment says the whole sample; branch comment says otherwise. Code follows the branch comment. |
| Rework / retest | DOCUMENTED | WF-APPROVAL-002, BR-APPROVAL-006..009, 014..017 | Selected tests superseded and copied to a new sample (Retest Retained Sample: one; New Sample Required: two). |
| Retest outcome propagation | DOCUMENTED | BR-APPROVAL-021/022 | Origin outcome mirrored without a new signature. Origin test conformance defaults to conforming when no result exists. |
| Investigation / OOS Investigation decisions | NOT IMPLEMENTED | BR-APPROVAL-023/024, GAP-017 | Enum values exist; server refuses them; UI offers four options. |
| Close Testing (after another lab rejected) | DOCUMENTED | WF-APPROVAL-003, BR-APPROVAL-018 | Signed (TestingClosed). Intended text REQUIRES CONFIRMATION. |
| Final Release | NOT IMPLEMENTED | search of backend and frontend for "Final Release" / "FinalRelease": no match | There is no separate Final Release step. The Approve decision is presented as "Submit Release Decision"; the signature statement reads "I approve the release of this sample for its intended use." |
| Final report | PARTIAL | WF-COA-001, BR-APPROVAL-020, BR-COA-007 | A PDF of the summary is archived with a hash at each final decision; an on-demand export also exists (GAP-311). |
| Status transitions | DOCUMENTED | statuses | |

### MOD-COA-001 Sample report and Certificate of Analysis
| Checklist item | Status | Where | Note |
|---|---|---|---|
| Printable report | DOCUMENTED | SCR-COA-001 | No route guard; browser print. Footer says printed copies are uncontrolled. |
| Certificate of Analysis | DOCUMENTED | SCR-COA-002, BR-COA-001..005 | Page refuses non-final samples; the server API does not (GAP-306). |
| Signatory blocks | PARTIAL | BR-COA-004, GAP-307 | Multi-section variants print stored names and times, not signature rows. |
| Archived copy and integrity | PARTIAL | BR-COA-007, GAP-314 | Failed integrity check is a response header only; user-visible handling unknown. |
| Word export | NOT IMPLEMENTED (in UI) | GAP-310 | Endpoint only. |
| Intended report content | REQUIRES CONFIRMATION | WF-COA-001 | No specification. |

### MOD-OOS-001 OOS Tracking
| Checklist item | Status | Where | Note |
|---|---|---|---|
| OOS detection / group opening | PARTIAL | BR-OOS-001, BR-OOS-005, GAP-305 | A group exists only after a signed retest decision; an OOS result alone creates none. |
| Investigation documents | PARTIAL | WF-OOS-001, BR-OOS-006/007, GAP-313 | Upload and supersede work; void has no screen control; no signature; no review/approval of the document. |
| OOS record entity | NOT IMPLEMENTED | modules (MOD-OOS-001) | Group is the set of samples sharing OosGroupCode. |

## Phase 6 GMP / data-integrity facts

### MOD-REVIEW-001 Technical review and return to analyst
- Review requires password re-entry; the signature (meaning Reviewed), the review event and the status change are saved in one transaction, and a failed password writes nothing. [BR-REVIEW-004; SampleReviewService, verified]
- The review signature records user id, full name, username and role snapshots, meaning, entity, time, optional comment and IP address. [audit-trail]
- Review requires a person with `Samples.Review`; the system refuses the review if the reviewer is the assigned analyst, the sample preparer, or the author of any result, reading, observation, step result, analysis or confirmatory observation on any current test of that section. The rule applies to System Administrator as well. [BR-REVIEW-003; roles; SampleReviewService, verified]
- Reviewer separation is evaluated per section only; a reviewer who tested another section of the same sample is not blocked (GAP-312).
- Result modification after entry: a reviewer may return a count test (UI) or an analysis test (service) that is at Status ResultEntered. The system sets the active count readings inactive (kept, not deleted), reopens the latest incubation, writes a TestReturnEvent (test order, reviewer, assigned analyst, reason, time), writes a history note and, if the section was Under review, moves it back to In testing. [BR-REVIEW-010; WF-REVIEW-002]
- Return to analyst takes no password and no electronic signature, the reason is optional, and no separation-of-duties check is made; a reviewer can return a test they performed. [BR-REVIEW-011; SampleReviewService return path, verified; GAP-009, GAP-301]
- Return to analyst is refused once the section is Under approval or later: "Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead." [validations; SampleReviewService, verified]
- The sample route id is not compared with the test order's sample; section scope is checked on the test order only. [BR-REVIEW-011]
- The assigned analyst is unchanged by a return; how the analyst is informed was not traced (a returned-tests notification query exists). [WF-REVIEW-002]
- Time gate: none for review. Material-expiry check: none in the review path. [WF-REVIEW-001]

### MOD-APPROVAL-001 Approval, retest, new sample, close testing
- Approval requires password re-entry and a meaning; the signature is written before the status change and a failed password changes nothing. The signature meaning is Approved (Approve), Rejected (Reject and New Sample Required) or RetestRequested (Retest Retained Sample). [BR-APPROVAL-010]
- Approval requires `Samples.Approve` (System Administrator, Section Head by default). The approver may not be the section's reviewer and may not have performed any current test of the section; both rules apply to System Administrator. [BR-APPROVAL-002/003; SampleApprovalService, verified]
- Approval is not blocked when results are out of specification; the screen shows a warning that approval certifies them as conforming. [BR-APPROVAL-025]
- Approve is refused when a test needing system suitability has no linked passed HPLC run, or an analysis-type test lacks an active entry or has a pending stage. [BR-APPROVAL-004/005]
- Segregation of duties between analyst, reviewer and approver: three separate people are required per section (analyst-side performer, reviewer, approver). No role bypasses it. The check compares user ids only. [roles; BR-REVIEW-003; BR-APPROVAL-002/003]
- Retest / new-sample spin-offs: the originals of the selected tests are marked superseded (not deleted) with a history note naming the signer; the new sample is linked by OriginSampleId and shares OosGroupCode; identity fields are copied; preparation and test orders are not copied (new test orders are created). [BR-APPROVAL-014..016]
- For New Sample Required, the two analysts must be different, active, unlocked, Analyst role type and not the assigned analyst of a selected test. People who entered results but were not the assigned analyst are not checked, although the screen says "neither may be whoever tested the original sample". No section-scope check applies. [BR-APPROVAL-007/008; GAP-302]
- The signature for New Sample Required has meaning Rejected, and the certificate/trail show a rejection for a decision that spawns new samples. [GAP-308]
- When a retest sample reaches a final status, the outcome is copied to the origin section without a new signature, and the origin is archived again. [BR-APPROVAL-021]
- Close Testing requires a reason and password; open tests become Cancelled with the step and stage recorded; Approved, Voided and Cancelled tests are left alone. No reviewer/performer separation check exists on this path. [BR-APPROVAL-018; WF-APPROVAL-003]
- Final Release: no function, screen, status or signature named "Final Release" exists. After Approve, the next and last state is Approved, followed by report/CoA printing; no further QA release or batch disposition step is implemented. [repository search; statuses]
- Review and approval are not time-gated and no material-expiry check exists in either path. [WF-REVIEW-001, WF-APPROVAL-001]

### MOD-COA-001 Sample report and Certificate of Analysis
- The report and certificate pages have no route-level permission guard; the summary API requires sign-in and a per-sample section access check. Archived-copy download requires sign-in only. [roles]
- Single-section certificate: reviewer and approver blocks read the signature rows. Multi-section combined and section certificates print stored names and times and never read signature rows. A printed name may therefore appear without a signature row being consulted. [BR-COA-004; GAP-307]
- Certificate remarks print exactly as typed; blank prints "No remarks."; the internal comment never prints. [BR-COA-003]
- At each final decision a PDF of the summary is archived with a SHA-256 hash; an archive failure is logged and the decision stands; on a hash mismatch the download still returns the file and only sets the header "X-Archive-Integrity: FAILED". [BR-COA-007; GAP-314]
- The on-demand "Export PDF" renders the current data and can differ from the archived copy; which is the controlled copy is not defined. [GAP-311]
- Signature statements differ between the certificate, the report page and the approval card; the trail has no statement for the close-testing signature. [GAP-315]
- The report and CoA are forced to the light theme; printed copies are marked uncontrolled in the footer. [BR-COA-005; SCR-COA-001/002]

### MOD-OOS-001 OOS Tracking
- Opening an OOS group is a side effect of a signed retest decision; an out-of-specification result by itself creates no OOS record, and the OOSInvestigation decision is never written. [BR-OOS-001/005; GAP-305]
- Investigation documents record uploader and time; supersede and void record reason, user and time on the document; the file hash (SHA-256) is rechecked on download. No electronic signature, no review or approval and no separation-of-duties check apply. Void has no screen control. [BR-OOS-006/007; GAP-313]
- Upload is limited to PDF, JPG, JPEG, PNG, WEBP, TIFF, 25 MB as configured; no malware scanner is integrated. [BR-OOS-006]

### Cross-cutting (C2 and C3)
- Audit trail: review completion, approval decision and close testing each write a review workflow event plus a signature; return to analyst writes a TestReturnEvent; automatic submit for review writes an event without a signature; retest and approval write test-order history notes. Automatic change capture on save is described in the specification only and was not verified here. [audit-trail]
- Record history: superseded and inactive records are kept (superseded test orders, inactive readings, superseded documents). Session reset and void are the exceptions: reset deletes data (C2); void keeps rows but marks them Voided. [traceability; C2 BR-PATH-012]
- Void and reject are different: void sets Voided and requires `Samples.Correct`; reject is an approver decision. [C2 BR-SAMPLE-007; BR-APPROVAL-013]
- Session security and password rules: see C1.

## Mock / placeholder / unverified data
- No mock data found in the C3 screens read.
- Not read: `ResultController` response shape (GET /api/results, no UI caller); whether the report page shows the archive integrity header; physicochemical review screens (HPLC, ICP send-for-review) — it is UNCLEAR whether they use this review path.
- Specification references (BusinessRules.md §8, §9, §12; WorkflowDiagrams.md) describe a per-test-order design that differs from the code (GAP-017, GAP-018, GAP-309); the code is documented as IMPLEMENTED, the spec text as INTENDED, never merged.
- Unverified: automatic change capture on save (BusinessRules.md §11).

## Guide callouts
1. Review and approval are decided per laboratory section, not per test order; Quick Table review and Investigation / OOS Investigation decisions in the specification are not available. (GAP-017, GAP-018)
2. There is no Final Release step; Approved is the last status, shown as a "Release Decision" on the screen. (GAP-017, GAP-018; new confirmation item)
3. Return to Analyst takes no password or signature, the reason is optional, and the reviewer who performed the test is not blocked. (GAP-009, GAP-301)
4. Return to Analyst is offered in the UI for count tests and every physicochemical test; whether this is intended is open. (GAP-300) (corrected in Phase 13, RECONCILIATION.md)
5. A reviewer cannot return a test after the section is reviewed; correction then requires the approver's Reject or Retest. (BR-REVIEW-007)
6. Reviewer and approver separation is checked per section only. (GAP-312)
7. Approval of out-of-specification results is allowed after a warning; it certifies them as conforming. (GAP-304)
8. "Not Conform (Final Conclusion): Close this sample" rejects the deciding section only; other laboratories continue; the code comments contradict each other. (GAP-303)
9. For New Sample Required the screen says neither analyst may be anyone who tested the original sample; only the assigned analyst of the selected tests is checked. (GAP-302)
10. New Sample Required and the retest decisions are signed with meanings Rejected and RetestRequested; the trail and certificate show a rejection for New Sample Required. (GAP-308)
11. Outcome of a retest is mirrored to the origin sample without a new signature. (BR-APPROVAL-021, new confirmation item)
12. An out-of-specification result by itself does not create an OOS record; only a signed retest decision does. (GAP-305)
13. OOS investigation documents are not signed, have no review step, and cannot be voided from the screen. (GAP-313)
14. Multi-laboratory certificates print stored names and times rather than signature records. (GAP-307)
15. The certificate page is available only for approved or rejected samples; the underlying API does not enforce this. (GAP-306)
16. Two PDFs exist (archived at final decision, on-demand export); which one is the controlled record is not defined. (GAP-311)
17. Archive integrity failure is signalled by a response header only; no on-screen warning is confirmed. (GAP-314)
18. Approval, rejection and close-testing signature statements are worded differently on the certificate, the report and the approval card. (GAP-315)
19. Dashboard review and approval counts are per sample, not per test order as in the specification. (GAP-309)
20. The Word export of the summary has no screen. (GAP-310)

## Corrections made to the cluster file
None. Spot checks (review SoD, approval SoD, return-to-analyst preconditions and no signature, absence of Final Release) agreed with C3.
