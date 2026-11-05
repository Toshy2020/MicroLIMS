# Microbiology Solutions and Diluent / Neutralizer Traceability — Plan

Status: **Decided** (2026-11-05). All open questions are answered (D1–D11). Nothing is built yet.

## 1. Problem

Microbiology's **Test Preparation** (Product / RM / PM / Water samples, once per sample, before any result) records the
diluent and neutralizer as **free text**:

- `ItemPreparationConfiguration.Diluent / Neutralizer` (Item master, `ItemPreparationConfigurationSection.tsx`) are text boxes.
- `SamplePreparation.Diluent / Neutralizer` copy that text at confirmation.

So no result can be traced to the bottle of diluent or neutralizer that was actually used. The old `DiluentType`
list with batch tracking was dropped in the 2026-09 simplification because no diluent type ever needed it.

Microbiology also has **no place to prepare chemical solutions**. Media Preparation covers solutions made from
dehydrated media (one dehydrated media lot, autoclave, GPT, release). It does not cover recipes built from chemicals
or reagents, such as:

- phosphate buffer stock
- phosphate buffer solution
- 0.9% saline
- Tween 80 / lecithin neutralizer

The physicochemical lab already has this area (**Solution Master + Solution Preparation**), built for HPLC.

## 2. Decisions so far

| # | Question | Answer |
|---|---|---|
| D1 | Do all Microbiology solutions need autoclaving? | **No.** Some recipes are not autoclaved. For example, *phosphate buffer stock* is refrigerated and then used to prepare *phosphate buffer solution*, which is the one that is autoclaved. |
| D2 | Who releases a prepared solution before use? | **A Reviewer or a Section Head** releases it, with an e-signature. The person who prepared it cannot release it. |
| D3 | How is the neutralizer used? | Three ways: **prepared** as its own solution, **used directly after autoclaving**, or **added to a culture medium during media preparation**. |
| D4 | Are ready-to-use purchased diluents used? | **Mostly prepared in house.** Purchased ready-to-use lots stay possible (as received stock) but are a minor path. |
| D5 | Which solution types does Microbiology use? | **Diluent, Reagent, Neutralizer.** |
| D6 | When does the expiry count from? Is there a shorter life after opening? | **From the preparation date.** There is **no** separate shelf life after opening. |
| D7 | Which checks are needed before release? | **pH only when the recipe needs it.** No sterility test before release, because the negative control in routine work is enough. |
| D8 | How is purified water recorded? | **As text**, not as a stock lot. |
| D9 | Does each use deduct volume from the prepared bottle? | **No.** Picking the lot is enough. |
| D10 | Does grouped preparation use one diluent lot for the whole group? | **Yes.** |
| D11 | Can a missing media additive block media preparation? | **No.** Adding a neutralizer is an optional step during media preparation and never blocks anything. |

## 3. Design

### 3.1 Reuse Solution Master / Solution Preparation for Microbiology

Solution Master and Solution Preparation records already belong to one lab (`SectionId`). A preparation already:

- follows a recipe
- picks real stock lots and deducts them
- gets a code and an expiry
- is e-signed and audited

Changes:

1. **Recipe types for Microbiology (D5): Diluent, Reagent, Neutralizer.** `Diluent` already exists in `SolutionType`,
   and only Mobile Phase links to an HPLC method, so Microbiology reuses it. `Reagent` and `Neutralizer` are added.
   These types hide the HPLC-only fields (HPLC method, titrant standardization). The HPLC types are unchanged.
2. **Per-recipe flags** (on `SolutionMaster`):
   - **Sterilization required.** When on, completing a preparation requires the autoclave record (D1): autoclave
     equipment, program, load type, temperature, cycle time and cycle number, the same fields as Media Preparation.
   - **Release required.** When on, a Reviewer or Section Head must release the preparation before it can be used (D2).
     It is on by default for Microbiology types.
   - Storage condition and shelf life already exist (e.g. phosphate buffer stock: 2–8 °C). The expiry is the
     preparation date plus shelf life (D6), as today. There is no separate shelf life after opening.
   - **pH** stays optional: it is recorded only when the recipe has a pH target (D7). There is no sterility test step;
     the negative control in routine work covers sterility.
   - **Text lines (D8).** A recipe can carry free-text lines such as "Purified water to 1000 mL". These lines are
     printed on the preparation record but don't pick a lot or deduct stock.
3. **A recipe can use another prepared solution as a component (D1).** Today a `SolutionComponent` can only point at a
   material master entry. Add a second component kind, **prepared solution of recipe X**. At preparation, the
   analyst picks a valid preparation of X (prepared, released if required, unexpired) instead of a stock lot.
   Example chain: *Phosphate buffer stock* (not autoclaved, refrigerated) → *Phosphate buffer solution pH 7.2*
   (stock + water, autoclaved, released).
4. **Release step.** Preparation status becomes `InProgress → Prepared → Released` (or `Rejected`) for recipes that
   need release. Release uses a new permission, `Solutions.Release`, held by the Reviewer and Section Head roles. It is
   e-signed, and segregation of duties is enforced: whoever prepared it cannot release it. Rejection needs a comment.
5. **Codes.** The existing MP/DL/VS code scheme gains `RE` (Reagent) and `NT` (Neutralizer). Diluent keeps `DL`.

### 3.2 Neutralizer added to culture media during media preparation (D3)

Media Preparation today consumes exactly one dehydrated media lot. Add an optional **Additives** step to media
preparation. The analyst decides whether to add a neutralizer; leaving the step empty never blocks anything (D11).
Each additive is recorded with its quantity and is one of:

- a stock lot (e.g. polysorbate 80 or lecithin received as reagents), or
- a released solution preparation (e.g. a prepared neutralizer).

An additive that is a stock lot is deducted from stock. An additive that is a prepared solution is only recorded
(D9). Additives are listed on the media lot. A medium with a neutralizer added (e.g. TSA with neutralizers) is then
itself the traceable "neutralizer" for the test.

### 3.3 Item master: pick the diluent and neutralizer from master data

`ItemPreparationConfiguration` replaces the two text fields with structured references. Each one is exactly one of:

| Source | Use for |
|---|---|
| **Media product** | Diluents made from dehydrated media (TSB, BPW, buffered sodium chloride-peptone, Letheen) and media that contain the neutralizer (D3) |
| **Solution recipe** (Microbiology type) | Buffers, saline, neutralizers prepared from chemicals |
| **Material master entry** | Ready-to-use purchased lots (D4, minor path) |

The neutralizer can also be **"None / contained in the medium"**, for when the neutralizer is in the selected media
product. Changing these fields sets the configuration back to PendingReview, as edits do today.

### 3.4 Test Preparation: pick the actual lot

When the analyst confirms preparation of a sample, the workspace shows only valid lots of what the Item master
names, from a lab the analyst can read:

- **Media product:** media lots that are GPT-conforming, released, active and unexpired.
- **Solution recipe:** preparations that are Prepared and Released (if release is required) and unexpired.
- **Material entry:** lots that are in stock, released if their category needs approval, and unexpired.

The chosen lot ids are stored on `SamplePreparation`, beside the existing text snapshot, and are audited with the
e-signature. Picking a lot does not deduct volume from it (D9). The lot appears on the sample report, in the audit trail, and in a "where used" view for each lot, so a
failed or recalled lot shows every sample it touched. Grouped (batch) preparation picks one diluent lot and one neutralizer lot for the whole group (D10).

### 3.5 Existing data

- Existing Item configurations: each text value is matched to a media product, solution recipe or material entry
  when the name matches exactly (case-insensitive). The rest are listed for a Section Head to set. Until a
  configuration is set, it keeps working as today, with no lot picked and a warning shown. It is one audited run with
  a summary row, the same approach as Q9 in the Master Testing Materials plan.
- Past `SamplePreparation` rows are never changed.
- The unused `DiluentType` list and its lot-lookup helpers (`GetSelectableDiluentLotsAsync`) are removed.

## 4. Phases

1. **Solutions for Microbiology (backend):** new types, sterilization and release flags, component kind "prepared
   solution", release step and permission, codes, tests.
2. **Solutions for Microbiology (UI):** recipe screen for Microbiology types, autoclave section in the preparation
   wizard, release dialog, menu entry under Microbiology.
3. **Media additives:** an optional additives step on media preparation (never blocking), UI.
4. **Item master and Test Preparation:** structured diluent / neutralizer, lot picking at confirmation, report and
   where-used view, existing data matching, removal of `DiluentType`.

Each phase is verified with the full backend test run against PostgreSQL and the frontend tests, as in the previous
plan.

## 5. Open questions

None. All questions were answered on 2026-11-05 (D5–D11 in section 2).
