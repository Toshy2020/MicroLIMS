# Controlled Change Record: CC-DC-R1B-003
## Release 1b — Work Package 3 (WP3) Approval Workflow Extensions

**Document Identifier:** `CC-DC-R1B-003`  
**Change Request Date:** September 4, 2026  
**Change Classification:** Minor — Backward Compatible Extension (Additive Domain & Safety Net Integration)  
**Status:** APPROVED & BASELINED  
**Initiator:** Antigravity AI (Pair Programmer)  
**System Classification:** GAMP 5 Category 5 / 21 CFR Part 11 Compliant  

---

### 1. Change Description & Rationale

#### 1.1 Purpose
Work Package 3 (WP3) introduces the formal Document Control Approval Workflow, fulfilling `DC-URS-074` (Approver inspection dossier), `DC-URS-075` (Approver decision options), `DC-URS-078` (Segregation of Duties), and related requirements from `ML-DC-FRS-1B-001`.

To support relational navigation, database referential integrity, and user deletion protection for approval tasks, existing baseline components must receive strictly additive extensions.

#### 1.2 Affected Baselined Components
1. **`backend/MicroLIMS.Domain/Entities/DocumentRevision.cs`:**
   - Additive navigation collection:
     ```csharp
     public virtual ICollection<DocumentApprovalTask> ApprovalTasks { get; set; } = new List<DocumentApprovalTask>();
     ```
   - Rationale: Enables relational navigation from a revision to its historical and active approval tasks.
2. **`backend/MicroLIMS.Application/Services/UserReferenceRegistry.cs`:**
   - Registration of user references:
     - `DocumentApprovalTask.AssignedApproverUserId` (`UserReferenceDisposition.Blocks`)
     - `DocumentApprovalTask.AssignedByUserId` (`UserReferenceDisposition.Blocks`)
     - `DocumentApprovalTask.DecisionByUserId` (`UserReferenceDisposition.Blocks`)
   - Rationale: GAMP 5 / 21 CFR Part 11 compliance requires that user records associated with formal document approval decisions cannot be hard-deleted, preventing orphaned audit trails and broken electronic governance.
3. **`backend/MicroLIMS.Persistence/DbContext/MicroLimsDbContext.cs`:**
   - Registration of `DbSet<DocumentApprovalTask> DocumentApprovalTasks` and its EF Core configuration.
   - Rationale: EF Core persistence integration with `ON DELETE RESTRICT` constraints on all foreign keys.

---

### 2. Risk & Impact Assessment

| Evaluation Dimension | Assessment | Justification |
|---|---|---|
| **Schema Impact** | **Low / Additive Only** | Adds a new table `"DocumentApprovalTasks"`. No columns on existing Release 1a or WP1/WP2 tables are modified or deleted. |
| **Data Migration Risk** | **None** | Existing rows in `"DocumentRevisions"` and `"DocumentMasters"` are unaffected. The migration is 100% non-destructive and reversible. |
| **API Compatibility** | **Zero Breaking Changes** | All existing Release 1a and WP1/WP2 API routes retain their exact contracts and behaviors. New endpoints are housed under distinct routes (e.g. `/api/document-control/approvals/*`). |
| **Regression Risk** | **Very Low** | Verified against the complete 631-test backend regression suite. |
| **Audit Trail Integrity** | **Maintained** | Approval events (`ApprovalTaskCreated`, `RevisionApprovalInitiated`, `RevisionReturnedByApprover`, `RevisionDeclinedByApprover`, `DocumentRevisionApproved`) use the established semantic audit event framework (`IAuditEventService`). |
| **Segregation of Duties** | **Reinforced** | Hard enforcement that Author $\neq$ Reviewer $\neq$ Approver across all approval operations. System Administrators are non-exempt. |

---

### 3. Affected Requirements

- **`DC-URS-073`**: Technical Review transition to `AwaitingApproval`.
- **`DC-URS-074`**: Approver inspection dossier aggregation.
- **`DC-URS-075`**: Approver formal decision options (`Approve`, `ReturnForCorrection`, `Decline`).
- **`DC-URS-078`**: Segregation of Duties enforcement.
- **`DC-URS-079`**: Complete audit trail capture for workflow actions.
- **`DC-URS-164` through `DC-URS-169`**: Comprehensive SoD rules.

---

### 4. Verification & Testing Requirements

1. **EF Core Migration Verification:** Ensure migration generates clean `CREATE TABLE "DocumentApprovalTasks"` with `Restrict` foreign keys and proper index creation.
2. **Unit Testing:**
   - Approval task creation and eligibility verification.
   - Dossier compilation including PDF, change items, impact assessment, and review findings.
   - Readiness validation logic.
   - Decision state transitions (`Approve` $\rightarrow$ `Effective`/`FutureEffective`, `ReturnForCorrection` $\rightarrow$ `Draft`, `Decline` $\rightarrow$ `Cancelled`).
   - Negative SoD tests: Author cannot approve, Reviewer cannot approve, Author cannot be assigned approver, Reviewer cannot be assigned approver, Admin cannot bypass SoD.
3. **Integration Testing:** Live PostgreSQL test execution verifying foreign key constraints, user-reference deletion protection, and transaction isolation.
4. **Regression Suite:** All 631 existing tests must pass with zero failures.
5. **Frontend Build:** Clean compilation with `npm run build` (zero TypeScript errors).

---

### 5. Sign-off & Baseline Approval

- **Change Authorized By:** MicroLIMS Document Control Baseline Authority
- **Effective Date:** September 4, 2026
- **Baseline Release:** Release 1b WP3
