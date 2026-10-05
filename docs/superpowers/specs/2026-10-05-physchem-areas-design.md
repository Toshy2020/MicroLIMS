# Physicochemical areas (FP / RM & PM), primary standards, titration hardening - design

Date: 2026-10-05. Branch `feat/physchem-areas` (from main 45011b1, after PR #79).
Recon: `E:\MicroLIMS\rls-tmp\titration-recon-2026-10-05.md`. User decisions 2026-10-05 below.

## 0. Decisions

| # | Decision |
|---|---|
| D1 | Titration settings stay on the Test Master (no Titration Method master). |
| D2 | Two-endpoint titration deferred. When built, output volume = free integer formula c1*V1 + c2*V2. |
| D3 | Relative mode, titrant Due: warning that the analyst passes with an acknowledgement, a justification and their own e-signature. |
| D4 | New Primary Standard type in Reagents & Standards. Physicochemical stock offers no micro types. |
| D5 | Editing titration settings on a test requires a reason for change. |
| D6 | Indicator solutions stay method information (no lot / expiry tracking). |
| D7 | Seed titration test data and run a browser E2E. |
| D8 | One Physicochemical lab (section `FP`) with two **areas**: FP and RM & PM. Stock, solutions, titrants and equipment are shared. |
| D9 | A test belongs to FP, RM & PM or Both. Separate Test Master pages per area make it clear where a test is created. |
| D10 | Workspaces: the current one is renamed "FP Workspace"; a new "RM & PM Workspace" is added. |
| D11 | Area access is per user: FP, RM & PM or Both. A user with Both sees both workspaces. |
| D12 | A Both test counts on each workspace's workload separately, per sample. |

## 1. Slice S1 - Primary Standard type and stock cleanup

### Domain
- `MaterialMasterCategory` += `PrimaryStandard` (append).
- `MaterialType` += `PrimaryStandard` (append; stored as int).
- `MaterialService`:
  - default unit Gram
  - master link required (like Chemical / Indicator / ReferenceStandard), category mapping `PrimaryStandard -> PrimaryStandard`
  - **Purity is required for PrimaryStandard** lots (0 < p <= 100) as well as ReferenceStandard; moisture stays ReferenceStandard-only.
- **Physicochemical lots** (section code `FP`): `MaterialType` must be one of Chemical, Indicator, ReferenceStandard, PrimaryStandard. WorkingStandard is created only by its approval flow (unchanged). The rule is enforced on create and update with a clear message. Microbiology lots are unchanged.

### Titrant rules
- Solution master (Titrant, `StandardizationMode.PrimaryStandard`): `StandardEntryId` must be a `PrimaryStandard` entry (create and update).
- Standardization (`TitrantStandardizationService`):
  - the lot must be `MaterialType.PrimaryStandard` with purity set; otherwise "Lot X has no purity - enter it in Materials Stock before standardizing"
  - `TitrationEngine.PrimaryStandardFactor` gets a non-null purity (the `?? 100` default is removed)

### Migration `PrimaryStandardType`
- Entries referenced by any Titrant solution master's `StandardEntryId` become category `PrimaryStandard`. Today that is RG-022..RG-027 in prod and LIMSV2.
- Their lots that are `Chemical` become `PrimaryStandard` (7 lots in prod, purity null).
- Lots without purity cannot be used for standardization until purity is entered. Existing standardizations and their factors are untouched (history).
- Pre-check (Neon, read-only): an entry used both as a titrant primary standard and as an ordinary reagent component in a solution recipe. List any such rows; the migration keeps them usable as components (component pickers accept PrimaryStandard entries).
- Down: revert category and type.

### Frontend
- Reagents & Standards page (`MaterialMasterPage.tsx`): a "Primary Standard" category.
- Physicochemical Materials Stock (`AddMaterialDialog.tsx`, `MaterialFilterBar.tsx`, `materialTypes.ts`): the type list is limited to the 4 physicochemical types when the stock page is the physicochemical one. Purity is shown and required for Primary Standard.
- Solution master titrant section: the primary-standard picker lists PrimaryStandard entries only.

### Tests
- Purity required for PrimaryStandard.
- Micro type rejected for a physicochemical lot.
- Titrant master refuses a non-PrimaryStandard entry.
- Standardization refuses a lot without purity.
- Migration Up/Down against Postgres.

## 2. Slice S2 - Titration hardening

### Due-titrant acknowledgement (D3)
- Applies only where the context marks a preparation `usable = true` with a `warning` (Relative + Due, Relative + BeforeEachUse not today).
- Request `RecordTitrationResultRequest` += `dueTitrantAcknowledged: bool`, `dueTitrantJustification: string?`.
- Server: if the chosen titrant (or excess titrant) carries a warning, both fields are required (justification 10-500 chars). Otherwise both must be absent.
- On submit, after the password check, a **second `ElectronicSignature`** is created in the same transaction. Same user, same password entry, meaning "Use of due titrant acknowledged", linked to the test order. Its id and the justification go into the snapshot (`dueTitrantAcknowledgement { signatureId, justification, titrantCode, factorState }`).
- Reviewer sees it in `TestResultCards` / `SampleSummaryDialog` (titration block) as a warning line.
- Frontend `TitrationPanel`: when the selected option has a warning, show the warning, a required checkbox and a justification field. The submit button stays disabled until both are filled. The signature dialog text names both meanings.

### Reason for change on titration settings (D5)
- `UpdateTestDefinitionRequest` += `changeReason: string?`.
- When any `Titration*` field (or the area, S3) changes on a Titration test, `changeReason` is required. The service records `RecordUserEventAsync("TestDefinition.TitrationChanged", before/after JSON of the titration fields + reason)`, the same pattern as HPLC method edits.
- Test Master: an edit reason field appears in the save step when titration fields changed.

### Snapshot
- `engineVersion: "titration-1"` constant in `TitrationRecorder` snapshot.

### Named tests
- Citric acid direct = 99.57 %.
- Aspirin residual with blank (0.5 N back titrant, F 90.08) = 99.86 %.
- Failed standardization (only record failed) blocks.
- Restandardize after recording leaves `ReportedValue`, readings and snapshot unchanged.
- Due override: missing acknowledgement refused; with acknowledgement two signatures persist.
- Reason required on titration edit; not required for non-titration edits.

## 3. Slice S3 - Physicochemical areas

### Domain
- `enum PhyschemArea { FinishedProduct = 0, RawPackaging = 1, Both = 2 }` (new file).
- `TestDefinition.PhyschemArea PhyschemArea?`. Required when the test's section is the physicochemical lab (code `FP`); must be null otherwise.
- `UserOrgMembership.PhyschemArea PhyschemArea?`. Meaningful only on a membership whose `SectionId` is the physicochemical section; null = Both (keeps every existing user's access). Department-level memberships and System Administrators = Both.

### Area of a sample
- `SampleCategory.RawMaterial` and `PackagingMaterial` -> RM & PM area.
- Every other category -> FP area. Prod has only FP samples in this lab.
- One helper `PhyschemAreas.OfCategory(SampleCategory)` plus `PhyschemAreas.Includes(PhyschemArea? testArea, area)`.

### Rules (server)
- **Specifications:** a spec row for an item may only use a physicochemical test whose area includes the item's area (FP items: FP or Both; RM/PM items: RmPm or Both). Enforced in `SpecificationMasterDataService` / `SpecificationService` create + update.
  - Changing a test's area so that existing specs no longer fit is refused, with the list of item codes.
- **Workspace:** `TestingWorkspaceController` endpoints that take `labSectionId` also take `area` (`fp` | `rmpm`), required when the lab is physicochemical.
  - `TestingWorkspaceService` narrows samples to the area's categories and refuses (403) when the user's area access does not include it.
  - Workload counts are per area, so a Both test counts in the workspace of its sample (D12).
- **In-workspace receiving:** the category list is limited to the area's categories (main Receiving unchanged).
- **Area access:** `IUserSectionScopeService` gains `GetPhyschemAreasAsync(userId)` -> set of areas. `GET /org/my-sections` returns the areas for the physicochemical section so the menu can show the right workspaces.

### Migration `PhyschemAreas`
- Add both columns.
- Existing tests in section `FP`: `FinishedProduct`, except tests with specs only on RM/PM items, which get `RawPackaging`. Locally that is `TIT-E2E-ACID`; prod has none.
- Tests with specs on both FP and RM/PM items get `Both`. Prod has none; checked read-only 2026-10-05.
- Memberships stay null (= Both).

### Frontend
- Menu (`menuConfig.ts`), Physicochemical Laboratory: "FP Workspace" `/physicochemical/workspace` (path kept) and "RM & PM Workspace" `/physicochemical/rm-pm-workspace`, each shown only if the user has that area.
- Physicochemical Configuration: "FP Test Master" (existing path) and "RM & PM Test Master" `/laboratory-configuration/rm-pm-test-master`.
- `LabWorkspaceRoute` gets an `area` prop, passed down to `ReceivingTestingWorkspacePage` and its service calls. The page title shows the workspace name.
- `TestMasterPage` gets `area` (`fp` | `rmpm`):
  - the list shows tests whose area includes the page's area
  - new tests are created with the page's area
  - a checkbox "Also used for RM & PM" (or "...for FP") sets Both
  - a Both test edited from either page keeps Both unless unticked
  - **TestMasterPage is 2762 lines:** the area control and filtering go in a new small component/hook, and the page grows only by the prop wiring.
- Users > sections dialog (`UserSectionsDialog.tsx`): for a physicochemical-section membership, an area select (FP / RM & PM / Both).
- Specification dialog: the test picker offers only tests whose area fits the item's category (server is authoritative).

### Tests
- Area helper.
- Spec rule (FP item + RmPm test refused, Both accepted).
- Area change refused when specs conflict.
- Workspace: area filter and 403 without access.
- Workload counts per area.
- In-workspace receiving category limit.
- Membership area round trip.
- Migration backfill.
- Frontend vitest for the area filter/checkbox and the menu by area.

## 4. Slice S4 - Seed and browser E2E

Seed SQL (idempotent, `docs/superpowers/plans/2026-10-05-titration-rmpm-test-data.sql`), LIMSV2 only:
- Primary standard KHP lot with purity.
- 1 N NaOH VS, 0.5 N H2SO4 VS, 0.1 N iodine VS (masters exist for 0.1 N; add 1 N NaOH and 0.5 N H2SO4).
- RM items: Citric Acid, Aspirin, Ascorbic Acid with Range specs.
- Tests in RM & PM:
  - `TIT-CITRIC` (AcidBase, Direct, UspFactor, F 64.03, phenolphthalein)
  - `TIT-ASPIRIN` (Residual + blank, excess 0.5 N NaOH 50 mL, back 0.5 N H2SO4, F 90.08)
  - `TIT-ASCORBIC` (Redox, Direct, F 88.06, starch)
- One Relative test (for the Due override).

E2E (browser, users e2e.head / e2e.analyst / e2e.reviewer):
- Standardize titrants (Primary Standard lot with purity).
- RM & PM workspace: receive RM samples, record citric, aspirin and ascorbic results, matching hand calculations.
- Due override in relative mode (acknowledgement + signature visible to the reviewer).
- Review + approval.
- An FP-only user cannot open the RM & PM workspace.
- FP workspace still shows FP samples.
- The Test Master pages show the right tests, and a Both test appears on both.

## 5. Out of scope
Two-endpoint titration (D2); a separate RM & PM section/lab; area segregation of stock, solutions or equipment; area-scoped dashboards and reports (they stay lab-level).

## 6. Order and gates
S1 -> S2 -> S3 -> S4, stopping after each slice. Backend per slice: Postgres suite green, migration applied to LIMSV2 after a backup, Down verified. Frontend: tsc / eslint / vitest clean. Before merge: Neon pre-merge checks (S1 primary-standard component usage, S3 backfill counts); prod migrates at startup.

## 7. Amendments after code reading (2026-10-05, before planning)

A1. The backend already limits physicochemical lot types (`MaterialTypeRules.Physicochemical` =
    Chemical, ReferenceStandard, Indicator, ReferenceBuffer, DisposableTool, Other). Those general
    types stay; only `PrimaryStandard` is added. The micro names the user saw come from the
    frontend: `MaterialFilterBar.MATERIAL_TYPE_OPTIONS` is a fixed micro list, and
    `AddMaterialDialog` defaults to `DehydratedMedia`. S1 makes both follow `GET /materials/type-options`.
    This replaces the "4 types only" wording in section 1.
A2. Physicochemical in-workspace receiving already offers only product / rm / pm
    (`ALLOWED_CATEGORIES_BY_LAB.FP`). The FP area therefore receives `product`, and RM & PM receives
    `rm` and `pm`. This is a frontend-only list, as today; main Receiving is unchanged.
A3. Area access is enforced on the workspace list, count and single-sample endpoints
    (`/testorders/page`, `/testorders/counts`, `/testorders/{id}`). Result-entry endpoints stay
    lab-scoped (section membership), as today.
A4. The due-titrant acknowledgement signature is linked to the TestOrder (EntityType "TestOrder",
    meaning `TitrantDueAcknowledged`); the snapshot stores the justification and time, not the
    signature id (unknown before SaveChanges).
A5. Migration `PrimaryStandardType` is data-only (both enums are stored as int, so there is no
    schema change).
