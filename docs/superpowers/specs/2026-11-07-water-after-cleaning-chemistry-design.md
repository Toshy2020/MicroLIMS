# Water and After-Cleaning: Chemical Testing — Design

Date: 2026-11-07 · Status: draft for user review

## Goal
Extend Water and After-Cleaning monitoring, today microbiology-only, so the
Physicochemical laboratory tests the same water points and machine parts:
- Water chemistry is owned by the **RM** area (`PhyschemArea.RawPackaging`).
- After-cleaning chemistry is owned by the **FP** area (`PhyschemArea.FinishedProduct`).

## User decisions (2026-11-07)
1. Water departments, sampling points, machines and parts move to **General
   Laboratory Configuration** and are shared by every lab.
2. Each lab assigns **its own tests** (picked from Test Master) to the shared
   points and parts and edits only its own rows.
3. **Each lab receives its own** water and after-cleaning samples. There is no
   split sample.
4. Frequency: some points share one frequency across labs and some don't.
   The point keeps a default; a lab assignment may override it.
5. After-cleaning chemistry is tested by **UV, HPLC or both**, chosen at receipt.
6. After-cleaning limits (revised 2026-11-07):
   - HPLC: a dedicated **cleaning HPLC method** holds the MACO, and the
     swab/rinse result is checked against it (NMT MACO).
   - Metals: tested in the **ICP workspace the same way as today**. The ICP
     test's element carries the limit.
   - UV: qualitative, *Not detected* / *Detected*. This limit is set on the part.
9. One cleaning HPLC method **per previous product**. The previous product
   becomes a **pick from the product list** at receipt, no longer free text
   (existing rows keep their text).
10. The **same MACO** applies to every machine and part.
11. Receipt offers **any combination of UV, HPLC and ICP**.
12. Units: HPLC MACO is **µg/swab or µg/cm²** (fixed list). The ICP limit is in **ppm**.
13. UV after cleaning is a **wide-range wavelength scan**. Any peak means
    failure, and the **analyst decides**: they record *Peaks detected* (fail) or
    *No peaks* (pass), with the scan range and an optional attached scan.
    The system computes nothing.
7. Active-ingredient residue and detergent residue are **separate methods**,
   so separate tests.
8. Water chemistry tests already exist in Test Master. Limits are set **per
   water point**, and some have **both lower and upper** limits (e.g. pH).

## What exists today
- `WaterSamplingPoint.AssignedTestCodes` + `SamplingConfiguration` (limits
  per point × test code; alert/action/spec text only).
- `MachinePart` + `MachinePartConfiguration` (limits per part × test).
- Every test already resolves to a lab section (`TestSectionLookup`) and,
  for physchem, to an area (`TestDefinition.PhyschemArea`). Ownership needs
  no new field.
- `Specification` already models `LimitType` (Range / NotMoreThan /
  Qualitative …), `LowerLimit`/`UpperLimit` with inclusive flags and
  `ExpectedResultText`. Point/part limits reuse this shape. No new limit model.
- Gap: `WaterWorkflowEngine.PrepareAsync` creates orders for **every**
  assigned code and requires access to all their sections. Once chemistry
  tests are assigned, a micro user cannot prepare. Phase 2 fixes this.

## Design

### 1. Shared master data (General Laboratory Configuration)
- Water departments/points and machines/parts move to the General Laboratory
  Configuration menu and stay editable by Section Heads. Micro and Physchem
  config screens show the same list.
- Test assignment rows are filtered and editable by the user's lab, plus
  area for physchem. This is the same ownership rule as specification rows
  (lab via TestCode → section; physchem area via `TestDefinition.PhyschemArea`).
- `AssignedTestCodes` on the point stays the list of codes. Ownership is derived
  from each code, not stored.

### 2. Limits per point / part
Add to `SamplingConfiguration` and `MachinePartConfiguration`, copying
`Specification` names and semantics:
`LimitType`, `LowerLimit`, `UpperLimit`, `LowerInclusive`, `UpperInclusive`,
`ExpectedResultText`.
- Micro rows keep `CountTiered` + alert/action/spec text and are unchanged.
- Water chemistry: `Range` (pH), `NotMoreThan` (conductivity, TOC, nitrates …).
- After-cleaning UV: `Qualitative`, expected *Not detected*.
- The physchem workspaces evaluate against these rows. A water or AC sample
  has no Item, so the spec resolver gets a branch that takes the point/part
  row instead of `Specification` by ItemId. **Riskiest part; Phase 3 starts here.**

### 2b. Limits on the method (after-cleaning HPLC and ICP)
- New `HplcResultMode.CleaningResidue` (append-only enum value 3). It is
  modelled on ResidualSolvents, which is already a limit-test mode with a
  standard concentration per analyte. Each analyte (the previous product's
  active ingredient) carries `MacoLimit` + `MacoUnit` (enum: UgPerSwab,
  UgPerCm2). The method also names the product it detects (`ProductItemId`,
  required in this mode). The run evaluates each swab/rinse result NMT MACO,
  and the part row needs no limit.
- Receipt: `Sample.PreviousProductItemId` (new, nullable; the existing
  `PreviousProductName` text stays for old rows). Picking HPLC offers only the
  part's cleaning HPLC tests whose method's product is the previous product.
  If none exists, receipt says so and HPLC cannot be chosen.
- The ICP element limit for item-less samples is in ppm.
- ICP: the existing `IcpTestMode.Impurity` flow runs unchanged. The element
  limit for an after-cleaning sample comes from a new limit field on
  `TestDefinitionIcpElement`, used when the sample has no Item. Item
  specifications keep working as they do now.
- The part's test list says which cleaning HPLC / ICP / UV tests can be run on it.

### 3. Per-lab receiving and preparation
- `Sample` gets the receiving lab (section id) for Water and AfterCleaning
  categories. It is set from the workspace the sample was received in.
- Preparation offers and creates only that lab's tests (and only the user's
  area for physchem). Codes owned by another lab are ignored, so the
  all-sections access check no longer blocks.
- After-cleaning physchem receipt asks for the previous product and for **any
  combination of UV / HPLC / ICP**. Preparation creates orders only for the
  part's tests that use the chosen techniques.
- Existing micro samples: receiving lab backfilled to MICRO by migration.

### 4. Frequency per lab
- The point keeps its frequency as the default.
- Optional frequency override per point × lab. Blank = point default.
- The Monitoring Plan generates occurrences per point × lab, so each lab's due
  list shows only its own work.

## Phases (stop after each for user check)
1. Shared master data in General Laboratory Configuration + lab-owned test
   assignment rows. No workflow change.
2. Receiving lab on Sample + per-lab preparation (water, after cleaning micro
   unchanged in behaviour; MICRO backfill migration).
3. Limit columns on point/part configs + physchem spec resolver branch;
   water chemistry runs end to end in the RM area. Browser E2E.
4. After cleaning in the FP area: the method choice at receipt, the
   CleaningResidue HPLC mode with MACO per analyte, the ICP element limit for
   item-less samples, and the UV qualitative check. Browser E2E.
5. Frequency override per point × lab + Monitoring Plan per lab.

Each phase: Postgres suite green, migration applied to LIMSV2 with a backup,
committed locally. Neon pre-merge check before any PR reaches prod.

## Open points (resolve in the plan, not blocking the spec)
- Does the CoA / report for water and AC need a combined micro + chem view, or
  is it per lab only (the default here, since the samples are separate)?
