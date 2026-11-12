# Specification set index

This folder holds the user requirements (URS), functional specifications (FS), the calculation register and the list of conflicts for MicroLIMS. Every document is a draft. Only QA sets a document to Approved.

## Areas

Each of the 24 areas has one URS and one FS. Both are drafts. The files are not written yet; the paths show where they will go.

| Area | URS file | FS file | Version | Status |
|---|---|---|---|---|
| RCV | `URS/URS-RCV.md` | `FunctionalSpecification/FS-RCV.md` | 0.1 | Draft 0.1 |
| MIC | `URS/URS-MIC.md` | `FunctionalSpecification/FS-MIC.md` | 0.1 | Draft 0.1 |
| PAT | `URS/URS-PAT.md` | `FunctionalSpecification/FS-PAT.md` | 0.1 | Draft 0.1 |
| WAT | `URS/URS-WAT.md` | `FunctionalSpecification/FS-WAT.md` | 0.1 | Draft 0.1 |
| ENV | `URS/URS-ENV.md` | `FunctionalSpecification/FS-ENV.md` | 0.1 | Draft 0.1 |
| ACL | `URS/URS-ACL.md` | `FunctionalSpecification/FS-ACL.md` | 0.1 | Draft 0.1 |
| PHY | `URS/URS-PHY.md` | `FunctionalSpecification/FS-PHY.md` | 0.1 | Draft 0.1 |
| INS | `URS/URS-INS.md` | `FunctionalSpecification/FS-INS.md` | 0.1 | Draft 0.1 |
| MED | `URS/URS-MED.md` | `FunctionalSpecification/FS-MED.md` | 0.1 | Draft 0.1 |
| SOL | `URS/URS-SOL.md` | `FunctionalSpecification/FS-SOL.md` | 0.1 | Draft 0.1 |
| CRY | `URS/URS-CRY.md` | `FunctionalSpecification/FS-CRY.md` | 0.1 | Draft 0.1 |
| WST | `URS/URS-WST.md` | `FunctionalSpecification/FS-WST.md` | 0.1 | Draft 0.1 |
| INV | `URS/URS-INV.md` | `FunctionalSpecification/FS-INV.md` | 0.1 | Draft 0.1 |
| EQP | `URS/URS-EQP.md` | `FunctionalSpecification/FS-EQP.md` | 0.1 | Draft 0.1 |
| OOS | `URS/URS-OOS.md` | `FunctionalSpecification/FS-OOS.md` | 0.1 | Draft 0.1 |
| MON | `URS/URS-MON.md` | `FunctionalSpecification/FS-MON.md` | 0.1 | Draft 0.1 |
| REV | `URS/URS-REV.md` | `FunctionalSpecification/FS-REV.md` | 0.1 | Draft 0.1 |
| SIG | `URS/URS-SIG.md` | `FunctionalSpecification/FS-SIG.md` | 0.1 | Draft 0.1 |
| AUD | `URS/URS-AUD.md` | `FunctionalSpecification/FS-AUD.md` | 0.1 | Draft 0.1 |
| RPT | `URS/URS-RPT.md` | `FunctionalSpecification/FS-RPT.md` | 0.1 | Draft 0.1 |
| MDM | `URS/URS-MDM.md` | `FunctionalSpecification/FS-MDM.md` | 0.1 | Draft 0.1 |
| NTF | `URS/URS-NTF.md` | `FunctionalSpecification/FS-NTF.md` | 0.1 | Draft 0.1 |
| ADM | `URS/URS-ADM.md` | `FunctionalSpecification/FS-ADM.md` | 0.1 | Draft 0.1 |
| SEC | `URS/URS-SEC.md` | `FunctionalSpecification/FS-SEC.md` | 0.1 | Draft 0.1 |

## Other documents

| Document | Location | Status |
|---|---|---|
| Calculation register | `DesignSpecification/CalculationRegister.md` | Draft |
| Conflicts list | `Conflicts.md` | Draft |
| Document Control set (Releases 1a to 1d) | `DocumentControl/` (see `DocumentControl/README.md`) | Moved, unchanged |

## Templates and checks

- `_templates/URS-template.md` and `_templates/FS-template.md` hold the header table and the entry format.
- `node scripts/docs/check-documents.mjs documents` checks IDs, cross-references and headers. It skips `DocumentControl/` and `_templates/`.
