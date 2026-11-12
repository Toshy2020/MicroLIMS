# Specification set index

This folder holds the user requirements (URS), functional specifications (FS), the calculation register and the list of conflicts for MicroLIMS. Every document is a draft. Only QA sets a document to Approved.

## Areas

Each of the 24 areas has one URS and one FS. Both are drafts.

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

## Counts (2026-11-12)

- URS entries: 240
- FS entries: 247
- Calculation register rows (CALC): 83
- Conflicts (CONF) entries: 93. Open: 92. Decided: 1.

| Area | URS entries | FS entries |
|---|---|---|
| RCV | 12 | 12 |
| MIC | 12 | 11 |
| PAT | 12 | 13 |
| WAT | 8 | 7 |
| ENV | 5 | 4 |
| ACL | 8 | 7 |
| PHY | 15 | 16 |
| INS | 16 | 16 |
| MED | 12 | 12 |
| SOL | 9 | 8 |
| CRY | 10 | 11 |
| WST | 8 | 7 |
| INV | 10 | 11 |
| EQP | 12 | 14 |
| OOS | 2 | 2 |
| MON | 13 | 11 |
| REV | 12 | 10 |
| SIG | 4 | 5 |
| AUD | 8 | 10 |
| RPT | 9 | 8 |
| MDM | 16 | 25 |
| NTF | 5 | 5 |
| ADM | 10 | 10 |
| SEC | 12 | 12 |
| **Total** | **240** | **247** |

## Status of this set

- Every document is a Draft.
- An AI agent wrote these documents by reading the code.
- No statement has been verified by running the system.
- No pharmacopoeia or regulation reference is confirmed. Where a reference appears, it is marked "to be confirmed by QA".
- Only QA can set a document to Approved.
- The open conflicts are questions for the laboratory. They are not decisions.

## How to sign a document

1. Open the rendered document in the browser.
2. Print it and save the print as a PDF.
3. QA signs the PDF.
4. The signed copy is stored in Document Control.

## Open items for the site

1. Who is the QA reviewer and approver named in the document headers?
2. The document numbering your site uses (for example `ML-URS-001`), if it must follow an existing scheme.
3. The editions of USP, Ph. Eur. and BP in force at your site.

## Templates and checks

- `_templates/URS-template.md` and `_templates/FS-template.md` hold the header table and the entry format.
- `node scripts/docs/check-documents.mjs documents` checks IDs, cross-references and headers. It skips `DocumentControl/` and `_templates/`.
