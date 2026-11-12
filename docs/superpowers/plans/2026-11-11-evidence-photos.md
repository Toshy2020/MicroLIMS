# Plate and Evidence Photos (F2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Camera capture of plate photos linked to readings, stored as unaltered originals with server hash, required per workflow step where configured, added-later and void rules, and review display.

**Architecture:** An append-only `EvidencePhoto` table with typed nullable links (`CountTestReadingId`, `WorkflowStepResultId`, `SampleLocationId`, `MediaEvaluationChallengeId`) plus `TestOrderId` / `MediaEvaluationId` context; `EvidencePhotoService` stores originals via `IFileStorageService` and creates derivatives with SkiaSharp; a `PhotoRequirement` check is called by the recorders. Frontend `CameraCapture` wraps `getUserMedia` behind `services/camera.ts`.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, SkiaSharp (already referenced), xUnit `[PostgresFact]`; React 19, TypeScript, MediaDevices API, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-evidence-photos-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (anti-fake tests, triad, `[PostgresFact]`, `TimeProvider`, `CancellationToken`, no new `any`, `userEvent`, WCAG 2.2 AA: camera dialog keyboard operable, image viewer with alt text from caption).
- Original bytes stored exactly as received; hash = SHA-256 of those bytes computed on the server. Derivatives: display JPEG max 1600 px long side, thumbnail 320 px; derivatives never hashed as evidence.
- Accepted types and size: reuse `MaterialDocumentFileValidator` logic restricted to `.jpg`, `.jpeg`, `.png`, `.webp`, limit `EvidencePhotos:MaxFileSizeBytes` default 26214400.
- Storage key `evidence-photos/{yyyy}/{photoId}/{sha256}{ext}` and derivatives under the same folder.
- Requirement snapshot: the reading stores `PhotoRequiredAtReading` (bool) taken from the step / media setting at the time of the reading.
- "Locked" moments: count reading row saved; pathogen `WorkflowStepResult.SubmittedAtUtc` set; location result saved; media challenge `ReadAt` set. Afterwards add needs reason (10–1000 chars) → `AddedLater = true`. After the section's approval (`SampleSectionSignoff.Status == Approved`) or media evaluation `Completed` → refused.
- No new permissions: upload and void reuse the write scope + D4 competency guard of the reading's test order; media uses the media evaluation permissions.

## Review Focus

1. Same photo uploaded twice (retry after timeout) → one row, same id returned → `Duplicate_upload_by_hash_and_target_returns_existing` (Task 1).
2. A voided photo still satisfies "photo required" → `Voided_photo_does_not_satisfy_requirement` (Task 3).
3. EXIF orientation makes the display copy sideways → derivative applies orientation, original untouched → `Derivative_respects_exif_orientation_original_unchanged` (Task 1).
4. Photo attached to another laboratory's reading by id → refused → `Upload_to_other_section_reading_refused` (Task 2).
5. Device queue sends a photo after the section was approved → refused with message, queue item shown as failed → `Queued_upload_after_approval_refused` (Task 4, frontend).

## File Structure

| File | Responsibility |
|---|---|
| `Domain/Entities/EvidencePhoto.cs` | Entity |
| `Domain/Entities/TestWorkflowStep.cs` (modify) | `PhotoRequired` |
| `Domain/Entities/MediaEvaluationPhotoSetting.cs` | Per section and evaluation type |
| reading entities (modify) | `PhotoRequiredAtReading` on `CountTestReading`, `WorkflowStepResult`, `SampleLocation`, `MediaEvaluationChallenge` |
| `Application/Services/EvidencePhotos/*.cs` | `EvidencePhotoService`, `PhotoRequirement` |
| `Infrastructure/Imaging/PhotoDerivatives.cs` | SkiaSharp resize + orientation |
| `API/Controllers/EvidencePhotosController.cs` | Endpoints |
| `frontend/src/services/camera.ts`, `services/evidencePhotoService.ts`, `components/photos/*` | Capture, gallery, viewer |

---

### Task 1: Photo record, storage, derivatives

**Interfaces:** `EvidencePhoto { Id; TestOrderId?; MediaEvaluationId?; CountTestReadingId?; WorkflowStepResultId?; SampleLocationId?; MediaEvaluationChallengeId?; StepName?; Caption? (≤ 200); OriginalStorageKey; DisplayStorageKey; ThumbStorageKey; Sha256; SizeBytes; ContentType; DeviceCapturedAtText?; UploadedByUserId; UploadedAtUtc; AddedLater; AddedLaterReason?; VoidedAtUtc?; VoidedByUserId?; VoidReason? }` — check constraint: exactly one reading link; trigger: reject DELETE; reject UPDATE except the three void columns once. `IPhotoDerivatives.Create(byte[] original) -> (byte[] display, byte[] thumb)`. `EvidencePhotoService.UploadAsync(PhotoTarget target, byte[] content, string fileName, string contentType, string? caption, string? deviceCapturedAt, string? addedLaterReason, int userId, ct)`; storage writes happen **before** the database transaction and the row is inserted after; an orphan file on rollback is acceptable and logged (no evidence lost); dedupe by (`Sha256`, target).

- [ ] **Step 1:** Failing tests: original byte-identical on read-back; server hash; derivatives sizes; `Derivative_respects_exif_orientation_original_unchanged`; type and size refusals by content; `Duplicate_upload_by_hash_and_target_returns_existing`; trigger rejects edit and delete; void once with reason and audit.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(photos): evidence photo record and storage`.

### Task 2: Targets and scope

**Interfaces:** `PhotoTarget` = one of `CountReading(int id)`, `StepResult(int id)`, `Location(int id)`, `MediaChallenge(int id)`; resolve owning test order / media evaluation; checks: write scope (`EnsureTestOrderAccessAsync`), D4 competency guard for test orders, media evaluation permissions; lock rules from Global Constraints. Endpoints: `POST api/evidence-photos` (multipart, `[RequestSizeLimit(30 MB)]`), `GET api/evidence-photos?target=…`, `GET api/evidence-photos/{id}/display|thumb|original` (original logged as download), `POST api/evidence-photos/{id}/void`.

- [ ] **Step 1:** Failing tests: upload for each target kind; `Upload_to_other_section_reading_refused`; competency refusal; added-later requires reason after lock and is marked; refused after approval / completion; listing returns voided marked; original download logged.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(photos): targets, scope and lock rules`.

### Task 3: Required photos

**Files:** `TestWorkflowStep.PhotoRequired` (+ Test Master DTOs and `WorkflowStepsSection.tsx` payload L163-190), `MediaEvaluationPhotoSetting { SectionId; EvaluationType; PhotoRequired }`; `PhotoRequirement.EnsureSatisfiedAsync(PhotoTarget target, ct)` called in: count reading recording (`IncubationStepRecorder.RecordCountTestAsync` and batch path), pathogen step submission (`PathogenConfirmationRecorder` submit methods incl. `SubmitBiochemicalAsync`), location result recording (`LocationResultRecorder`), media challenge result (`MediaEvaluationEngine.RecordResultAsync`). Because the photo must exist before the reading, photos for a not-yet-saved reading attach to a **draft target** `{ testOrderId, stepName, plateKey }` (`PendingTargetKey` column) and are bound to the reading row in the same transaction when it is saved.

- [ ] **Step 1:** Failing tests: each recorder refuses without photo when required and accepts with one; `Voided_photo_does_not_satisfy_requirement`; pending photos bound on save; requirement snapshot stored; switch change later does not affect earlier readings; not required → no check.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(photos): required photos per step and media evaluation`.

### Task 4: Frontend capture, gallery, review

**Files:** `services/camera.ts` (`listCameras()`, `start(deviceId?, facingMode='environment')`, `capture(): Blob` via canvas at native resolution, `stop()`), `components/photos/CameraCaptureDialog.tsx`, `PhotoStrip.tsx` (thumbnails, caption, added-later and voided marks), `PhotoViewer.tsx` (zoom, metadata, hash), integration into count reading, pathogen step panels, location result grid and media evaluation screens; review in `modules/testingWorkspace/TestResultCards.tsx` (`CountTestCard` L544 and others), `LocationResultGridDialog.tsx`, `sampleSummary/TestResultsSection.tsx`; offline queue through C2's device drafts if built, else a simple IndexedDB queue in `services/photoQueue.ts`; Test Master step switch; report appendix option.

- [ ] **Step 1:** Failing tests (`userEvent`; `services/camera` and `services/evidencePhotoService` mocked): capture and upload; file chooser fallback when camera denied; required message blocks save; add-later asks for reason; void with reason; review viewer metadata; `Queued_upload_after_approval_refused`; keyboard use of the camera dialog.
- [ ] **Step 2:** FAIL → implement → lint, types, tests, debt check, build PASS. Commit `feat(ui): plate photo capture and review`.

### Task 5: Report appendix and verification

- [ ] **Step 1:** Sample report option "Include photos" adds `ImageBlock`s (display copy transcoded to PNG via SkiaSharp, caption = test, step, caption, author, time). Test: PDF contains the images and captions; certificate unchanged.
- [ ] **Step 2:** `BusinessRules.md` note; A1 entries if present; manual section.
- [ ] **Step 3:** Full verification per `CLAUDE.md`; report counts. Commit `feat(photos): report appendix`.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Storage key is `evidence-photos/{yyyy}/{guid}/{sha256}{ext}` with a new GUID (the row id does not exist before the insert).
2. Task 1 entity includes `PendingTargetKey?`. The check constraint is: exactly one reading link **or** a `PendingTargetKey`. The trigger allows one update that sets a reading link and clears `PendingTargetKey` (binding), plus the void columns once; nothing else.
3. `MediaEvaluationPhotoSetting` is edited with `MasterData.Manage`.
4. Migration `AddEvidencePhotos`.

## Review corrections (independent review, 2026-11-11)

From the independent review of the build order. These win over the text above and over the earlier corrections section where they differ.

1. The photo requirement also applies to the pathogen session screen's recording methods (`PathogenSessionService`, see D4 review corrections); photos there attach to the same `WorkflowStepResult` / plate observations.

**Translation (build order 2.7):** if F4 Task 4 is merged when this package is built, every new screen uses translation keys and every new refusal carries a message code.
