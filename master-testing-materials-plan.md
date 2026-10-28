# Master Testing Materials — Plan

**Status:** all 10 open questions answered (2026-10-28, see section 6). **Phases 1–5 implemented** on branch `ccr-a9bd0ba9-dwle6o` (see section 7).
**Date:** 2026-10-28
**Related:** `barcode-labels-recon-report.md` (gap G1: Material Stock lots have no unique code, so they can't be labelled yet).

---

## 1. Goal

One app-wide master that defines **every material the labs receive into stock**, in five groups (decision Q1: standards are a group of their own):

| Group | Built from today's | Examples |
|---|---|---|
| **Reagents & Indicators** | `MaterialMasterEntry` categories Reagent and Indicator | Sodium hydroxide AR, Phenolphthalein |
| **Reference & Primary Standards** | `MaterialMasterEntry` categories Reference standard and Primary standard. Working standards (created by qualification, never received) are shown here read-only, next to the reference standard they came from | Ascorbic acid USP RS, Potassium hydrogen phthalate PS |
| **Culture Media** | `MediaProduct` (Media Configurations) | TSA, SDA, MacConkey broth |
| **Reference Organisms** | **New strain records**, each pointing at an `Organism` species (decision Q6) | *E. coli* ATCC 8739, *S. aureus* ATCC 6538 |
| **Other Materials** | **Nothing today** (free text on each stock lot) | Supplements, antibiotic discs, ID kits, ID reagents, reference buffers, disposable tools, plus any new type the lab adds |

Every received stock lot (`Material`) must then point to one master entry. The stock register keeps only what is specific to the lot: batch, quantity, expiry, location, and a new system-generated **stock ID**, which is what the barcode label encodes.

## 2. Where we are today (code findings)

| Fact | Evidence |
|---|---|
| Stock receipt links to a master for only some types: Dehydrated media → `MediaProduct` (required); Chemical, Indicator, Reference standard, Primary standard → `MaterialMasterEntry` (required for new lots; older lots stay unlinked); Lyophilized microorganism → `Organism` (optional at receipt) | `MaterialService.CreateAsync` (`backend/MicroLIMS.Application/Services/MaterialService.cs:134-200`), `RequiresMasterEntry` (`:86-87`) |
| The other 7 types have **no master**. Name and code are typed on every lot. `Other` also takes a free-text `CustomType` | `Material.cs:30-34`; `MaterialTypeRules.cs:11-12` |
| The 7 enum values carry only three pieces of logic: a **default unit** (`MaterialService.cs:49-64`), **which section's list shows them** (`MaterialTypeRules.cs:14-45`), and **"COA required before use" for Supplement** (`MaterialService.cs:366-371`, `MaterialDocumentService.cs:72-77`) | Turning them into data is low risk, as long as the COA rule is carried over. |
| The reagent master is **per section**: unique on (SectionId, Code), with codes like `RG-011` | `MaterialMasterEntryConfiguration.cs`, `MaterialMasterService.cs:63-67` |
| Media and Organism masters are already **global** | `MediaProduct.cs`, `Organism.cs` |
| `Organism` holds name and an ATCC number only: **no code, no active flag**, and only ATCC collection numbers | `Organism.cs`; filtered unique index on `AtccNumber` (`OrganismConfiguration.cs:22`) |
| A media product code change already needs a reason and an **electronic signature** | `MediaProductService.cs:119-140` |
| Master maintenance uses `MasterData.Manage` | `MaterialMasterController.cs:26-37`, `MediaMasterDataController.cs`, `OrganismMasterDataController.cs` |
| The three masters are on **three pages in two menus**: Organisms and Media Configurations under *Microbiology Configuration*; Reagents & Standards under *Physicochemical Configuration* | `frontend/src/routes/menuConfig.ts:181-182,232` |
| **Test Master points at stock lots instead of a product.** The permitted medium on a workflow step (`TestWorkflowStepMedia.MaterialId`) and batch-tracked diluent types (`DiluentType.MaterialId`) reference a received `Material` row. Matching already prefers the product and falls back to the exact batch for legacy rows | `TestWorkflowStepMedia.cs:3-5`, `DiluentType.cs`, `Helpers/StepMediumMatcher.cs` |
| Other masters link to the reagent master correctly: solution recipes, solution preparation components, working standard qualifications | `SolutionComponent.cs:13`, `SolutionPreparationComponent.cs:16`, `WorkingStandardQualification.cs:24` |
| The master-data Excel workbook already has `MaterialMaster` and `Organisms` sheets, which can be reused for the backfill review | `MasterDataWorkbook/Sheets/InventorySheets.cs:33-64`, `ReferenceSheets.cs:10-38` |

## 3. Design

### 3.1 Two levels under "Other Materials": categories and entries

Your idea of letting the master add new types needs two levels:

- **Category** (a type, maintained as data): *Supplement*, *Antibiotic Disc*, *Identification Kit*, … The lab can add new ones, for example *Culture Collection Media*, *PPE*, *Filter Membrane*.
- **Entry** (the material itself): *Egg Yolk Tellurite Emulsion*, which is a Supplement; *Ampicillin 10 µg disc*, which is an Antibiotic Disc.

Each category carries the behaviour the enum carries today, so no logic is lost:

| Category field | Replaces | Seeded value for the existing 7 |
|---|---|---|
| Name, code prefix (e.g. `SUP`) | Enum label | One category per existing type |
| Default unit | `DefaultUnitFor` | Same as today (Supplement → mL, Disc → Disc, Kit → Kit, …) |
| **COA required before use** | Hard-coded `CoaRequiredTypes` | **Supplement = Yes**, the others = No |
| Expiry required | (new) | Yes, except Disposable Tool |
| Available in sections | `MaterialTypeRules` | Same as today's Microbiology / Physicochemical lists |
| **Quantity tracked** (Q5) | (new) | Yes, except Disposable Tool (received and listed, not decremented) |
| **Default storage condition** (Q5) | (new) | Blank, suggested at receipt and printed on the label once set |
| **Approval before use** (Q5) | (new; today only media and cryovials have a release gate) | No for all 7. A lot in a category with this switched on can't be used until a second person releases it, using the same segregation-of-duties rule as media release (preparer can't approve) |
| **Label required** (Q5) | (new) | No until the barcode work ships. When on, receipt prompts to print the stock label |
| Active | (new) | Yes |

**Who may create or change a category (Q4):** a Section Head, with an electronic signature and a reason. A category carries GMP rules (COA, approval before use), so it is controlled more tightly than an entry.

Seeding:
- The 6 named types become categories: Supplement, Antibiotic Disc, Identification Kit, Identification Reagent, Reference Buffer, Disposable Tool.
- Each **distinct `CustomType`** already used on `Other` lots becomes its own category, once QA confirms it during reconciliation.
- `Other` lots with no `CustomType` go to a **General** category.

### 3.2 One master, five groups

Recommended shape: **a common master record plus one detail record per group.** The groups share code, name, owner lab, active flag and audit fields. Each group keeps its own fields:

- **Reagents & Indicators** and **Reference & Primary Standards:** both use `MaterialMasterEntry` as their detail (grade, source, base unit, indicator fields). The two groups are views over its category field, so recipes, solution preparations and working standard qualifications keep their links unchanged. The entry stops being per section, and its code becomes unique across the app.
- **Culture Media:** `MediaProduct` stays as the detail, with its configurations, incubation conditions and the lot-number prefix rule (code ≤10). Nothing about prepared media changes.
- **Reference Organisms (Q6):** a **new strain record** with code, active flag, **culture collection** (ATCC, NCTC, NCIMB, WDCM…) and **strain number**, pointing at an `Organism` species. The list holds **reference strains only**. `Organism` stays as the species list, because a test step's target organism (`TestWorkflowStep.TargetOrganismId`) is a species, not a strain. Lyophilized stock lots, cryovials and media challenge specs move to the strain record, or keep the species link with a strain added (decided in phase 3 design).
- **Other Materials:** the category from 3.1 plus default unit and supplier or grade.

Option rejected: **one wide table with every group's columns.** Every group would have to ignore columns that belong to the others, and the organism and media configuration links (`MediaConfiguration.OrganismId`, `MediaEvaluation.OrganismId`, `Cryovial.OrganismId`) would all have to move.

### 3.3 Codes

- **Unique across the app**, frozen once the entry has stock, and changed only with a reason and an e-signature (Q7). This is the existing `MediaProduct` pattern, applied to every group. Labels and cryovial codes are built from these codes.
- Enforced prefixes (Q7): `RG-` reagent, `IND-` indicator, `RS-` reference standard, `PS-` primary standard, `ORG-` reference organism, the category prefix for Other Materials (`SUP-`, `ABD-`, `IDK-`, `IDR-`, `BUF-`, `DSP-`, and the prefix set on any new category). Culture media keep their existing short codes (TSA, SDA), because prepared lot numbers depend on them. Working standards keep their `WS-nn/MM/yyyy` codes.
- **Conflict to resolve:** the same reagent code may already exist in two sections with different meanings. The merge in phase 1 must settle these.

### 3.4 Stock lot ID (what the label carries)

- Every received lot gets a system-generated, unique, never-edited **stock ID** in the format `STK-{yyyy}-{seq:D5}`, for example `STK-2026-00123` (Q8). Existing lots get one only if still in stock. Expired and depleted lots don't. Use one sequence for the whole app, protected by a unique index with one retry on a clash, the same approach as the other code generators.
- **Do not use `{MasterCode}/{seq}/{yy}`.** A received TSA powder lot would then look almost the same as a prepared TSA media lot (`TSA/03/26`), which invites mix-ups at the bench.
- The supplier batch number stays on the lot, for information and search.

### 3.5 Stock receipt after the change

1. Choose the **master entry first**: one searchable picker across all five groups, filtered to entries available to the user's section.
2. The type, name, code, default unit, storage condition and category rules come from the entry. **Free-text names are removed completely (Q10).** If a material isn't in the master, the owner lab's master-data manager creates the entry first.
3. Enter the lot details: batch, quantity, expiry, location, plus purity and moisture for standards, as today.
4. The stock ID is generated on save.

The stored `MaterialName` and `Code` on each lot stay as a **snapshot** taken at receipt, as they are now. Renaming a master entry later never rewrites historical lots.

### 3.6 Migration of existing stock ("create the names from the stock")

Decision Q9: **no formal protocol or review step.** The backfill runs as one audited operation:

1. **Build entries:**
   - Group existing lots by type, custom type, cleaned-up name (trimmed, spaces collapsed, case ignored) and code. Each group becomes one master entry with a generated code.
   - Match unlinked reagent and standard lots to existing `MaterialMasterEntry` rows by name and code.
   - Match lyophilized-organism lots with no `OrganismId` by ATCC number.
2. **Link:** link each lot to its entry and set its category (Q2: the stored type on old lots is kept, not rewritten). Every link is captured in the audit trail through the normal `SaveChanges` capture, with the reason "Master Testing Materials backfill".
3. **Stock IDs:** generate one for every lot still in stock (Q8).
4. **Report:** a run summary with counts per type, entries created, lots linked, and lots that couldn't be matched. Near-duplicate names that weren't merged (for example "Egg yolk tellurite" vs "Egg-Yolk Tellurite Emulsion") are listed so the owner lab can deactivate the spare entry afterwards. The snapshot names on historical lots are unchanged.

### 3.7 Repoint Test Master to the product

- `TestWorkflowStepMedia.MaterialId` (a stock lot) is replaced by the **Culture Media entry**. `StepMediumMatcher` already prefers the product, so this mostly removes the legacy fallback to the exact batch.
- `DiluentType.MaterialId` is replaced by the Culture Media entry.
- `ConfirmatoryMediaSelection.MaterialId` stays as it is. It records what an analyst actually used, so it should keep pointing at the real lot.

### 3.8 One page

A single **Master Testing Materials** page, in a shared configuration menu so both labs see it. It has five tabs (Reagents & Indicators | Reference & Primary Standards | Culture Media | Reference Organisms | Other Materials), and the Other tab has a *Manage categories* panel. Each tab reuses the screens that exist today, so this mostly regroups pages. The old routes redirect to the matching tab (`routes.ts` already has redirect helpers for these targets, `:118-199`).

### 3.9 Permissions and audit

- **Entries (Q3):** each entry has an **owner lab**. Only `MasterData.Manage` holders who are members of that lab can create or edit it. **Every lab can see and use every entry**, so the same material isn't created twice.
- **Categories (Q4):** created and changed only by a Section Head, with an e-signature and a reason.
- Code change on any group: a reason plus an e-signature (`SignatureMeaning.MasterDataChanged`), the same as media today.
- Deactivate, never delete, once an entry has stock or is used in a recipe or test.
- Every change is captured automatically in `AuditLog`.

## 4. Phased plan (each phase ends with a sign-off gate)

| Phase | Scope | Gate |
|---|---|---|
| **0. Decisions** | Done: all 10 questions answered (section 6). | Complete |
| **1. Common master; Reagents & Indicators and Standards go global** | Common master record with owner lab. Make `MaterialMasterEntry` app-wide, shown as two groups. Merge per-section duplicates automatically: same code and same cleaned-up name become one entry; same code with a different name keeps both, and the second gets a new code. Enforced prefixes. Code-change signature. | Merge run summary reviewed by the owner labs. Recipes, solution preparations and working standard qualifications still resolve correctly (regression tests). |
| **2. Other Materials** | Categories (seeded from the 7 types and the distinct `CustomType` values) with all behaviour fields from 3.1, including quantity tracked, storage condition, approval before use and label required. Category create/change needs a Section Head e-signature. COA rule moves from code to the category, with a test proving Supplement still blocks use without a COA. Release gate for categories with approval before use. Stock receipt requires a master entry for every type, with free text removed. Backfill from stock (section 3.6). | Backfill run summary produced. Every in-stock lot linked. COA and release-gate rules verified by tests. |
| **3. Culture Media and Reference Organisms join** | Link `MediaProduct` to the common master. New reference strain records (code, active flag, culture collection, strain number) pointing at `Organism` species, seeded from organisms that have an ATCC number. Lyophilized lots must have a strain at receipt. Backfill missing organism links by ATCC. Test-step target organisms stay on the species. | No change to prepared media lot numbers. Challenge-organism matching and target-organism tests pass. |
| **4. Test Master repoint** | Step media and diluent types point at Culture Media entries. Legacy batch fallback removed. | Every configured test step resolves to a product. Pathogen and count workflow regression tests pass. |
| **5. Single page and stock ID** | Master Testing Materials page with five tabs. Stock ID generation plus backfill for in-stock lots only. Stock receipt picker. | Users walk through it. **This unblocks Material lot labels in the barcode plan (phase 4 there).** |

Phases 1 and 2 can run in parallel after phase 0. Phase 5's stock ID can be pulled forward if labels are urgent, because it doesn't depend on the master.

## 5. Risks

| Risk | Mitigation |
|---|---|
| Merging per-section reagent masters joins two different materials that share a code | Only same code **and** same cleaned-up name are merged. Any code collision with a different name keeps both entries, and the second gets a new code. The run summary lists every collision. |
| Backfill links a lot to the wrong entry, or splits one material into near-duplicate entries (no review step, decision Q9) | Matching is strict (exact cleaned-up name), so mistakes show up as unmerged duplicates, not wrong links. The run summary lists near-duplicates for the owner lab to deactivate. Every link is audited. Snapshot names on lots are never changed, so the original record stays readable. |
| The COA-before-use rule for Supplement is lost when the enum logic moves to categories | Dedicated test in phase 2. The category field is seeded, not left to the user. |
| A user creates duplicate entries or categories later | Duplicate-name warning (cleaned-up name match) on create. Codes unique across the app. |
| Labs edit each other's entries | Edit rights follow the owner lab (Q3). Every lab can only read and use the others' entries. |
| `MaterialType` enum is stored as an int in history and audit labels (`auditEnumLabels.ts:34`) | Never remove or renumber enum values. Historical lots keep their stored type, and the category becomes the main field from now on (Q2). |

## 6. Decisions (answered 2026-10-28)

| # | Question | Decision |
|---|---|---|
| Q1 | Groups | **Standards split out.** Five groups: Reagents & Indicators, Reference & Primary Standards, Culture Media, Reference Organisms, Other Materials. |
| Q2 | Type on lots already received as one of the 7 types | **Keep the stored type and add the category.** No rewrite of GMP records. The category is the main field from now on. |
| Q3 | Ownership and visibility | **Each entry has an owner lab that edits it. Every lab can see and use every entry.** |
| Q4 | Who creates or changes categories | **A Section Head, with an e-signature and a reason.** |
| Q5 | Extra category rules | **All four:** quantity tracked, default storage condition, approval before use, label required (on top of default unit, COA required, expiry required, available sections). |
| Q6 | Reference organisms | **Culture collection + strain number, reference strains only.** `Organism` stays as the species list (test-step target organisms need it). |
| Q7 | Codes | **Enforced prefixes. Frozen once the entry has stock; changed only with a reason and an e-signature.** |
| Q8 | Stock ID | **`STK-{yyyy}-{seq:D5}`, one sequence for the whole app. Existing lots get one only if still in stock.** |
| Q9 | Backfill control | **No formal protocol or review step.** One audited run with a summary report. |
| Q10 | Free-text names at receipt | **Removed completely.** The material must exist in the master first. |

## 7. Implementation record (2026-10-28)

| Phase | Commits | Migration | Notes |
|---|---|---|---|
| 1 | `9854cb5` | `GlobalMaterialMasterCodes` | Cross-lab duplicate codes: same name → deactivated + lab suffix; different name → lab suffix. Audit row per change. |
| 2 | `7f30fc3`, `9494469` | `MaterialCategoriesAndOtherMaterials` | Categories with all rules; `MaterialCategories.Manage` and `Materials.Release` permissions (Section Head). Backfill builds entries from stock names, links lots, links unlinked reagent lots by code or unique name; summary audit row lists unmatched lots. |
| 3 | `ff142e0`, `31f73c2` | `ReferenceOrganisms` | `ReferenceOrganism` strain records over `Organism` species; challenge matching stays species-level. Media products gain `IsActive`. |
| 4 | `0060831`, `b6afd58` | `StepMediaDiluentMediaProduct` | Step media and diluent types point at `MediaProduct`; unresolved rows stay empty and are listed in the audit summary (they must be re-saved in Test Master before the step can start). Confirmatory readout uses the recorded selection. |
| 5 | `0671f24`, this commit | `MaterialStockIds` | `STK-{yyyy}-{seq:D5}` from one app-wide sequence; in-stock lots backfilled. Five-tab page; old menu entries replaced, old routes redirect. |

**Decisions taken during implementation (not in section 6):**
- Rejecting a lot at release requires a comment.
- Category prefixes: 2–5 letters; RG, IND, RS, PS, ORG, WS reserved. Backfill adds a digit when a prefix is taken (e.g. `IND2`).
- Category activate/deactivate needs the permission but no signature.
- A reference organism's species, collection and strain number are frozen once stocked, not only its code.
- The Excel workbook cannot create Other Materials entries.

**Verification:** backend 4,012 tests pass (all PostgreSQL integration tests included; the only failures are 4 OCR tests that need the Tesseract binary, also failing before this work). Every migration's backfill is covered by a PostgreSQL test. Frontend: 618 tests pass, type-check clean.

**Not yet built:**
- Batch-tracked diluent lot selection at Test Preparation (backend helpers exist; diluent became free text in 2026-09).
- A Diluent Types master screen (none exists in the frontend).
- Barcode label printing (see `barcode-labels-recon-report.md`; this plan removes its gap G1).
