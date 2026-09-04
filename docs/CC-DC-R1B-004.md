# Controlled Change Record: CC-DC-R1B-004
## Release 1b — Work Package 4 (WP4) 21 CFR Part 11 Electronic Signature Integration

**Document Identifier:** `CC-DC-R1B-004`  
**Change Request Date:** September 4, 2026  
**Change Classification:** Minor — Backward Compatible Service Integration & Clean Extension  
**Status:** APPROVED & BASELINED  
**Initiator:** Antigravity AI (Pair Programmer)  
**System Classification:** GAMP 5 Category 5 / 21 CFR Part 11 & EU Annex 11 Compliant  

---

### 1. Change Description & Rationale

#### 1.1 Purpose
Work Package 4 (WP4) implements 21 CFR Part 11 Electronic Signature integration for the Document Control approval workflow, fulfilling `DC-URS-076` (Authenticated electronic signature for approval), `DC-URS-077` (Electronic signature record completeness), `DC-URS-078` (Segregation of Duties enforcement), and `DC-URS-079` (Audit trail completeness).

The implementation strictly reuses the existing, qualified `IElectronicSignatureService`, the `ElectronicSignatures` database table, and the PostgreSQL immutability trigger (`trg_electronicsignatures_immutable`).

#### 1.2 Affected Baselined Components
1. **`backend/MicroLIMS.Application/DTOs/DocumentControl/ApprovalDtos.cs`:**
   - Extend `ExecuteApprovalDecisionRequest` with optional `string? Password = null`.
   - Add DTO `ElectronicSignatureDto` to expose signature audit records to the API and frontend.
   - Extend `ApprovalDossierDto` with `ElectronicSignatureDto? ActiveSignature = null`.
   - Rationale: Enables password transmission during approval decision execution and signature record retrieval without modifying underlying entity schemas.
2. **`backend/MicroLIMS.Application/Interfaces/DocumentControl/IDocumentApprovalService.cs`:**
   - Add method `Task<ElectronicSignatureDto?> GetApprovalSignatureAsync(int approvalTaskId, int userId);`
   - Extend `ExecuteApprovalDecisionAsync` signature or documentation to process password re-authentication for `Approve` decisions.
   - Rationale: Exposes clean signature verification and retrieval interface.
3. **`backend/MicroLIMS.Application/Services/DocumentControl/DocumentApprovalService.cs`:**
   - Inject `IElectronicSignatureService`.
   - In `ExecuteApprovalDecisionAsync`, when `request.Decision == DocumentApprovalDecision.Approve`:
     1. Strictly validate password presence (must not be null or empty).
     2. Call `_signatureService.SignAsync(userId, request.Password, SignatureMeaning.Approved, "DocumentRevision", revision.Id, request.DecisionNotes, ipAddress)`.
     3. Ensure both the electronic signature and the document status change commit atomically within the same `DbContext` transaction.
     4. Log semantic audit events `ElectronicSignatureApplied` and `DocumentRevisionApproved`.
   - Add `GetApprovalSignatureAsync` to query the immutable signature record bound to the revision/action.
4. **`backend/MicroLIMS.API/Controllers/DocumentControl/DocumentApprovalController.cs`:**
   - Add `POST /api/document-control/approvals/{id}/sign`: dedicated signing endpoint forwarding to `ExecuteApprovalDecisionAsync` with `Decision = Approve`.
   - Add `GET /api/document-control/approvals/{id}/signature`: dedicated endpoint to view the immutable signature manifest.
   - Capture client IP address from `HttpContext.Connection.RemoteIpAddress`.
5. **`frontend/src/modules/documentControl/components/ElectronicSignatureDialog.tsx`:**
   - Standalone Part 11 compliant re-authentication modal displaying Document Code, Title, Revision, Meaning, Signer Full Name/Role, regulatory warning notice, and password entry field.
6. **`frontend/src/modules/documentControl/components/ApprovalWorkspaceDialog.tsx`:**
   - Wire "Confirm Approve" to invoke `ElectronicSignatureDialog` for password entry before submitting decision.

---

### 2. Risk & Impact Assessment

| Evaluation Dimension | Assessment | Justification |
|---|---|---|
| **Schema Impact** | **Zero / No Schema Changes** | Reuses existing `ElectronicSignatures` table with `EntityType = "DocumentRevision"` and `EntityId = revision.Id`. No migrations or table alterations required. |
| **Database Immutability** | **Preserved & Enforced** | `trg_electronicsignatures_immutable` PostgreSQL trigger remains active and prevents any `UPDATE` or `DELETE` on signature rows. |
| **Data Migration Risk** | **None** | No existing tables or data are altered. |
| **API Compatibility** | **100% Backward Compatible** | Optional password field added to request record; dedicated `/sign` and `/signature` routes added cleanly. |
| **Segregation of Duties** | **Hard Invariant Preserved** | Author $\neq$ Reviewer $\neq$ Approver invariant strictly checked before password validation and signing. Admins are non-exempt. |
| **Transaction Integrity** | **Atomic** | Signature insertion and revision state mutation commit together in one atomic transaction; failures leave zero orphaned state. |
| **Regression Risk** | **Zero** | 659 baseline backend tests verified; new tests are purely additive. |

---

### 3. Affected Requirements

- **`DC-URS-076`**: Authenticated electronic signature for approval (Part 11 password re-entry).
- **`DC-URS-077`**: Electronic signature record completeness (User, Role, UTC timestamp, Meaning, Entity binding).
- **`DC-URS-078`**: Segregation of Duties enforcement.
- **`DC-URS-079`**: Complete audit trail for electronic signatures and approvals.

---

### 4. Verification & Testing Requirements

1. **Unit Testing (`DocumentControlElectronicSignatureUnitTests.cs`):**
   - Successful approval signing with valid credentials and atomic transaction commit.
   - Authentication failure on invalid password (no state mutation, failed attempt logged).
   - Inactive or locked user rejected.
   - Missing password on approval rejected with validation exception.
   - SoD enforcement: Author cannot sign approval (even with valid password).
   - SoD enforcement: Reviewer cannot sign approval (even with valid password).
   - SoD enforcement: System Administrator cannot bypass author/reviewer exclusion.
   - Duplicate signature prevention: Already approved revision rejects subsequent signing.
   - Non-approval decisions (`ReturnForCorrection`, `Decline`) do not generate `Approved` e-signatures.
   - Signature metadata completeness: Snapshot captures FullName, Username, Role, UTC time, Meaning `Approved`.
2. **Integration Testing (`DocumentControlElectronicSignaturePostgresIntegrationTests.cs`):**
   - Live PostgreSQL database test verifying row creation in `ElectronicSignatures`.
   - Verification of `trg_electronicsignatures_immutable`: Raw SQL `UPDATE` fails with exception.
   - Verification of `trg_electronicsignatures_immutable`: Raw SQL `DELETE` fails with exception.
   - Relational linkage to `DocumentRevision` and `User`.
   - Transactional rollback on simulated failure.
3. **Regression Suite:** Complete solution test suite (659 baseline) must pass 100% green.
4. **Frontend Production Build:** `tsc -b && vite build` must compile with 0 errors.

---

### 5. Sign-off & Baseline Approval

- **Change Authorized By:** MicroLIMS Document Control Baseline Authority
- **Effective Date:** September 4, 2026
- **Baseline Release:** Release 1b WP4
