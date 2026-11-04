# Microbiology Solutions and Diluent / Neutralizer Traceability — Plan

Status: **Draft for decision** (2026-11-03). Nothing is built yet.

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

## 3. Design

### 3.1 Reuse Solution Master / Solution Preparation for Microbiology

Solution Master and Solution Preparation records already belong to one lab (`SectionId`). A preparation already:

- follows a recipe
- picks real stock lots and deducts them
- gets a code and an expiry
- is e-signed and audited

Changes:

1. **Recipe types for Microbiology.** Extend `SolutionType` with `Buffer`, `Neutralizer` and `RinseFluid`. These types
   hide the HPLC-only fields (HPLC method, titrant standardization). The HPLC types are unchanged.
2. **Per-recipe flags** (on `SolutionMaster`):
   - **Sterilization required.** When on, completing a preparation requires the autoclave record (D1): autoclave
     equipment, program, load type, temperature, cycle time and cycle number, the same fields as Media Preparation.
   - **Release required.** When on, a Reviewer or Section Head must release the preparation before it can be used (D2).
     It is on by default for Microbiology types.
   - Storage condition and shelf life already exist (e.g. phosphate buffer stock: 2–8 °C).
3. **A recipe can use another prepared solution as a component (D1).** Today a `SolutionComponent` can only point at a
   material master entry. Add a second component kind, **prepared solution of recipe X**. At preparation, the
   analyst picks a valid preparation of X (prepared, released if required, unexpired) instead of a stock lot.
   Example chain: *Phosphate buffer stock* (not autoclaved, refrigerated) → *Phosphate buffer solution pH 7.2*
   (stock + water, autoclaved, released).
4. **Release step.** Preparation status becomes `InProgress → Prepared → Released` (or `Rejected`) for recipes that
   need release. Release uses a new permission, `Solutions.Release`, held by the Reviewer and Section Head roles. It is
   e-signed, and segregation of duties is enforced: whoever prepared it cannot release it. Rejection needs a comment.
5. **Codes.** The existing MP/DL/VS code scheme gains prefixes for the new types, for example `BF`, `NT` and `RF`.

### 3.2 Neutralizer added to culture media during media preparation (D3)

Media Preparation today consumes exactly one dehydrated media lot. Add optional **additives** to a media
preparation, each recorded with its quantity:

- a stock lot (e.g. polysorbate 80 or lecithin received as reagents), or
- a released solution preparation (e.g. a prepared neutralizer).

Additives are deducted from stock and listed on the media lot. A media product can declare its **expected
additives** so the form proposes them. Such a medium (e.g. TSA with neutralizers) is then itself the traceable
"neutralizer" for the test.

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
e-signature. The lot appears on the sample report, in the audit trail, and in a "where used" view for each lot, so a
failed or recalled lot shows every sample it touched. Grouped (batch) preparation picks one lot for the whole group.

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
3. **Media additives:** additives on media preparation, expected additives on the media product, UI.
4. **Item master and Test Preparation:** structured diluent / neutralizer, lot picking at confirmation, report and
   where-used view, existing data matching, removal of `DiluentType`.

Each phase is verified with the full backend test run against PostgreSQL and the frontend tests, as in the previous
plan.

## 5. Open questions

1. **Names of the new types.** Is *Buffer / Neutralizer / Rinse fluid* the right set, or do you use other groups (e.g.
   *Diluent*, *Stain*, *Disinfectant*)?
2. **Expiry after autoclaving.** Does shelf life count from the preparation date or from the autoclave date? Is there
   a different shelf life once a bottle is opened?
3. **Checks before release.** Should the release record pH (target ± tolerance, which already exists on recipes)? Is
   a sterility check needed for any solution, and if so, should release wait for it?
4. **Water.** Is purified water a component picked as a stock lot, or recorded as text (e.g. "Purified water, loop
   point")?
5. **Partial use.** Should each use at Test Preparation deduct volume from the prepared bottle, or is picking the lot
   enough?
6. **Grouped preparation.** Is one diluent lot for the whole group correct, or can samples in a group use different
   lots?
7. **Media additives.** Should a media product with expected additives refuse preparation without them, or only warn?
