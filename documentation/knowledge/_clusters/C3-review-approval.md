# C3 Review, approval, reports out

## modules

### MOD-REVIEW-001 Technical review and return to analyst
- Purpose: a reviewer signs a technical review per laboratory section once every current test in that section is Ready [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L13-L17, L98-L155]. IMPLEMENTED.
- Auto-submit: when the last test of a section reaches Ready, the section moves to UnderReview with event text "All tests completed - automatically submitted for review". Only runs while the sample status is Received or InTesting, and only for a section that has at least one current test and is still InTesting [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L43-L55, L72-L93]. IMPLEMENTED (verified).
- Sub-features: "Submit Technical Review" (signed); "Return to Analyst" (unsigned) [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:235-283; frontend/src/modules/testingWorkspace/sampleSummary/TestResultsSection.tsx:109-129].
- Lab scope: section-scoped (GetAccessibleSectionIdsAsync; null means all sections) [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L103-L104]. Physicochemical review screens (HPLC, ICP send-for-review) are not in this cluster; UNCLEAR whether they use this path.
- Spec status: INTENDED design differs (per test order, Quick Table review). DISCREPANCY, see GAP-018.

### MOD-APPROVAL-001 Approval, retest, new sample, close testing
- Purpose: a Section Head signs a release decision per section: Approve, Reject, Retest Retained Sample, New Sample Required (NewSampleRequest) [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L12-L19, L136-L141]. IMPLEMENTED.
- Close testing: after another section rejects the sample, a section may cancel its own open tests with a signed reason [src: backend/MicroLIMS.Application/Services/SectionClosureService.cs:L14-L22, L31-L100]. IMPLEMENTED.
- Lab scope: section-scoped, same scope service as review [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L152-L153].
- Spec status: BusinessRules.md section 9 describes per-test-order approval and two more decisions (Investigation, OOSInvestigation). DISCREPANCY, see GAP-017, GAP-018.

### MOD-COA-001 Sample report and Certificate of Analysis
- Printable sample summary report page, Certificate of Analysis page, server PDF export of the summary, and frozen archived PDF written at the final decision [src: frontend/src/routes/AppRoutes.tsx:102-103; backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L44-L51; backend/MicroLIMS.Application/Services/RecordArchiveService.cs:L12-L20]. IMPLEMENTED.
- Status: the application map lists this as PARTIAL; the full SampleCoaPage.tsx and SampleReportPage.tsx were read in Phase 3 and are described under SCR-COA-001 and SCR-COA-002 (verified).
- Lab scope: report and CoA routes have no route guard; server summary is scoped by section for non-final samples [src: backend/MicroLIMS.Application/Services/SampleSummaryService.cs:L84-L90].

### MOD-OOS-001 OOS Tracking
- Purpose: groups every retest spin-off of an OOS chain under one OosGroupCode, and holds lab investigation documents for the group [src: backend/MicroLIMS.Application/Services/OosTrackingService.cs:L11-L24]. IMPLEMENTED.
- There is no separate OOS record entity. The group is the set of Sample rows sharing OosGroupCode; the only other OOS entity is OosInvestigationDocument [src: backend/MicroLIMS.Domain/Entities/OosInvestigationDocument.cs:L12-L43; git grep of backend/MicroLIMS.Domain/Entities].
- Lab scope: list filtered by section when the caller is section-scoped [src: backend/MicroLIMS.Application/Services/OosTrackingService.cs:L24-L34].

### MOD-RESULT-001 Results list (backend)
- GET /api/results, class-level [Authorize] only. No UI caller found in this cluster [src: backend/MicroLIMS.API/Controllers/ResultController.cs:L10-L22]. Response shape: not read; no screen in this cluster uses it (UNCLEAR, out of scope).

## screens

### SCR-REVIEW-001 Sample Summary (dialog)
- Opened from: lifecycle badge on the testing workspace. UnderReview badge is clickable only with Samples.Review; UnderApproval badge only with Samples.Approve; Approved always; InTesting and Rejected not clickable. The badge has labels only for InTesting ("Under Testing"), UnderReview, UnderApproval, Approved and Rejected; any other sample status shows no badge. The badge is not interactive on the Sample Receiving list [src: frontend/src/modules/testingWorkspace/SampleLifecycleBadge.tsx:8-14, 27-33, 40-56]. (verified) Also a "Sample Summary" button [src: documentation/knowledge/_raw/frontend-testing-workspace.md:L63-L72]. The dialog also has full-width buttons "Open Printable Report" and "View Certificate of Analysis"; "View COA" / "View Certificate of Analysis" show only when certificate results exist and either the single-section flag combinedCoaAvailable is set or (multi-section) Combined or a section certificate is eligible [src: SampleSummaryDialog.tsx:333-360, 417-441, 548-575]. (verified)
- Title "Sample Summary" [src: frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx:357-361].
- Header buttons: "Open Pathogen Workflow" (only if a test uses a shared TSB); "Printable Report" (opens /samples/{id}/report); "View COA" (only when coaEligible); "Export PDF" / "Exporting…" [src: SampleSummaryDialog.tsx:388-442].
- Section chips "Sections:" with "{section}: {status}". Labels: In testing, Under review, Under approval, Approved, Rejected, Retest requested, Cancelled, Closed, Voided [src: frontend/src/modules/testingWorkspace/sampleSummary/summaryFormatting.ts:1-26]. Closed text "{section} closed by {name} on {date}: {reason}" [src: SampleSummaryDialog.tsx:470-477].
- "Workflow History" strip, empty text "No lifecycle events yet." [src: SampleSummaryDialog.tsx:39-51].
- Footer "Sample Reference: {ref} · Record Status: {status} · MicroLIMS Operational View" [src: SampleSummaryDialog.tsx:591-593].
- Permission flags: canReview = a section of the user is UnderReview AND Samples.Review; canReturn = Samples.Review; canApprove = a section of the user is UnderApproval AND Samples.Approve; canCloseTesting = overall "Rejected" AND a closable section AND Samples.Approve [src: SampleSummaryDialog.tsx:262-278]. (verified) These are UI gates only; the server enforces its own checks (see roles).
- Signing flow: "Submit Review" and "Submit Decision" open the shared SignatureDialog (password). The review dialog shows the statement "By submitting, I confirm I have reviewed all test results for {section}." (or "... for this sample."); the decision dialog shows the selected radio label as its statement. On success the Sample Summary dialog closes [src: SampleSummaryDialog.tsx:226-238, 612-630]. (verified)
- Errors: "Failed to load sample summary." [src: SampleSummaryDialog.tsx:189]; "Export failed." [src: SampleSummaryDialog.tsx:290].
- Empty/blocked: UNCLEAR for a sample with no section the user can act on beyond the flags above.

### SCR-REVIEW-002 Return Test to Analyst (dialog)
- Opened from "Return to Analyst" in the test results section. Button shown only when canReturn, the test is not superseded, status is ResultEntered, and the test is a count test [src: frontend/src/modules/testingWorkspace/sampleSummary/TestResultsSection.tsx:33-45, 109-129].
- Title "Return Test to Analyst"; subtitle "Return count test for recount or result re-entry" [src: frontend/src/components/ReturnToAnalystDialog.tsx:62-136].
- Field "Reason for Return (optional)", placeholder "e.g. Plate count recount requested due to bubble artifact" [src: ReturnToAnalystDialog.tsx:62-136].
- Buttons "Cancel", "Confirm Return" / "Returning...". No password step [src: SampleSummaryDialog.tsx:232-238; frontend/src/modules/testingWorkspace/services/SampleSummaryService.ts:42-47]. (verified)
- Info text: "Returning this test order will soft-supersede active count readings, reopen the incubation step, and return the test to the assigned analyst. If the parent sample is Under Review, it will revert to In Testing." On failure the server message is shown, fallback "Failed to return test to analyst." [src: frontend/src/components/ReturnToAnalystDialog.tsx:L26-L28, L125-L126]. The reason is trimmed; blank is sent as no reason. After success the dialog closes and the summary reloads [src: SampleSummaryDialog.tsx:232-238]. (verified)

### SCR-APPROVAL-001 Approval & Electronic Signatures (card, inside Sample Summary)
- Card title "Approval & Electronic Signatures" [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:143-145].
- Signature trail lines "{role}: {printedName} ({username})" with chip "Verified" and a meaning statement [src: ApprovalSignaturesCard.tsx:153-193]. Fallback blocks "Reviewer:" / "Section Head:" when no signature row [src: ApprovalSignaturesCard.tsx:196-222].
- Empty states: "Awaiting technical review by authorized reviewer." (UnderReview); "Reviewed. Awaiting release approval decision by Section Head." (UnderApproval); "No formal signatures recorded." [src: ApprovalSignaturesCard.tsx:225-231].
- Review form "Submit Technical Review":
  - Select "Laboratory Section" only when more than one section is reviewable [src: ApprovalSignaturesCard.tsx:240-257].
  - Alert "By submitting, I confirm I have reviewed all test results for {section}." (or "... for this sample.") [src: ApprovalSignaturesCard.tsx:258-262].
  - Field "Comment (optional)" [src: ApprovalSignaturesCard.tsx:263-272].
  - Button "Submit Review"; disabled when more than one section is reviewable and none chosen [src: ApprovalSignaturesCard.tsx:273-281]. Opens SignatureDialog [src: SampleSummaryDialog.tsx:612].
- Release form "Submit Release Decision":
  - Select for approvable sections (when more than one) [src: ApprovalSignaturesCard.tsx:291-308].
  - Field "Comment (optional)" [src: ApprovalSignaturesCard.tsx:309-318].
  - Radio options (label and helper text): "Approve: Results conform to specifications"; "Not Conform (Final Conclusion): Close this sample"; "New Sample Required: Close and request new sample"; "Retest Retained Sample: Return to testing" [src: frontend/src/modules/testingWorkspace/sampleSummary/summaryFormatting.ts:73-78; ApprovalSignaturesCard.tsx:319-332].
  - Field "Certificate Remarks (optional)", shown only for Approve. Helper: "Printed on the Certificate of Analysis exactly as typed. Leave blank to show "No remarks." Separate from the internal comment above, which never appears on the certificate." [src: ApprovalSignaturesCard.tsx:333-345].
  - Retest block (Retest Retained Sample or New Sample Required): heading "Tests to Retest"; text "Non-conforming tests are pre-checked. Adjust as needed - only the checked test(s) move to the new sample(s); everything else on this sample is left untouched."; one checkbox per non-superseded test with section chip and "Non-conforming" chip [src: ApprovalSignaturesCard.tsx:347-408]. Warning "Select at least one test to retest." [src: ApprovalSignaturesCard.tsx:402-406].
  - New Sample block (New Sample Required only): heading "Analysts for the Two New Samples"; text "Two different analysts are required, and neither may be whoever tested the original sample."; selects "New Sample 1 - Analyst" and "New Sample 2 - Analyst"; warning "The two new samples must be assigned to two different analysts." [src: ApprovalSignaturesCard.tsx:410-457]. Load error "The list of eligible analysts could not be loaded. Close and reopen this sample to try again." [src: ApprovalSignaturesCard.tsx:418-422].
  - Approve with out-of-spec tests: warning "Out-of-specification results in {section}: {tests}. Approving will certify them as conforming - consider Not Conform or a retest." [src: ApprovalSignaturesCard.tsx:459-463, logic 101-136].
  - Button "Submit Decision", disabled when the decision is not valid (section chosen when more than one; at least one test for retest decisions; two distinct analysts for New Sample Required) [src: ApprovalSignaturesCard.tsx:85-92, 465-473]. Opens SignatureDialog with the decision label as meaning text [src: SampleSummaryDialog.tsx:623-630].
- Close Testing block (only when canCloseTesting) [src: ApprovalSignaturesCard.tsx:477-518]: title "Close Testing"; warning "Another laboratory has rejected this sample. You may close your own laboratory's remaining tests instead of continuing them."; section select when more than one; button "Close Testing".

### SCR-APPROVAL-002 Close Testing (dialog)
- Title "Close Testing: {section}"; subtitle "Stops your laboratory's remaining tests on this sample - signed with your password" [src: frontend/src/modules/testingWorkspace/dialogs/CloseTestingDialog.tsx:67-98].
- Text "Every still-open test in this laboratory will be recorded as Cancelled at the stage it had reached. Approved results already on file are kept." [src: CloseTestingDialog.tsx:117-120].
- Field "Reason" (required, multiline), default "Sample rejected by {lab}" [src: CloseTestingDialog.tsx:37-41, 121-130].
- Buttons "Cancel", "Sign & Close" (disabled when reason empty). Then SignatureDialog with meaning "I am closing this laboratory's testing; it will not be resumed on this sample." [src: CloseTestingDialog.tsx:101-113, 133-137].

### SCR-COA-001 Sample summary report (printable page)
- Route /samples/:id/report; no route-level permission guard. Component SampleReportPage [src: frontend/src/routes/AppRoutes.tsx:102; frontend/src/modules/testingWorkspace/SampleReportPage.tsx:108]. (verified)
- Loading "Loading report…"; error "Failed to load the sample report." [src: SampleReportPage.tsx:119, 130]. Archived copies load failure: "The archived copies of this record could not be loaded. Reload the page to try again." [src: SampleReportPage.tsx:122]. Browser tab title "Sample Summary - {reference}" [src: SampleReportPage.tsx:126].
- Document heading "sample summary report"; header badge shows the sample status with labels Received, Under testing, Under review, Under approval, Approved, Rejected (other statuses show the raw value) and "Received {date time}" [src: SampleReportPage.tsx:78-81, 144-157].
- Timeline steps: Received, In testing, Under review, Under approval, then Voided (danger) or Rejected / Approved. "In testing" time = earliest incubation start; "Under review" = time of the SubmittedForReview event; "Under approval" = sample ReviewedAt; final step = sample ApprovedAt or the ApprovalDecisionMade event, only when status is Approved or Rejected [src: SampleReportPage.tsx:87-106]. (verified)
- Section labels: "sample identity", "dates & personnel", "sample preparation", "test results", "workflow timeline", "electronic signatures", plus a test summary matrix (SummaryMatrix) and per-test cards [src: SampleReportPage.tsx:163, 190, 205, 235-242, 250, 274]. The "electronic signatures" block renders only when signatures exist [src: SampleReportPage.tsx:271].
- Sample identity keys: Reference, Category, Item, Prev. Product and Prev. Batch (After Cleaning only), Stage, Batch (not After Cleaning), Control, Cause, Quantity, Sampling point, Storage [src: SampleReportPage.tsx:165-186]. Dates and personnel keys: Manufactured, Expires, Received by, Received at, Sampled by, "Reviewed by" and "Approved by" (shown only when set) [src: SampleReportPage.tsx:192-198].
- Sample preparation block (only when a preparation exists): Amount, Technique (with Filtration and Washing volumes for filtration), Diluent, Neutralizer, Prepared by [src: SampleReportPage.tsx:203-232].
- Signature cards show printed name, @username, role, time, "Meaning: {text}" and the optional comment in quotes [src: SampleReportPage.tsx:278-294]. The meaning texts on this page are listed under terminology.
- Empty test results: "No tests recorded." [src: SampleReportPage.tsx:244].
- Button "Print / Save PDF" calls window.print(); footer "This is a controlled document generated by MicroLIMS. Any printed copy is uncontrolled." [src: SampleReportPage.tsx:307, 312-318].
- Archived copies section with download [src: SampleReportPage.tsx:303].

### SCR-COA-002 Certificate of Analysis (page)
- Route /samples/:id/coa; no route-level permission guard. Component SampleCoaPage, loads the summary with forCertificate=true [src: frontend/src/routes/AppRoutes.tsx:103; frontend/src/modules/testingWorkspace/SampleCoaPage.tsx:273-284]. (verified)
- Loading "Loading certificate…"; error "Failed to load the certificate of analysis." [src: SampleCoaPage.tsx:283, 345].
- Single-section sample, not final: "A Certificate of Analysis is only available once this sample has been approved or rejected. Current status: {status}." (shown when the server flag combinedCoaAvailable is false; the server sets it when the sample status is Approved or Rejected) [src: SampleCoaPage.tsx:358-366; backend/MicroLIMS.Application/Services/SampleSummaryService.cs:L342]. Final but no result data: "A Certificate of Analysis is not available for this sample - none of its tests have recorded results yet." [src: SampleCoaPage.tsx:369-376].
- Multi-section sample with no certificate variant available: Alert "Certificate of Analysis Not Available"; text "A Certificate of Analysis is not available yet. The following laboratories are not final yet:" with "{section}: {status}" lines for sections still InTesting, UnderReview or UnderApproval; if none are open the text is "No approved laboratory sections are currently accessible." [src: SampleCoaPage.tsx:380-411].
- Variants (multi-section only): "Certificate Variant:" with toggle buttons "Combined" (needs combinedCoaAvailable AND every section visible to the viewer) plus one button per section the viewer can see whose section status is Approved (server CoaAvailable = section Approved). A Rejected or closed section therefore has no section-only certificate [src: SampleCoaPage.tsx:286-315, 429-471; backend/MicroLIMS.Application/Services/SampleSummary/SampleSummarySectionsBuilder.cs:L29]. (verified) Default variant is Combined if eligible, else the first eligible section. The toggle bar is hidden when printing (class no-print).
- Header: title "Certificate of Analysis" (or "Certificate of Analysis - {section}"); sub-line category, item name, "Batch: {batch}"; right side reference and "Generated {date time}" [src: SampleCoaPage.tsx:419-487].
- Water/EM/After Cleaning style (location matrix): id strip Reference, Category, "Batch / Control", Received; heading "Test Results by Location"; matrix columns "Location" then per test Alert, Action, Spec, Result (qualitative tests share one "Spec: Absent / 10 mL" cell); footnote about units and absence [src: SampleCoaPage.tsx:159-222, 494-503].
- Other categories (simple style): item strip with Qty and a dates grid labelled Batch No., Mfg. Date, Exp. Date, "Sampling / Arrival", Test Date, Result Date, "QC No.", Certificate Date; heading "Test Results"; table "Test", "Specification", "Sample Result", "Analyst" (analyst name and time; "via retest {reference}" under tests carried from a retest) [src: SampleCoaPage.tsx:224-271, 506-527].
- Remarks: heading "Remarks" (combined variant: "Remarks ({section})"); fallback text "No remarks." [src: SampleCoaPage.tsx:529-537, 633-639]. Combined variant headings "{section} Test Results", "No recorded results for this section."; cancelled tests listed as "{test}: Not completed, testing closed ({step}, stage {n})." [src: SampleCoaPage.tsx:608-631].
- "Overall Conclusion" box with server-built conclusion text (red styling when the certificate does not comply) [src: SampleCoaPage.tsx:541-550, 648-651].
- Signature blocks: single-section sample: Reviewer block (last "Reviewed" signature row, else stored name and time) and Approver block (last Approved or Rejected signature row; for a sample whose decision was RetestRetainedSample or NewSampleRequest no signature row is used, only the stored name and time). Multi-section variants never read signature rows: the combined variant prints one block per section ("Approver: {section}", "Rejected by {section}" or "Testing closed by {section}", with "Reason: {reason}" for closed) from stored names; the section variant prints Reviewer (if a reviewer name exists) and Approver from stored names [src: SampleCoaPage.tsx:413-417, 552-555, 661-688, 760-783]. (verified)
- Footer: "This Certificate of Analysis is a controlled document generated by MicroLIMS. Any printed copy is uncontrolled." plus a line pointing to the Sample Summary Report by Document ID; button "Print / Save PDF" (window.print, hidden when printing) [src: SampleCoaPage.tsx:789-802].
- Print layout: A4 portrait, zero page margin, padding 18mm 16mm 22mm 16mm [src: SampleCoaPage.tsx:43-50].

### SCR-OOS-001 Out-of-Specification Tracking
- Route /oos-tracking; guard Oos.Manage [src: frontend/src/routes/AppRoutes.tsx:216-217; frontend/src/modules/oosTracking/OosTrackingPage.tsx:64].
- Title "Out-of-Specification Tracking"; subtitle "Every out-of-specification investigation chain grouped by OOS number, tracing all retest spin-offs back to the original sample." [src: OosTrackingPage.tsx:104-105].
- Section "OOS Investigation Groups"; summary "1 OOS Group" or "N OOS Groups (M retest samples)" [src: OosTrackingPage.tsx:111-116]. Empty: "No active OOS investigation chains found." [src: OosTrackingPage.tsx:134].
- Per group: chip "Lab Investigation Report Attached" or "No Report Attached"; button "Manage Investigation" (when a document exists) or "Upload Investigation" [src: OosTrackingPage.tsx:223-247].
- Retest table headers: "Retest Sample", "Parent / Origin", "Item / Point", "Retest Type", "Test(s)", "Analyst(s)", "Retest Status", "Opened", "Actions" [src: OosTrackingPage.tsx:258]. Retest type labels "Retest Retained Sample", "New Sample Request" [src: OosTrackingPage.tsx:45-46]. Empty: "No retest samples have been spun off yet for this OOS chain." [src: OosTrackingPage.tsx:270]. Row action tooltip "Open the retest sample's printable report" [src: OosTrackingPage.tsx:339-351].
- Investigation documents dialog, title "Lab Investigation Documents: {oosGroupCode}" [src: OosTrackingPage.tsx:486]. Actions: "Select File", "Upload", "Choose Replacement File", "Supersede"; field "Reason for Supersession *"; info "The current document will be marked Superseded. A new active version will take its place." [src: OosTrackingPage.tsx:551-555].
- Document table headers "Status", "Filename", "Size", "Uploaded By", "Date", "Actions"; status chips "Current", "Superseded", "Voided"; empty "No investigation documents uploaded for this OOS group yet." [src: OosTrackingPage.tsx:609, 627, 640-646]. Voided shows "Voided: {voidReason}" [src: OosTrackingPage.tsx:656-658]. Tooltips "Download file", "Supersede with replacement report" [src: OosTrackingPage.tsx:673-679].
- Void control: the server endpoint and the frontend service method voidDocument exist, but OosTrackingPage.tsx has no caller and no button for them; a user cannot void a document from the screen (a Voided chip can still appear if a document was voided another way) [src: backend/MicroLIMS.API/Controllers/OosInvestigationDocumentController.cs:L96-L105; frontend/src/modules/oosTracking/services/OosTrackingService.ts:92; frontend/src/modules/oosTracking/OosTrackingPage.tsx]. (verified by search of the page)
- Documents dialog behaviour: the heading "Upload Initial Investigation Report" with "Select File" / "Upload" shows only when no Current document exists (including when all are Voided); the supersede icon shows only on the Current row and only while no supersede panel is open; "Supersede" is disabled until a replacement file and a non-blank reason exist [src: OosTrackingPage.tsx:481, 503-540, 594, 677-683]. (verified)
- Accepted file types .pdf, .jpg, .jpeg, .png, .webp, .tiff in the file chooser; the server enforces its own checks (see validations) [src: OosTrackingPage.tsx:513, 569].
- Errors: "Failed to load OOS tracking items."; "Failed to load investigation documents."; "Failed to upload document."; "Failed to download document content."; "Failed to supersede document." [src: OosTrackingPage.tsx:79, 414, 436, 454, 475].

### Shared: SignatureDialog (password re-entry)
- Title "Electronic Signature"; shows the signer's full name (or username) and role, an info box with the meaning statement, and the field "Password" (required, Enter submits). Buttons "Cancel", "Sign" / "Signing..." (disabled when password empty or submitting). On failure the server message is shown (fallback "Signature failed."), the password is cleared and the dialog stays open. A "Comment (optional)" field exists only when the caller sets showComment; the review and decision dialogs do not, so their comment is the one typed on the form [src: frontend/src/components/SignatureDialog.tsx:20-26, 33-36, 52-84; SampleSummaryDialog.tsx:612-630]. (verified; corrected from the extractor, which listed the comment field as always present)

## roles

### MOD-REVIEW-001 Technical review and return to analyst
- Server gate: SampleReviewController is class-level Policy Samples.Review [src: backend/MicroLIMS.API/Controllers/SampleReviewController.cs:L19]. Return endpoint also checks EnsureTestOrderAccessAsync for the test order's section [src: SampleReviewController.cs:L48-L54].
- Permission: Samples.Review, Enforced. Default grants: SystemAdministrator Y (all 50 codes), SectionHead Y, Reviewer Y, Analyst - [src: documentation/knowledge/_raw/verified-roles-permissions.md:L85-L88]. (verified; the extractor's L428 reference pointed at an empty line)
- UI gate: canReview / canReturn flags as in SCR-REVIEW-001.
- Analyst-only filter: an Analyst-type user sees only own tests unless scope is "all" [src: documentation/knowledge/_raw/verified-roles-permissions.md:L110].
- Section scope errors: "This test belongs to a laboratory section you are not assigned to." [src: backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:L130, L142].
- SoD (sample review): "You cannot review a sample you tested." Applies to every current test in the section being reviewed [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L109-L112; SegregationOfDutiesGuard.cs:L25-L54]. Performer = assigned analyst, sample preparer, any result/count reading/analysis/pathogen observation/workflow step result/confirmatory plate observation on that order [src: verified-roles-permissions.md:L120].
- Guard is role-agnostic: no role bypasses it, including System Administrator [src: verified-roles-permissions.md:L118].

### MOD-APPROVAL-001 Approval, retest, new sample, close testing
- Server gate: SampleApprovalController is class-level Policy Samples.Approve [src: backend/MicroLIMS.API/Controllers/SampleApprovalController.cs:L34]. Routes: POST /api/samples/{id}/approval/decide; POST /api/samples/{id}/approval/sections/{sectionId}/close [src: SampleApprovalController.cs:L48, L58].
- Permission: Samples.Approve, Enforced. Default grants: SystemAdministrator Y, SectionHead Y, Reviewer -, Analyst - [src: verified-roles-permissions.md:L85-L88, L90]. (verified; L429 in the extractor's draft was an empty line)
- Close testing scope: "This test belongs to a laboratory section you are not assigned to." [src: backend/MicroLIMS.Application/Services/SectionClosureService.cs:L43-L45]. Close testing is on the same controller, so it also needs Samples.Approve [src: SampleApprovalController.cs:L34, L58]. (verified)
- Section scope: GetAccessibleSectionIdsAsync returns null for unrestricted users; when not null, the section must be in the list or the action is refused [src: SampleSectionRollup.cs:L129-L130, L140-L142]. (verified)
- SoD (sample approval):
  - "You cannot approve a sample you reviewed." (approver equals the section's reviewer) [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L162-L163].
  - "You cannot approve a sample you tested." (approver performed any test in the section) [src: SampleApprovalService.cs:L167-L170].
  - Reviewer/approver rule applies to System Administrator too [src: verified-roles-permissions.md:L90, L118].
- Retest analyst rules: both new-sample analysts must be active, not locked, Analyst role type [src: SampleApprovalService.cs:L272-L277]. Analyst may not be the AssignedAnalystId of a selected test [src: SampleApprovalService.cs:L279-L283]. Section scope is not applied to the two analysts (see GAP-302).

### MOD-COA-001 Sample report and Certificate of Analysis
- Report and CoA pages: no route guard [src: frontend/src/routes/AppRoutes.tsx:102-103].
- Summary API: class-level Authenticated; per-sample access check EnsureSampleAccessAsync [src: backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L16-L19, L31-L33].
- Signature trail endpoint GET /api/signatures requires Signatures.Manage [src: backend/MicroLIMS.API/Controllers/SignaturesController.cs:L15, L25]. Permission Signatures.Manage: SectionHead Y, SystemAdministrator Y, Reviewer -, Analyst - [src: verified-roles-permissions.md:L85-L88]. (verified) The report and CoA pages do not call this endpoint; they read signatures from the summary [src: SampleReportPage.tsx:117; SampleCoaPage.tsx:281].
- Archived records API: class-level [Authorize] only [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L16, L26, L41]. Archived copy download is not permission-gated beyond sign-in.
- Summary PDF/Word endpoints: Authenticated + sample access check [src: SampleSummaryController.cs:L44-L53].

### MOD-OOS-001 OOS Tracking
- Controller: OosTrackingController and OosInvestigationDocumentController, class-level Policy Oos.Manage [src: backend/MicroLIMS.API/Controllers/OosTrackingController.cs:L15-L16; OosInvestigationDocumentController.cs:L14-L15].
- Permission Oos.Manage, Enforced. Default grants: SystemAdministrator Y, SectionHead Y, Reviewer -, Analyst - [src: verified-roles-permissions.md:L85-L88]. (verified; L466 in the draft was an empty line)
- Every document endpoint (list, upload, content, supersede, void) first calls EnsureOosGroupAccessAsync for the caller and group code [src: OosInvestigationDocumentController.cs:L32, L52, L64, L86, L102]. The OOS document actions are not electronically signed (no SignAsync call) and have no separation-of-duties check. (verified; see GAP-313)
- Section filter for groups: samples visible when section-scoped user has tests in the group [src: backend/MicroLIMS.Application/Services/OosTrackingService.cs:L24-L34].

### MOD-RESULT-001 Results list (backend)
- Class-level [Authorize] only; no permission code [src: backend/MicroLIMS.API/Controllers/ResultController.cs:L10].

### Signatures for this cluster
- Review: "Reviewed" meaning, signed inside the same transaction as the state change; a wrong password throws before any change is written [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L118-L123; ReviewGateService.cs:L29-L37; verified-roles-permissions.md:L147]. (verified)
- Signature type names: the enum SignatureMeaning holds Reviewed, Approved, Rejected, RetestRequested, InvestigationOrdered, PreparationConfirmed and later values including TestingClosed [src: backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs:L3-L17]. (verified for the first values; the rest belong to other clusters)
- Approval decision meanings: Approve = Approved; Reject = Rejected; NewSampleRequest = Rejected; RetestRetainedSample = RetestRequested [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L293-L300].
- Close testing meaning: TestingClosed [src: backend/MicroLIMS.Application/Services/SectionClosureService.cs:L62-L65].
- Password failure text: "Password verification failed. The signature was not applied." Throttle after 5 failed attempts in 15 minutes: "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied." Signature failures do not lock the login account [src: verified-roles-permissions.md:L142-L143].
- Return to Analyst: no signature; no SoD check on the return path [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L157-L269]. See GAP-009, GAP-300, GAP-301.
- Out of this cluster but same mechanism: pathogen biochemical decision SoD ("A reviewer cannot decide on a result they performed.") [src: verified-roles-permissions.md:L127].

## workflows
Statuses referenced below are defined in the statuses section. All behaviour is from code; the application was not run. The review and approval design in the specifications is per test order and differs from the code (GAP-018, GAP-017).

### WF-REVIEW-001 Technical review of a sample section
- **Label:** IMPLEMENTED (verified in code). INTENDED source: none matches. BusinessRules.md section 8 and WorkflowDiagrams.md "Review -> Approval" describe review per test order with Detailed and Quick Table modes; the code reviews per laboratory section with one signature. DISCREPANCY confirmed both sides (GAP-018). The intended section-level, signed review: REQUIRES CONFIRMATION.
- **Start / trigger (who):** the system moves a section to Under review by itself; a person with Samples.Review (default roles: SystemAdministrator, SectionHead, Reviewer) then opens the sample and signs [src: backend/MicroLIMS.API/Controllers/SampleReviewController.cs:L19].
- **Prerequisites:**
  - Every current (non-superseded) test of the section is at step Ready. Then the section changes InTesting -> UnderReview automatically, only while the sample status is Received or InTesting, and only for a section that has at least one current test [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L43-L55, L72-L93].
  - The reviewer is assigned to the section (unrestricted users excepted) [src: SampleReviewService.cs:L103-L106; SampleSectionRollup.cs:L129-L142].
  - The reviewer did not perform any current test of the section (segregation of duties).
- **Steps:**
  1. System (no user action, no password): when a recorded result completes the last test of a section, the section becomes Under review; a Workflow History event is written with the text "All tests completed - automatically submitted for review" and no signature. Next action: the review queue. The dashboard counts it as "{n} sample(s) awaiting review" for Reviewer, SectionHead and SystemAdministrator [src: SampleReviewService.cs:L86-L89; backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L184-L189].
  2. Reviewer clicks the "Under Review" lifecycle badge on the sample (clickable only with Samples.Review) or the "Sample Summary" button. Response: the dialog "Sample Summary" opens and loads the summary; on failure "Failed to load sample summary." [src: SampleLifecycleBadge.tsx:27-33; SampleSummaryDialog.tsx:189].
  3. Reviewer reads the results and the Workflow History in the dialog. Nothing is recorded for reading.
  4. In the card "Approval & Electronic Signatures" the form "Submit Technical Review" shows only when one of the viewer's own sections is Under review and the user holds Samples.Review. If more than one section is reviewable, select "Laboratory Section" (required; "Submit Review" stays disabled until chosen). The alert reads "By submitting, I confirm I have reviewed all test results for {section}." [src: ApprovalSignaturesCard.tsx:235-281].
  5. Optional field "Comment (optional)". Clicking "Submit Review" opens the "Electronic Signature" dialog showing the reviewer's name, role and the confirmation sentence.
  6. Reviewer types the account password in "Password" and clicks "Sign" (disabled while empty). Server checks, in this order: sample exists ("Sample {id} not found."); section resolution (see errors); segregation of duties on each current test ("You cannot review a sample you tested."); every current test at step Ready ("Cannot complete review: one or more tests are not in Ready step."); password verification (signature). A failed check is shown in the dialog, the password field is cleared and nothing is saved [src: SampleReviewService.cs:L98-L123; SignatureDialog.tsx:33-36].
  7. System response on success (one save): signature with meaning Reviewed; Workflow History event ReviewCompleted with the comment and section; section -> UnderApproval with reviewer, time and signature stored; sample ReviewedBy and ReviewedAt set; each current test Status Reviewed, step Reviewed, history note "Sample review completed by {name}". The sample status is recomputed from all sections (it becomes Under approval only when no section is still in testing or review). The dialog closes [src: SampleReviewService.cs:L125-L154; SampleSectionRollup.cs:L78-L95].
- **Review:** this workflow is the review. **Approval:** next, a Section Head decides (WF-APPROVAL-001). **Final state:** section Under approval; sample status Under approval, or the less advanced status of another open section.
- **Required data:** password. **Optional:** comment; section choice only when one section is waiting (required when several).
- **Permissions per step:** steps 2-6 Samples.Review (server policy on the controller; UI gate canReview); step 1 none (system).
- **Blocking conditions:** section not Under review or sample status wrong ("Sample must be under review before it can be reviewed."); any current test not Ready; reviewer performed a test of the section (assigned analyst, sample preparer, or entered any result, count reading, analysis, pathogen observation, workflow step result or confirmatory plate observation); reviewer not assigned to the section. No time gate and no material-expiry check exists in this path (none found in the service). The rule applies to every role including SystemAdministrator.
- **Error messages (exact):** "Sample {id} not found."; "Sample must be under review before it can be reviewed."; "You cannot review a sample you tested."; "Cannot complete review: one or more tests are not in Ready step."; "This sample has no tests in the selected laboratory section."; "This test belongs to a laboratory section you are not assigned to."; "Choose a laboratory section - more than one section of this sample is waiting."; "Password verification failed. The signature was not applied."; "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied."; fallback "Signature failed." [src: SampleReviewService.cs:L100-L116; SampleSectionRollup.cs:L128-L144; verified-roles-permissions.md:L142-L143].
- **Signature points:** one electronic signature, meaning Reviewed, at step 6.
- **Resulting statuses:** section UnderReview -> UnderApproval; tests Reviewed (see statuses).
- **REQUIRES CONFIRMATION:** whether a reviewer who tested a different section may review this one (GAP-312); whether review is meant per test order (GAP-018); how an analyst is told a section is waiting for review is outside this cluster.

### WF-REVIEW-002 Return a count test to the analyst
- **Label:** IMPLEMENTED (verified). INTENDED: no specification describes returning a test; REQUIRES CONFIRMATION (GAP-009 unsigned; GAP-300 scope; GAP-301 no separation of duties).
- **Start / trigger (who):** a person with Samples.Review (SystemAdministrator, SectionHead, Reviewer) in the Sample Summary dialog [src: backend/MicroLIMS.API/Controllers/SampleReviewController.cs:L48-L54].
- **Prerequisites:**
  - The test has Status ResultEntered, is not superseded, and the screen treats it as a count test (it has count readings, or results without biochemical, location or pathogen data). Only then the button "Return to Analyst" is shown [src: sampleSummary/TestResultsSection.tsx:33-45, 109-129].
  - The test's section is In testing or Under review, and the sample status is not Under approval, Approved, Rejected, Retest requested, Cancelled or Voided [src: SampleReviewService.cs:L165-L180].
  - The caller may access the test order's section.
- **Steps:**
  1. Reviewer opens the Sample Summary dialog (badge or "Sample Summary" button) and expands the test. Response: the test details with "Return to Analyst" when the prerequisites hold.
  2. Click "Return to Analyst". Response: dialog "Return Test to Analyst" (subtitle "Return count test for recount or result re-entry") with an information text and the field "Reason for Return (optional)".
  3. Optionally type a reason; click "Confirm Return" (button shows "Returning..."). No password is requested.
  4. Server checks in this order: scope on the test order ("This test belongs to a laboratory section you are not assigned to."); test order exists; sample/section state; Status ResultEntered; test definition exists; workflow type is Count Test or a result-entry (analysis) workflow [src: SampleReviewService.cs:L160-L189].
  5. System response (count test): active count readings set inactive; the latest incubation reopened (completion time, completed-by and outcome cleared); if the section was Under review it goes back to In testing and the sample status is recomputed; the test moves to step Incubating with history note "Returned to analyst: {reason}" (or "Returned to analyst by reviewer" when no reason); a return event is stored with reviewer, assigned analyst, reason and time. The assigned analyst is unchanged [src: SampleReviewService.cs:L191-L266].
  6. Dialog closes and the summary reloads.
- **Next action:** the analyst re-enters or recounts the readings in the testing workspace; that screen belongs to the count workflows of another cluster. REQUIRES CONFIRMATION of how the analyst is told (a returned-tests notification query exists in DashboardNotificationService but was not traced here).
- **Review / Approval:** after the analyst finishes again, the section returns to Under review automatically (WF-REVIEW-001 step 1).
- **Required data:** none. **Optional:** reason.
- **Permissions:** Samples.Review (server and UI); section scope.
- **Blocking conditions:** a section already reviewed (Under approval or later) cannot be returned; a test not at Status ResultEntered; a workflow type other than count or analysis. No time gate. No separation-of-duties check (a reviewer may return a test they performed; GAP-301).
- **Error messages (exact):** "Test order {id} not found."; "Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead."; "Cannot return a test order in {status} status. Only test orders in ResultEntered status can be returned to the analyst."; "Test definition \"{code}\" not found."; "Return to Analyst is only supported for Count Test and result-entry (analysis) workflows. \"{code}\" is a {workflow} workflow."; "This test belongs to a laboratory section you are not assigned to."; client fallback "Failed to return test to analyst." [src: SampleReviewService.cs:L163-L189; ReturnToAnalystDialog.tsx:L26-L28].
- **Signature points:** none (GAP-009). The return is recorded as a return event and a test history note.
- **Resulting statuses:** test step -> Incubating (count) or Running (analysis, API only); section Under review -> In testing when applicable.
- **DISCREPANCY (UI versus service):** the screen offers the action only for count tests; the service also accepts analysis workflows (GAP-300).

### WF-APPROVAL-001 Approval decision (Approve or Not Conform)
- **Label:** IMPLEMENTED (verified). INTENDED: BusinessRules.md section 9 and WorkflowDiagrams.md describe a Section Head decision per test order with six options including Investigation and OOS Investigation. DISCREPANCY confirmed both sides (GAP-017, GAP-018). Intended section-level signed decision: REQUIRES CONFIRMATION.
- **Start / trigger (who):** a person with Samples.Approve (SystemAdministrator, SectionHead) opens a sample whose section is Under approval [src: backend/MicroLIMS.API/Controllers/SampleApprovalController.cs:L34, L48].
- **Prerequisites:** section status UnderApproval (after WF-REVIEW-001); the user is assigned to the section; the user is not the section's reviewer; the user did not perform any current test of the section; for Approve: every test needing system suitability has a linked passed HPLC run, and every analysis-type test has an active entry with no pending stage.
- **Steps:**
  1. Click the "Under Approval" badge (clickable only with Samples.Approve) or "Sample Summary". The dialog opens; the form "Submit Release Decision" shows when one of the viewer's sections is Under approval and the user holds Samples.Approve [src: SampleLifecycleBadge.tsx:27-33; SampleSummaryDialog.tsx:262-278].
  2. If more than one section is waiting, choose the section. Choosing a section pre-fills "Certificate Remarks (optional)" with any remarks already stored for it.
  3. Optional "Comment (optional)" (internal; never printed on the certificate).
  4. Select one radio option: "Approve: Results conform to specifications"; "Not Conform (Final Conclusion): Close this sample"; "New Sample Required: Close and request new sample"; "Retest Retained Sample: Return to testing". The last two are WF-APPROVAL-002.
  5. Approve only: optional "Certificate Remarks (optional)" (printed exactly as typed; blank prints "No remarks."). If any test in the section is non-conforming per the server certificate, the warning "Out-of-specification results in {section}: {tests}. Approving will certify them as conforming - consider Not Conform or a retest." appears. It does not block (GAP-304).
  6. Click "Submit Decision" (disabled until the decision is valid). The "Electronic Signature" dialog shows the selected option label. Enter "Password", click "Sign".
  7. Server checks in order: sample exists; section resolution; the signer is not the section's reviewer ("You cannot approve a sample you reviewed."); no current test performed by the signer ("You cannot approve a sample you tested."); for Approve, system suitability then analysis checks; decision-specific checks; password verification [src: SampleApprovalService.cs:L143-L307].
  8. Approve, on success: signature meaning Approved; event ApprovalDecisionMade with decision Approve; section -> Approved with approver, time and certificate remarks (trimmed); each current test Status Approved, step Approved, note "Sample approved by {name}". When no section is open any more and none is Rejected the sample becomes Approved with approver, time and (single-section samples only) the remarks.
  9. Not Conform (Reject), on success: signature meaning Rejected; section -> Rejected with approver and time; each current test Status Rejected with the step unchanged, note "Sample rejected by {name}". Other sections stay open and carry on (the code comment says so; the file header says the opposite, GAP-303).
  10. Finalisation: if the sample status is now Approved or Rejected, result records are refreshed, the summary PDF is frozen as an archived copy (label "Sample Approve" or "Sample Reject") with a SHA-256 hash, and the outcome is propagated to an origin sample if this sample is a retest (BR-APPROVAL-021). The dialog closes [src: SampleApprovalService.cs:L314-L375, L522-L539].
- **Next action:** view or print the report / Certificate of Analysis (WF-COA-001). After a Reject, other sections may continue or use Close Testing (WF-APPROVAL-003).
- **Review / Approval / Final state:** review was WF-REVIEW-001; final section state Approved or Rejected; sample state per the roll-up (BR-APPROVAL-019).
- **Required data:** decision, password; for retest decisions see WF-APPROVAL-002. **Optional:** comment; certificate remarks (Approve only); section choice only when one section is waiting.
- **Permissions:** Samples.Approve (server policy and UI gate); section scope.
- **Blocking conditions (SoD):** approver equals the section reviewer; approver performed a test of the section. Both apply to SystemAdministrator. No time gate; no expiry check found.
- **Error messages (exact):** "Sample must be under approval before a decision can be made."; "You cannot approve a sample you reviewed."; "You cannot approve a sample you tested."; "Cannot approve section: test order {id} (\"{code}\") requires system suitability, but only HPLC workspace tests (assay, dissolution) can supply one. Clear \"Requires system suitability\" for this test in Test Master."; "Cannot approve section: test order {id} (\"{code}\") lacks an HPLC run with a passed system suitability."; "Cannot approve section: test order {id} (\"{code}\") lacks an active {display name} entry."; "Cannot approve section: test order {id} (\"{code}\") has a pending stage."; "'{decision}' is not a valid sample-level decision."; plus the section and signature messages listed in WF-REVIEW-001.
- **Signature points:** one signature at step 6: meaning Approved (Approve) or Rejected (Not Conform).
- **Resulting statuses:** section UnderApproval -> Approved or Rejected; tests Approved or Rejected.
- **REQUIRES CONFIRMATION:** whether approving out-of-specification results should be blocked (GAP-304); what "Close this sample" means when other sections are open (GAP-303).

### WF-APPROVAL-002 Retest or new-sample spin-off
- **Label:** IMPLEMENTED (verified). INTENDED: the specifications name RetestRetainedSample and NewSampleRequest as decisions but do not describe the spin-off; REQUIRES CONFIRMATION for the intended procedure. The Investigation and OOS Investigation options in the specification are not available (GAP-017).
- **Start / trigger (who):** the same Section Head step as WF-APPROVAL-001 step 4, choosing "Retest Retained Sample: Return to testing" or "New Sample Required: Close and request new sample".
- **Prerequisites:** all WF-APPROVAL-001 prerequisites except the Approve-only checks; a cause of testing named "Retest" exists; for New Sample Required the user can load the analyst list (it loads only for users holding Samples.AssignAnalyst; otherwise the message "The list of eligible analysts could not be loaded. Close and reopen this sample to try again." can appear) [src: SampleSummaryDialog.tsx:191-199; ApprovalSignaturesCard.tsx:418-422].
- **Steps:**
  1. In the release form choose the section if asked, then a retest option. Response: the block "Tests to Retest" lists each non-superseded test of the section with its section chip; tests judged non-conforming by the screen are pre-checked with a "Non-conforming" chip. Text: "Non-conforming tests are pre-checked. Adjust as needed - only the checked test(s) move to the new sample(s); everything else on this sample is left untouched."
  2. Adjust the checkboxes. With none checked: warning "Select at least one test to retest." and "Submit Decision" is disabled.
  3. New Sample Required only: block "Analysts for the Two New Samples" ("Two different analysts are required, and neither may be whoever tested the original sample."). Choose "New Sample 1 - Analyst" and "New Sample 2 - Analyst". Same analyst twice: warning "The two new samples must be assigned to two different analysts." and the button stays disabled.
  4. Optional comment; click "Submit Decision"; sign with the password (dialog shows the option label).
  5. Server checks: the reviewer and performer checks of WF-APPROVAL-001 step 7, then "At least one test must be selected for retest."; "One or more selected tests do not belong to this sample's current test orders."; for New Sample Required: both analyst ids present, different, both active, not locked and of Analyst role type, and neither the assigned analyst of a selected test; the "Retest" cause must exist. Only the assigned analyst of selected tests is compared; other people who performed the original tests are not (GAP-302).
  6. Signature: meaning RetestRequested (Retest Retained Sample) or Rejected (New Sample Required, GAP-308); event ApprovalDecisionMade with the decision.
  7. System response, Retest Retained Sample: section -> RetestRequested; the origin sample gets an OOS group code if it has none; ONE new sample is created (status Received, cause "Retest", identity fields copied, link to the origin, same group code) holding new copies of the selected tests (Status Pending, step Waiting, same assigned analyst); the selected originals are marked superseded with history note "Retest ordered by {name} - new sample pending reference number". Other tests on the original are untouched [src: SampleApprovalService.cs:L377-L438].
  8. System response, New Sample Required: section -> RetestRequested; group code as above; TWO new samples are created and saved one at a time, each with copies of the selected tests assigned to its chosen analyst; originals superseded with note "New sample requested by {name}" [src: SampleApprovalService.cs:L440-L503].
  9. For Water, EM and After Cleaning only the failed locations are copied to the new tests (all locations if none failed) and the preparation screen is skipped; other samples get the normal initial preparation status.
  10. The sample status is recomputed; nothing is archived unless the sample is now Approved or Rejected. The dialog closes.
- **Next action:** the new sample(s) appear in the testing workspace for the original or chosen analysts and follow the normal test workflow (other clusters). The group appears in OOS Tracking (WF-OOS-001) for users with Oos.Manage.
- **Review / Approval:** each new sample goes through review and approval itself. When a retest sample is finally Approved or Rejected, the outcome is copied to the origin section: Retest Retained Sample takes the retest's outcome; New Sample Required takes Approve only if both new samples are Approved, otherwise Reject, and only after both have a final status; origin tests are then finalised from their own recorded results (BR-APPROVAL-021, BR-APPROVAL-022). No new signature is created for this mirrored outcome.
- **Final state:** origin section Approved or Rejected after the retest outcome; originals of retested tests stay superseded.
- **Required data:** at least one selected test; password; for New Sample Required two different analysts. **Optional:** comment.
- **Permissions:** Samples.Approve; analyst list requires Samples.AssignAnalyst in the UI; section scope.
- **Blocking conditions:** SoD as WF-APPROVAL-001; missing "Retest" cause of testing; analyst inactive, locked or not of Analyst type.
- **Error messages (exact):** "At least one test must be selected for retest."; "One or more selected tests do not belong to this sample's current test orders."; "Two analysts must be assigned - one per new sample - when requesting a new sample."; "The two new samples must be assigned to two different analysts."; "Both retest analysts must be active, eligible Analyst-role users."; "Neither new sample's analyst may be the analyst who tested the original sample."; "The 'Retest' cause of testing is not configured."; plus WF-APPROVAL-001 SoD and scope messages.
- **Signature points:** one signature at step 4.
- **REQUIRES CONFIRMATION:** wording of the signature for New Sample Required (GAP-308); breadth of the analyst separation (GAP-302); the history note says "pending reference number" although the number is generated at this step.

### WF-APPROVAL-003 Close testing after another laboratory rejected
- **Label:** IMPLEMENTED (verified). INTENDED: the code comments cite a "design.md section 5.3" that is not among the listed spec sources; no intended text verified, so REQUIRES CONFIRMATION.
- **Start / trigger (who):** a person with Samples.Approve whose laboratory still has open work on a sample where another laboratory's section is Rejected [src: SampleSummaryDialog.tsx:262-278; SampleApprovalController.cs:L58].
- **Prerequisites:** another section of the sample is Rejected; the user's section is In testing, Under review or Under approval; the user is in scope of that section.
- **Steps:**
  1. Open the Sample Summary dialog. The "Close Testing" block shows (when the overall outcome is Rejected, the viewer has a closable section and Samples.Approve) with: "Another laboratory has rejected this sample. You may close your own laboratory's remaining tests instead of continuing them." Choose the section if several.
  2. Click "Close Testing". Dialog "Close Testing: {section}" (subtitle "Stops your laboratory's remaining tests on this sample - signed with your password") with the text "Every still-open test in this laboratory will be recorded as Cancelled at the stage it had reached. Approved results already on file are kept."
  3. The field "Reason" is required and pre-filled "Sample rejected by {lab}". Edit if needed; click "Sign & Close" (disabled when empty).
  4. The "Electronic Signature" dialog shows "I am closing this laboratory's testing; it will not be resumed on this sample." Enter "Password"; "Sign".
  5. Server checks in order: reason not blank; sample exists; user's scope; section belongs to the sample; another section is Rejected; section not already closed; section is open [src: SectionClosureService.cs:L34-L58].
  6. System response: signature meaning TestingClosed; event SectionTestingClosed with the reason; each current test that is not Approved, Voided or Cancelled becomes Status Cancelled with the step and incubation stage it had reached recorded, history note "Testing closed by {name}: {reason}"; section -> Cancelled (shown as "Closed") with closer, time, reason and signature. The sample status is recomputed; if now final, the report is archived with label "Laboratory testing closed" [src: SectionClosureService.cs:L60-L100].
- **Next action:** view or print the Certificate of Analysis; the combined variant lists the closed laboratory's tests as "Not completed, testing closed". No section-only certificate exists for a closed section.
- **Required data:** reason, password. **Permissions:** Samples.Approve; section scope. No reviewer or performer separation check exists on this path (none in the service).
- **Blocking conditions / errors (exact):** "A reason is required to close testing."; "Sample {id} not found."; "This test belongs to a laboratory section you are not assigned to."; "This sample has no tests in the selected laboratory section."; "Testing can be closed only after another laboratory rejected the sample."; "This laboratory's testing is already closed."; "This laboratory has no open tests on the sample."; signature failure messages as in WF-REVIEW-001.
- **Signature points:** one, meaning TestingClosed (the trail has no statement text for it, GAP-315).
- **Resulting statuses:** section -> Cancelled ("Closed"); tests -> Cancelled.

### WF-COA-001 Print the sample report and view the Certificate of Analysis
- **Label:** IMPLEMENTED (verified). INTENDED: no specification describes the report or certificate; REQUIRES CONFIRMATION. No signature is applied in this workflow; the signatures printed were captured in earlier workflows.
- **Start / trigger (who):** any signed-in user who can reach the sample: the report and certificate routes have no permission guard, and the summary API checks sample access by section (EnsureSampleAccessAsync) [src: frontend/src/routes/AppRoutes.tsx:102-103; backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L30-L41].
- **Prerequisites:** for the certificate, the sample status is Approved or Rejected (combined) or the section is Approved (section variant), and results exist.
- **Steps:**
  1. From the Sample Summary dialog click "Printable Report" (opens /samples/{id}/report), "View COA" (shown only when the sample is certificate-eligible: results exist and combined or a section certificate is available), or "Export PDF" (server-built PDF; button shows "Exporting..."; error "Export failed."). The dialog also repeats these as full-width buttons "Open Printable Report" and "View Certificate of Analysis" [src: SampleSummaryDialog.tsx:333-360, 417-441, 548-575].
  2. Report page: loads ("Loading report…"), shows identity, personnel, preparation, tests, timeline, signatures and archived copies with download. "Print / Save PDF" calls the browser print function. Footer: "This is a controlled document generated by MicroLIMS. Any printed copy is uncontrolled."
  3. Certificate page: loads ("Loading certificate…"). Not final: the availability text of SCR-COA-002. Multi-section: choose "Certificate Variant:" Combined or a section (Combined needs every section visible to the viewer). Review the Remarks and Overall Conclusion, then "Print / Save PDF".
  4. Archived copies: the report page lists frozen PDFs written at each final decision; a download returns the file and a response header reports "verified" or "FAILED" for the hash check (GAP-314).
- **Next action:** none in the system; printed copies are marked uncontrolled.
- **Required data:** none. **Optional:** variant selection.
- **Blocking conditions:** certificate not available until the sample (or section) is final; viewers who cannot see every section cannot use Combined; a Rejected or closed section has no section-only certificate.
- **Error messages (exact):** "Failed to load the sample report."; "Failed to load the certificate of analysis."; "A Certificate of Analysis is only available once this sample has been approved or rejected. Current status: {status}."; "A Certificate of Analysis is not available for this sample - none of its tests have recorded results yet."; "A Certificate of Analysis is not available yet. The following laboratories are not final yet:"; "No approved laboratory sections are currently accessible."; "A Certificate of Analysis is not available for this section - none of its tests have recorded results yet."; "The archived copies of this record could not be loaded. Reload the page to try again."; "Archived record {id} not found." (API).
- **Signature points:** none (printing). Reviewer and approver blocks show earlier signatures or stored names (BR-COA-004, GAP-307).
- **Resulting statuses:** none.
- **DISCREPANCY / open:** the server returns certificate data for non-final samples (GAP-306); which PDF is the controlled copy (GAP-311); the Word export has no screen (GAP-310).

### WF-OOS-001 Manage lab investigation documents for an OOS group
- **Label:** IMPLEMENTED (verified). INTENDED: no specification describes OOS Tracking or investigation documents; the only spec text is the Investigation / OOS Investigation decisions (not implemented, GAP-017). Everything intended: REQUIRES CONFIRMATION (GAP-305, GAP-313).
- **Start / trigger (who):** a person with Oos.Manage (SystemAdministrator, SectionHead) opens /oos-tracking [src: frontend/src/routes/AppRoutes.tsx:216-217; backend/MicroLIMS.API/Controllers/OosTrackingController.cs:L15].
- **Prerequisites:** an OOS group exists. A group is created only when a Section Head signed Retest Retained Sample or New Sample Required (WF-APPROVAL-002); an out-of-specification result alone does not create one (GAP-305). Section-scoped users see groups that have a retest sample with tests in their sections.
- **Steps:**
  1. Open the page "Out-of-Specification Tracking". Response: "OOS Investigation Groups" with "1 OOS Group" or "N OOS Groups (M retest samples)"; empty: "No active OOS investigation chains found."; load error "Failed to load OOS tracking items."
  2. Each group shows the chip "Lab Investigation Report Attached" or "No Report Attached", the retest table (Retest Sample, Parent / Origin, Item / Point, Retest Type, Test(s), Analyst(s), Retest Status, Opened, Actions) and, per row, an action that opens that retest sample's printable report.
  3. Click "Upload Investigation" (no document yet) or "Manage Investigation". Dialog "Lab Investigation Documents: {oosGroupCode}" lists documents (Current first). Load error "Failed to load investigation documents."
  4. Upload: with no Current document the section "Upload Initial Investigation Report" shows; click "Select File", choose a PDF, JPG, JPEG, PNG, WEBP or TIFF file, click "Upload". Server checks the group exists and the file (size, type, signature) and stores it with a SHA-256 hash; status Current. On storage failure the record is removed.
  5. Supersede: on the Current row click the replace icon ("Supersede with replacement report"); read "The current document will be marked Superseded. A new active version will take its place."; enter "Reason for Supersession *"; "Choose Replacement File"; click "Supersede" (disabled until both are given). The old document becomes Superseded with reason, user and time; the new one is Current.
  6. Download: the icon "Download file" fetches the content after a hash re-check.
  7. Void: the endpoint exists but there is no screen control (GAP-313).
- **Next action:** none in the system; the group and its retest samples are followed through their own review and approval.
- **Review / Approval:** none for documents (no review or approval step is implemented for investigation reports).
- **Required data:** a file (upload, supersede); a reason (supersede, void). **Optional:** none.
- **Permissions:** Oos.Manage at every step (server policy on both controllers); group access check per request.
- **Blocking conditions:** file too large (25 MB as configured), wrong type or content; no Current document means upload only; a Voided document cannot be superseded; no time gate.
- **Error messages (exact):** see validations (file checks and OOS documents), including "No file was provided.", "A replacement file is required.", "A reason for supersession is required.", "A supersession reason is required.", "A voided document cannot be superseded. Upload a new document instead.", "A void reason is required.", "This document is already voided.", "OOS group \"{code}\" not found.", "Document {id} not found for OOS group {code}.", "Document storage failed. The upload was not completed.", "Replacement file storage failed. The supersession was not completed.", "The document file could not be retrieved.", "Document integrity check failed. The file may have been altered. Please contact your System Administrator."; screen messages "Failed to upload document.", "Failed to download document content.", "Failed to supersede document."
- **Signature points:** none (GAP-313).
- **Resulting statuses:** document Current -> Superseded; any -> Voided via API only.

## business-rules

### MOD-REVIEW-001 Technical review and return to analyst
- BR-REVIEW-001 IMPLEMENTED: review is decided per laboratory section, not per test order. A section moves from InTesting to UnderReview automatically when every current test in it is at WorkflowStep.Ready [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L46-L55, L72-L93]. DISCREPANCY with BusinessRules.md section 8 (per test order), see GAP-018. (verified)
- BR-REVIEW-002 IMPLEMENTED: review completion is refused unless the section is UnderReview and every current test is Ready. Messages in Validations [src: SampleReviewService.cs:L104-L116]. (verified)
- BR-REVIEW-003 IMPLEMENTED: the reviewer must not have performed any current test in the section [src: SampleReviewService.cs:L108-L113; SegregationOfDutiesGuard.cs:L25-L54]. (verified)
- BR-REVIEW-004 IMPLEMENTED: the review signature is written before the state change, in one save; a failed password writes nothing [src: SampleReviewService.cs:L118-L123, L153-L154; ReviewGateService.cs:L29-L37]. (verified)
- BR-REVIEW-005 IMPLEMENTED: on completion the section becomes UnderApproval, section ReviewedBy, ReviewedAt and ReviewSignature are set, sample ReviewedBy and ReviewedAt are set, and each current test becomes ApprovalStatus.Reviewed with WorkflowStep.Reviewed and history note "Sample review completed by {signer name}" [src: SampleReviewService.cs:L125-L153]. (verified)
- BR-REVIEW-006 IMPLEMENTED: the review comment is stored on the ReviewWorkflowEvent, not on the certificate [src: ReviewGateService.cs:L39-L49; SampleApprovalService.cs:L320-L323]. (verified)
- BR-REVIEW-007 IMPLEMENTED: return to analyst is refused when the sample is UnderApproval, Approved, Rejected, RetestRequested, Cancelled or Voided, or when the test's section is not InTesting or UnderReview [src: SampleReviewService.cs:L165-L180]. (verified)
- BR-REVIEW-008 IMPLEMENTED: return to analyst requires the test order status ResultEntered [src: SampleReviewService.cs:L182-L183]. (verified)
- BR-REVIEW-009 IMPLEMENTED: return to analyst is supported for CountTest and for analysis workflows (AnalysisWorkflows.UsesTestAnalysis); other workflow types are refused [src: SampleReviewService.cs:L188-L189]. DISCREPANCY with the UI, which offers the action only for count tests [src: frontend/src/modules/testingWorkspace/sampleSummary/TestResultsSection.tsx:33-45], see GAP-300. (verified)
- BR-REVIEW-010 IMPLEMENTED: return to analyst effects. Count test: active CountTestReading rows set IsActive=false; latest Incubation reopened (CompletedAt, CompletedByUserId, Outcome cleared). Analysis test: active TestAnalysis and ParameterResult rows set IsActive=false. If the section was UnderReview it returns to InTesting. The order moves to WorkflowStep.Running (analysis) or Incubating (count) with note "Returned to analyst: {reason}" or "Returned to analyst by reviewer". A TestReturnEvent row is written [src: SampleReviewService.cs:L191-L266]. (verified)
- BR-REVIEW-011 IMPLEMENTED: return to analyst is not signed and has no separation-of-duties check. The route sample id is not compared with the test order's sample; scope is checked on the test order only [src: backend/MicroLIMS.API/Controllers/SampleReviewController.cs:L44-L54; SampleReviewService.cs:L157-L170]. See GAP-009, GAP-301. (verified)
- BR-REVIEW-012 INTENDED (BusinessRules.md section 8): "A test order cannot be reviewed before its status is `ResultEntered`" [src: documents/DesignSpecification/BusinessRules.md:L161]. Code checks section state and WorkflowStep.Ready instead and does not look at the test order Status when reviewing [src: SampleReviewService.cs:L104-L116]. DISCREPANCY confirmed both sides, see GAP-018.
- BR-REVIEW-013 INTENDED (BusinessRules.md section 8 and WorkflowDiagrams.md "Review -> Approval"): two review modes, Detailed and Quick Table (QuickReviewBatchAsync, skips ineligible orders) [src: documents/DesignSpecification/BusinessRules.md:L157-L168; documents/DesignSpecification/WorkflowDiagrams.md:L100-L115]. Quick Table review: no such method in the repository (git ls-files shows no ReviewService.cs or ApprovalService.cs under Application/Services; git grep for QuickReview in backend returns no match). DISCREPANCY confirmed both sides, see GAP-018.

### MOD-APPROVAL-001 Approval, retest, new sample, close testing
- BR-APPROVAL-001 IMPLEMENTED: a decision requires the chosen section to be UnderApproval [src: SampleApprovalService.cs:L149-L156]. INTENDED per BusinessRules.md section 9 is per test order status Reviewed. DISCREPANCY, see GAP-018. (verified)
- BR-APPROVAL-002 IMPLEMENTED: the approver must not be the section's reviewer. The check compares against the section sign-off ReviewedByUserId [src: SampleApprovalService.cs:L162-L163]. Section sign-off rows copy the sample-level reviewer for sections already past review [src: backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:L44-L65]. (verified)
- BR-APPROVAL-003 IMPLEMENTED: the approver must not have performed any current test in the section [src: SampleApprovalService.cs:L165-L171]. (verified)
- BR-APPROVAL-004 IMPLEMENTED (Approve only): a section cannot be approved if a test needing system suitability has no linked passed run. Non-HPLC tests with system suitability cannot satisfy it. HPLC assay and dissolution tests take SST from their run [src: SampleApprovalService.cs:L173-L206]. Comment cites REQ-FP-033. (verified)
- BR-APPROVAL-005 IMPLEMENTED (Approve only): a section cannot be approved if an analysis-type test has no active entry, or has a parameter result at NextStageRequired [src: SampleApprovalService.cs:L208-L241]. (verified)
- BR-APPROVAL-006 IMPLEMENTED: retest and new-sample decisions need at least one selected test, and every selected test must belong to the sample's current, non-superseded orders in the decided section [src: SampleApprovalService.cs:L247-L257]. (verified)
- BR-APPROVAL-007 IMPLEMENTED (New Sample Required): two analyst ids are required; they must differ; both must be active, not locked, and have Role.Type == Analyst [src: SampleApprovalService.cs:L264-L277]. No section-scope check on these analysts (GAP-302). (verified)
- BR-APPROVAL-008 IMPLEMENTED (New Sample Required): neither new analyst may equal the AssignedAnalystId of a selected test. Other people who performed tests are not checked here (GAP-302) [src: SampleApprovalService.cs:L279-L283]. (verified)
- BR-APPROVAL-009 IMPLEMENTED: retest and new-sample decisions need a CauseOfTesting named "Retest" [src: SampleApprovalService.cs:L286-L291]. (verified)
- BR-APPROVAL-010 IMPLEMENTED: signature meaning by decision: Approve = Approved; Reject = Rejected; NewSampleRequest = Rejected; RetestRetainedSample = RetestRequested. Signed before any state change [src: SampleApprovalService.cs:L293-L307]. (verified)
- BR-APPROVAL-011 IMPLEMENTED (Approve): the section becomes Approved with ApprovedBy and ApprovedAt. CertificateRemarks is trimmed and stored on the section, never derived from the internal comment. The sample-level copy of remarks is set only when one section tested the sample [src: SampleApprovalService.cs:L314-L335]. (verified)
- BR-APPROVAL-012 IMPLEMENTED (Approve): each current test becomes ApprovalStatus.Approved and WorkflowStep.Approved; history "Sample approved by {signer name}" [src: SampleApprovalService.cs:L336-L349]. (verified)
- BR-APPROVAL-013 IMPLEMENTED (Reject): the section becomes Rejected with ApprovedBy and ApprovedAt. Each current test becomes ApprovalStatus.Rejected, step unchanged; history "Sample rejected by {signer name}". A rejection does not close other sections [src: SampleApprovalService.cs:L353-L375]. (verified)
- BR-APPROVAL-014 IMPLEMENTED (Retest Retained Sample): the section becomes RetestRequested. The original sample is not closed and keeps its other tests. OosGroupCode is generated if the sample has none [src: SampleApprovalService.cs:L377-L393]. One new sample is created with the selected tests only (Status Received, reference from ReferenceNumberGenerator). Selected originals are marked IsSuperseded=true with history "Retest ordered by {signer name} - new sample pending reference number" [src: SampleApprovalService.cs:L395-L423, L436]. (verified)
- BR-APPROVAL-015 IMPLEMENTED (New Sample Required): one new sample per analyst, each with the selected tests assigned to that analyst. The two samples are saved one at a time so reference numbers stay distinct. Originals are marked IsSuperseded=true with history "New sample requested by {signer name}" [src: SampleApprovalService.cs:L440-L501]. (verified)
- BR-APPROVAL-016 IMPLEMENTED: retest samples copy identity attributes from the original (item, category, batch, control number, dates, quantity, sampling point, machine, department, production stage, cause set to "Retest", OriginSampleId, OosGroupCode). They do not copy preparation or test orders [src: SampleApprovalService.cs:L54-L82]. (verified)
- BR-APPROVAL-017 IMPLEMENTED: for Water, EM and After Cleaning, retest orders carry only the failed locations (status not WithinLimits or Absent). If none failed, all locations are carried. Carried samples skip the preparation screen (PreparationStatus Ready); others get the initial preparation status [src: SampleApprovalService.cs:L93-L134, L431-L434, L481-L484]. (verified)
- BR-APPROVAL-018 IMPLEMENTED (Close testing): the reason is required; another section of the same sample must already be Rejected; the caller must be in scope; the section must be open. Signed with meaning TestingClosed. Open tests become ApprovalStatus.Cancelled with CancelledAtStep and CancelledAtStage recorded; Approved, Voided and Cancelled tests are left alone. History "Testing closed by {signer name}: {reason}". The section becomes Cancelled and the sample is finalised if closed [src: SectionClosureService.cs:L31-L100]. (verified)
- BR-APPROVAL-019 IMPLEMENTED (sample status roll-up): Compute order is all Voided -> Voided; any InTesting -> InTesting; any UnderReview -> UnderReview; any UnderApproval -> UnderApproval; any Rejected -> Rejected; any RetestRequested -> RetestRequested; else Approved [src: SampleSectionRollup.cs:L78-L88]. Overall (what users see as outcome): Voided, Cancelled, any Rejected -> Rejected, any open section -> InProgress, any RetestRequested -> RetestRequested, else Approved [src: SampleSectionRollup.cs:L97-L106]. (verified)
- BR-APPROVAL-020 IMPLEMENTED (finalisation): when the sample is Approved or Rejected, approval fields are refreshed, the summary report document is built and archived as a frozen PDF with label "Sample {decision}" where the decision is the enum name of the deciding action, for example "Sample Approve" or "Sample Reject" (or "Laboratory testing closed" for close testing). For a multi-section sample this happens only when the decision leaves no section open [src: SampleApprovalService.cs:L528-L539; SectionClosureService.cs:L99]. (verified)
- BR-APPROVAL-021 IMPLEMENTED (OOS outcome propagation to the origin sample): only when the origin's section is RetestRequested. For a retest-retained child the origin takes the child's outcome. For a new-sample child the origin waits for the sibling sample and takes Approve only if both are Approved. Origin tests are then finalised per their own recorded result [src: SampleApprovalService.cs:L541-L627]. Archive label "Sample {outcome} (OOS resolved)" [src: SampleApprovalService.cs:L636-L639]. (verified)
- BR-APPROVAL-022 IMPLEMENTED (origin test conformance): last active count reading, else last location, else last biochemical result, else pathogen chain, else last result value. No result: conforming by default [src: SampleApprovalService.cs:L648-L686]. (verified)
- BR-APPROVAL-023 INTENDED (BusinessRules.md section 9): Investigation and OOSInvestigation decisions require a non-blank comment, enforced server-side [src: documents/DesignSpecification/BusinessRules.md:L171-L184]. Code: DecideCoreAsync refuses any decision other than the four sample-level ones with "'{decision}' is not a valid sample-level decision." (the check sits in the signature-meaning switch, after the other checks) [src: SampleApprovalService.cs:L293-L299]. No comment rule found for these values. The enum still holds all six values [src: backend/MicroLIMS.Domain/Enums/ApprovalDecision.cs]. DISCREPANCY confirmed both sides, see GAP-017.
- BR-APPROVAL-024 INTENDED (WorkflowDiagrams.md "Review -> Approval"): the Section Head may choose Investigation or OOS Investigation with a comment required [src: documents/DesignSpecification/WorkflowDiagrams.md:L114-L115]. Not reachable at sample approval (the UI offers four options) [src: SampleApprovalService.cs:L17-L19; summaryFormatting.ts:73-78]. DISCREPANCY confirmed both sides, see GAP-017.
- BR-APPROVAL-025 IMPLEMENTED: the server does not block approval when results are out of specification. Only the SST and analysis checks in BR-APPROVAL-004 and BR-APPROVAL-005 can refuse Approve. The UI shows a warning that approval certifies them as conforming [src: SampleApprovalService.cs:L173-L242; ApprovalSignaturesCard.tsx:459-463]. REQUIRES CONFIRMATION, see GAP-304. (verified)
- BR-APPROVAL-026 IMPLEMENTED (code comment conflict): the service header says rejecting "rejects the whole sample" [src: SampleApprovalService.cs:L12-L16]. The Reject branch says a rejection "no longer closes any other lab still open" [src: SampleApprovalService.cs:L360-L362]. Behaviour follows the second. See GAP-303. (verified)

### MOD-COA-001 Sample report and Certificate of Analysis
- BR-COA-001 IMPLEMENTED: a single-section sample's CoA page displays only when the sample status is Approved or Rejected (server flag combinedCoaAvailable); text "A Certificate of Analysis is only available once this sample has been approved or rejected. Current status: {status}." For a multi-section sample the combined certificate needs that same flag plus every section visible to the viewer, and a section-only certificate needs that section to be Approved (a Rejected or closed section has none) [src: frontend/src/modules/testingWorkspace/SampleCoaPage.tsx:293-315, 358-366; backend/MicroLIMS.Application/Services/SampleSummaryService.cs:L342; SampleSummary/SampleSummarySectionsBuilder.cs:L29]. The server certificate endpoint does not refuse other statuses [src: SampleSummaryService.cs:L84-L90; backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L30-L41]. (verified) REQUIRES CONFIRMATION, see GAP-306.
- BR-COA-002 IMPLEMENTED: for a final sample (Approved or Rejected) the certificate summary uses all sections regardless of the caller's section scope; for a non-final sample the scope applies [src: SampleSummaryService.cs:L84-L90]. (verified)
- BR-COA-003 IMPLEMENTED: certificate remarks print exactly as typed; blank shows "No remarks."; the internal comment never prints [src: ApprovalSignaturesCard.tsx:333-345; SampleCoaPage.tsx:529-534; SampleApprovalService.cs:L320-L323]. (verified)
- BR-COA-004 IMPLEMENTED (corrected): only the single-section certificate looks up signature rows (reviewer = last "Reviewed" signature; approver = last "Approved" or "Rejected" signature matching the sample status, and none for a retest-spawning decision). When no row is used, the block shows the stored name and time. The multi-section combined and section variants always print stored names and times and never read signature rows [src: SampleCoaPage.tsx:126-157, 413-417, 552-555, 661-688, 760-783]. (verified) REQUIRES CONFIRMATION, see GAP-307.
- BR-COA-005 IMPLEMENTED: the report and CoA pages (and the signature dialog) are wrapped in PinnedLightTheme, which forces the light theme regardless of the application dark-mode setting [src: frontend/src/theme/PinnedLightTheme.tsx:L1-L17; frontend/src/modules/testingWorkspace/SampleReportPage.tsx:129-137; SampleCoaPage.tsx:333-337]. (verified) CoA print CSS sets A4 portrait, page margin 0, padding 18mm/16mm/22mm, and hides elements with class no-print [src: SampleCoaPage.tsx:43-50].
- BR-COA-006 IMPLEMENTED: the server summary PDF is built from fixed sections: SAMPLE IDENTITY, SAMPLE PREPARATION (when present), TEST RESULTS, WORKFLOW HISTORY (per test), TIMELINE, and SIGNATURES (only when signatures exist) [src: backend/MicroLIMS.Application/Services/SampleSummaryService.cs:L383-L545].
- BR-COA-007 IMPLEMENTED: an archived PDF is frozen at each final decision with a SHA-256 hash. An archive failure is logged and the decision stands [src: backend/MicroLIMS.Application/Services/RecordArchiveService.cs:L12-L20, L38-L80]. The hash is re-checked on read; on a mismatch the download still returns the file and only sets the response header "X-Archive-Integrity: FAILED" (otherwise "verified"); whether the screen shows this is not known [src: RecordArchiveService.cs:L86-L90; backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L35-L48]. (verified) See GAP-314.

### MOD-OOS-001 OOS Tracking
- BR-OOS-001 IMPLEMENTED: no OOS group exists until a Section Head signs a retest decision (RetestRetainedSample or NewSampleRequest). The origin sample then receives an OosGroupCode if it has none [src: SampleApprovalService.cs:L392-L393, L447-L448]. No other writer of Sample.OosGroupCode in Application was found (git grep); TestingWorkspaceService.cs:L615 only reads it. (verified)
- BR-OOS-002 IMPLEMENTED: retest samples carry OriginSampleId and the same OosGroupCode [src: SampleApprovalService.cs:L54-L82, L395, L459]. The group root is the sample with no OriginSampleId, else the lowest Id [src: backend/MicroLIMS.Application/Services/OosTrackingService.cs:L72-L74]. (verified)
- BR-OOS-003 IMPLEMENTED: a micro count, location or water result is classified by SpecLimitParser.Compare in this order: specification exceeded -> OutOfSpecification; else action exceeded -> ActionLimitExceeded; else alert exceeded -> AlertLimitExceeded; no limits configured -> LimitsNotConfigured; else WithinLimits [src: backend/MicroLIMS.Application/Services/SpecLimitParser.cs:L209-L224; Workflows/TestWorkflow/IncubationStepRecorder.cs:L990; Workflows/WaterWorkflowEngine.cs:L211].
- BR-OOS-004 IMPLEMENTED: physicochemical specification limits are evaluated by SpecificationEvaluator (range, not-more-than, not-less-than) to OutOfSpecification or WithinLimits [src: backend/MicroLIMS.Application/Services/SpecificationEvaluator.cs:L8-L51]. Detail belongs to C6.
- BR-OOS-005 IMPLEMENTED: an OutOfSpecification result does not open an OOS group by itself. Opening a group is an approver's retest decision (BR-OOS-001). See GAP-305. (verified)
- BR-OOS-006 IMPLEMENTED: investigation document upload and supersede replacement files are checked by MaterialDocumentFileValidator (maximum size from configuration MaterialDocuments:MaxFileSizeBytes, default and configured value 26214400 bytes = 25 MB; the controller transport ceiling is 30 MB). Checks run in this order: size, empty file, extension (PDF, JPG, JPEG, PNG, WEBP, TIFF), declared content type matches extension, minimum length, file signature. Messages are listed under validations. No malware scanner is integrated [src: backend/MicroLIMS.Application/Services/MaterialDocumentFileValidator.cs:L1-L83; backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs:L184; backend/MicroLIMS.API/appsettings.json:L27-L28; OosInvestigationDocumentController.cs:L39]. (verified) Storage and integrity failures: "Document storage failed. The upload was not completed."; "Document integrity check failed. The file may have been altered. Please contact your System Administrator." [src: backend/MicroLIMS.Application/Services/OosInvestigationDocumentService.cs:L104-L109, L143, L172-L174].
- BR-OOS-007 IMPLEMENTED: supersede needs a reason and a non-voided document; void needs a reason and is refused for an already voided document [src: OosInvestigationDocumentService.cs:L189-L203, L259-L268]. (verified)
- BR-OOS-008 IMPLEMENTED: the group list shows an attached-document flag when any non-voided investigation document exists for the group [src: backend/MicroLIMS.Application/Services/OosTrackingService.cs:L47-L53]. The document list shows Current first, then by upload date [src: OosInvestigationDocumentService.cs:L84-L89]. (verified)
- BR-OOS-009 IMPLEMENTED: retest rows show analyst names from the retest's test orders, or "User #{id}" when the name is not found [src: OosTrackingService.cs:L84-L94]. Retest Type is the origin sample's ApprovalDecision enum name, mapped in the UI to "Retest Retained Sample" or "New Sample Request" [src: OosTrackingService.cs:L76-L82; frontend/src/modules/oosTracking/OosTrackingPage.tsx:45-46]. (verified)

### MOD-RESULT-001 Results list (backend)
- No business rule read. Response semantics UNCLEAR.

## validations

### Server messages (exact text)
Review:
- "Sample {id} not found." [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L100-L101]
- "Sample must be under review before it can be reviewed." [src: SampleReviewService.cs:L105-L106]
- "You cannot review a sample you tested." [src: SampleReviewService.cs:L112]
- "Cannot complete review: one or more tests are not in Ready step." [src: SampleReviewService.cs:L116]
- "Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead." [src: SampleReviewService.cs:L179]
- "Cannot return a test order in {status} status. Only test orders in ResultEntered status can be returned to the analyst." [src: SampleReviewService.cs:L183]
- "Test definition \"{code}\" not found." [src: SampleReviewService.cs:L186]
- "Return to Analyst is only supported for Count Test and result-entry (analysis) workflows. \"{code}\" is a {workflow} workflow." [src: SampleReviewService.cs:L189]
- "Test order {id} not found." [src: SampleReviewService.cs:L162-L163]

Approval:
- "Sample must be under approval before a decision can be made." [src: SampleApprovalService.cs:L154-L155]
- "You cannot approve a sample you reviewed." [src: SampleApprovalService.cs:L163]
- "You cannot approve a sample you tested." [src: SampleApprovalService.cs:L170]
- "Cannot approve section: test order {id} (\"{code}\") requires system suitability, but only HPLC workspace tests (assay, dissolution) can supply one. Clear \"Requires system suitability\" for this test in Test Master." [src: SampleApprovalService.cs:L190-L191]
- "Cannot approve section: test order {id} (\"{code}\") lacks an HPLC run with a passed system suitability." [src: SampleApprovalService.cs:L202-L203]
- "Cannot approve section: test order {id} (\"{code}\") lacks an active {display name} entry." [src: SampleApprovalService.cs:L231-L232]
- "Cannot approve section: test order {id} (\"{code}\") has a pending stage." [src: SampleApprovalService.cs:L237-L238]
- "At least one test must be selected for retest." [src: SampleApprovalService.cs:L251]
- "One or more selected tests do not belong to this sample's current test orders." [src: SampleApprovalService.cs:L256]
- "Two analysts must be assigned - one per new sample - when requesting a new sample." [src: SampleApprovalService.cs:L267]
- "The two new samples must be assigned to two different analysts." [src: SampleApprovalService.cs:L269]
- "Both retest analysts must be active, eligible Analyst-role users." [src: SampleApprovalService.cs:L277]
- "Neither new sample's analyst may be the analyst who tested the original sample." [src: SampleApprovalService.cs:L282]
- "The 'Retest' cause of testing is not configured." [src: SampleApprovalService.cs:L290]
- "'{decision}' is not a valid sample-level decision." [src: SampleApprovalService.cs:L299]

Sections and scope (shared):
- "This sample has no tests in the selected laboratory section." [src: backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:L128; SectionClosureService.cs:L47]
- "This test belongs to a laboratory section you are not assigned to." [src: SampleSectionRollup.cs:L130, L142; SectionClosureService.cs:L45]
- "Choose a laboratory section - more than one section of this sample is waiting." [src: SampleSectionRollup.cs:L144]
- Wrong-state text is passed in by the caller: "Sample must be under review before it can be reviewed." or "Sample must be under approval before a decision can be made." [src: SampleSectionRollup.cs:L131-L138]

Close testing:
- "A reason is required to close testing." [src: backend/MicroLIMS.Application/Services/SectionClosureService.cs:L36-L37]
- "Testing can be closed only after another laboratory rejected the sample." [src: SectionClosureService.cs:L52]
- "This laboratory's testing is already closed." [src: SectionClosureService.cs:L56]
- "This laboratory has no open tests on the sample." [src: SectionClosureService.cs:L58]

Signature (shared):
- "Password verification failed. The signature was not applied." [src: documentation/knowledge/_raw/verified-roles-permissions.md:L142]
- "Too many failed signature attempts. Wait 15 minutes, then try again. The signature was not applied." [src: verified-roles-permissions.md:L143]

OOS documents (file checks, same text for upload and supersede) [src: backend/MicroLIMS.Application/Services/MaterialDocumentFileValidator.cs:L38-L83]:
- "File exceeds the maximum allowed size of {n} MB." (n = 25 with the shipped setting)
- "The uploaded file is empty."
- "File type '{ext}' is not supported. Allowed types: PDF, JPG, JPEG, PNG, WEBP, TIFF."
- "The declared content type '{type}' does not match the file extension '{ext}'."
- "The file content could not be validated because it is too short."
- "The file does not appear to be a valid WEBP image."
- "The file content does not match the expected format for '{ext}'. The file may be corrupted or misnamed."
- Controller-level (HTTP 400): "No file was provided."; "A replacement file is required."; "A reason for supersession is required."; "A reason for voiding is required." [src: backend/MicroLIMS.API/Controllers/OosInvestigationDocumentController.cs:L43, L75, L77, L100]
OOS documents (service):
- "OOS group \"{code}\" not found." [src: backend/MicroLIMS.Application/Services/OosInvestigationDocumentService.cs:L81, L104]
- "Document {id} not found for OOS group {code}." [src: OosInvestigationDocumentService.cs:L155, L195, L265]
- "The document file could not be retrieved." [src: OosInvestigationDocumentService.cs:L165]
- "Document storage failed. The upload was not completed." [src: OosInvestigationDocumentService.cs:L143]
- "Document integrity check failed. The file may have been altered. Please contact your System Administrator." [src: OosInvestigationDocumentService.cs:L174]
- "A supersession reason is required." [src: OosInvestigationDocumentService.cs:L191]
- "A voided document cannot be superseded. Upload a new document instead." [src: OosInvestigationDocumentService.cs:L198]
- "Replacement file storage failed. The supersession was not completed." [src: OosInvestigationDocumentService.cs:L250]
- "A void reason is required." [src: OosInvestigationDocumentService.cs:L261]
- "This document is already voided." [src: OosInvestigationDocumentService.cs:L268]

### Client messages (exact text)
- Decision controls (disabled-button rules, warnings "Select at least one test to retest.", "The two new samples must be assigned to two different analysts.", "The list of eligible analysts could not be loaded. Close and reopen this sample to try again.", and the out-of-spec approve warning): see SCR-APPROVAL-001 [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:85-92, 402-422, 459-463].
- Close Testing card warning: "Another laboratory has rejected this sample. You may close your own laboratory's remaining tests instead of continuing them." [src: ApprovalSignaturesCard.tsx:477-518].
- Return dialog: no validation; reason is optional [src: frontend/src/components/ReturnToAnalystDialog.tsx:62-136].
- Signature dialog: "Sign" disabled until a password is entered [src: frontend/src/components/SignatureDialog.tsx:52-75].
- Load and export errors: "Failed to load sample summary."; "Export failed."; "Failed to load the sample report."; "Failed to load the certificate of analysis."; "No approved laboratory sections are currently accessible." [src: frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx:189, 290; SampleReportPage.tsx:119; SampleCoaPage.tsx:283, 389-407].
- OOS page: "Failed to load OOS tracking items."; "Failed to load investigation documents."; "Failed to upload document."; "Failed to download document content."; "Failed to supersede document." [src: frontend/src/modules/oosTracking/OosTrackingPage.tsx:79, 414, 436, 454, 475].

## statuses

### Sample (SampleStatus)
| Enum | Value | UI label (where read) |
|---|---|---|
| Received | 0 | "Received" (report timeline); no label in lifecycle badge |
| InTesting | 1 | "Under Testing" (badge); "In testing" (sections, report) |
| UnderReview | 2 | "Under Review" (badge); "Under review" (report) |
| UnderApproval | 3 | "Under Approval" (badge); "Under approval" (report) |
| Approved | 4 | "Approved" |
| Rejected | 5 | "Rejected" |
| RetestRequested | 6 | "Retest Requested" (kanban) |
| Cancelled | 7 | No label in the lifecycle badge (the badge shows nothing for it); report page shows the raw value |
| Voided | 8 | "Voided" (report timeline, sections) |
Sources: [src: backend/MicroLIMS.Domain/Enums/SampleStatus.cs:L3; documentation/knowledge/_raw/backend-enums.md:L606-L619; frontend/src/modules/testingWorkspace/SampleLifecycleBadge.tsx:6-12; frontend/src/modules/testingWorkspace/SampleKanbanView.tsx:6-14; frontend/src/modules/testingWorkspace/sampleSummary/summaryFormatting.ts:1-26].

### Section sign-off (SectionSignoffStatus)
| Enum | Value | UI label (summaryFormatting) |
|---|---|---|
| InTesting | 0 | In testing |
| UnderReview | 1 | Under review |
| UnderApproval | 2 | Under approval |
| Approved | 3 | Approved |
| Rejected | 4 | Rejected |
| RetestRequested | 5 | Retest requested |
| Cancelled | 6 | Cancelled |
| Voided | 7 | Voided |
A cancelled section also shows as "Closed" with "{section} closed by {name} on {date}: {reason}" [src: SampleSummaryDialog.tsx:470-477]. Sources: [src: backend/MicroLIMS.Domain/Enums/SectionSignoffStatus.cs:L5; documentation/knowledge/_raw/backend-enums.md:L631-L643; summaryFormatting.ts:1-26].

### Test order (ApprovalStatus)
Pending 0, InProgress 1, ResultEntered 2, Reviewed 3, Approved 4, Rejected 5, RetestRequested 6, Voided 7, Cancelled 8 [src: backend/MicroLIMS.Domain/Enums/ApprovalStatus.cs:L3; backend-enums.md:L34-L47]. Test-level UI labels: UNCLEAR for this cluster (test chips belong to C2).

### Overall sample outcome (OverallSampleStatus)
InProgress 0, Approved 1, Rejected 2, RetestRequested 3, Voided 4, Cancelled 5 [src: backend/MicroLIMS.Domain/Enums/OverallSampleStatus.cs:L6; backend-enums.md:L411-L421]. Computed by SampleSectionRollup.Overall (BR-APPROVAL-019).

### Approval decision (ApprovalDecision)
Values: Approve, Reject, RetestRetainedSample, NewSampleRequest, Investigation, OOSInvestigation [src: backend/MicroLIMS.Domain/Enums/ApprovalDecision.cs:L4]. Reachable at sample approval: the first four [src: SampleApprovalService.cs:L17-L19]. UI labels: "Approve: Results conform to specifications"; "Not Conform (Final Conclusion): Close this sample"; "New Sample Required: Close and request new sample"; "Retest Retained Sample: Return to testing" [src: summaryFormatting.ts:73-78].

### Result status (ResultStatus), count, location and micro results
WithinLimits 1, AlertLimitExceeded 2, ActionLimitExceeded 3, OutOfSpecification 4, LimitsNotConfigured 5, RequiresReview 6, Absent 7, Detected 8, PendingConfirmation 9, NotDetected 10, NextStageRequired 11, Inconclusive 12 [src: backend/MicroLIMS.Domain/Enums/ResultStatus.cs:L7; backend-enums.md:L506-L522]. Report labels: "Within Limits", "Out of Specification", "Alert Limit Exceeded", "Action Limit Exceeded", "Limits Not Configured" [src: frontend/src/components/StatusBadge.tsx:17-26].

### Transitions (sample level; sections change first, then the sample rolls up)
- InTesting -> UnderReview, automatic, per section, when all its current tests are Ready. Event "All tests completed - automatically submitted for review". No password [src: SampleReviewService.cs:L72-L93].
- UnderReview -> UnderApproval, Submit Technical Review; signed (Reviewed); Samples.Review holder in section scope who did not test the section [src: SampleReviewService.cs:L95-L155].
- UnderReview -> InTesting, Return to Analyst; unsigned; count or analysis test in ResultEntered; Samples.Review holder [src: SampleReviewService.cs:L157-L269].
- UnderApproval -> Approved, Approve; signed (Approved); Samples.Approve holder who is not the reviewer and did not test [src: SampleApprovalService.cs:L143-L335].
- UnderApproval -> Rejected, Reject; signed (Rejected) [src: SampleApprovalService.cs:L353-L375].
- UnderApproval -> RetestRequested, Retest Retained Sample; signed (RetestRequested); selected tests superseded; one new sample (section status RetestRequested; the sample status follows the roll-up and becomes RetestRequested only when no section is still open or rejected) [src: SampleApprovalService.cs:L377-L438; SampleSectionRollup.cs:L78-L88].
- UnderApproval -> RetestRequested, New Sample Required; signed (Rejected meaning); selected tests superseded; two new samples (same roll-up rule) [src: SampleApprovalService.cs:L440-L503].
- Open section (InTesting, UnderReview, UnderApproval) -> Cancelled, Close Testing; signed (TestingClosed); only after another section is Rejected [src: SectionClosureService.cs:L31-L100].
- Origin sample RetestRequested -> Approved or Rejected, automatically, when the retest outcome is final [src: SampleApprovalService.cs:L541-L642].
- Test order (corrected): at review the test Status is set to Reviewed and its step from Ready to Reviewed (the review does not read the test Status); at approval Status becomes Approved and step Approved, or on rejection Status Rejected with the step unchanged; Return to Analyst needs Status ResultEntered and moves the step to Running (analysis) or Incubating (count) - a Reviewed test cannot be returned; the superseded flag is set on retest; Status Cancelled on testing closure (the draft wrongly listed "Reviewed -> Running or Incubating (return)") [src: SampleReviewService.cs:L137-L151, L236-L253; SampleApprovalService.cs:L336-L375, L490-L501; SectionClosureService.cs:L74-L88].
- Mixed sections: while any lab is still open the sample sits at the least advanced open status, even after another lab rejected [src: SampleSectionRollup.cs:L73-L77].
- Note: Reject and Close Testing give sections their own final state; the sample status and Overall outcome are computed from all sections (BR-APPROVAL-019).

### Review workflow event types (ReviewWorkflowEventType, shown in Workflow History)
SubmittedForReview, ReviewCompleted, SubmittedForApproval, ApprovalDecisionMade, SampleVoided, SampleCorrected, SectionTestingClosed, LaboratoryAdded, StorageLocationChanged [src: backend/MicroLIMS.Domain/Enums/ReviewWorkflowEventType.cs:L6-L32]. (verified) In this cluster only SubmittedForReview (automatic), ReviewCompleted, ApprovalDecisionMade and SectionTestingClosed are written; SubmittedForApproval is never written (git grep over backend .cs files finds only the enum member and a comment); the others belong to receiving, correction and storage workflows.

### OOS investigation document
Current, Superseded, Voided (UI chips) [src: frontend/src/modules/oosTracking/OosTrackingPage.tsx:640-646]. Stored as MaterialDocumentStatus: Current 0, Superseded 1, Voided 2 [src: backend/MicroLIMS.Domain/Enums/MaterialDocumentStatus.cs:L6-L11]. (verified) Transitions: Current -> Superseded when a replacement is uploaded (reason required); any non-Voided -> Voided through the void endpoint (no screen control); a new upload is offered only when no Current document exists [src: OosInvestigationDocumentService.cs:L185-L275].

## terminology
- Technical review: a reviewer's signed statement per laboratory section that the test data is complete [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:235-283]. Recorded as meaning Reviewed (BR-REVIEW-005).
- Release decision: a Section Head's signed decision per section; four options (see Statuses) [src: ApprovalSignaturesCard.tsx:287-475].
- Submit Technical Review / Submit Release Decision: the two signed forms.
- Comment (optional): internal comment on a review or decision. It never prints on the certificate [src: ApprovalSignaturesCard.tsx:263-272, 309-318].
- Certificate Remarks: text printed on the CoA, shown only for Approve. Blank prints "No remarks." [src: ApprovalSignaturesCard.tsx:333-345; SampleCoaPage.tsx:529-534].
- Return to Analyst: sends a count test back for re-entry (analysis tests in the backend, see GAP-300). Unsigned [src: SampleReviewService.cs:L157-L269].
- Tests to Retest: checklist of tests that move to a new sample; non-conforming tests pre-checked [src: ApprovalSignaturesCard.tsx:347-408].
- Retest Retained Sample: one new sample with the selected tests, same analyst assignments [src: SampleApprovalService.cs:L377-L438].
- New Sample Required: two new samples with the selected tests, one per chosen analyst [src: SampleApprovalService.cs:L440-L503].
- Not Conform (Final Conclusion): the Reject decision label, which closes the sample for this section [src: summaryFormatting.ts:73-78].
- Close Testing: a section stops its own open tests after another section rejected the sample. Open tests become Cancelled [src: SectionClosureService.cs:L74-L88].
- OOS group: all samples sharing one OosGroupCode (origin plus retests) [src: OosTrackingService.cs:L11-L24].
- Origin sample and retest sample: a retest sample points back to its origin through OriginSampleId [src: SampleApprovalService.cs:L79].
- Lab Investigation Report: investigation document attached to an OOS group, status Current, Superseded or Voided [src: OosTrackingPage.tsx:223-233, 640-646].
- Printable Report: the sample summary report page. Not a certificate [src: SCR-COA-001 above].
- Certificate of Analysis (CoA): the certificate page for approved or rejected samples; combined or per-section variant [src: SCR-COA-002 above].
- Archived copy: frozen PDF saved at a final decision, with SHA-256 hash [src: RecordArchiveService.cs:L12-L20].
- Electronic signature statements: the wording differs by screen (corrected; the draft said one map was copied everywhere). Reviewed is the same everywhere: "I have reviewed the test data and confirm it is complete and accurate." Approved: CoA "I approve the release of this sample for its intended use."; report page "I approve the release of this sample for its intended use."; Approval card and reportPrimitives "I approve the release of this record for its intended use." Rejected: CoA "I reject the release of this sample; it does not conform to specification."; report page "I reject this sample; the results do not conform to specification."; Approval card and reportPrimitives "I reject this record; it does not conform to specification." RetestRequested and InvestigationOrdered have statements on the report page and the card ("I am ordering a retest of the retained sample."; "I am ordering an investigation into these results."), not on the CoA. The CoA "Closed" statement "I am closing this laboratory's testing; it will not be resumed on this sample." is used only for closed section blocks; the signature trail on the card and the report page have no statement for TestingClosed (the card prints the raw value, the report page the humanized value) [src: SampleCoaPage.tsx:54-60; SampleReportPage.tsx:42-48; sampleSummary/summaryFormatting.ts:76-83; reportPrimitives.tsx:44-51]. (verified) See GAP-315.
- Signature meaning shown in the signature dialog at decision time is the radio label (for example "Approve: Results conform to specifications"), not the statements above [src: SampleSummaryDialog.tsx:623-630].

## reports
- Printable sample summary report, route /samples/:id/report: browser Print / Save PDF; archived copies list; footer "This is a controlled document generated by MicroLIMS. Any printed copy is uncontrolled." [src: SCR-COA-001 above; SampleReportPage.tsx:303-318].
- Certificate of Analysis, route /samples/:id/coa: combined or per-section variant; sections "Test Results by Location", "Test Results", "Remarks", "Overall Conclusion"; reviewer and approver signatories [src: SCR-COA-002 above].
- Server PDF export of the summary: GET /api/samples/{id}/summary/pdf; file name from the sample reference; application/pdf [src: backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L44-L51; backend/MicroLIMS.Application/Services/SampleSummaryService.cs:L351-L357, L383-L545].
- Word export of the summary: GET /api/samples/{id}/summary/word. No UI caller in frontend/src [src: SampleSummaryController.cs:L53-L60; documentation/knowledge/_raw/frontend-testing-workspace.md:L468, L603, L685 (service method, no caller)]. See GAP-310.
- Archived PDF: listed on the report page; download via GET /api/archived-records/{id}/download; integrity re-hash on read [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L26, L41; SampleReportPage.tsx:303; RecordArchiveService.cs:L88-L90].
- OOS Tracking: no export; investigation document download via the content endpoint [src: frontend/src/modules/oosTracking/services/OosTrackingService.ts:L71].
- Reports page tabs (Overview, Record Search, Trending, KPI and others) belong to C8 and are not described here.

## audit-trail
- Review completed: a ReviewWorkflowEvent (entity Sample, performer, name snapshot, comment, section) and an ElectronicSignature (meaning Reviewed), written in one transaction [src: ReviewGateService.cs:L32-L52; SampleReviewService.cs:L121-L123].
- Automatic submit for review: a ReviewWorkflowEvent with no signature [src: SampleReviewService.cs:L86-L89; ReviewGateService.cs:L57-L60].
- Approval decision: a ReviewWorkflowEvent with the decision value, and an ElectronicSignature with the meaning for that decision [src: SampleApprovalService.cs:L305-L307].
- Close testing: a ReviewWorkflowEvent (SectionTestingClosed) with the reason as comment, and an ElectronicSignature TestingClosed [src: SectionClosureService.cs:L62-L65].
- Test-order history (WorkflowHistory): review, approval, rejection, retest, new sample, close and return each write a note (texts in BR-REVIEW-010, BR-APPROVAL-012 to BR-APPROVAL-015, BR-APPROVAL-018) [src: SampleReviewService.cs:L143-L150; SampleApprovalService.cs:L341-L348, L366-L373, L415-L422, L493-L500; SectionClosureService.cs:L80-L87].
- Return to analyst: a TestReturnEvent (test order, reviewer, assigned analyst, reason, UTC time) [src: SampleReviewService.cs:L256-L266].
- Signature row: user id, full name, username and role snapshots, meaning, entity type and id, SignedAt, optional comment, IP address; append-only by design [src: documentation/knowledge/_raw/verified-roles-permissions.md:L144].
- Final archive: an ArchivedRecord with label and SHA-256 [src: SampleApprovalService.cs:L534-L535; RecordArchiveService.cs:L38-L80].
- OOS documents: uploaded by and at; supersession reason, superseded by and at; void reason, voided by and at [src: backend/MicroLIMS.Domain/Entities/OosInvestigationDocument.cs:L26-L43].
- Automatic change capture on SaveChanges is described in BusinessRules.md section 11 (not verified in this pass) [src: documents/DesignSpecification/BusinessRules.md:L206-L220].

## traceability
- Sample -> SampleSectionSignoff rows (one per section): status; reviewer and time; approver, decision and time; closer, reason and time; review, approval and close signatures; certificate remarks [src: SampleSectionRollup.cs:L44-L65; SampleApprovalService.cs:L309-L335; SectionClosureService.cs:L90-L95].
- Sample -> TestOrder (current and superseded). A superseded order is linked to its retest sample only through the new sample's OriginSampleId, not through a test-order field [src: SampleApprovalService.cs:L395-L409, L459-L473].
- Retest sample -> origin sample (OriginSampleId) -> OOS group (OosGroupCode) -> all samples in the group [src: OosTrackingService.cs:L11-L24, L72-L100].
- OOS group -> investigation documents (any non-voided shows as attached); supersession chain via SupersededByDocumentId [src: OosTrackingService.cs:L47-L53; OosInvestigationDocument.cs:L33-L34].
- Sample -> ReviewWorkflowEvent timeline, shown in Workflow History and in the PDF TIMELINE block [src: SampleSummaryDialog.tsx:39-51; SampleSummaryService.cs:L516-L524].
- Sample -> ArchivedRecord (frozen PDF at final decision; the origin is archived again after OOS resolution) [src: SampleApprovalService.cs:L534-L535, L636-L639].
- TestOrder -> TestReturnEvent (one per return) [src: SampleReviewService.cs:L256-L265].
- Two new samples from one New Sample Required decision share the OosGroupCode and both carry the origin's id as OriginSampleId [src: SampleApprovalService.cs:L455-L488].
- The Sample Summary dialog links to the Printable Report and View COA pages [src: SampleSummaryDialog.tsx:388-432].

## gaps
Existing gaps referenced, not duplicated: GAP-009 (return unsigned), GAP-010 (count and pathogen steps unsigned), GAP-017 (decision set), GAP-018 (review and approval design), GAP-043 (OOS void control and CoA body not read).

### GAP-300 Return to Analyst scope: service versus UI
- **Question:** Should Return to Analyst be offered for analysis (non-count) tests, as the service allows, or count tests only, as the UI offers?
- **Affected:** MOD-REVIEW-001, SCR-REVIEW-002, WF-REVIEW-002 (Phase 4).
- **Facts:** The service accepts CountTest and analysis workflows and refuses others [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L188-L189]. The UI button is shown only for count tests [src: frontend/src/modules/testingWorkspace/sampleSummary/TestResultsSection.tsx:33-45, 109-129].
- **Why it matters:** The guide must state one rule. Analysis-test return is reachable only by a direct API call today.
- **Needs confirmation:** Intended scope of return to analyst.
- **Who or what:** QA; Section Head.

### GAP-301 Return to Analyst: separation of duties and sample-route check
- **Question:** Should the reviewer who performed a test be prevented from returning it, as they are prevented from reviewing it?
- **Affected:** MOD-REVIEW-001, WF-REVIEW-002 (Phase 4).
- **Facts:** The return path calls no SegregationOfDutiesGuard check [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L157-L269]; the review path does [src: SampleReviewService.cs:L109-L113]. The route id is not compared with the test order's sample [src: backend/MicroLIMS.API/Controllers/SampleReviewController.cs:L44-L54].
- **Why it matters:** The guide must say whether a reviewer can return their own test.
- **Needs confirmation:** Intended rule for returns.
- **Who or what:** QA; Section Head.

### GAP-302 New Sample Required analysts: separation of duties and section scope
- **Question:** Should the two new-sample analysts be checked against everyone who tested the original sample (not only the analyst assigned to each selected test), and against the decider's section scope?
- **Affected:** MOD-APPROVAL-001, SCR-APPROVAL-001.
- **Facts:** The UI text says "neither may be whoever tested the original sample" [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:410-457]. The service checks only AssignedAnalystId of selected tests [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L279-L283]. Other performers are checked by the guard elsewhere (DidUserPerformTestAsync) [src: SegregationOfDutiesGuard.cs:L25-L54]. The analysts only need Role.Type Analyst, active and unlocked; no section scope check [src: SampleApprovalService.cs:L272-L277].
- **Why it matters:** The UI promise is wider than the check.
- **Needs confirmation:** Intended rule for the two new analysts.
- **Who or what:** QA; Section Head.

### GAP-303 Rejection scope: whole sample or one section
- **Question:** Does Reject close the whole sample, or only the deciding section?
- **Affected:** MOD-APPROVAL-001, statuses of the sample.
- **Facts:** The service header says rejecting "rejects the whole sample" [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L12-L16]. The Reject branch says a rejection "no longer closes any other lab still open" [src: SampleApprovalService.cs:L360-L362]. The roll-up keeps the sample at the least advanced open status while another section is open [src: backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:L73-L77].
- **Why it matters:** The status words users see for a rejected multi-lab sample depend on this.
- **Needs confirmation:** Intended behaviour; the header comment may be stale.
- **Who or what:** Developer; QA.

### GAP-304 Approval of out-of-specification results
- **Question:** Should the server block or require a reason when a section with out-of-specification results is approved?
- **Affected:** MOD-APPROVAL-001, SCR-APPROVAL-001.
- **Facts:** The UI warns that approval certifies out-of-spec results as conforming [src: frontend/src/modules/testingWorkspace/sampleSummary/ApprovalSignaturesCard.tsx:459-463]. The server approve path checks only system suitability and analysis entries [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L173-L242].
- **Why it matters:** The guide must state whether approval of OOS results is possible and what it certifies.
- **Needs confirmation:** Intended control.
- **Who or what:** QA; Section Head.

### GAP-305 OOS detection and group opening
- **Question:** Should an OOS group be opened automatically when a result is out of specification, or only when a Section Head chooses a retest?
- **Affected:** MOD-OOS-001, WF-OOS-001 (Phase 4).
- **Facts:** An OutOfSpecification result is recorded by the result classifier [src: backend/MicroLIMS.Application/Services/SpecLimitParser.cs:L209-L224]. An OOS group code is written only by the retest decision [src: SampleApprovalService.cs:L392-L393, L447-L448]. There is no OOS record entity [src: backend/MicroLIMS.Domain/Entities/OosInvestigationDocument.cs]. The OOSInvestigation decision is never written [src: backend/MicroLIMS.Application/Services/Kpi/OosRateKpi.cs:L5] (path corrected from the draft; the file comment says nothing writes the OOSInvestigation decision) (verified).
- **Why it matters:** An OOS that is not retested never appears in OOS Tracking.
- **Needs confirmation:** Intended trigger for an OOS group.
- **Who or what:** QA; laboratory owner.

### GAP-306 Certificate of Analysis: availability gate
- **Question:** Is it intended that the server returns certificate data for samples that are not yet approved or rejected?
- **Affected:** MOD-COA-001, SCR-COA-002.
- **Facts:** The page refuses non-final samples [src: frontend/src/modules/testingWorkspace/SampleCoaPage.tsx:363]. The server returns a summary for any status, using section scope for non-final samples [src: backend/MicroLIMS.Application/Services/SampleSummaryService.cs:L84-L90; backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L35-L40].
- **Why it matters:** The guide describes the page gate; the API gate is weaker.
- **Needs confirmation:** Intended API rule.
- **Who or what:** Developer; QA.

### GAP-307 Certificate signatory fallback without a signature
- **Question:** Should the CoA print a reviewer or approver name and time when no signature row exists?
- **Affected:** MOD-COA-001, SCR-COA-002.
- **Facts:** The signature block falls back to the stored name and time when no matching signature exists [src: frontend/src/modules/testingWorkspace/SampleCoaPage.tsx:126-147, 413-416, 553-554].
- **Why it matters:** A printed name may look signed when it is not.
- **Needs confirmation:** Intended printing rule.
- **Who or what:** QA.

### GAP-308 Certificate and signature meaning for New Sample Required
- **Question:** What should the certificate and the signature trail show for a New Sample Required decision?
- **Affected:** MOD-APPROVAL-001, MOD-COA-001.
- **Facts:** The signature meaning is Rejected [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L297]. The UI label is "New Sample Required: Close and request new sample" [src: frontend/src/modules/testingWorkspace/sampleSummary/summaryFormatting.ts:73-78]. The deciding section is set to RetestRequested; the origin sample reaches Approved or Rejected only through OOS propagation (BR-APPROVAL-021) [src: SampleApprovalService.cs:L377-L393, L541-L627].
- **Why it matters:** A retest-spawning decision is printed as a rejection.
- **Needs confirmation:** Intended meaning and printed wording.
- **Who or what:** QA; Section Head.

### GAP-309 DISCREPANCY: review and approval notifications versus BusinessRules.md
- **Question:** Are the approval and review notifications counted per sample or per test order?
- **Affected:** MOD-DASH-001 (C1), MOD-APPROVAL-001, MOD-REVIEW-001.
- **Facts:** BusinessRules.md section 12 says "Approval Waiting: any TestOrder.Status == Reviewed" and "Review Waiting: any TestOrder.Status == ResultEntered" [src: documents/DesignSpecification/BusinessRules.md:L230-L231]. The code counts samples in section state UnderApproval and UnderReview for Section Head/Admin and Reviewer/Section Head/Admin [src: backend/MicroLIMS.Application/Services/DashboardNotificationService.cs:L171-L189].
- **Why it matters:** Counts shown in the notification differ from the spec's unit.
- **Needs confirmation:** Intended unit.
- **Who or what:** QA.

### GAP-310 Word export of the sample summary
- **Question:** Is the Word export of the sample summary intended to be available to users?
- **Affected:** MOD-COA-001.
- **Facts:** Endpoint GET /api/samples/{id}/summary/word exists [src: backend/MicroLIMS.API/Controllers/SampleSummaryController.cs:L53-L60]. No frontend caller found [src: documentation/knowledge/_raw/frontend-testing-workspace.md:L468, L603, L685].
- **Why it matters:** The guide must not list an export that users cannot reach.
- **Needs confirmation:** Keep or retire.
- **Who or what:** Developer; QA.

### GAP-311 Which PDF is the record
- **Question:** Is the archived PDF (frozen at the final decision) the controlled copy, and what is the on-demand PDF export for?
- **Affected:** MOD-COA-001.
- **Facts:** The archive is written at FinalizeIfClosedAsync and frozen with a SHA-256 [src: backend/MicroLIMS.Application/Services/SampleApprovalService.cs:L528-L539; RecordArchiveService.cs:L12-L20]. The "Export PDF" button renders the summary on demand [src: frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx:433-442; SampleSummaryController.cs:L44-L51].
- **Why it matters:** Two PDFs for one sample can differ after later changes.
- **Needs confirmation:** Intended controlled copy.
- **Who or what:** QA.

### GAP-312 Reviewer separation of duties across sections
- **Question:** Should a reviewer who tested a different section of the same sample be allowed to review this section?
- **Affected:** MOD-REVIEW-001, MOD-APPROVAL-001.
- **Facts:** The reviewer check covers only the current test orders of the section being reviewed [src: backend/MicroLIMS.Application/Services/SampleReviewService.cs:L108-L113]. The approver check is also per section [src: SampleApprovalService.cs:L165-L171].
- **Why it matters:** The guide must say whether separation is per section or per sample.
- **Needs confirmation:** Intended rule.
- **Who or what:** QA; Section Head.


### GAP-313 OOS investigation documents: no signature, no void screen
- **Question:** Should uploading, superseding or voiding a lab investigation document need an electronic signature, and should users be able to void a document from the screen?
- **Affected:** MOD-OOS-001, SCR-OOS-001, WF-OOS-001 (Phase 4).
- **Facts:** The upload, supersede and void service methods call no signature service; supersede and void record reason, user and time on the document row [src: backend/MicroLIMS.Application/Services/OosInvestigationDocumentService.cs:L97-L275]. The void endpoint and the frontend service method exist; the OOS Tracking page has no control that calls them [src: backend/MicroLIMS.API/Controllers/OosInvestigationDocumentController.cs:L96-L105; frontend/src/modules/oosTracking/OosTrackingPage.tsx].
- **Why it matters:** The guide cannot tell users how to void a wrongly uploaded report, and must not imply the actions are signed.
- **Needs confirmation:** Intended signing rule and whether a void control is wanted.
- **Who or what:** QA; laboratory owner.

### GAP-314 Archived PDF integrity failure is a response header only
- **Question:** What should a user see when an archived copy fails its integrity re-check?
- **Affected:** MOD-COA-001, WF-COA-001 (Phase 4).
- **Facts:** The download endpoint returns the file and sets the header "X-Archive-Integrity: FAILED" on a hash mismatch [src: backend/MicroLIMS.API/Controllers/ArchivedRecordsController.cs:L35-L48]. Whether the report page reads this header was not checked.
- **Why it matters:** The guide must not promise a visible warning that may not exist.
- **Needs confirmation:** Intended user-visible behaviour; the front-end handling of the header.
- **Who or what:** Developer; QA.

### GAP-315 Signature statement wording differs between screens
- **Question:** Which wording is the official statement of meaning for Approved, Rejected and Closed signatures?
- **Affected:** MOD-APPROVAL-001, MOD-COA-001, SCR-APPROVAL-001, SCR-COA-001, SCR-COA-002.
- **Facts:** The certificate, the report page and the approval card print different sentences for Approved and Rejected ("sample" versus "record", "release" present or absent); the trail has no statement for the close-testing signature [src: frontend/src/modules/testingWorkspace/SampleCoaPage.tsx:54-60; SampleReportPage.tsx:42-48; sampleSummary/summaryFormatting.ts:76-83].
- **Why it matters:** The same signature reads differently depending on where it is viewed.
- **Needs confirmation:** The approved wording and where it must appear.
- **Who or what:** QA.
