# Item STM from the Document Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Items show their STM straight from the Document Library (effective revision's controlled PDF, or a "No effective STM" warning), the STM upload leaves Item Master, and the old uploaded STM files are hard deleted.

**Architecture:** A read-only Application service resolves `Item.SopNumber` to the Active `DocumentMaster` with that `CompanyDocumentCode` (any type) and reports one of four statuses. A new `GET api/items/{itemId}/stm` endpoint exposes it. One shared React panel renders it in Receiving, Testing and Item Master. File view/download reuses the existing document-control file endpoints. The cleanup is a one-off SQL script, not app code.

**Tech Stack:** ASP.NET Core, EF Core + PostgreSQL (Npgsql), xUnit (`[PostgresFact]` + `PostgresTestFixture`), React + TypeScript + MUI, Vitest.

**Spec:** `docs/superpowers/specs/2026-10-24-item-stm-from-document-library-design.md`

## Global Constraints

- Match = Active `DocumentMaster` (`RecordStatus == Active`), `CompanyDocumentCode` equal to `Item.SopNumber` after trim, case-insensitive, **any document type**.
- Statuses, exactly: `NotLinked` (blank SopNumber), `NotFound`, `NoEffectiveRevision` (no `CurrentEffectiveRevisionId`, or that revision is not `Effective`, or it has no active `FileRole.ControlledPdf` file), `Effective`.
- No fallback to uploaded item files, ever.
- Verification Report upload, list and view are unchanged.
- `ItemDocumentType.Sop` enum value stays (history/audit labels); only new SOP uploads are rejected.
- Item save is not blocked by an unmatched SOP number; no new e-signature.
- Builds/tests while the API runs: `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/itemstm-test`. All temp/artifacts on E:.
- Commit each task locally; do not push.

## Review Focus

1. SopNumber with surrounding spaces or different case (`" stm-rm-102 "`) must still match `STM-RM-102`. Test in Task 1.
2. A **voided** document with the same code as an active one: only the active one may match. Test in Task 1.
3. Effective revision whose controlled PDF was deactivated (`IsActive = false`): must be `NoEffectiveRevision`, not a broken link. Test in Task 1.
4. A Draft revision newer than the effective one: the panel shows the effective revision, not the draft. Test in Task 1.
5. Analyst without download rights (setting `DocumentControl.ControlledCopy.DownloadPermitted = false`): sees View only. `canDownload` is tested in Task 1; the hidden button in Task 3.

---

### Task 1: Resolve an item's STM (backend service + endpoint)

**Files:**
- Create: `backend/MicroLIMS.Application/Services/ItemStmService.cs`
- Create: `backend/MicroLIMS.Application/DTOs/Responses/ItemStmResponse.cs`
- Modify: `backend/MicroLIMS.API/Controllers/ItemController.cs` (new GET action)
- Modify: `backend/MicroLIMS.API/Extensions/ServiceCollectionExtensions.cs` (register `ItemStmService` next to `ItemService`, line ~128)
- Test: `backend/MicroLIMS.Tests/IntegrationTests/ItemStmPostgresIntegrationTests.cs`

**Interfaces:**
- Produces:
  - `public enum ItemStmStatus { NotLinked, NotFound, NoEffectiveRevision, Effective }` (in the DTO file; serialized as names by the global `JsonStringEnumConverter`)
  - `public record ItemStmResponse(ItemStmStatus Status, string? SopNumber, int? DocumentMasterId, string? CompanyDocumentCode, string? Title, string? DocumentTypeCode, string? RevisionNumber, DateTime? EffectiveDate, int? ControlledPdfFileId, string? ControlledPdfFileName, bool CanDownload);`
  - `public class ItemStmService(IMicroLimsDbContext db)` with `Task<ItemStmResponse> GetForItemAsync(int itemId, int userId)`. Throws `NotFoundException` for an unknown item.
  - `GET api/items/{itemId:int}/stm` → `ApiResponse<ItemStmResponse>` (class-level `[Authorize]` only, same as the item document list).
- `CanDownload` = true unless the `ConfigurationSettings` row `DocumentControl.ControlledCopy.DownloadPermitted` is `"false"` (case-insensitive) and the user's role is neither `SystemAdministrator` nor `SectionHead`. This is the same rule as `DocumentFileService` lines ~316-331. Extract a shared helper only if it stays small; otherwise duplicate the 6 lines with a comment pointing at the original.

- [ ] **Step 1: Write the failing tests** in `ItemStmPostgresIntegrationTests` (`[Collection("PostgresDatabaseCollection")]`, `PostgresTestFixture`, same setup style as `DocumentMasterSectionsPostgresIntegrationTests`). Each test creates its own Item with a unique code (`Guid` suffix) and, where needed, a `DocumentMaster` + `DocumentRevision` (+ `RevisionFile`) directly through the DbContext, using the fixture's seeded type/department/section/user ids.
  - `BlankSopNumber_IsNotLinked` → `Status == NotLinked`, `DocumentMasterId == null`.
  - `NoMatchingDocument_IsNotFound` → `NotFound`, `SopNumber` echoed.
  - `DraftOnlyDocument_IsNoEffectiveRevision` → `NoEffectiveRevision`, `CompanyDocumentCode` and `Title` filled, `RevisionNumber == null`.
  - `EffectiveRevisionWithPdf_IsEffective` → `Effective`, `RevisionNumber == "01"`, `EffectiveDate` set, `ControlledPdfFileId == pdf.Id`, `DocumentTypeCode` = the seeded type's code.
  - `MatchIgnoresCaseAndSpaces` (Review Focus 1): item SopNumber `"  " + code.ToLower() + " "` → `Effective`.
  - `VoidedDocumentWithSameCode_IsIgnored` (Review Focus 2): one voided master with the code (with an effective rev + PDF) and one active Draft master with the same code → `NoEffectiveRevision`, `DocumentMasterId == active.Id`.
  - `InactiveControlledPdf_IsNoEffectiveRevision` (Review Focus 3).
  - `NewerDraftRevision_StillShowsEffective` (Review Focus 4): effective "01" + draft "02" → `RevisionNumber == "01"`.
  - `DownloadDisabledForAnalyst_CanDownloadFalse` and `DownloadDisabled_AdminCanDownload` (Review Focus 5): insert/update the setting row to `"false"` and restore it in a `finally`.
  - `UnknownItem_ThrowsNotFound`.
- [ ] **Step 2: Run the tests to verify they fail**
  Run: `dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/itemstm-test --filter FullyQualifiedName~ItemStmPostgresIntegrationTests`
  Expected: build error (`ItemStmService` not defined).
- [ ] **Step 3: Implement `ItemStmService.GetForItemAsync` and the DTO**: one item lookup plus one master query (`AsNoTracking`, include `DocumentType` and the current effective revision with its files), then the setting/role check for `CanDownload`.
- [ ] **Step 4: Add the controller action and the DI registration**, following the try/catch → `NotFound(ApiResponse<object>.Fail(...))` style of `ItemDocumentController.GetDocuments`.
- [ ] **Step 5: Run the tests to verify they pass** (same command). Expected: all pass.
- [ ] **Step 6: Commit** `feat(items): resolve an item's STM from the Document Library`

### Task 2: Item Master stops accepting STM uploads (backend)

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/ItemDocumentService.cs` (`UploadDocumentCoreAsync`, start of the method)
- Modify: `backend/MicroLIMS.Tests/InventoryTests/ItemDocumentTests.cs` (existing tests upload `ItemDocumentType.Sop` to exercise versioning; switch those to `VerificationReport`, assertions unchanged)

**Interfaces:**
- Produces: uploading with `documentType == ItemDocumentType.Sop` throws `InvalidOperationException("STMs are managed in the Document Library. Link the item's SOP number to its library document instead of uploading a file.")`. The controller already maps `InvalidOperationException` to 400; confirm that and keep it.

- [ ] **Step 1: Write the failing test** `UploadSop_IsRejected_AndNothingStored` in `ItemDocumentTests`: assert the exception message above, `db.ItemDocuments` count unchanged, and no file under the temp storage dir.
- [ ] **Step 2: Run it to verify it fails**: `dotnet test ... --filter FullyQualifiedName~ItemDocumentTests`. Expected: FAIL (upload succeeds).
- [ ] **Step 3: Add the guard** before any validation or I/O.
- [ ] **Step 4: Switch the existing SOP-versioning tests to `VerificationReport`** and run the class. Expected: all pass.
- [ ] **Step 5: Run the full suite** (command in Global Constraints). Expected: 0 failed. Report the counts.
- [ ] **Step 6: Commit** `feat(items): STM uploads move to the Document Library`

### Task 3: STM panel in Receiving and Testing (frontend)

**Files:**
- Create: `frontend/src/modules/laboratoryConfiguration/items/services/ItemStmService.ts`
- Create: `frontend/src/components/ItemStmPanel.tsx`
- Create: `frontend/src/components/ItemStmPanel.test.tsx`
- Modify: `frontend/src/components/ReadOnlyItemDocumentsDialog.tsx` (replace the current/historical SOP sections, lines ~63-70 and their render, with `<ItemStmPanel itemId=… />`)
- Modify: `frontend/src/modules/testingWorkspace/ItemDocumentsCard.tsx` (same replacement)
- Modify: `frontend/src/modules/laboratoryConfiguration/items/services/ItemDocumentService.ts` (`getDocumentCountForItem`: count only `VerificationReport` documents with status `Current`)
- Modify: `frontend/src/modules/receiving/components/SampleRegisterTable.tsx` (~line 940: badge = verification count + 1 when STM status is `Effective`; reuse the existing per-item cache pattern for the STM call)
- Modify: `frontend/src/modules/receiving/components/SampleRegisterTable.test.tsx` (mock `ItemStmService`)

**Interfaces:**
- Consumes: `GET /items/{itemId}/stm` (Task 1), `documentControlService.viewFileBlob(fileId)` and `documentControlService.downloadFile(fileId, fileName)` from `frontend/src/modules/documentControl/services/documentControlService.ts`.
- Produces:
  - `export type ItemStmStatus = "NotLinked" | "NotFound" | "NoEffectiveRevision" | "Effective";`
  - `export interface ItemStmDto { status: ItemStmStatus; sopNumber: string | null; documentMasterId: number | null; companyDocumentCode: string | null; title: string | null; documentTypeCode: string | null; revisionNumber: string | null; effectiveDate: string | null; controlledPdfFileId: number | null; controlledPdfFileName: string | null; canDownload: boolean; }`
  - `ItemStmService.getForItem(itemId: number): Promise<ItemStmDto>`
  - `<ItemStmPanel itemId: number />`, which fetches on mount and on `itemId` change.

Panel copy, exact:
- `Effective`: header `{documentTypeCode} {companyDocumentCode}`, title, `Revision {revisionNumber} · Effective {date}`, buttons **View** (opens the blob URL in a new tab) and **Download** (only when `canDownload`).
- Otherwise an MUI `Alert severity="warning"` titled **No effective STM**, with one line:
  - `NotLinked`: "This item has no SOP number."
  - `NotFound`: "SOP number {sopNumber} is not in the Document Library."
  - `NoEffectiveRevision`: "{companyDocumentCode} has no effective revision yet."

- [ ] **Step 1: Write the failing tests** in `ItemStmPanel.test.tsx` (Vitest + RTL, mock `ItemStmService` and `documentControlService`): one per status, asserting the exact copy above; `Effective` with `canDownload: false` has no Download button (Review Focus 5); clicking View calls `viewFileBlob(controlledPdfFileId)`.
- [ ] **Step 2: Run them to verify they fail**: `cd frontend && npx vitest run src/components/ItemStmPanel.test.tsx`. Expected: FAIL (module missing).
- [ ] **Step 3: Implement `ItemStmService.ts` and `ItemStmPanel.tsx`**, matching the existing dialog/card styling.
- [ ] **Step 4: Replace the SOP sections** in `ReadOnlyItemDocumentsDialog` and `ItemDocumentsCard`, then update the count badge and its test.
- [ ] **Step 5: Verify**: `npx vitest run src/components src/modules/receiving src/modules/testingWorkspace`, `npm run build`, `npx eslint` on the changed files. Expected: all pass, 0 eslint errors.
- [ ] **Step 6: Commit** `feat(items): analysts see the effective STM from the Document Library`

### Task 4: Item Master: no STM upload, panel, SOP number picker (frontend)

**Files:**
- Modify: `frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx` (remove the SOP upload button ~line 423 and the current/historical SOP sections ~lines 85-90 plus their render; add `<ItemStmPanel itemId={item.id} />`; keep the Verification Report section)
- Modify: `frontend/src/modules/laboratoryConfiguration/items/components/UploadItemDocumentDialog.tsx` (only `VerificationReport`; drop the SOP choice and default)
- Modify: `frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.tsx` (SOP Number field ~line 127 becomes an MUI `Autocomplete freeSolo`)
- Test: `frontend/src/modules/laboratoryConfiguration/items/components/AddItemDialog.test.tsx` (create if missing)

**Interfaces:**
- Consumes: `ItemStmPanel` (Task 3). The library search is `documentControlService`'s existing library call with `{ searchTerm, pageSize: 20 }` (`GET /document-control/library`), so no new endpoint.
- Option label `"{companyDocumentCode} — {title}"`; the stored value is `companyDocumentCode`. Debounce typing (300 ms), at least 2 characters before searching. Free text is still saved as typed (trimmed).

- [ ] **Step 1: Write the failing tests**: typing "STM-RM" calls the library search and lists `STM-RM-102 — Specification…`; picking it saves `sopNumber: "STM-RM-102"`; typing an unmatched value and saving still submits it.
- [ ] **Step 2: Run them to verify they fail**: `npx vitest run src/modules/laboratoryConfiguration/items`.
- [ ] **Step 3: Implement the Autocomplete; remove the SOP upload paths; add the panel to `ItemWorkspace`.**
- [ ] **Step 4: Verify**: vitest for the folder, `npm run build`, `npx eslint` on the changed files. Expected: pass.
- [ ] **Step 5: Commit** `feat(items): pick the SOP number from the Document Library; no STM upload`

### Task 5: Hard delete the old uploaded STM files (one-off script)

**Files:**
- Create: `scripts/2026-10-24-delete-item-sop-uploads.sql`

**Interfaces:** none (data only).

Script content, in this order:
1. `SELECT "Id","ItemId","OriginalFileName","StorageKey","Status" FROM "ItemDocuments" WHERE "DocumentType" = 0;` (show the user first)
2. `BEGIN; DELETE FROM "ItemDocumentAccessLogs" WHERE "DocumentId" IN (SELECT "Id" FROM "ItemDocuments" WHERE "DocumentType" = 0);`. Before deleting the documents, null out `SupersededByDocumentId` on any row pointing at them, then `DELETE FROM "ItemDocuments" WHERE "DocumentType" = 0; COMMIT;`

- [ ] **Step 1: Write the script** and dry-run step 1 against local LIMSV2 (`psql -h localhost -U postgres -d LIMSV2`). Expected: 4 rows.
- [ ] **Step 2: Show the rows to the user and get a go-ahead**, then run the transaction locally. Delete the local stored files by their `StorageKey` under the local storage base path (`Storage:BasePath`, default `storage` under the API content root). Expected: 0 SOP rows left; the files are gone.
- [ ] **Step 3: Commit** the script: `chore(items): one-off script to hard delete uploaded STM files`
- [ ] **Step 4 (at prod release, after deploy, with the user's go-ahead):** run step 1 on Neon (expected: 1 row), then the transaction. The prod file lives in B2 (S3 storage); deleting it needs B2 credentials, so give the user the exact bucket key as a checklist item.

---

**Final verification (after Task 5 step 3):** full backend suite (command in Global Constraints), `npm run build`, vitest for the touched folders. Then browser-check on local: an item with an effective STM, an item with a Draft STM (warning), an item with no SOP number, Item Master with no SOP upload, and the SOP number picker.
