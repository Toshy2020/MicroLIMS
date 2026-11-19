# C8 action-availability reason sentences

For the user's wording review: every sentence the action-availability checks can return, as written in code. `{name}` marks a value filled in at run time. Source lines are for the sentence as it appears in the file.

| Record kind | Action | Sentence | Source file:line |
|---|---|---|---|
| Sample | review.complete | Your role does not include this action. | backend/MicroLIMS.Application/Services/ActionCheck.cs:20; backend/MicroLIMS.Application/Services/ActionAvailabilityService.cs:182 (permission Samples.Review) |
| Sample | review.complete | Sample must be under review before it can be reviewed. | backend/MicroLIMS.Application/Services/SampleReviewService.cs:234; SampleSectionRollup.cs:169, 175 |
| Sample | review.complete | This sample has no tests in the selected laboratory section. | backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:165 |
| Sample | review.complete | This test belongs to a laboratory section you are not assigned to. | backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:167, 179 |
| Sample | review.complete | Choose a laboratory section - more than one section of this sample is waiting. | backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:181 |
| Sample | review.complete | You cannot review a sample you tested. | backend/MicroLIMS.Application/Services/SampleReviewService.cs:240 |
| Sample | review.complete | Cannot complete review: one or more tests are not in Ready step. | backend/MicroLIMS.Application/Services/SampleReviewService.cs:243 |
| Sample | approval.approve, approval.reject | Your role does not include this action. | backend/MicroLIMS.Application/Services/ActionCheck.cs:20; backend/MicroLIMS.Application/Services/ActionAvailabilityService.cs:182 (permission Samples.Approve) |
| Sample | approval.approve, approval.reject | Sample must be under approval before a decision can be made. | backend/MicroLIMS.Application/Services/SampleApprovalService.cs:155; SampleSectionRollup.cs:169, 175 |
| Sample | approval.approve, approval.reject | This sample has no tests in the selected laboratory section. | backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:165 |
| Sample | approval.approve, approval.reject | This test belongs to a laboratory section you are not assigned to. | backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:167, 179 |
| Sample | approval.approve, approval.reject | Choose a laboratory section - more than one section of this sample is waiting. | backend/MicroLIMS.Application/Services/SampleSectionRollup.cs:181 |
| Sample | approval.approve, approval.reject | You cannot approve a sample you reviewed. | backend/MicroLIMS.Application/Services/SampleApprovalService.cs:159 |
| Sample | approval.approve, approval.reject | You cannot approve a sample you tested. | backend/MicroLIMS.Application/Services/SampleApprovalService.cs:166 |
| Sample | approval.approve only | Cannot approve section: test order {id} ("{testCode}") requires system suitability, but only HPLC workspace tests (assay, dissolution) can supply one. Clear "Requires system suitability" for this test in Test Master. | backend/MicroLIMS.Application/Services/SampleApprovalService.cs:200 |
| Sample | approval.approve only | Cannot approve section: test order {id} ("{testCode}") lacks an HPLC run with a passed system suitability. | backend/MicroLIMS.Application/Services/SampleApprovalService.cs:204 |
| Sample | approval.approve only | Cannot approve section: test order {id} ("{testCode}") lacks an active {displayName} entry. | backend/MicroLIMS.Application/Services/SampleApprovalService.cs:232 |
| Sample | approval.approve only | Cannot approve section: test order {id} ("{testCode}") has a pending stage. | backend/MicroLIMS.Application/Services/SampleApprovalService.cs:238 |
| Sample | sample.void | Your role does not include this action. | backend/MicroLIMS.Application/Services/ActionAvailabilityService.cs:182 (permission Samples.Correct) |
| Sample | sample.void | This test belongs to a laboratory section you are not assigned to. | backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:249 |
| Sample | sample.void | Sample is already voided. | backend/MicroLIMS.Application/Services/SampleCorrectionService.cs:290 |
| Sample | sample.assign | Your role does not include this action. | backend/MicroLIMS.Application/Services/ActionAvailabilityService.cs:182 (permission Samples.AssignAnalyst) |
| Sample | sample.assign | This test belongs to a laboratory section you are not assigned to. | backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:249 |
| Sample | sample.assign | This sample has no open tests you can assign. | backend/MicroLIMS.Application/Services/SampleAssignmentService.cs:57 |
| Test order | review.return | Your role does not include this action. | backend/MicroLIMS.Application/Services/ActionAvailabilityService.cs:182 (permission Samples.Review) |
| Test order | review.return | This test belongs to a laboratory section you are not assigned to. | backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:193 |
| Test order | review.return | Cannot return a test to the analyst after the sample has been reviewed. Use Reject or Retest at approval instead. | backend/MicroLIMS.Application/Services/SampleReviewService.cs:340 |
| Test order | review.return | Cannot return a test order in {status} status. Only test orders in ResultEntered status can be returned to the analyst. | backend/MicroLIMS.Application/Services/SampleReviewService.cs:344 |
| Test order | review.return | Test definition "{testCode}" not found. | backend/MicroLIMS.Application/Services/SampleReviewService.cs:349 |
| Test order | review.return | Return to Analyst is only supported for Count Test and result-entry (analysis) workflows. "{testCode}" is a {workflowType} workflow. | backend/MicroLIMS.Application/Services/SampleReviewService.cs:352 |
| Test order | review.return | Cannot return {testCode}: assay {dependentTestCode} has already been reviewed. | backend/MicroLIMS.Application/Services/SampleReviewService.cs:367 |
| Test order | test.take | You do not have permission to take or release tests. | backend/MicroLIMS.Application/Services/TestSelfAssignmentService.cs:157 (permission Tests.SelfAssign) |
| Test order | test.take | This test belongs to a laboratory section you are not assigned to. | backend/MicroLIMS.Application/Services/TestSelfAssignmentService.cs:110 |
| Test order | test.take | Taking tests is switched off in this section. Ask a Section Head to assign this test. | backend/MicroLIMS.Application/Services/TestSelfAssignmentService.cs:113 |
| Test order | test.take | Already taken by {fullName}. | backend/MicroLIMS.Application/Services/TestSelfAssignmentService.cs:116 |
| Test order | test.take | This test is closed, replaced or outsourced and can no longer be taken. | backend/MicroLIMS.Application/Services/TestSelfAssignmentService.cs:118 |
| Test order | test.take | User '{fullName}' is locked out. | backend/MicroLIMS.Application/Services/AnalystAssignmentRules.cs:54 |
| Test order | test.take | '{fullName}' cannot be assigned tests - only Analysts, Reviewers and Section Heads can. | backend/MicroLIMS.Application/Services/AnalystAssignmentRules.cs:58-59 |
| Test order | test.take | '{fullName}' is not a member of the {sections} laboratory section{s}. | backend/MicroLIMS.Application/Services/AnalystAssignmentRules.cs:71-72 |
| Test order | test.take (not reachable from availability, see notes) | User '{fullName}' is not active. | backend/MicroLIMS.Application/Services/AnalystAssignmentRules.cs:52 |
| Test order | test.take (not reachable from availability, see notes) | '{fullName}' is a Section Head - a Section Head can only assign themselves. | backend/MicroLIMS.Application/Services/AnalystAssignmentRules.cs:63 |
| Test order | test.release | Only the analyst who holds this test can release it. | backend/MicroLIMS.Application/Services/TestSelfAssignmentService.cs:139 |
| Test order | test.release | This test can no longer be released. Ask a Section Head to reassign it. | backend/MicroLIMS.Application/Services/TestReleaseRules.cs:20 (used at TestSelfAssignmentService.cs:141) |
| Media lot | media.release | Your role does not include this action. | backend/MicroLIMS.Application/Services/ActionAvailabilityService.cs:182 (permissions Media.Prepare and Media.Release) |
| Media lot | media.release | This record was made from another laboratory section's material and you are not assigned to that section. | backend/MicroLIMS.Application/Services/UserSectionScopeService.cs:333, 344 |
| Media lot | media.release | Media lot {lotNumber} has already been decided ({approvalStatus}). | backend/MicroLIMS.Application/Services/MediaReleaseService.cs:69 |
| Media lot | media.release | Media lot {lotNumber} has no evaluation record. | backend/MicroLIMS.Application/Services/MediaReleaseService.cs:73 |
| Media lot | media.release | Media lot {lotNumber}'s evaluation is not complete yet. | backend/MicroLIMS.Application/Services/MediaReleaseService.cs:81 |
| Media lot | media.release | Media lot {lotNumber}'s evaluation did not conform - it cannot be released. | backend/MicroLIMS.Application/Services/MediaReleaseService.cs:83 |
| Media lot | media.release | You cannot approve a media lot you prepared or evaluated. | backend/MicroLIMS.Application/Services/MediaReleaseService.cs:87 |
| Document revision | document.approve | User is inactive or not found. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:409, 440 |
| Document revision | document.approve | Cannot execute decision on approval task in status '{status}'. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:467 |
| Document revision | document.approve | Segregation of Duties Violation: The author of a document revision cannot approve or decide upon their own revision. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:819 |
| Document revision | document.approve | Segregation of Duties Violation: A technical reviewer for this revision cannot approve or decide upon this revision. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:831 |
| Document revision | document.approve | Only the assigned approver or Document Controller may execute an approval decision. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:476 |
| Document revision | document.approve | Your role does not hold the Documents.Approve permission required to execute an approval decision. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:479 |
| Document revision | document.approve | This document type requires approval by Quality Assurance. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:750, 782 |
| Document revision | document.approve | An electronic signature for approval has already been applied to revision {revisionNumber}. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:510 |
| Document revision | document.approve | Cannot approve document revision: Prerequisites not met. Errors: {error list, joined with "; "} | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:502 |
| Document revision | document.approve (error in the list above) | Revision is in status '{revisionStatus}'. Status must be 'AwaitingApproval'. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:845 |
| Document revision | document.approve (error in the list above) | An active Controlled PDF file is required. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:852 |
| Document revision | document.approve (error in the list above) | Technical review has not been completed. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:859 |
| Document revision | document.approve (error in the list above) | Mandatory technical review findings remain unresolved. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:867 |
| Document revision | document.approve (error in the list above) | Multi-category revision impact assessment is incomplete. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:876 |
| Document revision | document.approve (error in the list above) | One or more originating review findings in change items remain unaddressed. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:884 |
| Document revision | document.approve (error in the list above) | Segregation of Duties Violation: You are the author of this revision and cannot approve it. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:892 |
| Document revision | document.approve (error in the list above) | Segregation of Duties Violation: You participated in technical review and cannot approve this revision. | backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs:904 |

Notes for the wording review:

- Count: 55 distinct sentences, each interpolated template counted once. Rows that repeat a sentence for another action (for example "Your role does not include this action.") are not counted again.
- Two take sentences (`User '{fullName}' is not active.` and the Section Head sentence) cannot be returned by the take availability check. The permission check runs first and requires an active user, and the caller is always the assignee. They are listed for completeness.
- The document readiness sentences at lines 892 and 904 repeat the segregation rules of lines 819 and 831, which the decision check runs first. Through the availability endpoint they are effectively unreachable.
- `approval.reject` uses the same sentences as `approval.approve`, except the four "Cannot approve section" sentences, which apply only to approval.
- Sentences that are not reasons: "Choose a laboratory section." and "Choose a laboratory section - you belong to more than one." (UserSectionScopeService.cs:162, 175) are not returned by these checks.
- A record the caller may not read returns 404 with "Record not found." and no reason sentence.
