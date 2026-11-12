# Release 1d Delta Requirements Traceability Matrix (RTM Delta)
## Traceability from Delta URS to FRS, Design Components, Work Packages, Risk & Verification

- **Document ID:** `ML-DC-RTM-1D-001`
- **Version:** 1.0 (Formal Planning Baseline — WP1)
- **Status:** **APPROVED FOR PLANNING & ARCHITECTURE**
- **Authoritative Baseline Extended:** Release 1c RTM (`ML-DC-RTM-1C-001`)
- **Delta URS Baseline:** `ML-DC-URS-1D-001` (`DC-URS-1D-001` .. `DC-URS-1D-025`)
- **Delta FRS Baseline:** `ML-DC-FRS-1D-001` (`FS-1d-001` .. `FS-1d-016`)
- **Governing Change Control:** `CC-DC-R1D-001`
- **Architecture Decision Record:** `ADR-DC-004`
- **Date:** September 7, 2026

---

### 1. Traceability Methodology & Work Package Phasing Rules

1. **Delta Traceability Integrity:**  
   Every requirement in the Release 1d Delta URS (`DC-URS-1D-001` through `DC-URS-1D-025`) maps bi-directionally to at least one Delta FRS specification (`FS-1d-001` through `FS-1d-016`), an affected system component, a designated Work Package (`WP1`..`WP8`), a formal verification method, an OQ/UAT protocol candidate, and a calibrated GxP risk rating.
2. **Protection of Qualified Release 1c Baseline:**  
   The Release 1c Requirements Traceability Matrix (`Release_1c_Requirements_Traceability_Matrix.md`) remains the frozen qualification record for Release 1c. Requirements qualified in Release 1a, 1b, or 1c are not duplicated here unless explicitly modified or superseded.
3. **Status Legend:**  
   - `PLANNED (WP1 APPROVED)`: The requirement scope and design are approved in WP1; awaiting code implementation in WP2–WP7 and formal qualification in WP8.
   - `QUALIFIED`: Reserved strictly for post-WP8 status following formal execution and sign-off of OQ and UAT protocols.

---

### 2. Comprehensive Release 1d Delta Traceability Matrix

| Delta URS ID | Functional Requirement ID | Affected Component / Class | Implementation WP | Verification Method | OQ / UAT Candidate | Risk Rating | Implementation Status |
| :--- | :--- | :--- | :---: | :--- | :--- | :---: | :---: |
| **DC-URS-1D-001** | `FS-1d-001`, `FS-1d-003` | `DocumentReviewService`, `DocumentFileService` | **WP3, WP4** | Automated Integration Test | `OQ-1D-01`, `UAT-1D-01` | **High** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-002** | `FS-1d-001` | `DocumentFileService.UploadRevisionFileAsync` | **WP3** | Unit / Negative Test | `OQ-1D-02` | **Medium** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-003** | `FS-1d-001`, `FS-1d-002` | `LocalFileStorageService`, `RevisionFiles` Table | **WP2, WP3** | Storage & DB Integrity Test | `OQ-1D-03` | **Critical** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-004** | `FS-1d-002` | `RevisionFile.FileVersion`, `DocumentFileService` | **WP2, WP3** | Automated Service Test | `OQ-1D-03`, `UAT-1D-02` | **High** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-005** | `FS-1d-002` | `RevisionFile.SupersededByFileId`, DB Migration | **WP2, WP3** | Database Integrity Test | `OQ-1D-03` | **Medium** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-006** | `FS-1d-004` | `DocumentAuthorizationService.CanAccessSourceFileAsync` | **WP3** | Security Matrix Test | `OQ-1D-04`, `UAT-1D-03` | **High** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-007** | `FS-1d-001`, `FS-1d-004` | `DocumentAuthorizationService`, API Controllers | **WP3** | Security Negative Test | `OQ-1D-04` | **Critical** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-008** | `FS-1d-006` | `DocumentReviewFinding.SourceFileVersion` | **WP2, WP4** | Automated Integration Test | `OQ-1D-05`, `UAT-1D-04` | **High** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-009** | `FS-1d-005`, `FS-1d-007` | `TechnicalReviewDrawer.tsx`, `DocumentDetailPage.tsx` | **WP6** | UI Component / Workflow Test | `OQ-1D-06`, `UAT-1D-05` | **High** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-010** | `FS-1d-002`, `FS-1d-007` | `DocumentFileService`, `FileUploadDialog.tsx` | **WP3, WP6** | End-to-End Workflow Test | `OQ-1D-06`, `UAT-1D-05` | **High** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-011** | `FS-1d-008`, `FS-1d-009` | `DocumentReviewService`, `DocumentReviewTask` | **WP2, WP4** | Multi-Cycle State Machine Test | `OQ-1D-07`, `UAT-1D-06` | **Critical** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-012** | `FS-1d-008`, `FS-1d-009` | `DocumentReviewService.VerifyFindingAsync` | **WP4** | Workflow Verification Test | `OQ-1D-07`, `UAT-1D-06` | **High** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-013** | `FS-1d-010` | `DocumentReviewService.DecideReviewAsync` | **WP4** | Automated Negative Gate Test | `OQ-1D-08`, `UAT-1D-06` | **Critical** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-014** | `FS-1d-004`, `FS-1d-005` | `TechnicalReviewDrawer.tsx`, Document Files API | **WP6** | UI Inspection & Watermark Test | `OQ-1D-09` | **Medium** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-015** | `FS-1d-011`, `FS-1d-012` | `RevisionFile.IsApprovedFinalSource`, DB Migration | **WP2, WP5** | State Persistence Test | `OQ-1D-10`, `UAT-1D-07` | **High** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-016** | `FS-1d-011` | `ApprovalWorkspaceDialog.tsx`, Dossier Service | **WP5, WP7** | UI Dossier Inspection Test | `OQ-1D-10`, `UAT-1D-07` | **High** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-017** | `FS-1d-012`, `FS-1d-014` | `DocumentApprovalService.ExecuteApprovalDecisionAsync` | **WP5** | State Sequence Test | `OQ-1D-11`, `UAT-1D-08` | **Critical** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-018** | `FS-1d-013` | `GotenbergDocumentConversionService` | **WP5** | Container Integration Test | `OQ-1D-12` | **Critical** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-019** | `FS-1d-014` | `RevisionFile.GeneratedFromSourceFileId`, Checksums | **WP2, WP5** | Cryptographic Binding Test | `OQ-1D-12`, `UAT-1D-08` | **Critical** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-020** | `FS-1d-014`, `FS-1d-016` | `DocumentAuthorizationService`, Library UI | **WP3, WP7** | Access Restriction Test | `OQ-1D-13` | **High** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-021** | `FS-1d-015` | `DocumentApprovalService` Transaction Boundary | **WP5** | Fault Injection Rollback Test | `OQ-1D-14` | **Critical** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-022** | `FS-1d-016` | `DocumentEffectiveDateService`, Training Service | **WP5, WP8** | Regression & Cascade Test | `OQ-1D-15`, `UAT-1D-09` | **High** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-023** | `FS-1d-001` .. `FS-1d-016`| `AuditEventService`, `AuditLog` Table | **WP3, WP4, WP5** | Semantic Audit Verification Test | `OQ-1D-16`, `UAT-1D-10` | **High** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-024** | `FS-1d-003`, `FS-1d-006`, `FS-1d-012` | Review & Approval Authorization Engines | **WP3, WP4, WP5** | SoD Permutation Test Suite | `OQ-1D-17` | **Critical** | `PLANNED (WP1 APPROVED)` |
| **DC-URS-1D-025** | All FRS Specifications | Test Harness, Authentication Providers | **WP8** | Session Attribution Audit | `OQ-1D-18`, `UAT-1D-11` | **Critical** | `PLANNED (WP1 APPROVED)` |

---

### 3. Traceability Coverage & Verification Summary

- **Total Release 1d Delta URS Requirements:** 25 (`DC-URS-1D-001` through `DC-URS-1D-025`)
- **Total Delta FRS Specifications Mapped:** 16 (`FS-1d-001` through `FS-1d-016`)
- **Total Work Packages Assigned:** 7 Implementation/Qualification WPs (`WP2` through `WP8`) + 1 Planning WP (`WP1`)
- **URS-to-FRS Mapping Coverage:** **100% (25 / 25 requirements fully mapped)**
- **FRS-to-Component Mapping Coverage:** **100% (16 / 16 specifications fully assigned)**
- **Planned Formal OQ Verification Protocols:** 18 Protocols (`OQ-1D-01` through `OQ-1D-18`)
- **Planned Operational UAT Business Scenarios:** 11 Scenarios (`UAT-1D-01` through `UAT-1D-11`)
- **Unresolved Traceability Conflicts:** **0 (None)**
