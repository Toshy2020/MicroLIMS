# RM Receiving (Plan 1 of 6) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Raw-material samples are received against a grade (BP/USP/In-house) chosen from the supplier CoA, carry the supplier CoA as an attachment, warn below the STM minimum quantity, and — for periodic retests — wait for a Section Head or Supervisor to sign which tests run.

**Architecture:** Grade is a nullable string on `Specification` and `Sample`; `SpecificationLookup` gains a grade dimension mirroring its existing stage dimension, and receiving skips tests whose spec rows all belong to another grade. Periodic retest is a `CauseOfTesting` flag; such samples are created with no test orders and an `AwaitingTestSelection` flag until a signed selection creates them. Supplier CoA reuses the `MaterialDocument` upload pattern as a new `SampleDocument`.

**Tech Stack:** ASP.NET Core 10, EF Core + PostgreSQL (Npgsql), xUnit, React + TypeScript + MUI.

**Spec:** `docs/superpowers/specs/2026-10-25-raw-material-stm-support-design.md` (goals 1, 2, "Minimum quantities" call).

### Roadmap (later plans, written when this one is merged locally)

| Plan | Covers (spec goal) |
|---|---|
| 2 RM result entry | per-container description (3), dried/anhydrous basis from sample LOD/KF (4), retention quantity at review |
| 3 HPLC impurities | related-substances mode with RRF (5), internal-standard assay (6) |
| 4 UV workflow | UV assay vs standard + specific absorbance (7) |
| 5 Supporting records | QC Manager role, not-applicable tests, outsourced tests, supplier-stated attributes (8) |
| 6 Material setup | AAS as ICP methods (9), 14 materials' specs/tests/methods on local, E2E per material, Neon (10) |

## Global Constraints

- Clean Architecture: rules in `MicroLIMS.Application`; frontend only displays and collects.
- Every new sensitive action writes an audit entry via `IAuditEventService.RecordUserEventAsync`; test selection is signed via `IElectronicSignatureService.SignAsync` and saved in the same `SaveChangesAsync`.
- "Section Head or Supervisor" = `RoleType.SectionHead` or `RoleType.Reviewer` (the role named "Supervisor"), scoped to the sample's sections via `IUserSectionScopeService`.
- Grade values are free text trimmed, max 20 chars, compared case-insensitively; UI suggests `BP`, `USP`, `In-house`.
- Existing finished-product / water / EM / ungraded samples behave exactly as today.
- Builds/tests: `dotnet build backend/MicroLIMS.sln --artifacts-path E:/MicroLIMS/rls-tmp/rm-build`; `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/rm-test` with `MICROLIMS_TEST_POSTGRES` set; migrations `dotnet ef migrations add <Name> --configuration Release`.
- Commit each tested task locally on `feat/rm-stm-support`; never push.

## Review Focus

1. **Item with graded and ungraded rows for the same test** (micro rows usually ungraded, physchem graded): a BP sample must get the BP rows for physchem tests and the ungraded rows for micro tests — never an empty spec. Test in Task 2.
2. **Grade typed with different case/spaces** ("bp ", "BP"): treated as the same grade at spec save and at receiving. Test in Task 1.
3. **Periodic-retest sample with zero test orders** must not break the sample register, tracking board, KPI tiles or status rollup (status stays `Received`, shown as "Awaiting test selection"). Test in Task 4.
4. **Second selection attempt / selection by an Analyst / selection of a test not eligible for the sample's grade or labs**: rejected with a clear message, nothing written. Test in Task 4.
5. **Non-numeric sample quantity** ("2 bottles") with a minimum set: no crash, no warning, receipt succeeds. Test in Task 3.

---

### Task 1: Grade on specifications

**Files:**
- Modify: `backend/MicroLIMS.Domain/Entities/Specification.cs` (add `public string? Grade { get; set; }`)
- Modify: `backend/MicroLIMS.Persistence/Configurations/SpecificationConfiguration.cs:51-53` (unique index becomes `ItemId, TestCode, ParameterName, ProductionStageRole, Grade`, `AreNullsDistinct(false)`; `Grade` max length 20)
- Create: migration `AddSpecificationAndSampleGrade` (also adds `Samples.Grade varchar(20) null` for Task 2)
- Modify: `backend/MicroLIMS.Domain/Entities/Sample.cs` (add `public string? Grade { get; set; }`)
- Modify: `backend/MicroLIMS.Application/DTOs/MasterDataRequests.cs` (`Grade` on Create/UpdateSpecificationRequest), spec response DTO, `SpecificationMasterDataService` create/update mapping
- Modify: `backend/MicroLIMS.Application/Services/SpecificationService.cs` (`ValidateAsync`: normalise grade, duplicate check at :235 includes grade)
- Modify: `backend/MicroLIMS.Application/Services/MasterDataWorkbook/Sheets/SpecificationSheets.cs` (column `Grade` after `ProductionStageRole`, in key of both sheets)
- Modify: `frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx` (Grade autocomplete, free solo, options BP/USP/In-house), `ItemSpecificationsSection.tsx` (group/label rows by grade), `frontend/src/modules/laboratoryConfiguration/specifications/services/SpecificationService.ts` types
- Test: `backend/MicroLIMS.Tests/UnitTests/SpecificationGradeTests.cs`

**Interfaces:**
- Produces: `public static string? SpecificationService.NormalizeGrade(string? grade)` — trims, collapses inner whitespace, returns null for blank, upper-cases `BP`/`USP` and keeps other text as typed; throws `InvalidOperationException("Grade must be 20 characters or fewer.")`.
- Produces: `Specification.Grade`, `Sample.Grade` columns.

- [ ] **Step 1: Write failing tests** — `NormalizeGrade(" bp ") == "BP"`, `NormalizeGrade("usp") == "USP"`, `NormalizeGrade("In-house") == "In-house"`, `NormalizeGrade("  ") == null`; `ValidateAsync` rejects a second row with same item/test/parameter/stage and grade `"bp"` when `"BP"` exists ("A specification for this parameter and grade already exists."), accepts the same parameter with grade `USP`.
- [ ] **Step 2: Run** `dotnet test ... --filter SpecificationGradeTests` — expect FAIL (no member `NormalizeGrade`).
- [ ] **Step 3: Implement** entity, configuration, normalisation in create/update paths, duplicate check, DTO + workbook column, migration.
- [ ] **Step 4: Run** the filter, then the full suite — expect PASS, and `dotnet ef migrations script` shows only the two columns + index change.
- [ ] **Step 5: Frontend** grade field + grouping; `npm run build` and `npx eslint` on changed files clean.
- [ ] **Step 6: Commit** `feat(spec): grade per specification row`.

### Task 2: Grade at receiving and in spec lookup

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/SpecificationLookup.cs` (grade dimension in `ApplicableStageAsync`/`SelectForStage`/`ForSampleAsync`/`PrimaryAsync`)
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TestWorkflowSupport.cs:625`, `backend/MicroLIMS.Application/Services/SampleSummaryService.cs:230` (route through `SpecificationLookup` or apply the same grade filter)
- Modify: `backend/MicroLIMS.Application/Workflows/ProductWorkflowEngine.cs:9,44-135` (`ItemBasedReceiveRequest` gains `string? Grade`; validation; test filter)
- Modify: `backend/MicroLIMS.API/Controllers/SampleController.cs:16-21` (`ReceiveItemBasedSampleRequest.Grade`), `ReceivingService`
- Create: `GET api/items/{id}/grades` in the items controller → `IReadOnlyList<string>` (distinct normalised `Specification.Grade` of the item, ordered)
- Modify: `backend/MicroLIMS.Application/DTOs/SampleSummaryDto.cs` (+`Grade`), `frontend/src/modules/testingWorkspace/SampleCoaPage.tsx` (header row "Grade" when set, all three header copies ~479-521, ~562-583, ~710-736)
- Modify: `frontend/src/modules/receiving/dialogs/NewSampleDialog.tsx`, `ReceiveService.ts`, `receivingTypes.ts:124-126` (Grade select shown when the item has grades; required then; helper text "Choose the grade stated on the supplier's CoA")
- Modify: `backend/MicroLIMS.Application/Services/SampleCorrectionService.cs` (grade correctable only while every test order is `Pending`; re-derives the order set; signed like the other corrections)
- Test: `backend/MicroLIMS.Tests/WorkflowTests/GradedReceiptTests.cs`, `backend/MicroLIMS.Tests/IntegrationTests/GradedSpecLookupPostgresTests.cs`

**Interfaces:**
- Consumes: `SpecificationService.NormalizeGrade` (Task 1).
- Produces: `public static async Task<string?> SpecificationLookup.SampleGradeAsync(IMicroLimsDbContext db, int sampleId, CancellationToken ct = default)`; lookup rule: for a test, rows with `Grade == sampleGrade` when any exist, else rows with `Grade == null`; stage rule applied within the chosen grade set.
- Produces: `public static bool ProductWorkflowEngine.TestAppliesToGrade(IEnumerable<Specification> rowsForTest, string? grade)` — true when the test has no rows, has an ungraded row, or has a row of that grade.

- [ ] **Step 1: Failing tests** — item with tests ASSAY (rows BP + USP), CHLORIDE (USP only), TAMC (ungraded): receiving with `Grade="BP"` creates orders ASSAY + TAMC only; `Grade="usp"` creates ASSAY + CHLORIDE + TAMC and stores `"USP"`; graded item with `Grade=null` throws "Choose the grade stated on the supplier's CoA."; `Grade="EP"` throws "Grade EP has no specification for this item."; ungraded item with `Grade=null` unchanged from today. Postgres test: `PrimaryAsync` for BP sample returns the BP ASSAY row, for TAMC the ungraded row.
- [ ] **Step 2: Run** filters — FAIL.
- [ ] **Step 3: Implement** lookup, engine, controller, grades endpoint, summary DTO, correction rule.
- [ ] **Step 4: Run** filters then full suite — PASS.
- [ ] **Step 5: Frontend** receive grade select + CoA header; build + eslint clean.
- [ ] **Step 6: Commit** `feat(receiving): receive raw materials against a grade`.

### Task 3: Minimum sample quantity warning

**Files:**
- Modify: `backend/MicroLIMS.Domain/Entities/Item.cs` (+`decimal? MinSampleQuantity`, `decimal? MinRetestSampleQuantity`, `string? MinSampleQuantityUnit`), migration `AddItemMinimumSampleQuantity`
- Modify: `ItemService` create/update + item DTOs, `MasterDataWorkbook/Sheets/InventorySheets.cs` Items sheet (columns `MinSampleQuantity`, `MinRetestSampleQuantity`, `MinSampleQuantityUnit`)
- Modify: `ProductWorkflowEngine.ReceiveAsync` result to carry `IReadOnlyList<string> Warnings`; controller returns them
- Modify: item edit screen (`frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx`) and `NewSampleDialog.tsx` (show warnings in a snackbar after save)
- Test: `backend/MicroLIMS.Tests/WorkflowTests/MinimumSampleQuantityTests.cs`

**Interfaces:**
- Produces: `public static decimal? ProductWorkflowEngine.LeadingQuantity(string? sampleQuantity)` — the leading decimal number (dot or comma decimal), null when none.

- [ ] **Step 1: Failing tests** — min 100 g, quantity "80 g" → warning "Sample quantity 80 g is below the STM minimum of 100 g."; "120 g" → none; "2 bottles" → none, receipt succeeds; periodic-retest cause uses `MinRetestSampleQuantity` (50 g: "60 g" → none); `LeadingQuantity("1,5 kg") == 1.5m`.
- [ ] **Step 2: Run** — FAIL. **Step 3: Implement.** **Step 4: Run** filter + suite — PASS. **Step 5:** frontend, build + eslint. **Step 6: Commit** `feat(receiving): warn below STM minimum sample quantity`.

### Task 4: Periodic retest test selection

**Files:**
- Modify: `backend/MicroLIMS.Domain/Entities/CauseOfTesting.cs` (+`bool RequiresTestSelection`), `Sample.cs` (+`bool AwaitingTestSelection`, `int? TestSelectionSignatureId`, `ElectronicSignature? TestSelectionSignature`)
- Create: migration `AddPeriodicRetestSelection` — columns + data insert of CauseOfTesting `"Periodic retest"` with `RequiresTestSelection = true` only when no row of that name exists (runs on Neon at startup)
- Modify: `backend/MicroLIMS.Persistence/Seed/DbSeeder.cs:38-45` (seed the same row for fresh databases)
- Modify: `backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs` (+`TestsSelected` at the end)
- Modify: `ProductWorkflowEngine.ReceiveAsync` (cause flagged → no TestOrders, `AwaitingTestSelection = true`)
- Create: `backend/MicroLIMS.Application/Services/SampleTestSelectionService.cs`
- Modify: `SampleController.cs` (`GET api/samples/{id}/test-selection`, `POST api/samples/{id}/test-selection`), DI registration
- Modify: sample register / tracking DTOs and `frontend/src/modules/receiving/components/SampleRegisterTable.tsx`, `TestStatusSummaryCell.tsx`, `SampleActionMenu.tsx`; create `frontend/src/modules/receiving/dialogs/SelectRetestTestsDialog.tsx` (checkbox list of eligible tests, password field, Sign button; read-only list for other roles)
- Test: `backend/MicroLIMS.Tests/IntegrationTests/SampleTestSelectionPostgresTests.cs`

**Interfaces:**
- Consumes: `ProductWorkflowEngine.TestAppliesToGrade` (Task 2), `TestSectionLookup.ResolveAsync`, `IElectronicSignatureService.SignAsync`, `IAuditEventService.RecordUserEventAsync`.
- Produces:
  - `public record EligibleTestDto(string TestCode, string DisplayName, int SectionId, string SectionName);`
  - `public record SelectTestsRequest(IReadOnlyList<string> TestCodes, string Password, string? Comment);`
  - `Task<IReadOnlyList<EligibleTestDto>> SampleTestSelectionService.GetEligibleAsync(int sampleId, CancellationToken ct)` — assigned tests passing the grade filter and the sample's target labs.
  - `Task SampleTestSelectionService.SelectAsync(int sampleId, SelectTestsRequest r, int userId, string? ip, CancellationToken ct)` — creates `TestOrder`s exactly as receiving does (Pending/Waiting), clears `AwaitingTestSelection`, sets `TestSelectionSignature` (meaning `TestsSelected`, entity type `"Sample"`), audit action code `SAMPLE_TESTS_SELECTED` with the chosen codes, then runs `PreparationRules.InitialStatusAsync`.

- [ ] **Step 1: Failing Postgres tests** — receive with "Periodic retest" → 0 orders, `AwaitingTestSelection` true, register query returns it with status Received; Section Head selects [LOD, ASSAY] with correct password → 2 orders, flag cleared, signature + audit row exist; Supervisor (RoleType.Reviewer) allowed; Analyst → "Only a Section Head or Supervisor can choose the tests for a periodic retest."; head outside the sample's sections rejected; wrong password → `SignatureVerificationException`, no orders; empty list → "Choose at least one test."; code not eligible → "Test X is not available for this sample."; second call → "Tests for this sample have already been selected."
- [ ] **Step 2: Run** — FAIL. **Step 3: Implement** service, endpoints, engine change, migration, seed. **Step 4: Run** filter + full suite — PASS; apply migration to local LIMSV2 after a `pg_dump` backup to `E:/MicroLIMS/backups`.
- [ ] **Step 5: Frontend** "Awaiting test selection" chip, dialog, menu action visible to heads/supervisors; build + eslint.
- [ ] **Step 6: Commit** `feat(receiving): periodic retest tests chosen by section head or supervisor`.

### Task 5: Supplier CoA attachment

**Files:**
- Create: `backend/MicroLIMS.Domain/Entities/SampleDocument.cs` (mirror `MaterialDocument.cs`: SampleId, DocumentType, OriginalFileName, StorageKey, FileExtension, ContentType, ContentSha256, Status, UploadedBy/At, Voided fields), `Enums/SampleDocumentType.cs` (`SupplierCoa = 0, Other = 1`), configuration, migration `AddSampleDocuments`
- Create: `backend/MicroLIMS.Application/Services/SampleDocumentService.cs` (mirror `MaterialDocumentService.cs:131-193`; storage key `sample-documents/{sampleId}/{id}{ext}`; audit codes `SAMPLE_DOCUMENT_UPLOADED`, `SAMPLE_DOCUMENT_VOIDED`)
- Create: `backend/MicroLIMS.API/Controllers/SampleDocumentController.cs` (`GET/POST api/samples/{sampleId}/documents`, `GET api/sample-documents/{id}/content`, `POST api/sample-documents/{id}/void`; 30 MB limit; policy `SamplesReceiveOwnLab` for upload/void, sample read access for list/content)
- Modify: `NewSampleDialog.tsx` (optional "Supplier CoA" file picker, uploaded after the sample is created), sample detail/action menu "Documents" list
- Test: `backend/MicroLIMS.Tests/IntegrationTests/SampleDocumentPostgresTests.cs`

**Interfaces:**
- Produces: `Task<SampleDocumentDto> SampleDocumentService.UploadAsync(int sampleId, string fileName, string contentType, byte[] content, SampleDocumentType type, int userId, CancellationToken ct)`, `VoidAsync(int id, string reason, int userId, ...)`, `ListAsync(int sampleId, ...)`, `ReadContentAsync(int id, int userId, ...)`.

- [ ] **Step 1: Failing tests** — upload PDF stores row + file (fake `IFileStorageService`) + audit; disallowed extension rejected by the existing file validator; storage failure rolls back the row; void keeps the file and marks Voided with reason required; user outside the sample's sections cannot list.
- [ ] **Step 2: Run** — FAIL. **Step 3: Implement.** **Step 4: Run** filter + suite — PASS. **Step 5:** frontend, build + eslint. **Step 6: Commit** `feat(receiving): attach supplier CoA to a sample`.

### Task 6: Phase check

- [ ] Opus reviews the whole Plan 1 diff against spec goals 1–2 and the Review Focus list.
- [ ] Full Postgres suite green; local LIMSV2 migrated (backup first).
- [ ] Browser check on local: receive Pyridoxine (BP) with a supplier CoA, see only BP + micro tests and "Grade: BP" on the CoA; receive a periodic retest, select tests as Section Head; confirm analyst sees the list read-only.
- [ ] Stop and report to the user before Plan 2.
