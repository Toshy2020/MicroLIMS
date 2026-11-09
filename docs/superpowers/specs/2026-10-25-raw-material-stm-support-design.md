# Raw-material STM support and routine setup for 14 materials

Date: 2026-10-25. Source documents: `E:\RM STMs\94e49139-New_STM_2025-3.zip`
(extracted to `E:/MicroLIMS/rls-tmp/rm-stms`, text copies in `E:/MicroLIMS/rls-tmp/rm-stms-txt`).

## Intent

The QC lab tests incoming raw materials against a Specifications and Testing Method (STM) and a
Specifications Sheet per material. MicroLIMS already holds the 14 items but none has specifications
or tests, and a few things the STMs require have no home in the system. This work closes those gaps
and then loads the 14 materials so they can be received and tested as routine work: first in local
LIMSV2, then in production (Neon) once verified locally.

## How RM testing works (from the STMs)

- Physical tests (description) are done on **each received container**; chemical tests on a
  **composite** of all containers.
- Routine and planning samples get **all** tests; periodic retest samples get only the
  stability-indicating tests.
- Two labs: RM physchem analyst -> RM unit head review; microbiologist -> micro unit head review;
  physchem section head reviews the final CoA; QC/R&D manager accepts / rejects / orders re-analysis.
- A test can be outsourced when equipment is unavailable.
- A test can be formally declared "not applicable" with justification, criticality and actions,
  signed by QC and QA managers (Riboflavin 5-phosphate form).
- Some spec sheets carry supplier-stated attributes ("Additional requirements": particle size, plant
  part, extraction ratio, carrier) that QC does not test.

## Goals (system changes)

1. **Grade per specification set.** An item can hold one spec set per grade (BP, USP, In-house).
   Pyridoxine and Sodium Hydroxide have both BP and USP sets under the same item. At receiving, the
   receiver picks the grade **based on the supplier CoA**; the grade decides the ordered tests and
   limits, is stored on the sample and printed on the CoA. Items with a single grade preselect it.
   The supplier CoA can be attached to the sample at receiving.
2. **Selectable tests for a retest sample.** When a sample is received as a periodic retest, the
   item's assigned tests are listed and **only a section head or supervisor** chooses which ones run.
   Analysts cannot change the selection. The choice is signed and audit-trailed.
3. **Per-container description workflow.** For description/appearance-type tests the analyst enters
   the number of received containers (drums), records a result for each container, and records a
   final conclusion ("all containers comply" or not). Any non-complying container makes the test
   non-conforming. Chemical tests remain one result on the composite.
4. **Dried / anhydrous basis from the sample's own result.** Assays whose spec says "dried
   substance" or "anhydrous substance" (titration, HPLC, ICP) take LOD % or water % from the same
   sample's LOD / Karl Fischer result instead of a hand-typed value. The assay cannot be finalized
   until that result exists. If the LOD/KF result is later corrected, the dependent assay is flagged
   for recalculation.
5. **HPLC related substances.** A related-substances result mode on the HPLC method: named
   (specified) impurities with their own limits, unspecified impurities (limit per impurity), total
   impurities, disregard limit / reporting threshold, and relative retention for peak identification,
   calculated against a diluted reference solution. (Area normalization is only an alternative
   method in the Sildenafil STM, so it is not built - see "Main method only".)
6. **HPLC internal standard.** HPLC assay option to calculate from the analyte / internal-standard
   peak-response ratio (USP Pyridoxine assay with p-hydroxybenzoic acid).
7. **UV spectrophotometry workflow.** Assay against a standard (absorbance ratio x weights x
   potency x dilutions, with basis), and specific absorbance A(1%, 1 cm) evaluated against a range
   (Pyridoxine BP identification, Riboflavin phosphate assay, Riboflavin inorganic phosphate,
   Soya total isoflavones once its method exists).
8. **Supporting records.**
   - Outsourced test: mark a test order as outsourced, record the external lab, attach its report,
     enter the result against the spec; it goes through normal review.
   - Not-applicable test: per item (and grade), test + justification + criticality + immediate and
     long-term action, approved with signatures (Section Head, then QC Manager). It is not ordered and shows
     on the CoA as "Not applicable" with the reference.
   - Supplier-stated attributes: per item spec set, name + stated value, printed on the CoA as
     "stated by supplier", no result entry.
   - Retention quantity: recorded **by the reviewer at the end of review**, not at receiving.
9. **AAS runs as ICP.** Every AAS test (Lead, Sodium, Potassium, Selenium: total, as selenate, as
   selenite) is configured as an ICP workspace method (calibration curve from standard levels, e.g.
   1/3/6 ppm Pb). No separate AAS workflow; no relabeling of the ICP screens is required.

## Goal (data setup)

10. For each of the 14 materials below, from the **latest-dated** STM and spec sheet in the zip:
    - spec set(s) per grade: every test row with limit, unit, reference (e.g. "BP 2024"), basis;
    - test definitions: reuse general ones across materials where the procedure is the same
      (description per container, solubility, LOD, sulfated ash / residue on ignition, pH, limit
      tests, TAMC, TYMC, E. coli, Salmonella); material-specific ones where the procedure is specific
      (each titration with its titrant and equivalence factor, each HPLC/ICP/UV method);
    - titrant and other solution masters the STMs require (e.g. 0.1 M perchloric acid VS, 0.1 N
      silver nitrate VS) and their standardization definitions;
    - HPLC methods and ICP (AAS) methods from the STM text;
    - not-applicable records (Riboflavin related substances, composition);
    - supplier-stated attributes;
    - minimum sample quantities.
    Verify each material end to end on local with one test sample (receive -> test -> review ->
    approve -> CoA), then apply the same setup to Neon.

| Item (local Id) | Code | STM file used | Spec sheet used | Grades |
|---|---|---|---|---|
| Pyridoxine Hydrochloride (119) | STM-RM-097 | STM-Pyridoxine (004) after edit | ...Pyridoxine Hydrochloride BP + USP (v1/2) | BP, USP |
| Riboflavin Phosphate Sodium (92) | STM-RM-56 | Riboflavin 5-phosphate -SM | Specifications sheet Riboflavin 5-phosphate | BP |
| Siberian Ginseng (80) | STM-RM-151 | STM ... 1-1 | Specifications sheet Siberian Ginseng | In-house + USP micro |
| Sildenafil citrate (88) | STM-RM-051 | STM Sildenafil Citrate New | Specifications sheet Sildenafil Citrate | BP |
| Soluble sodium chloride tablets (90) | STM-RM-053 | STM Sodium Chloride Tablet 1-3 | Specifications sheet Sodium Chloride Tablet | BP |
| Sodium hydroxide (79) | STM-RM-150 | Sodium Hydroxide- STM (+ SM, SM BP) | ...Sodium Hydroxide BP + USP | BP, USP |
| Sodium Hypochlorite (62) | STM-RM-126 | STM Sodium Hypochlorite | Specifications sheet Sodium Hypochlorite | BP |
| Sodium Lauryl Sulfate (116) | STM-RM-091 | Sodium Lauryl sulfate | Specifications sheet Sodium Lauryl sulfate | BP |
| Sodium Metabisulfite (95) | STM-RM-060 | Sodium Metabisulfit - SM without arsenic | Specifications sheet | BP |
| Sodium Methyl Paraben (99) | STM-RM-064 | Sodium Methyl Paraben- SM | Specifications sheet Sodium Methyl Paraben | BP |
| Sodium Propyl Paraben (98) | STM-RM-063 | Sodium propyl Paraben- SM | (non-"old") Specifications sheet | BP |
| Sodium selenate anhydrous (180) | STM-RM-025 | STM Sodium Selenate Anhydrous | Specifications sheet Sodium Selenate | In-house |
| Sodium Starch Glycolate Type A (114) | STM-RM-089 | STM Sodium Starch Glycolate Type A | ...Type A new | BP |
| Soya isoflavone extract 10-12% (77) | STM-RM-147 | Soya Isoflavone Extract -SM - Micro | Specifications sheet Soya Isoflavone | In-house + USP micro |

Micro tests (TAMC/TYMC/E. coli/Salmonella) follow each spec sheet even where it cites USP while the
physchem tests cite BP; that is one spec set, not two grades.

## Non-goals

- **Disinfectant efficacy** (Sodium Hypochlorite biocidal effectiveness, log reduction): deferred to
  its own later project. Its spec row is not created now.
- No separate AAS workflow or AAS screens.
- No ERP item codes (VB...) stored; items stay keyed by STM code.
- No change to the Document Control STM documents themselves.

## Anti-goals

- Inventing a method or limit the documents do not state. Gaps go back to the user (list below).
- An analyst being able to change which tests a retest sample runs.
- An assay on dried/anhydrous basis finalizing with a moisture value not traceable to a result.
- Changing behaviour for existing finished-product / water / EM samples (grade and container
  features apply where configured; everything else keeps working as today).

## Constraints

- Clean Architecture; GMP rules live in the Application layer, never the frontend.
- Every new sensitive action (grade choice, retest selection, NA approval, outsourced result,
  retention quantity) is audit-trailed; selections and approvals carry electronic signatures.
- EF Core migrations; prod migrates at startup, so a read-only Neon pre-merge check is required.
- Work is phased with a stop point after each phase; each tested phase is committed locally;
  nothing is pushed or merged without the user asking.
- Builds/tests run with artifacts under `E:/MicroLIMS/rls-tmp`; Postgres test suite must pass.

## Known document issues to bring back to the user (do not guess)

- Soya isoflavone: spec lists "Total isoflavones (UV) NLT 100 mg/g", but only a micro STM exists.
- Siberian Ginseng: the "Assay" line under additional requirements is cut off; no assay method.
- Pyridoxine USP sheet: "Content of Chloride" technique says Colorimetry, STM uses titration.
- Methyl/Propyl Paraben sheets: IR identification text is HPLC retention-time wording.
- Sodium Starch Glycolate: E. coli "Absence / gm" vs other materials' "/10 gm".
- Riboflavin phosphate: minimum sample quantity "200 ml" for a powder.
- Sodium Lauryl Sulfate: micro limits appear only under "Additional requirements".

## User answers on the document issues (2026-11-04)

- Soya isoflavone total isoflavones (UV): **skipped**, no row.
- Siberian Ginseng assay: **skipped**, no row.
- Pyridoxine USP content of chloride: **titration** (as the STM).
- Methyl/Propyl Paraben identification: **HPLC retention time**.
- Sodium Starch Glycolate E. coli: **keep "Absence / gm"** as written.
- Riboflavin phosphate minimum sample quantity: **200 g**.
- Sodium Lauryl Sulfate micro limits: **not added**.

## User decisions after the builder check

- **Main method only.** Each grade's spec set runs its main method; "alternative" methods in the
  STMs (Pyridoxine in-house HPLC, Sildenafil normalization, NaCl tablet alternative) are not set up
  and the analyst cannot switch method. Pyridoxine: BP grade assay = potentiometric titration,
  USP grade assay = HPLC with internal standard (so goal 6 stays).
- **QC Manager role** is added (new role, alongside Analyst, Supervisor, Section Head, System
  Administrator). A not-applicable test is approved with two electronic signatures: Section Head
  and QC Manager (mirrors the form's QC/QA sign-off; there is no QA role in the system).
  *Author's reading:* the QC Manager role gets only this approval for now; whether it also takes
  over the final accept/reject decision is a later question.

## Calls made after the builder check (author's calls, user to confirm)

- **Periodic retest** is a new Cause of Testing value "Periodic retest", not the existing OOS
  "Retest" (avoids side effects in OOS/approval flows). A periodic-retest sample waits in
  "awaiting test selection" and no test orders exist until a Section Head or Supervisor (existing
  roles) signs the selection. No per-test stability flag; the head chooses every time.
- **Dried / anhydrous source:** "dried substance" reads the sample's LOD result, "anhydrous" reads
  its water (KF) result. An outsourced LOD/KF result counts once entered. If the spec set has no
  such row the assay basis cannot be dried/anhydrous (configuration error at spec save time).
  Values come from the composite result.
- **Minimum quantities:** spec set stores routine and periodic-retest minimums; receiving warns
  below the minimum, does not block.
- **Retention quantity:** required before the physchem final review is passed; value + unit; kept
  on the sample record, not printed on the CoA. Micro review does not ask for it.
- **Not-applicable test:** the spec row is absent; the NA record is the trace and prints on the CoA.
- **Related substances:** optional relative response factor per named impurity.
- **Neon:** items matched by STM code; any prod item missing or differing is reported, not created.

## Reagent inventory loaded (2026-10-25, local only)

`E:\RM STMs\List of materials in QC laboratories (New) ...doc` (Wet chemical Lab, 315 lots) was
loaded into the FP section master list and stock through the API (audit-trailed), script in the
session scratchpad, log `E:/MicroLIMS/rls-tmp/rm-matlist/import-run*.log`, DB backup
`E:/MicroLIMS/backups/LIMSV2-before-reagent-import-2026-10-25.dump`. 253 new master entries
(225 reagents, 18 reference standards = ICP/AAS element standards, 10 indicators), 309 lots.
Placeholder values to fix: quantity 500 g / 1000 mL per lot; purity 100 % on reference/primary
standard lots (KHP 99.5 %); 12 lots with "x years from opening" have no expiry date.
Data setup (goal 10) links STM reagents and AAS standards to these entries.

## Left to the builder

Data model shape (how grade attaches to spec rows, how container results are stored), screen
layout, phase order beyond "system changes before data setup", which existing test definitions to
reuse vs replace, and how master data is loaded (workbook import, API, or seed script), provided it
is repeatable on Neon.
