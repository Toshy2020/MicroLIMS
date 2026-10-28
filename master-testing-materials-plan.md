# Master Testing Materials — Plan

**Status:** draft for Mohamed's review. No code has been written.
**Date:** 2026-10-28
**Related:** `barcode-labels-recon-report.md` (gap G1: Material Stock lots have no unique code, so they can't be labelled yet).

---

## 1. Goal

One app-wide master that defines **every material the labs receive into stock**, in four groups:

| Group | Built from today's | Examples |
|---|---|---|
| **Reagents & Standards** | `MaterialMasterEntry` (Reagent, Indicator, Reference standard, Primary standard) | Sodium hydroxide AR, Phenolphthalein, Ascorbic acid USP RS |
| **Culture Media** | `MediaProduct` (Media Configurations) | TSA, SDA, MacConkey broth |
| **Reference Organisms** | `Organism` | *E. coli* ATCC 8739, *S. aureus* ATCC 6538 |
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
| Active | (new) | Yes |

Seeding:
- The 6 named types become categories: Supplement, Antibiotic Disc, Identification Kit, Identification Reagent, Reference Buffer, Disposable Tool.
- Each **distinct `CustomType`** already used on `Other` lots becomes its own category, once QA confirms it during reconciliation.
- `Other` lots with no `CustomType` go to a **General** category.

### 3.2 One master, four groups

Recommended shape: **a common master record plus one detail record per group.** The groups share code, name, active flag, available sections and audit fields. Each group keeps its own fields:

- **Reagents & Standards:** the current fields (grade, source, base unit, indicator fields). `MaterialMasterEntry` becomes this group's detail. It stops being per section, and its code becomes unique across the app.
- **Culture Media:** `MediaProduct` stays as the detail, with its configurations, incubation conditions and the lot-number prefix rule (code ≤10). Nothing about prepared media changes.
- **Reference Organisms:** `Organism` stays as the detail. It gains a **code**, an **active flag** and **culture collection + strain number** (ATCC, NCTC, NCIMB, WDCM…), because pharmacopoeia often cite the WDCM equivalent rather than ATCC.
- **Other Materials:** the category from 3.1 plus default unit and supplier or grade.

Option rejected: **one wide table with every group's columns.** Every group would have to ignore columns that belong to the others, and the organism and media configuration links (`MediaConfiguration.OrganismId`, `MediaEvaluation.OrganismId`, `Cryovial.OrganismId`) would all have to move.

### 3.3 Codes

- **Unique across the app**, frozen once the entry has stock, and changed only with a reason and an e-signature. This is the existing `MediaProduct` pattern, applied to every group. Labels and cryovial codes are built from these codes.
- Suggested prefixes: `RG-` reagent, `IND-` indicator, `RS-` reference standard, `PS-` primary standard, `ORG-` reference organism, the category prefix for Other Materials (`SUP-`, `ABD-`, `IDK-`, `IDR-`, `BUF-`, `DSP-`). Culture media keep their existing short codes (TSA, SDA), because prepared lot numbers depend on them.
- **Conflict to resolve:** the same reagent code may already exist in two sections with different meanings. The merge in phase 1 must settle these.

### 3.4 Stock lot ID (what the label carries)

- Every received lot gets a system-generated, unique, never-edited **stock ID** in the format `STK-{yyyy}-{seq:D5}`, for example `STK-2026-00123`. Use one sequence for the whole app, protected by a unique index with one retry on a clash, the same approach as the other code generators.
- **Do not use `{MasterCode}/{seq}/{yy}`.** A received TSA powder lot would then look almost the same as a prepared TSA media lot (`TSA/03/26`), which invites mix-ups at the bench.
- The supplier batch number stays on the lot, for information and search.

### 3.5 Stock receipt after the change

1. Choose the **master entry first**: one searchable picker across all four groups, filtered to entries available to the user's section.
2. The type, name, code, default unit and COA rule come from the entry. **No free-text name for new lots.**
3. Enter the lot details: batch, quantity, expiry, location, plus purity and moisture for standards, as today.
4. The stock ID is generated on save.

The stored `MaterialName` and `Code` on each lot stay as a **snapshot** taken at receipt, as they are now. Renaming a master entry later never rewrites historical lots.

### 3.6 Migration of existing stock ("create the names from the stock")

This is a **data migration on GMP records**, so it is reviewed before it is committed, never applied automatically.

1. **Propose:**
   - Group existing lots by type, custom type, cleaned-up name (trimmed, spaces collapsed, case ignored) and code. Each group becomes one proposed master entry with a proposed code.
   - Match unlinked reagent and standard lots to existing `MaterialMasterEntry` rows by name and code.
   - Match lyophilized-organism lots with no `OrganismId` by ATCC number.
2. **Review:** export the proposals through the master-data workbook. QA merges near-duplicates and fixes spellings, for example "Egg yolk tellurite" vs "Egg-Yolk Tellurite Emulsion".
3. **Commit:**
   - Import the approved workbook. This creates the entries and links each lot to its entry. The link is audited through the normal `SaveChanges` capture, with a reason like "Master Testing Materials backfill, approved workbook vX".
   - Generate stock IDs for **lots still in stock**. Expired and depleted lots don't need one unless Mohamed wants it.
4. **Verify:** before/after counts per type. Every in-stock lot is linked. The snapshot names on historical lots are unchanged.

### 3.7 Repoint Test Master to the product

- `TestWorkflowStepMedia.MaterialId` (a stock lot) is replaced by the **Culture Media entry**. `StepMediumMatcher` already prefers the product, so this mostly removes the legacy fallback to the exact batch.
- `DiluentType.MaterialId` is replaced by the Culture Media entry.
- `ConfirmatoryMediaSelection.MaterialId` stays as it is. It records what an analyst actually used, so it should keep pointing at the real lot.

### 3.8 One page

A single **Master Testing Materials** page, in a shared configuration menu so both labs see it. It has four tabs (Reagents & Standards | Culture Media | Reference Organisms | Other Materials), and the Other tab has a *Manage categories* panel. Each tab reuses the screens that exist today, so this mostly regroups pages. The old routes redirect to the matching tab (`routes.ts` already has redirect helpers for these targets, `:118-199`).

### 3.9 Permissions and audit

- Creating and editing entries and categories: `MasterData.Manage`, with the configuration section scope. Being global, editing needs `Sections.ConfigureAll`, or an owner section per entry (decision Q3).
- Code change on any group: a reason plus an e-signature (`SignatureMeaning.MasterDataChanged`), the same as media today.
- Deactivate, never delete, once an entry has stock or is used in a recipe or test.
- Every change is captured automatically in `AuditLog`.

## 4. Phased plan (each phase ends with a sign-off gate)

| Phase | Scope | Gate |
|---|---|---|
| **0. Decisions** | Answer the open questions in section 6. | Signed decision record |
| **1. Common master and Reagents & Standards go global** | Common master record. Make `MaterialMasterEntry` app-wide with available sections. Merge per-section duplicates through a reconciliation workbook. Code-change signature. | QA approves the merge list. Recipes, solution preparations and working standard qualifications still resolve correctly (regression tests). |
| **2. Other Materials** | Categories (seeded from the 7 types and the distinct `CustomType` values) with behaviour fields. COA rule moves from code to the category, with a test proving Supplement still blocks use without a COA. Stock receipt requires a master entry for every type. Backfill from stock (section 3.6). | Backfill workbook approved. Every in-stock lot linked. COA rule verified. |
| **3. Culture Media and Reference Organisms join** | Link `MediaProduct` and `Organism` to the common master. Organism gains a code, active flag and culture collection/strain number. Lyophilized lots must have an organism at receipt. Backfill missing `OrganismId` by ATCC. | No change to prepared media lot numbers. Challenge-organism matching tests pass. |
| **4. Test Master repoint** | Step media and diluent types point at Culture Media entries. Legacy batch fallback removed. | Every configured test step resolves to a product. Pathogen and count workflow regression tests pass. |
| **5. Single page and stock ID** | Master Testing Materials page. Stock ID generation plus backfill for in-stock lots. Stock receipt picker. | Users walk through it. **This unblocks Material lot labels in the barcode plan (phase 4 there).** |

Phases 1 and 2 can run in parallel after phase 0. Phase 5's stock ID can be pulled forward if labels are urgent, because it doesn't depend on the master.

## 5. Risks

| Risk | Mitigation |
|---|---|
| Merging per-section reagent masters joins two different materials that share a code | Human reconciliation, never automatic. Conflicting entries get new codes. |
| Backfill links a lot to the wrong entry | Workbook review and sign-off. Snapshot names on lots are never changed, so the original record stays readable. |
| The COA-before-use rule for Supplement is lost when the enum logic moves to categories | Dedicated test in phase 2. The category field is seeded, not left to the user. |
| A user creates duplicate entries or categories later | Duplicate-name warning (cleaned-up name match) on create. Codes unique across the app. |
| Users with an old (per-section) view lose access to entries | "Available in sections" defaults to the entry's current section. Read access for all sections is a deliberate choice (Q3). |
| `MaterialType` enum is stored as an int in history and audit labels (`auditEnumLabels.ts:34`) | Never remove or renumber enum values. Historical lots keep their stored type, and the category becomes the main field from now on (Q2). |

## 6. Open questions for Mohamed

1. **Groups:** confirm the four groups. Are Indicator and Primary Standard staying inside *Reagents & Standards* as sub-categories? (Recommended: yes.)
2. **Historical type values:** for lots already received as Supplement, Antibiotic Disc and so on, keep the stored type and add the category (recommended, no rewrite of GMP records), or rewrite them to "Other + category"?
3. **Ownership:** who maintains the global master: a central role with `Sections.ConfigureAll`, or each lab for the entries it owns? Can every lab *see* every entry?
4. **Category creation:** may any `MasterData.Manage` holder add a new category, or only QA / a section head with an e-signature?
5. **Category rules:** besides COA required, default unit, expiry required and available sections, do categories need more rules? For example *quantity tracked* (no for disposable tools?), *storage condition*, *requires approval before use*.
6. **Reference organisms:** add culture collection + strain number (ATCC, NCTC, NCIMB, WDCM) next to the ATCC field? Should identified isolates from testing ever live in the same list, or only reference strains?
7. **Codes:** approve the prefixes in 3.3, and freezing codes (with an e-signature to change) once an entry has stock.
8. **Stock ID format:** approve `STK-{yyyy}-{seq:D5}` as one sequence for the whole app, and say whether expired or depleted historical lots also get one.
9. **Backfill:** who in QA reviews the reconciliation workbook, and does it need a formal data-migration protocol (pre-approved, executed, reported)?
10. **Free text:** after go-live, is free-text naming at receipt removed completely, or kept behind a permission for emergencies? (Recommended: removed. The user creates the entry first.)
