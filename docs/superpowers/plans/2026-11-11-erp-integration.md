# ERP (SAP QM) Integration (E2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Inbox/outbox integration with SAP QM: inspection lots become expected samples, final decisions write usage decision, results and certificate messages delivered after commit, with clients, mapping, monitoring and QA-controlled corrections.

**Architecture:** `IntegrationInboxMessage` and `IntegrationOutboxMessage` tables; inbound handlers per message type; outbox rows written inside `SampleApprovalService.FinalizeIfClosedAsync`'s transaction; `ErpOutboxWorker` delivers through `IErpTransport` (`RestErpTransport`, `AgentFileErpTransport`). Integration clients authenticate with a hashed key scheme modelled on `AgentKeyAuthenticationHandler`.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; React 19, TypeScript, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-erp-integration-design.md`

## Global Constraints

- `CLAUDE.md` binds every task; especially Rule 5.4: no HTTP or file I/O inside a transaction — outbox rows only; delivery in the worker after commit.
- Message types (frozen names, persisted as string): `MaterialMaster`, `InspectionLot`, `InspectionLotCancelled`, `UsageDecision`, `Results`, `Certificate`, `ReceiptStatus`. Statuses (int, frozen): inbox `Received=1, Processed=2, Refused=3`; outbox `Pending=1, Sending=2, Delivered=3, Failed=4, Held=5`.
- Retry schedule exactly: 1 min, 5 min, 15 min, then every 60 min, up to `Erp:MaxAttempts` (default 24) → `Failed`.
- Ordering: outbox messages for the same inspection lot delivered in `Id` order; a lot's later message waits while an earlier one is not `Delivered`.
- Secrets (endpoint credentials) only in configuration (`Erp__Outbound__*` environment variables); never in DB, payload or audit. Client keys stored as SHA-256 like `InstrumentAgent.KeyHash` with `[AuditRedacted]`.
- Permission codes: `Erp.Configure`, `Erp.Monitor`, `Erp.ApproveCorrection` (QA, joins B4's QA-only set).
- New `SignatureMeaning`: `ErpCorrectionApproved`, appended.
- Items created from SAP: `IsActive = false`; never touch specifications or assigned tests.
- `IntegrationInboxMessage` and `IntegrationOutboxMessage` are added to `AuditCaptureExcludedTypes` with the comment `// integration message log; business effects audited by their services` (approved by the user 2026-11-11). All other new entities stay under automatic capture.

## Review Focus

1. Final decision rolls back after outbox rows were added → `Rolled_back_decision_leaves_no_outbox_rows` (Task 4).
2. Worker on two instances delivers the same row twice → `Concurrent_workers_claim_row_once` (Task 3): claim with `FOR UPDATE SKIP LOCKED`.
3. SAP retries the same inspection lot message with a new message id → `Same_lot_number_twice_creates_one_expected_sample` (Task 2).
4. Sample received from an expected sample, then the lot is cancelled → `Cancel_after_receipt_alerts_and_changes_nothing` (Task 2).
5. A correction path not covered by the QA gate (section close, reopen, void, result correction) → `Every_post_decision_change_path_requires_qa_approval` (Task 5).

## File Structure

| File | Responsibility |
|---|---|
| `Domain/Entities/Erp/*.cs` | `IntegrationClient`, `IntegrationInboxMessage`, `IntegrationOutboxMessage`, `ErpMapping` (plant, category, decision, characteristic tables), `ExpectedSample`, `ErpManualCorrectionTask` |
| `Domain/Entities/Item.cs`, `Sample.cs` (modify) | `Item.ExternalCode?` (unique when set); `Sample.ExpectedSampleId?`, `Sample.InspectionLotNumber?` |
| `API/Authorization/IntegrationClientKey*.cs` | `ClientKey` scheme, header `X-Integration-Key` |
| `Application/Services/Erp/*.cs` | `ErpInboxService`, handlers, `ErpOutboxWriter`, `ErpMappingService`, `ExpectedSampleService`, `ErpCorrectionGate`, `ErpMonitorService`, `IErpTransport` |
| `Infrastructure/Erp/RestErpTransport.cs`, `AgentFileErpTransport.cs` | Delivery |
| `API/BackgroundServices/ErpOutboxWorker.cs` | Delivery loop |
| `API/Controllers/Erp/*.cs` | Inbound endpoint, admin, monitor |
| `frontend/src/modules/erp/*` | Monitoring, mapping, clients; receiving integration |

---

### Task 1: Clients, inbox, outbox schema, permissions

**Files:** entities, migration, permission constants and grants, `IntegrationClientKeyAuthenticationHandler` (copy the structure of `AgentKeyAuthenticationHandler`: hash lookup, `IsActive`, `LastSeenAt` throttle, failed attempts to `SecurityAuditEvent`), `IntegrationClientService` (`RegisterAsync`, `RotateKeyAsync`, `RevokeAsync`), controller; Test `IntegrationTests/ErpClientAndSchemaPostgresTests.cs`

**Interfaces:** `IntegrationInboxMessage { Id; ClientId?; Transport (Rest|File); ExternalMessageId; Type; PayloadJson; ReceivedAtUtc; Status; Reason?; ProcessedAtUtc?; ResultEntity?; ResultId? }` unique (`ClientId`, `ExternalMessageId`). `IntegrationOutboxMessage { Id; Type; InspectionLotNumber; SampleId; PayloadJson; AttachmentStorageKey?; AttachmentSha256?; MessageGuid (unique); Status; Attempts; NextAttemptAtUtc; LastError?; DeliveredAtUtc?; HeldByUserId?; HeldReason? }`.

- [ ] **Step 1:** Failing tests: client key issued once, hash stored, redacted in audit; rotate invalidates old key; revoked key 401 with security event; inbox unique index; outbox `MessageGuid` unique; grants per role.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(erp): integration clients, inbox and outbox schema`.

### Task 2: Inbound — mapping, material master, inspection lots, expected samples

**Files:** `ErpMappingService` + entities (`ErpPlantMapping { Plant; SectionId }`, `ErpCategoryMapping { SapCategory; SampleCategory }`, `ErpDecisionMapping { Decision; SapCode }`, `ErpCharacteristicMapping { TestCode; ParameterName?; Characteristic }`), `ErpInboxService.ReceiveAsync(int clientId, ErpInboundEnvelope envelope, ct)` → `ErpReceipt { Accepted, Reasons[] }`, handlers `MaterialMasterHandler`, `InspectionLotHandler`, `InspectionLotCancelledHandler`; `ExpectedSample { Id; InspectionLotNumber (unique); Origin; ItemId; BatchNumber; ControlNumber; Plant; Quantity; Unit; Supplier?; SupplierBatch?; MfgDate?; ExpDate?; SectionIds; Status (Expected|Received|Cancelled); SampleId? }`; `ErpInboundController` `POST api/erp/inbound` (`ClientKey` scheme); Test `IntegrationTests/ErpInboundPostgresTests.cs`

- [ ] **Step 1:** Failing tests: duplicate message id returns the stored receipt and processes once; unknown material creates inactive item with `ExternalCode` and code collision handled (existing `Item.Code` equal → link by code, report); known material renames only; specs and tests unchanged; lot creates expected sample; refused for unknown material, inactive item, unmapped plant (inbox `Refused`, reason returned); `Same_lot_number_twice_creates_one_expected_sample`; reprocess after item activation; `Cancel_after_receipt_alerts_and_changes_nothing`; cancel before receipt cancels; Section Heads notified of new items after commit.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(erp): inbound material master and inspection lots`.

### Task 3: Outbox worker and transports

**Files:** `IErpTransport { Task<DeliveryResult> SendAsync(IntegrationOutboxMessage m, Stream? attachment, CancellationToken ct) }`, `RestErpTransport` (HttpClient from `IHttpClientFactory`, base URL and auth from configuration, idempotency header `Idempotency-Key: {MessageGuid}`), `AgentFileErpTransport` (marks the row available for agent pickup; the agent fetches `GET api/erp/outbound/next` with its key and confirms `POST api/erp/outbound/{guid}/written`), `ErpOutboxWorker` (catalog entry, Gxp; `Erp:Outbound:Enabled`, interval 30 s), claim query `SELECT … FOR UPDATE SKIP LOCKED` in a short transaction, send outside it, record result in another short transaction; Test `IntegrationTests/ErpOutboxPostgresTests.cs` with a local `HttpListener` test server

- [ ] **Step 1:** Failing tests: delivered once; `Concurrent_workers_claim_row_once`; retry schedule timestamps exact; `Failed` after max attempts with alert; manual retry resets to `Pending`; ordering per lot; `Held` never sent; idempotency header present; file route handed to agent and confirmed; credentials absent from DB and logs.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(erp): outbox worker and transports`.

### Task 4: Receiving from expected samples and outbound decision

**Files:** `ExpectedSampleService` (list for user's sections; `ReceiveAsync(expectedSampleId, ReceiveFromExpectedRequest { SampleQuantity, SampledBy, CauseOfTestingId, TargetSectionIds? }, userId)` → builds `ItemBasedReceiveRequest` from the lot and calls `ReceivingService.ReceiveSampleAsync` in one transaction; sets `Sample.ExpectedSampleId`, `InspectionLotNumber`), `ErpOutboxWriter.WriteForFinalDecisionAsync(Sample sample, ArchivedRecord certificate, ct)` called inside `SampleApprovalService.FinalizeIfClosedAsync` (L529) after `_archive.ArchiveAsync` returns, only when `sample.ExpectedSampleId != null`; results payload from `SampleSummaryDto` (`ParameterResultDetailDto.ReportedDisplay`, `Unit`, `SpecLimit`, `ComparisonStatus`) through characteristic mapping; Test `IntegrationTests/ErpDecisionOutboxPostgresTests.cs`

- [ ] **Step 1:** Failing tests: receive from expected fills fields and applies all receiving validators; expected sample of another section not listed; approved and rejected decisions write three messages with mapped codes; superseded and invalidated results not sent; unmapped results listed and not sent; certificate attachment is the archived record with its hash; sample without ERP link writes nothing; `Rolled_back_decision_leaves_no_outbox_rows`; ArchiveAsync storage write happens before commit today — read `RecordArchiveService.ArchiveAsync` and its catch block; if the storage write can succeed while the transaction rolls back, record it as a finding for the user (orphan file, no data loss) and do not change it in this task.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(erp): expected samples and outbound decision messages`.

### Task 5: Corrections after sending

**Files:** `ErpCorrectionGate.EnsureAllowedAsync(int sampleId, ErpCorrectionApproval? approval, ct)` called by `SampleCorrectionService`, sample void, section reopen and `SectionClosureService` reopen paths (find every method that changes a decided sample: grep `Status = SampleStatus` and signoff status writes after approval); `ErpCorrectionApproval { QaUsername, QaPassword, Reason }` verified through `IElectronicSignatureService.SignAsync` with `ErpCorrectionApproved` for the QA user (holder of `Erp.ApproveCorrection`, ≠ acting user); on approval: undelivered messages → `Held`; `ErpManualCorrectionTask { SampleId; InspectionLotNumber; CreatedAtUtc; ClosedByUserId?; ClosedComment? }`; Test `IntegrationTests/ErpCorrectionPostgresTests.cs`

- [ ] **Step 1:** Failing tests: `Every_post_decision_change_path_requires_qa_approval` (one row per path); without approval refused, nothing changed; with approval change made, messages held, task created, audit and signature; QA approving own correction refused; samples without ERP link unaffected; held message released only by QA from the monitor.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(erp): QA-approved corrections after sending`.

### Task 6: Monitoring and configuration UI

**Files:** `ErpMonitorService` (list inbox / outbox with filters, page size ≤ 100, payload view, retry, reprocess, release hold), controllers; frontend `modules/erp/ErpMessagesPage.tsx`, `ErpMappingPage.tsx`, `IntegrationClientsCard.tsx` on the Integrations page, `ManualCorrectionTasks.tsx` (QA My Work), receiving screen "Expected samples" tab with receive action; tests

- [ ] **Step 1:** Failing tests: backend filters and permissions (Section Head reads, cannot retry); frontend (`userEvent`, `services/` mocked) message list and actions, mapping edit with version, client key shown once, expected sample receive pre-fill, correction approval dialog with QA credentials.
- [ ] **Step 2:** FAIL → implement → full checks PASS. Commit `feat(ui): ERP monitoring, mapping and expected samples`.

### Task 7: Documents and verification

- [ ] **Step 1:** `documents/Interfaces/SAP-QM-Interface-Specification.md`: every message with fields, types, examples (JSON and XML), transports, idempotency, retry, error codes — for the SAP team.
- [ ] **Step 2:** Requirements (A1 area `ERP`), risk rows, tags; SOP note on manual SAP correction.
- [ ] **Step 3:** Full verification per `CLAUDE.md`; report counts; report the archive-before-commit finding if found.
