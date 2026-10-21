# MicroLIMS specification sources inventory (raw)

Read-only inventory, 2026-10-15. Repo: `E:\MicroLIMS\MicroLIMS`, branch `docs/user-guide`.

**Method and limits**
- Every fact carries `[src: path:Lnnn]`. A row's `Title` is the first heading, at `L1` unless a different line is shown.
- The `Summary` column is derived from the document title and its opening block, not from a full read. Files over ~400 lines were read by heading only (`grep ^#`) plus the first lines. Treat summaries as pointers, not as specification content.
- `.docx` and `.xlsx` files are listed by name only and were not opened.
- `Date` is the first date-like line found. `n/s` means no date was shown in the first matches.
- Type labels are the inventory's own classification by filename and title. They are not declared by the documents.

Type key: **URS** user requirements; **FRS/FS** functional spec; **DS/BS** design or build spec; **IP** implementation plan; **VAL** validation or qualification record (protocol, report, RTM, closure, deployment); **CCR** controlled change record; **RPT** completion or status report; **RECON** recon or analysis note; **README**; **OTHER**.

---

## 1. docs/ (top level, .md and .txt)

Total 111 files: 110 `.md`, 1 `.txt` (`scratch_fs_dump.txt`). Counts by family are in section 12.

### 1.1 Controlled change records (CCR), Document Control

| File | Title [src] | Type | Module | Date [src] | Summary |
|---|---|---|---|---|---|
| docs/CC-DC-R1B-002.md | Release 1a Controlled Change Record — CC-DC-R1B-002 [src: docs/CC-DC-R1B-002.md:L1] | CCR | DC | 2026-09-03 (`**Date:** September 3, 2026`) [src: docs/CC-DC-R1B-002.md:L5] | Change record for R1B-002 |
| docs/CC-DC-R1B-003.md | Controlled Change Record: CC-DC-R1B-003 [src: docs/CC-DC-R1B-003.md:L1] | CCR | DC | 2026-09-04 [src: docs/CC-DC-R1B-003.md:L5] | Change record R1B-003 |
| docs/CC-DC-R1B-004.md | Controlled Change Record: CC-DC-R1B-004 [src: docs/CC-DC-R1B-004.md:L1] | CCR | DC | 2026-09-04 [src: docs/CC-DC-R1B-004.md:L5] | Change record R1B-004 |
| docs/CC-DC-R1B-005.md | Controlled Change Record: CC-DC-R1B-005 [src: docs/CC-DC-R1B-005.md:L1] | CCR | DC | 2026-09-04 [src: docs/CC-DC-R1B-005.md:L5] | Change record R1B-005 |
| docs/CC-DC-R1B-006.md | Release 1b — Work Package 6 (WP6) Periodic Review Engine & Workflow [src: docs/CC-DC-R1B-006.md:L2] | CCR | DC | 2026-09-04 [src: docs/CC-DC-R1B-006.md:L5] | Change record for WP6 periodic review engine |
| docs/CC-DC-R1B-007.md | MicroLIMS Change Control Record [src: docs/CC-DC-R1B-007.md:L1] | CCR | DC | 2026-09-04 [src: docs/CC-DC-R1B-007.md:L7] | Change control record R1B-007 |
| docs/CC-DC-R1C-001.md | Controlled Change Record: CC-DC-R1C-001 [src: docs/CC-DC-R1C-001.md:L1] | CCR | DC | 2026-09-04 [src: docs/CC-DC-R1C-001.md:L11] | Change record R1C-001 |

### 1.2 Document Control FRS, URS and related design (ML-DC-*, Release 1d delta)

| File | Title [src] | Type | Module | Date [src] | Summary |
|---|---|---|---|---|---|
| docs/ML-DC-FRS-1A-001.md | Functional Requirements Specification (FRS) — Release 1a [src: docs/ML-DC-FRS-1A-001.md:L1] | FRS | DC | n/s (Version 1.0 at L5) | FS-1a-### requirements traced to DC-URS-### |
| docs/ML-DC-FRS-1B-001.md | Functional Requirements Specification (FRS) — Release 1b [src: docs/ML-DC-FRS-1B-001.md:L1] | FRS | DC | n/s (Version 1.0 at L5) | Release 1b functional requirements |
| docs/ML-DC-FRS-1C-001.md | MicroLIMS Document Control — Release 1c [src: docs/ML-DC-FRS-1C-001.md:L1] | FRS | DC | 2026-09-04 (Effective Date) [src: docs/ML-DC-FRS-1C-001.md:L7] | Release 1c functional requirements |
| docs/Release_1d_Delta_URS.md | Release 1d Delta User Requirements Specification (Delta URS) [src: docs/Release_1d_Delta_URS.md:L1] | URS | DC | n/s (Version 1.0 Formal Delta Baseline at L5) | Delta URS for Release 1d |
| docs/Release_1d_Delta_FRS.md | Release 1d Delta Functional Requirements Specification (Delta FRS) [src: docs/Release_1d_Delta_FRS.md:L1] | FRS | DC | n/s (Version 1.0 at L5) | Delta FRS for Release 1d |
| docs/Release_1d_ADR_Document_Lifecycle.md | Architecture Decision Record: ADR-DC-004 [src: docs/Release_1d_ADR_Document_Lifecycle.md:L1] | DS (ADR) | DC | 2026-09-07 [src: docs/Release_1d_ADR_Document_Lifecycle.md:L5] | Decision record on document lifecycle |
| docs/Release_1d_Change_Control.md | Formal Change Control: CC-DC-R1D-001 [src: docs/Release_1d_Change_Control.md:L1] | CCR | DC | 2026-09-07 [src: docs/Release_1d_Change_Control.md:L8] | Change control for Release 1d |

### 1.3 Release 1a validation and traceability

| File | Title [src] | Type | Module | Date [src] | Summary |
|---|---|---|---|---|---|
| docs/Release_1a_Requirements_Traceability_Matrix.md | Release 1a Requirements Traceability Matrix (RTM) [src: docs/Release_1a_Requirements_Traceability_Matrix.md:L1] | VAL (RTM) | DC (plus planned 1b-1e, Phase 2) | n/s | RTM with status per release |
| docs/Release_1a_Validation_Summary_Report.md | Release 1a Validation Summary Report (VSR) [src: docs/Release_1a_Validation_Summary_Report.md:L1] | VAL | DC | n/s (Version 1.0 at L4) | Release 1a validation result |
| docs/WP4_Release_1a_OQ_UAT_Protocol.md | Release 1a Operational Qualification (OQ) & User Acceptance Testing (UAT) Protocol [src: docs/WP4_Release_1a_OQ_UAT_Protocol.md:L1] | VAL | DC | n/s (Version 1.0 at L4) | OQ/UAT protocol, 611 lines |
| docs/WP1_Completion_Report.md | Work Package 1 (WP1) Completion Report: Document Control Foundation [src: docs/WP1_Completion_Report.md:L1] | RPT | DC | n/s | Release 1a WP1 completion |
| docs/WP2_Completion_Report.md | MicroLIMS Document Control Module — Work Package 2 (WP2) Completion Report [src: docs/WP2_Completion_Report.md:L1] | RPT | DC | 2026-09-02 [src: docs/WP2_Completion_Report.md:L5] | Release 1a WP2 completion |
| docs/WP2_Traceability_Report.md | MicroLIMS Document Control Module — Work Package 2 (WP2) Traceability Report [src: docs/WP2_Traceability_Report.md:L1] | VAL | DC | 2026-09-02 [src: docs/WP2_Traceability_Report.md:L5] | WP2 traceability |
| docs/WP3_Completion_Report.md | Work Package 3 (WP3) Completion Report — Document Control Frontend Layer [src: docs/WP3_Completion_Report.md:L1] | RPT | DC | n/s | Release 1a WP3 frontend completion |
| docs/WP3_Traceability_Report.md | Work Package 3 (WP3) Traceability Report — Document Control Frontend Layer [src: docs/WP3_Traceability_Report.md:L1] | VAL | DC | n/s | WP3 traceability |
| docs/WP4_Completion_Report.md | Work Package 4 (WP4) Completion Report — Release 1a Integration Verification & Qualification [src: docs/WP4_Completion_Report.md:L1] | RPT | DC | n/s | Release 1a WP4 completion |
| docs/DocumentControl_Integration_Report.md | Document Control Module (Release 1a) Integration Report [src: docs/DocumentControl_Integration_Report.md:L1] | RPT | DC | 2026-08-21 [src: docs/DocumentControl_Integration_Report.md:L5] | Integration report, 476 lines |

### 1.4 Release 1b records

Release 1b has 40+ files in `docs/`. Listed here by file with title and type.

| File | Title [src] | Type | Module | Date [src] |
|---|---|---|---|---|
| docs/Release_1b_Baseline_Analysis.md | Document Control — Release 1b Baseline Analysis [src: L1] | RECON | DC | n/s |
| docs/Release_1b_Implementation_Plan.md | Release 1b Implementation Plan [src: L1] | IP | DC | n/s |
| docs/Release_1b_Test_Strategy.md | Release 1b Test Strategy & Verification Plan [src: L1] | VAL (plan) | DC | n/s |
| docs/Release_1b_Validation_Strategy.md | Release 1b Validation Strategy & Compliance Framework [src: L1] | VAL (plan) | DC | n/s |
| docs/Release_1b_Risk_Impact_Assessment.md | Release 1b Risk Impact Assessment [src: L1] | VAL | DC | n/s |
| docs/Release_1b_Risk_Reconciliation_Report.md | Release 1b [src: L1] (risk reconciliation) | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_Requirements_Traceability_Matrix.md | Release 1b Requirements Traceability Matrix (RTM) [src: L1] | VAL (RTM) | DC | n/s (Version 2.0 at L4) |
| docs/Release_1b_OQ_UAT_Plan.md | Release 1b OQ & UAT Plan [src: L1] | VAL (plan) | DC | n/s (`Date = 2026-09-01` in text) |
| docs/Release_1b_OQ_Execution_Report.md | Release 1b OQ execution [src: L1] | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_UAT_Execution_Report.md | Release 1b UAT execution [src: L1] | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_Validation_Summary_Report.md | Release 1b [src: L1] (validation summary) | VAL | DC | n/s (Version 1.0 at L5) |
| docs/Release_1b_Validation_Closure_Record.md | Release 1b [src: L1] (validation closure) | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_Final_Governance_Checklist.md | Release 1b [src: L1] (governance checklist) | VAL | DC | 2026-09-04 [src: L5] |
| docs/Release_1b_Final_Qualification_Evidence_Index.md | Release 1b [src: L1] (evidence index) | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_Formal_Release_Approval_Record.md | Release 1b [src: L1] (formal approval) | VAL | DC | 2026-09-04 [src: L5] |
| docs/Release_1b_Governance_Status_Summary.md | Release 1b [src: L1] (governance summary) | VAL | DC | 2026-09-04 [src: L5] |
| docs/Release_1b_Production_Deployment_Readiness.md | Release 1b [src: L1] (deployment readiness) | VAL | DC | v1.1.0-rel1b [src: L5] |
| docs/Release_1b_Production_Deployment_Record.md | Release 1b [src: L1] (deployment record) | VAL | DC | 2026-09-04 [src: L5] |
| docs/Release_1b_Production_Closure_Record.md | Release 1b [src: L1] (production closure) | VAL | DC | 2026-09-04 [src: L5] |
| docs/Release_1b_PostDeployment_Smoke_Test_Plan.md | Release 1b [src: L1] (smoke test plan) | VAL (plan) | DC | 2026-09-04 [src: L5] |
| docs/Release_1b_Readiness_Summary.md | Release 1b Readiness Summary & Executive Briefing [src: L1] | VAL | DC | n/s |
| docs/Release_1b_Release_Readiness_Assessment.md | Release 1b [src: L1] (readiness assessment) | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_WP1_Completion_Report.md | Work Package 1 (WP1) Completion Report — Technical Review Foundation [src: L1] | RPT | DC | n/s |
| docs/Release_1b_WP2_Completion_Report.md | Release 1b [src: L1] (WP2) | RPT | DC | n/s (Version 1.0 at L5) |
| docs/Release_1b_WP3_Completion_Report.md | Release 1b [src: L1] (WP3) | RPT | DC | n/s |
| docs/Release_1b_WP4_Completion_Report.md | Release 1b [src: L1] (WP4) | RPT | DC | n/s |
| docs/Release_1b_WP5_Completion_Report.md | Work Package 5 (WP5) Completion Report: Effective Date Automation Worker [src: L2] | RPT | DC | n/s |
| docs/Release_1b_WP6_Completion_Report.md | Release 1b [src: L1] (WP6) | RPT | DC | n/s |
| docs/Release_1b_WP7_Completion_Report.md | Release 1b [src: L1] (WP7) | RPT | DC | n/s |
| docs/Release_1b_WP8_Defect_Log.md | Release 1b [src: L1] (WP8 defect log) | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_WP8_EndToEnd_Test_Scenarios.md | Release 1b [src: L1] (WP8 E2E scenarios) | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_WP8_Evidence_Index.md | Release 1b [src: L1] (WP8 evidence index) | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_WP8_Integration_Verification_Report.md | Release 1b [src: L1] (WP8 integration verification) | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_WP8_PreVerification_Baseline.md | Release 1b [src: L1] (WP8 baseline) | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_WP8_StateMachine_Verification.md | Release 1b [src: L1] (WP8 state machine) | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_WP9_Deviation_Defect_Log.md | Release 1b [src: L1] (WP9 deviation log) | VAL | DC | 2026-09-04 [src: L7] |
| docs/Release_1b_WP9_Qualification_Baseline.md | Release 1b [src: L1] (WP9 baseline) | VAL | DC | 2026-09-04 [src: L7] |

### 1.5 Release 1c records

| File | Title [src] | Type | Module | Date [src] |
|---|---|---|---|---|
| docs/Release_1c_Baseline_Analysis.md | Document Control — Release 1c [src: L1] (baseline analysis) | RECON | DC | 2026-09-04 [src: L5] |
| docs/Release_1c_Implementation_Plan.md | Release 1c [src: L1] (implementation plan) | IP | DC | 2026-09-04 [src: L5] |
| docs/Release_1c_Test_Strategy.md | Release 1c [src: L1] (test strategy) | VAL (plan) | DC | 2026-09-04 [src: L5] |
| docs/Release_1c_Validation_Strategy.md | Release 1c [src: L1] (validation strategy) | VAL (plan) | DC | 2026-09-04 [src: L5] |
| docs/Release_1c_Risk_Impact_Assessment.md | Release 1c [src: L1] (risk impact) | VAL | DC | 2026-09-04 [src: L5] |
| docs/Release_1c_Requirements_Traceability_Matrix.md | Release 1c Requirements Traceability Matrix (RTM) [src: L1] | VAL (RTM) | DC | n/s (Version 2.0 at L4) |
| docs/Release_1c_OQ_Protocol.md | Release 1c [src: L1] (OQ protocol) | VAL | DC | n/s (Version 2.0 at L5) |
| docs/Release_1c_OQ_UAT_Plan.md | Release 1c [src: L1] (OQ/UAT plan) | VAL (plan) | DC | 2026-09-04 [src: L5] |
| docs/Release_1c_OQ_Execution_Record.md | Release 1c [src: L1] (OQ execution) | VAL | DC | n/s (v1.2.0-rel1c at L5) |
| docs/Release_1c_UAT_Protocol.md | Release 1c [src: L1] (UAT protocol) | VAL | DC | n/s (Version 2.0 at L5) |
| docs/Release_1c_UAT_Execution_Record.md | Release 1c [src: L1] (UAT execution) | VAL | DC | n/s (v1.2.0-rel1c at L5) |
| docs/Release_1c_Deviation_Log.md | Release 1c [src: L1] (deviation log) | VAL | DC | n/s (v1.2.0-rel1c at L5) |
| docs/Release_1c_Validation_Summary_Report.md | Release 1c [src: L1] (validation summary) | VAL | DC | n/s (Version 1.0 at L5) |
| docs/Release_1c_Qualification_Closure_Record.md | Release 1c [src: L1] (qualification closure) | VAL | DC | 2026-09-04 [src: L5 version line] |
| docs/Release_1c_Readiness_Summary.md | Release 1c [src: L1] (readiness summary) | VAL | DC | 2026-09-04 [src: L5 version line] |
| docs/Release_1c_Production_Baseline.md | Release 1c [src: L1] (production baseline) | VAL | DC | 2026-09-04 [src: L8] |
| docs/Release_1c_Production_Deployment_Plan.md | Release 1c [src: L1] (deployment plan) | VAL (plan) | DC | 2026-09-04 [src: L5 version line] |
| docs/Release_1c_Production_Deployment_Record.md | Release 1c [src: L1] (deployment record) | VAL | DC | 2026-09-04T21:35:00+03:00 [src: L9] |
| docs/Release_1c_Post_Deployment_Verification.md | Release 1c [src: L1] (post-deployment verification) | VAL | DC | n/s (v1.2.0-rel1c at L5) |
| docs/Release_1c_Post_Deployment_Defect_Hotfix_Report.md | Post-Deployment Defect Correction Control & Hotfix Release Report [src: L2] | VAL | DC | 2026-09-04 [src: L10] |
| docs/Release_1c_Release_Closure_Record.md | Release 1c [src: L1] (release closure) | VAL | DC | n/s (`v1.2.0-rel1c` at L5) |
| docs/Release_1c_Real_SOP_Workflow_Verification.md | Complete Real SOP Lifecycle Execution: C2I-91-126 (Revision 01) [src: L2] | VAL | DC | 2026-09-04 [src: L12] |
| docs/Release_1c_Final_Source_Control_Reconciliation.md | Release 1c [src: L1] (source control reconciliation) | RECON | DC | 2026-09-04 [src: L6] |
| docs/Release_1c_WP1_Completion_Report.md | Release 1c [src: L1] (WP1) | RPT | DC | n/s (WP1 completed at L5) |
| docs/Release_1c_WP2_Completion_Report.md | Release 1c Work Package 2 (WP2) Completion Report [src: L1] | RPT | DC | 2026-09-04 [src: L7] |
| docs/Release_1c_WP3_Completion_Report.md | Release 1c Work Package 3 (WP3) Completion Report [src: L1] | RPT | DC | n/s |
| docs/Release_1c_WP4_Completion_Report.md | Release 1c Work Package 4 (WP4) Completion Report [src: L1] | RPT | DC | n/s |
| docs/Release_1c_WP5_Completion_Report.md | Release 1c WP5 [src: L15] (executive summary) | RPT | DC | n/s |
| docs/Release_1c_WP6_Completion_Report.md | Release 1c WP6 [src: L15] (executive summary) | RPT | DC | n/s |
| docs/Release_1c_WP7_Completion_Report.md | Release 1c [src: L1] (WP7) | RPT | DC | n/s |
| docs/Release_1c_WP8_Integration_Verification_Report.md | Release 1c Work Package 8 (WP8) Completion Report [src: L1] | VAL/RPT | DC | n/s |

### 1.6 Release 1d records

| File | Title [src] | Type | Module | Date [src] |
|---|---|---|---|---|
| docs/Release_1d_Implementation_Plan.md | Release 1d Implementation Plan & Work Package Governance [src: L1] | IP | DC | n/s (Version 1.0 at L5) |
| docs/Release_1d_RTM_Delta.md | Release 1d Delta Requirements Traceability Matrix (RTM Delta) [src: L1] | VAL (RTM) | DC | n/s (Version 1.0 at L5) |
| docs/Release_1d_WP2_Completion_Report.md | Release 1d Work Package 2 (WP2) Completion Report & Closeout Reconciliation [src: L1] | RPT | DC | 2026-09-07 [src: L7] |
| docs/Release_1d_WP3_Completion_Report.md | Release 1d Work Package 3 (WP3) Completion Report [src: L1] | RPT | DC | 2026-09-07 [src: L7] |
| docs/Release_1d_WP4_Completion_Report.md | Release 1d Work Package 4 (WP4) Completion Report [src: L1] | RPT | DC | 2026-09-08 [src: L7] |

### 1.7 Frontend, FP, physchem and cross-cutting build specs, recon and reports

| File | Title [src] | Type | Module | Date [src] | Summary |
|---|---|---|---|---|---|
| docs/FP_Build_Status_2026-09-19.md | MicroLIMS — Build Status (2026-09-19) [src: L1] | RPT | FP / physchem (HPLC) | 2026-09-19 [src: L1] | Build status; notes HPLC label-claim conversion not built [src: L125] |
| docs/FP_Calibration_Curve_Build_Spec.md | Calibration Curve (ICP-OES) - Build Spec [src: L1] | DS/BS | FP / ICP-OES | 2026-09-19 [src: L1] | Calibration-curve build spec |
| docs/FP_Calibration_Curve_Phase0_Recon.md | Calibration Curve (ICP-OES) - Phase 0 Recon Findings [src: L1] | RECON | FP / ICP-OES | 2026-09-19 [src: L1] | Phase 0 recon |
| docs/FP_HPLC_Phase3_Build_Spec.md | Finished Product / HPLC — Phase 3 Build Spec (manual entry) [src: L1] | DS/BS | FP / HPLC | n/s | Phase 3 manual-entry build spec |
| docs/FP_Multi_Analyte_HPLC_Build_Spec.md | Multi-Vitamin (Multi-Analyte) HPLC Assay - Recon and Draft Build Spec [src: L1] | DS/BS | FP / HPLC | 2026-09-21 [src: L1] | Multi-analyte HPLC draft spec |
| docs/FP_Other_Equation_Types_Build_Spec.md | FP Equation Types beyond HPLC Assay and Calibration Curve - Build Spec [src: L1] | DS/BS | FP | 2026-09-19 [src: L5] | Other equation types build spec |
| docs/FP_Other_Equation_Types_Phase0_Recon.md | FP Equation Types beyond HPLC Assay and Calibration Curve - Phase 0 Recon [src: L1] | RECON | FP | 2026-09-19 [src: L4] | Phase 0 recon |
| docs/FP_Standard_Comparison_Assay_Build_Spec.md | Standard-Comparison Assay (PeakArea / Titration) + AAS — Draft Build Spec [src: L1] | DS/BS | FP / HPLC / titration / AAS | 2026-09-21 [src: L3] | Standard-comparison assay spec, 598 lines |
| docs/FP_Standard_Comparison_Assay_Phase0_Recon.md | Standard-Comparison Assay (HPLC/Titration) + AAS — Phase 0 Recon [src: L1] | RECON | FP / HPLC / titration / AAS | 2026-09-21 [src: L3] | Phase 0 recon |
| docs/Universal_Specifications_Build_Spec.md | Universal Specifications - Build Spec [src: L1] | DS/BS | Cross-cutting (specifications) | 2026-09-19 [src: L3 source line] | Universal specifications build spec |
| docs/Universal_Specifications_Migration_Report.md | Universal Specifications - Migration Report (LIMSV2, local) [src: L1] | RPT | Cross-cutting | 2026-09-19 [src: L4] | Migration report |
| docs/Storage_Migration_Implications.md | Storage Migration Implications [src: L1] | DS (note) | Platform / file storage | 2026-09-13 [src: L1 status line] | Storage provider decision, Backblaze B2 selected [src: L3] |
| docs/recon-layout-config.md | Recon: User-Configurable Layout (columns + panels) [src: L1] | RECON | UI layout | 2026-10-05 [src: L4] | Layout recon, 326 lines |
| docs/scratch_fs_dump.txt | (no heading; .txt scratch dump) | OTHER | unknown | n/s | Filesystem dump, not reviewed |

---

## 2. docs/ .docx and .xlsx (names only, not opened)

- docs/MicroLIMS_Document_Control_FS_Release_1a_v0_1.docx (functional spec, Document Control, Release 1a v0.1) [src: docs/ (dir listing)]
- docs/MicroLIMS_Document_Control_URS_v1_1_Amendment.docx (URS amendment, Document Control) [src: docs/ (dir listing)]
- docs/MicroLIMS_Document_Control_Risk_Assessment_v1_0.xlsx (risk assessment, Document Control) [src: docs/ (dir listing)]

---

## 3. docs/superpowers/specs (design specs, FS)

| File | Title [src] | Type | Module | Date [src] |
|---|---|---|---|---|
| docs/superpowers/specs/2026-08-17-water-batch-workflow-design.md | Water Batch Workflow — Parity with EM / After Cleaning [src: L1] | DS | Micro: water | 2026-08-17 [src: L3] |
| docs/superpowers/specs/2026-08-17-water-config-em-parity-design.md | Water Configuration — Environmental Monitoring Parity [src: L1] | DS | Micro: water, EM | 2026-08-17 [src: L3] |
| docs/superpowers/specs/2026-09-01-config-driven-preparation-fs.md | Functional Specification: Config-Driven Preparation (Product / RM / PM) [src: L1] | FS | Preparation (product, RM, PM) | n/s (Version 1.0 at L3) |
| docs/superpowers/specs/2026-09-24-lab-separation-design.md | Laboratory Separation — Microbiology and Physicochemical [src: L1] | DS | Cross-cutting: micro vs physchem labs | 2026-09-24 [src: L3] |
| docs/superpowers/specs/2026-09-29-hplc-chain-design.md | HPLC Chain — Masters, Solution Preparation and HPLC Workspace (Design) [src: L1] | DS | Physchem: HPLC | 2026-09-29 [src: L3] |
| docs/superpowers/specs/2026-10-02-retire-legacy-sst-design.md | Retire the legacy System Suitability (HPLC) module [src: L1] | DS | Physchem: HPLC SST | 2026-10-02 [src: L3] |
| docs/superpowers/specs/2026-10-03-icp-gc-workspaces-design.md | ICP and GC Workspaces — remodel on the HPLC workspace (Design) [src: L1] | DS | Physchem: ICP, GC | 2026-10-03 [src: L3] |
| docs/superpowers/specs/2026-10-03-titration-test-design.md | Titration assay test - design [src: L1] | DS | Physchem: titration | 2026-10-03 [src: L3] |
| docs/superpowers/specs/2026-10-03-working-standard-qualification-design.md | Working Standard Qualification — Design [src: L1] | DS | Physchem: working standards | 2026-10-03 [src: L3] |
| docs/superpowers/specs/2026-10-05-master-data-workbook-design.md | Master Data Workbook — Design [src: L1] | DS | Master data (cross-cutting) | 2026-10-05 [src: L3] |
| docs/superpowers/specs/2026-10-05-physchem-areas-design.md | Physicochemical areas (FP / RM & PM), primary standards, titration hardening - design [src: L1] | DS | Physchem: areas, primary standards, titration | 2026-10-05 [src: L3] |
| docs/superpowers/specs/2026-10-11-kpi-performance-remodel-design.md | KPI & Performance page remodel - design [src: L1] | DS | KPI / dashboards | 2026-10-11 [src: L3] (`draft for review (no code written)`) |

## 4. docs/superpowers/reports (reports, recon)

| File | Title [src] | Type | Module | Date [src] |
|---|---|---|---|---|
| docs/superpowers/reports/2026-08-10-pathogen-workflow-backend-report.md | MicroLIMS Pathogen Workflow — Backend Refactor Report [src: L1] | RPT | Micro: pathogen | 2026-08-11 generated, revised later [src: L3] |
| docs/superpowers/reports/2026-08-13-pathogen-workflow-frontend-report.md | MicroLIMS Pathogen Workflow — Frontend Refactor Report [src: L1] | RPT | Micro: pathogen | n/s (covers Task 11 [src: L3]) |
| docs/superpowers/reports/2026-09-01-config-driven-preparation-recon.md | Recon: Config-Driven Preparation (Product / RM / PM) [src: L1] | RECON | Preparation | 2026-09-01 [src: L5] |

## 5. docs/superpowers/plans (titles only, 32 .md and 7 .sql)

| File | Title [src] | Type | Module |
|---|---|---|---|
| 2026-08-10-pathogen-workflow-refactor.md | Pathogen Detection Workflow — Backend Refactor Implementation Plan [src: L1] | IP | Micro: pathogen |
| 2026-08-12-pathogen-frontend-update.md | Pathogen Workflow Frontend Update Implementation Plan [src: L1] | IP | Micro: pathogen |
| 2026-08-14-plate-count-incubation-transfer.md | Two-Stage Incubation Transfer for PlateCount Steps — Implementation Plan [src: L1] | IP | Micro: plate count |
| 2026-08-17-water-batch-phase1-receive-prepare.md | Water Batch Workflow — Phase 1 (Receive + Prepare) Implementation Plan [src: L1] | IP | Micro: water |
| 2026-08-17-water-batch-phase2-count-results.md | Water Batch Workflow — Phase 2 (Per-Location Count Result Entry) Implementation Plan [src: L1] | IP | Micro: water |
| 2026-08-17-water-config-em-parity.md | Water Configuration — EM Parity Implementation Plan [src: L1] | IP | Micro: water, EM |
| 2026-08-20-biochemicaltest-phenotypic-type.md | BiochemicalTest Phenotypic Test Type Implementation Plan [src: L1] | IP | Micro: biochemical tests |
| 2026-09-24-lab-separation.md | Laboratory Separation Implementation Plan [src: L1] | IP | Cross-cutting: lab separation |
| 2026-09-29-hplc-chain-s1-material-master.md | HPLC Chain S1 — Material Master Implementation Plan [src: L1] | IP | Physchem: HPLC |
| 2026-09-29-hplc-chain-s2-solution-master.md | HPLC Chain S2 — Solution Master Implementation Plan [src: L1] | IP | Physchem: HPLC |
| 2026-09-29-hplc-chain-s3-hplc-method-master.md | HPLC Chain S3 — HPLC Method Master Implementation Plan [src: L1] | IP | Physchem: HPLC |
| 2026-09-29-hplc-chain-s4-solution-preparation.md | HPLC Chain S4 — Solution Preparation Area Implementation Plan [src: L1] | IP | Physchem: solution prep |
| 2026-09-29-hplc-chain-s5-titrant-standardization.md | HPLC Chain S5 — Titrant Standardization Implementation Plan [src: L1] | IP | Physchem: titration |
| 2026-09-29-hplc-chain-s6-workspace-backend.md | HPLC Chain S6 — HPLC Workspace Backend Implementation Plan [src: L1] | IP | Physchem: HPLC |
| 2026-09-29-hplc-chain-s7-workspace-frontend.md | HPLC Chain S7 — HPLC Workspace Frontend Implementation Plan [src: L1] | IP | Physchem: HPLC |
| 2026-09-30-hplc-chain-test-data.md | HPLC Chain — Real Test Data (USP-based seed for LIMSV2) [src: L1] | IP (data) | Physchem: HPLC |
| 2026-09-30-physicochemical-ui.md | Physicochemical Laboratory UI — Plan (A → F) [src: L1] | IP | Physchem: UI |
| 2026-10-02-retire-legacy-sst.md | Retire Legacy SST Implementation Plan [src: L1] | IP | Physchem: HPLC SST (retirement) |
| 2026-10-03-g1-gc-backend.md | G1 — GC Backend on the Chromatography Module Implementation Plan [src: L1] | IP | Physchem: GC |
| 2026-10-03-working-standard-qualification.md | Working Standard Qualification Implementation Plan [src: L1] | IP | Physchem: working standards |
| 2026-10-04-i1-icp-method-master.md | I1 — ICP Method Master (backend) Implementation Plan [src: L1] | IP | Physchem: ICP |
| 2026-10-04-i2-icp-run-backend.md | I2 — ICP Run Backend Implementation Plan [src: L1] | IP | Physchem: ICP |
| 2026-10-05-master-data-workbook.md | Master Data Workbook Implementation Plan [src: L1] | IP | Master data |
| 2026-10-05-physchem-areas.md | Physicochemical areas, primary standards, titration hardening - Implementation Plan [src: L1] | IP | Physchem |
| 2026-10-05-r-retire-calibration-runs.md | R — Retire the old ICP-OES / AAS calibration-run path Implementation Plan [src: L1] | IP | Physchem: ICP/AAS (retirement) |
| 2026-10-09-migration-chain-findings.md | Migration chain findings (Task 16, measurement only) [src: L1] | RPT | Data migration |
| 2026-10-09-technical-debt-remediation.md | Technical Debt Remediation Implementation Plan [src: L1] | IP | Cross-cutting: tech debt |
| 2026-10-11-kpi-remodel-index.md | KPI and Performance Remodel - Plan Index [src: L1] | IP (index) | KPI |
| 2026-10-11-kpi-remodel-s1-workflow-profiles.md | KPI Remodel S1 - Workflow Profiles Implementation Plan [src: L1] | IP | KPI |
| 2026-10-11-kpi-remodel-s2-kpi-registry-dashboard.md | KPI Remodel S2 - KPI Registry and Dashboard Contract Implementation Plan [src: L1] | IP | KPI |
| 2026-10-11-kpi-remodel-s3-workload-capacity.md | KPI Remodel S3 - Work-Unit Analyst Measures and Capacity Implementation Plan [src: L1] | IP | KPI |
| 2026-10-11-kpi-remodel-s4-kpi-page.md | KPI Remodel S4 - New KPI Page Implementation Plan [src: L1] | IP | KPI |

SQL files in plans (names only): 2026-09-30-hplc-chain-test-data.sql; 2026-09-30-physicochemical-config-neon.sql; 2026-09-30-physicochemical-equipment-neon.sql; 2026-09-30-physicochemical-equipment.sql; 2026-10-04-gc-test-data.sql; 2026-10-05-icp-test-data.sql; 2026-10-05-titration-rmpm-test-data.sql.

---

## 6. docs/validation (validation package)

| File | Title [src] | Type | Module | Date/Version [src] | Summary |
|---|---|---|---|---|---|
| docs/validation/ML-SOP-MDW-001_Master_Data_Workbook_User_Guide.md | Master Data Workbook — User Guide [src: L1] | OTHER (user guide) | Master data workbook | `dates as yyyy-MM-dd` at L1; Version 0.1 Draft [src: L6] | **Existing user documentation** (308 lines) |
| docs/validation/ML-SYS-CS-001_Configuration_Specification.md | Configuration Specification — MicroLIMS [src: L1] | DS/config spec | System-wide, lab master data | 2026-10-08 [src: L1 source line]; Version 0.1 Draft [src: L6] | Environment config and lab master data for one deployment |
| docs/validation/ML-SYS-SD-001_System_Description.md | System Description — MicroLIMS [src: L1] | DS | System-wide (micro + physchem) | 2026-10-05 [src: L1]; Version 0.1 Draft [src: L6] | Purpose (micro + physchem labs), users and roles [src: L7-L20] |
| docs/validation/ML-SYS-VP-001_Validation_Plan.md | Validation Plan — MicroLIMS New Production Deployment [src: L1] | VAL (plan) | System-wide | 2026-10-05 [src: L1]; Version 0.1 Draft [src: L6] | Scope: new production deployment; in-scope at L24, out of scope at L38 [src: L22, L24, L38] |

## 7. docs/migrations and docs/sql

| File | Title [src] | Type | Module | Date [src] |
|---|---|---|---|---|
| docs/migrations/2026-08-14-master-data-migration-plan.md | MicroLIMS Master/Config Data Migration Plan: Dev → Neon Production [src: L1] | IP | Data migration | 2026-08-14 [src: L1] |
| docs/migrations/2026-08-14-operational-data-inventory.md | Operational/Historical Data Inventory: Old LIMSV2 → Neon (Phase 2 discovery) [src: L1] | RECON | Data migration | n/s |
| docs/migrations/2026-08-14-phase2-migration-plan.md | Phase 2 Migration Plan: Operational/Historical Data — Dev → Neon Production [src: L1] | IP | Data migration | 2026-08-14 [src: L1] |
| docs/migrations/2026-08-14-master-data-migration.sql | (SQL, names only) | OTHER | Data migration | |
| docs/migrations/2026-08-14-phase2-migration.sql | (SQL, names only) | OTHER | Data migration | |
| docs/migrations/2026-08-14-phase2-source-data-export.json | (JSON, names only) | OTHER | Data migration | |
| docs/migrations/2026-08-14-source-data-export.json | (JSON, names only) | OTHER | Data migration | |
| docs/migrations/archive/2026-08-27-phase6-backup.sql | (SQL, names only, archive) | OTHER | Data migration | |
| docs/sql/revoke_audit_privileges.sql | (SQL, names only) | OTHER | Audit privileges | |

---

## 8. documents/ (all subfolders)

Total files: 9. Subfolders DesignSpecification, FunctionalSpecification, SOP, Screenshots, URS, UserManual, Validation.

| File | Title [src] | Type | Content [src] |
|---|---|---|---|
| documents/URS/README.md | User Requirements Specification (URS) [src: documents/URS/README.md:L1] | README | Describes intended URS. **No URS document exists in the folder** [src: documents/URS/README.md:L1-L5] |
| documents/FunctionalSpecification/README.md | Functional Specification (FS) [src: documents/FunctionalSpecification/README.md:L1] | README | Says FS would cover Sample Receiving, Testing Workspace, Review, Approval, Reporting [src: L3-L4]. **No FS document exists in the folder.** |
| documents/DesignSpecification/README.md | Design Specification (DS) [src: documents/DesignSpecification/README.md:L1] | README | Refers to `MicroLIMS_v2.doc` (not in repo) [src: L3-L4] |
| documents/DesignSpecification/BusinessRules.md | MicroLIMS — Business Rules (Frozen Logic) [src: documents/DesignSpecification/BusinessRules.md:L1] | DS (business rules) | 237 lines; see section 9 |
| documents/DesignSpecification/WorkflowDiagrams.md | MicroLIMS Workflow Diagrams [src: documents/DesignSpecification/WorkflowDiagrams.md:L1] | DS (workflows) | 116 lines; see section 9 |
| documents/SOP/README.md | Standard Operating Procedures [src: documents/SOP/README.md:L1] | README | Says SOPs cover receipt, result entry, review, approval, deviation, user accounts, backup. **No SOP files exist.** [src: L3] |
| documents/UserManual/README.md | User Manual [src: documents/UserManual/README.md:L1] | README | Says user manual per role with screenshots. **Only the README exists** [src: L3-L4] |
| documents/Screenshots/README.md | Screenshots [src: documents/Screenshots/README.md:L1] | README | Says screenshots of each module. **Only the README exists** [src: L3] |
| documents/Validation/README.md | Validation [src: documents/Validation/README.md:L1] | README | Says IQ/OQ/PQ, RTM, summary reports. **Only the README exists** [src: L3-L4] |

---

## 9. Special attention

### 9.1 documents/DesignSpecification/BusinessRules.md (237 lines)

Business rule IDs: **none.** `grep` found no `BR-` or rule-ID pattern. Rules are numbered by section only.

Headings with line numbers:
- L1 `# MicroLIMS — Business Rules (Frozen Logic)` [src: documents/DesignSpecification/BusinessRules.md:L1]
- L10 `## 1. Frozen Architectural Principles` [src: ...:L10]
- L33 `## 2. Automatic Test Order Generation` [src: ...:L33]
- L52 `## 3. Pathogen Engine` [src: ...:L52]
- L83 `## 4. Water Calculation Engine` [src: ...:L83]
- L102 `## 5. EM (Environmental Monitoring) Engine` [src: ...:L102]
- L120 `## 6. After Cleaning Engine` [src: ...:L120]
- L136 `## 7. GPT (Growth Promotion Test) Engine` [src: ...:L136]
- L157 `## 8. Review Engine` [src: ...:L157]
- L171 `## 9. Approval Engine` [src: ...:L171]
- L188 `## 10. Security Rules` [src: ...:L188]
- L206 `## 11. Audit Trail` [src: ...:L206]
- L222 `## 12. Dashboard Notification Triggers` [src: ...:L222]

Source-file references (no rule IDs): L35 ProductWorkflowEngine.cs; L54 PathogenWorkflowEngine.cs; L85 WaterWorkflowEngine.cs; L104 EMWorkflowEngine.cs; L122 AfterCleaningWorkflowEngine.cs; L138 GptWorkflowEngine.cs; L159 ReviewService.cs; L173 ApprovalService.cs; L190 AuthenticationService.cs; L208 MicroLimsDbContext.cs; L224 DashboardNotificationService.cs [src: documents/DesignSpecification/BusinessRules.md:Lnnn].

### 9.2 documents/DesignSpecification/WorkflowDiagrams.md (116 lines)

Headings with line numbers:
- L1 `# MicroLIMS Workflow Diagrams` [src: documents/DesignSpecification/WorkflowDiagrams.md:L1]
- L6 `## Product Workflow` [src: ...:L6]
- L20 `## Pathogen Engine — Universal Chain` [src: ...:L20]
- L32 `## Pathogen Engine — Salmonella Exception` [src: ...:L32]
- L47 `## Water Calculation Engine` [src: ...:L47]
- L61 `## EM (Environmental Monitoring) Engine` [src: ...:L61]
- L76 `## After Cleaning Engine` [src: ...:L76]
- L88 `## GPT (Growth Promotion Test) Engine` [src: ...:L88]
- L100 `## Review → Approval (shared across all domains)` [src: ...:L100]

No sample-receiving or testing-workspace workflow heading exists in this file.

---

## 10. Module inventories (existing)

No complete module inventory exists. Partial lists:

1. **frontend/README.md** [src: frontend/README.md:L12-L19]: `modules/` folders: authentication, dashboard, users, roles; receiving, testingWorkspace, review, approval, reports; pathogen (cross-cutting, from Testing Workspace); laboratoryConfiguration/ (items, specifications, media, gpt, water, environmentalMonitoring, afterCleaning). Also says "Laboratory Configuration" is one collapsible menu [src: frontend/README.md:L35-L40 approx.]. Note: list has no physchem, KPI or Document Control modules.
2. **README.md** [src: README.md:L12-L16]: top-level folder tree only (backend, frontend, database, documents, scripts). No module list.
3. **frontend/navigation_audit.txt** (137 lines, UTF-8 with BOM, Windows absolute paths) [src: frontend/navigation_audit.txt:L1-L137]: a list of `navigate(...)` and `<Link>` call sites in `src/components`, `src/modules/*`, `src/pages`, `src/layouts`. It contains route strings (e.g. `/testing-workspace`, `/laboratory-configuration/media`, `/audit-search`, `/users`, `/roles`, `/reports`, `/receiving`) but it is not a module list.
4. **MICROLIMS_FILE_LIST.txt** (547 lines, UTF-16) [src: MICROLIMS_FILE_LIST.txt:L1-L547]: git-tracked file list, not a module inventory. Lists controllers including Kpi, MasterData, Media, Reporting, Review, Sample, etc. [src: MICROLIMS_FILE_LIST.txt:L6-L30].
5. **MICROLIMS_STATUS.txt** (4 lines, UTF-16) [src: MICROLIMS_STATUS.txt:L1-L4]: output of git status only (`.claude/settings.local.json` modified; three untracked files). No module content.
6. **docs/validation/ML-SYS-SD-001_System_Description.md** §2 roles table [src: docs/validation/ML-SYS-SD-001_System_Description.md:L15-L25 approx.]: roles, not modules.

---

## 11. Existing user documentation

- **docs/validation/ML-SOP-MDW-001_Master_Data_Workbook_User_Guide.md** (308 lines, Draft v0.1) [src: docs/validation/ML-SOP-MDW-001_Master_Data_Workbook_User_Guide.md:L1, L6]. The only end-user guide in the repo.
- **DEPLOYMENT.md** (303 lines) [src: DEPLOYMENT.md:L1] operations guide for Neon, Render, Cloudflare, Backblaze B2 (sections 1-7). Not end-user.
- **INTEGRATION.md** (226 lines) [src: INTEGRATION.md:L1] developer integration guide.
- **frontend/README.md**, **backend/README.md** developer READMEs.
- **documents/UserManual/** and **documents/Screenshots/**: README only [src: documents/UserManual/README.md:L1-L5; documents/Screenshots/README.md:L1-L4]. No user manual pages or screenshots exist.
- **documents/SOP/**: README only [src: documents/SOP/README.md:L1-L4]. No SOP files.

Nothing in the list above is to be deleted.

---

## 12. Planned, not built, retired or deprecated statements

Quoted briefly, with file and line.

- docs/DocumentControl_Integration_Report.md:251 — "**NOT IMPLEMENTED.** No per-record policy or handler ... exists in any API controller or filter."
- docs/FP_Build_Status_2026-09-19.md:125 — "HPLC label-claim conversion (REQ-FP-021) was not built."
- docs/FP_Calibration_Curve_Build_Spec.md:51 — "`LimsFitted (reserved, not built)`."
- docs/FP_HPLC_Phase3_Build_Spec.md:7 — "Out of scope this round: instrument listener/adapters, CDS file import, duplicate-hash table, KPIs."
- docs/FP_Other_Equation_Types_Build_Spec.md:195 — "Media replacement / multi-point profile: out of scope now."
- docs/FP_Other_Equation_Types_Build_Spec.md:245 — "content uniformity is built (`<905>` not built)."
- docs/FP_Standard_Comparison_Assay_Build_Spec.md:11 — "`HplcAssayResult` is retired."
- docs/FP_Standard_Comparison_Assay_Build_Spec.md:19 — "`HplcAssay`/`HplcMultiAnalyte` retired (enum values ...)."
- docs/FP_Standard_Comparison_Assay_Build_Spec.md:198 — "DROPPED 2026-09-22 (D-W4) — reversed, not built."
- docs/FP_Standard_Comparison_Assay_Build_Spec.md:429 — "dropped and not built."
- docs/Release_1a_Requirements_Traceability_Matrix.md:22-26 — Release 1b to 1e and "Phase 2 / Future" rows marked "Planned (Deferred ...)".
- docs/Release_1b_Production_Closure_Record.md:121 — "Release 1c (Training Matrix & Reading Lists): **NOT IMPLEMENTED**."
- docs/Release_1b_WP3_Completion_Report.md:168-171 — e-signatures, automatic effective-date activation, periodic review scheduler, training/reading lists/KAF marked "NOT IMPLEMENTED".
- docs/Release_1b_Production_Deployment_Record.md:141 — "Release 1c functionality ... remains strictly out of scope and deferred."
- docs/Release_1c_Requirements_Traceability_Matrix.md:48-67 — five requirements DC-URS-091, -117, -121, -122, -176 marked "PLANNED", "Post-R1c / Tools", "Out of Current Implemented Baseline".
- docs/Release_1c_Release_Closure_Record.md:43 — "remain planned for subsequent development releases."
- docs/Release_1c_WP3_Completion_Report.md:151 and docs/Release_1c_WP4_Completion_Report.md:129 — "Release 1d (KAF / Quizzes): Remains deferred to Release 1d and is strictly out of scope."
- docs/Release_1d_RTM_Delta.md:23 — "`PLANNED (WP1 APPROVED)`: ... awaiting code implementation in WP2–WP7."
- docs/superpowers/specs/2026-10-02-retire-legacy-sst-design.md:L1 — title "Retire the legacy System Suitability (HPLC) module".
- docs/superpowers/plans/2026-10-05-r-retire-calibration-runs.md:L1 — "Retire the old ICP-OES / AAS calibration-run path".
- docs/superpowers/specs/2026-10-11-kpi-performance-remodel-design.md:3 — "Status: draft for review (no code written)."
- docs/Release_1c_Baseline_Analysis.md:28 — generic LMS features "are out of scope."
- documents/UserManual/README.md:3-4 and documents/Screenshots/README.md:3 — describe content that is not in the repo.

---

## 13. Coverage: URS or FRS by module

- **Document Control:** URS (`docs/Release_1d_Delta_URS.md`, plus URS amendment .docx) and FRS (`docs/ML-DC-FRS-1A-001.md`, `1B`, `1C`, `docs/Release_1d_Delta_FRS.md`). Yes.
- **Physicochemical (FP / HPLC / ICP / GC / titration):** design and build specs in `docs/FP_*`, `docs/superpowers/specs/` (HPLC chain, ICP/GC, titration, working standards, physchem areas). No URS. The only FRS-type doc is `docs/superpowers/specs/2026-09-01-config-driven-preparation-fs.md` (Preparation: Product / RM / PM).
- **Microbiology core (Sample, Testing, Review, Approval):** **No URS and no FRS exists in the repo.** The only specification sources are `documents/DesignSpecification/BusinessRules.md` (sections 2, 8, 9 cover test order generation, review and approval), `WorkflowDiagrams.md`, `docs/superpowers/specs/2026-08-17-water-*` and `docs/superpowers/plans/2026-08-*` (pathogen, plate count, water, biochemical), and `docs/validation/ML-SYS-SD-001_System_Description.md` (draft, system-wide). The `documents/URS` and `documents/FunctionalSpecification` folders are empty except for READMEs.
- **Sample receiving:** no URS/FRS. Only mentioned in `documents/FunctionalSpecification/README.md:L3`.

---

## 14. Counts by document type (inventory classification)

- Top-level `docs/` (111 .md/.txt files, approximate per family): CCR 7; Document Control FRS/URS/ADR/change control 7; Release 1a validation/traceability/completion 10; Release 1b records about 35; Release 1c records about 31; Release 1d 5; FP/physchem/cross-cutting specs, recon and reports about 14 (includes scratch .txt). Family counts are approximate; the table rows in sections 1.1-1.7 are the authoritative list, and a few Release 1b/1c rows are grouped rather than individually verified.
- `docs/superpowers/specs`: 12 (11 DS, 1 FS).
- `docs/superpowers/reports`: 3 (2 RPT, 1 RECON).
- `docs/superpowers/plans`: 32 .md (IP, data notes) and 7 .sql.
- `docs/validation`: 4 (1 user guide, 2 config/system specs, 1 validation plan).
- `docs/migrations`: 3 .md, 5 data files (sql/json) plus 1 archive .sql; `docs/sql`: 1 .sql.
- `documents/`: 9 files (2 DS documents: BusinessRules.md, WorkflowDiagrams.md; 7 README.md).
- Root: README.md, DEPLOYMENT.md, INTEGRATION.md, frontend/README.md, backend/README.md (READMEs, guides); MICROLIMS_STATUS.txt, MICROLIMS_FILE_LIST.txt, frontend/navigation_audit.txt (non-spec).

Prior module inventory: **none complete.** Partial list in frontend/README.md:L12-L19.
