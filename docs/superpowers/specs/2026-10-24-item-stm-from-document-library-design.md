# Item STM from the Document Library — design

Date: 2026-10-24. Status: draft for user approval.

## Intent

Today an item's STM is uploaded twice: once as a controlled document in the Document Library and again as a file on the Item Master page (`ItemDocuments`, type `Sop`). Analysts see only the Item Master copy, on the Receiving page and in the Testing workspace. That copy is double work, and it goes stale when a new STM revision becomes effective in Document Control.

The goal: **the item recalls its STM from the Document Library, and the analyst always sees the currently effective revision.** One controlled source and no second upload.

A later piece will block an analyst from performing a test on a received item until they have read the newly effective STM revision. This design leaves room for that but does not build it.

## Goals

1. Each item resolves to its STM document in the Document Library.
2. On the Receiving page (item documents dialog) and in the Testing workspace (Item Documents card), the analyst sees the **effective revision's controlled PDF** of that STM: document code, title, revision number, effective date, and view/download. When a newer revision becomes effective, it shows up there with no action on the item.
3. If the item has no linked STM, or the linked STM has no effective revision with an active controlled PDF, the analyst sees a **warning: "No effective STM"**. There is no fallback copy.
4. Item Master can no longer upload STM/SOP files. **Verification Reports stay as uploads on the item**, unchanged.
5. The existing uploaded STM/SOP item files (`ItemDocuments` with `DocumentType = Sop`: 1 row in prod, 4 locally) are **hard deleted**: rows, their access-log rows, and the stored files.

## Non-goals

- The read-before-test block (later piece).
- Moving Verification Reports into the Document Library.
- Changes to the Document Library's approval, effectivity or acknowledgement workflows.

## Anti-goals

- An analyst sees an outdated STM revision without being told.
- A silent fallback to an uncontrolled uploaded copy.

## Decided by the user

- STM comes from the Document Library; only the effective revision is shown.
- No effective STM → warning, no fallback.
- Verification Reports stay as item uploads.
- Remove the STM upload from Item Master.
- Hard delete the old uploaded STM files (no void or retain), **right away, as part of this release**: not gated on the STMs becoming effective. Until the 247 Draft STMs are approved and made effective, every item shows "No effective STM"; that is accepted.

## Context (current code)

- `Item` (`backend/MicroLIMS.Domain/Entities/Item.cs`) has a free-text `SopNumber`. The 239 local / 223 prod items created on 2026-10-23 from the Vitabiotics SOP list have `SopNumber` equal to the `CompanyDocumentCode` of their STM `DocumentMaster` (type `STM`, 247 documents, all currently Draft rev "01").
- `ItemDocument` + `ItemDocumentType { Sop = 0, VerificationReport = 1 }`, service `backend/MicroLIMS.Application/Services/ItemDocumentService.cs`. Storage keys `item-documents/{itemId}/...` go through the storage abstraction (`_storage`).
- UI using item documents: `frontend/src/modules/laboratoryConfiguration/items/components/ItemWorkspace.tsx` (upload buttons, current/historical SOP), `UploadItemDocumentDialog.tsx`, `frontend/src/components/ReadOnlyItemDocumentsDialog.tsx` (Receiving, opened from `receiving/components/SampleRegisterTable.tsx`), `frontend/src/modules/testingWorkspace/ItemDocumentsCard.tsx` (Testing workspace), `frontend/src/components/ItemDocumentActionButtons.tsx`.
- Document Library: `DocumentMaster.CurrentEffectiveRevisionId`; `RevisionFile` with `FileRole.ControlledPdf` + `IsActive`; view/download endpoints `GET api/document-control/files/{fileId}/view|download` (`DocumentFilesController`). `DocumentAuthorizationService.CanAccessControlledPdfAsync` already lets **every active user** read the controlled PDF of an **effective** revision, so analysts need no new permission.
- `CompanyDocumentCode` is unique among Active documents and can only be edited before a document's first effective revision.

## Design

### Link: by SOP number (my call)

No new foreign key. An item's STM is the **Active** `DocumentMaster` whose `CompanyDocumentCode` equals `Item.SopNumber` (trimmed, case-insensitive), **of any document type** (my call: older items point at method SOPs, e.g. `C2I-91-128`, which are the documents analysts follow; the panel shows the document's type code next to its code instead of always saying "STM"). Uniqueness of codes among Active documents means at most one match. This works for all imported items with no migration or backfill. A voided and re-registered document with the same code is followed automatically.

In Item Master, the SOP Number field becomes an **autocomplete over Document Library codes** (active documents, showing code + title). Free text is still accepted and save is **not** blocked (my call: keeps the master-data workbook import of `SopNumber` working). If the typed value matches no active document, the field shows the same "No effective STM / not found in Document Library" hint. Edits keep the existing item save path and its audit; no new e-signature (my call: item saves are unsigned today; adding one is a separate change).

### Backend

- New read endpoint, e.g. `GET api/items/{itemId}/stm`, returning:
  - `status`: `NotLinked` (blank SopNumber), `NotFound` (no active document with that code), `NoEffectiveRevision` (master found, but no effective revision or no active controlled PDF), `Effective`
  - when found: `documentMasterId`, `companyDocumentCode`, `title`, and for `Effective` also `revisionNumber`, `effectiveDate`, `controlledPdfFileId`.
  The service lives in Application. It's read-only and open to the same roles that can see item documents today.
- `ItemDocumentService` upload rejects `DocumentType = Sop` with a clear message ("STMs are managed in the Document Library").
- Keep the `ItemDocumentType.Sop` enum value; audit-label code and history still reference it.
- Tests: Postgres integration tests for each `status` and for the upload rejection.

### Frontend

- `ItemDocumentsCard` (Testing) and `ReadOnlyItemDocumentsDialog` (Receiving): replace the "current/historical SOP" part with an **STM panel** fed by the new endpoint. For `Effective`, show code, title, revision, effective date, and view/download through the existing document-control file endpoints. Access is logged at document level by those endpoints; no item-level access log (my call). If `DownloadPermitted` blocks download for the user, show view only. For any other status, show a warning alert, "No effective STM", with a one-line reason. The Verification Report part is unchanged.
- `ItemWorkspace` (Item Master): remove the SOP upload button and the SOP sections. Show the same STM panel read-only, and turn the SOP Number field into the autocomplete described above. Verification Report upload stays.
- `SampleRegisterTable`'s document-count badge: count Verification Reports + 1 if the STM status is `Effective` (builder may simplify, but the badge must not count deleted SOP uploads).

### One-time cleanup: hard delete

A one-off script run per database (local, then prod when this release deploys), not app code:
1. List `ItemDocuments` where `DocumentType = 0` and show them to the user before deleting.
2. Delete their storage objects (local disk / B2 depending on environment) by `StorageKey`.
3. Delete their `ItemDocumentAccessLogs` rows, then the `ItemDocuments` rows, in one transaction.
The `AuditLogs` rows for these uploads stay (they are immutable by trigger).

## Left to the builder

- Endpoint/route naming and DTO shape details, within the statuses above.
- Exact wording of the warning lines.
- Whether the Item Master autocomplete loads codes up-front or searches the server-side library endpoint.

## Later (not built now): read-before-test block

Hook point: when an analyst starts a test on a received sample, resolve the item's STM as above. If it is `Effective`, require a `DocumentAcknowledgementRecord` by that user for that `DocumentRevisionId`. That reuses the library's existing training assignment / acknowledgement records, which are already re-issued per revision.
