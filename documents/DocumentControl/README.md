# Document Control specification set

This folder holds the Document Control requirement, functional, risk and validation documents for Releases 1a to 1d. The files were moved here from `docs/` and `docs/validation/` without any change to their content. Each old markdown path now holds a one-line pointer to the new location. The Word and Excel files were moved only.

The checker (`scripts/docs/check-documents.mjs`) skips this folder. The test `existing_dc_ids_still_resolve` in `scripts/docs/check-documents.test.mjs` checks that every `DC-URS-nnn` and `FS-1x-nnn` ID used in `backend/` and `frontend/src` appears in a markdown file here, or is listed below.

## Files

- URS: `Release_1d_Delta_URS.md`, `MicroLIMS_Document_Control_URS_v1_1_Amendment.docx`
- FRS / FS: `ML-DC-FRS-1A-001.md`, `ML-DC-FRS-1B-001.md`, `ML-DC-FRS-1C-001.md`, `Release_1d_Delta_FRS.md`, `MicroLIMS_Document_Control_FS_Release_1a_v0_1.docx`
- Requirements traceability: `Release_1a_Requirements_Traceability_Matrix.md`, `Release_1b_Requirements_Traceability_Matrix.md`, `Release_1c_Requirements_Traceability_Matrix.md`, `Release_1d_RTM_Delta.md`
- Risk: `MicroLIMS_Document_Control_Risk_Assessment_v1_0.xlsx`, `Release_1b_Risk_Impact_Assessment.md`, `Release_1b_Risk_Reconciliation_Report.md`, `Release_1c_Risk_Impact_Assessment.md`
- Validation strategy and plan: `ML-SYS-VP-001_Validation_Plan.md`, `Release_1b_Validation_Strategy.md`, `Release_1c_Validation_Strategy.md`
- Operational qualification (OQ / UAT): `WP4_Release_1a_OQ_UAT_Protocol.md`, `Release_1b_OQ_UAT_Plan.md`, `Release_1b_OQ_Execution_Report.md`, `Release_1c_OQ_Protocol.md`, `Release_1c_OQ_UAT_Plan.md`, `Release_1c_OQ_Execution_Record.md`

## IDs defined in Word documents

These IDs are used in the code but do not appear in any markdown file in this folder. Their definitions are in the Word documents listed above. The Word files were not opened for this list.

- DC-URS-165
- FS-1a-173
