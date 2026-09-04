# MicroLIMS Change Control Record

**Change Control ID:** `CC-DC-R1B-007`  
**Title:** Release 1b Work Package 7 (WP7) — Document Control Frontend / UX Completion  
**Module:** Document Control (Release 1b)  
**Lifecycle Phase:** Implementation & Verification  
**Date:** 2026-09-04  
**Author:** AI Implementation Agent (Pair Programming)  
**Quality / Regulatory Baseline:** FDA 21 CFR Part 11, EU GMP Annex 11, GAMP 5 (Category 4 / Configured), ISO 17025:2017 §8.3  

---

### 1. Description of Change

Work Package 7 (WP7) delivers the user-facing interface completion for the Release 1b Document Control capabilities delivered across WP1 through WP6. Specifically, WP7 addresses:

1. **Dual PDF Inspection Workspace (`DC-URS-067` / `FS-1b-067`):**
   - Implements `DualPdfComparisonViewer.tsx` providing side-by-side inspection of the active/effective controlled PDF vs proposed draft revision PDF.
   - Integrates side-by-side inspection launch from `TechnicalReviewDrawer.tsx`, `DocumentDetailPage.tsx`, and `ApprovalWorkspaceDialog.tsx`.
   - Incorporates independent and synchronized navigation, cryptographic SHA-256 verification indicator badges, and controlled copy download safeguards.

2. **Segregation of Duties (SoD) UI Enforcement & Tooltips (`DC-URS-078` / `FS-1b-078`):**
   - Enforces visual indicators and contextual tooltips across review and approval action triggers (`TechnicalReviewDrawer.tsx`, `ApprovalWorkspaceDialog.tsx`, `DocumentDetailPage.tsx`).
   - Clearly explains blocking reasons when user is Author (blocked from Technical Review / Approval) or Reviewer (blocked from Approval).
   - Enforces System Administrator non-exemption directly in UI controls and banners.

3. **Complete Regulatory Audit Trail Formatting (`DC-URS-079` / `FS-1b-079`):**
   - Enhances human-readable presentation for all Release 1b review, revision, e-signature, approval, and worker events in `DocumentDetailPage.tsx` Tab 4.
   - Adds distinct visual iconography and chip styling for `ActorType.System` vs `ActorType.User` actions.
   - Clarifies field change transitions and reasons.

4. **Obsolescence Recommendation Approval Workspace Presentation (`DC-URS-052` / `FS-1b-052`):**
   - Customizes `ApprovalWorkspaceDialog.tsx` to handle obsolescence-targeted approval tasks (`SubmissionNotes` referencing Periodic Review obsolescence recommendation).
   - Renders clear decommissioning warnings, justification displays, and explicit obsolescence decision controls.

5. **Future Effective Revision Status Visualization (`DC-URS-177`):**
   - Renders a prominent countdown/pending activation alert banner in `DocumentDetailPage.tsx` header and Version History tab for revisions in `FutureEffective` status, indicating scheduled automated worker activation.

---

### 2. Justification & Regulatory Impact

- **Regulatory Compliance:**
  - 21 CFR §11.10(e) & Annex 11 Clause 9: Complete audit trail readability for all quality events.
  - 21 CFR §11.10(d) & Annex 11 Clause 12: Rigorous Segregation of Duties prevention in UI and server logic.
  - GAMP 5 §D4 & ISO 17025 §8.3: Reviewer ability to cross-examine proposed changes directly against the currently authorized effective SOP (`DC-URS-067`).
- **Risk Assessment:**
  - Risk Level: Low. Changes are confined to frontend presentation components and client-side validation cues.
  - Zero backend schema modifications.
  - Full backward compatibility maintained for all 707 existing automated backend regression tests.

---

### 3. Affected Components

- `frontend/src/modules/documentControl/components/DualPdfComparisonViewer.tsx` (New Component)
- `frontend/src/modules/documentControl/components/TechnicalReviewDrawer.tsx` (Enhanced)
- `frontend/src/modules/documentControl/components/ApprovalWorkspaceDialog.tsx` (Enhanced)
- `frontend/src/modules/documentControl/pages/DocumentDetailPage.tsx` (Enhanced)
- `docs/Release_1b_Requirements_Traceability_Matrix.md` (RTM status update)

---

### 4. Verification Plan

1. Frontend static analysis & production build: `npm run build` (`tsc -b && vite build`) with 0 errors.
2. Backend full regression suite: `dotnet test` with 707/707 passing tests.
3. RTM traceability update: Transition `DC-URS-052`, `DC-URS-067`, `DC-URS-078`, `DC-URS-079` to `IMPLEMENTED`.
4. Work Package 7 Completion Report authoring.

---

### 5. Approval & Sign-Off

- **Lead Engineer / Pair Programming Agent:** AI Specialist — 2026-09-04
- **Status:** **APPROVED & EXECUTING**
