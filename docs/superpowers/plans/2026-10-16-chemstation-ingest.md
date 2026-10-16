# ChemStation Ingest Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Get Agilent ChemStation C.01.07 injection data into MicroLIMS as attributable, hash-verified evidence and proposed values on the existing HPLC run flow, never as accepted results.

**Architecture:** A dumb edge agent (hash + courier + outbox) authenticates with its own key and posts original files; the backend stores them, parses server-side, and holds the result as *pending instrument data*. An analyst adopts it onto an open `HplcRun` (evidence + pre-filled responses) and signs through the existing `HplcMethodAssayRecorder`. No new run aggregate.

**Tech Stack:** ASP.NET Core controllers, EF Core 10 / Npgsql, xUnit (Postgres suite), React+MUI (one panel), .NET Worker Service + SQLite (agent).

**Spec:** `docs/reviews/chemstation-integration-review.md` (findings 1-30, §4 design changes). Decisions below override the original proposal where they differ.

## Decisions made here (so tasks don't re-open them)

- **Entities:** `InstrumentAgent`, `InstrumentIngest`, `InstrumentIngestVersion`, `InstrumentIngestPayload`. No `InstrumentRun`, no `PeakResult` rows — peaks live in the payload JSON (one audited row, not N).
- **Equipment comes from the agent registration** (`InstrumentAgent.EquipmentId`), never from a serial string in the report. "Unresolvable instrument" cannot happen at ingest.
- **Statuses** (`InstrumentIngestStatus`, persisted int, names frozen by test): `Quarantined=1, PendingReview=2, Adopted=3, Superseded=4, Dismissed=5`. Quarantined = parse failure, hash mismatch with manifest, or clock drift over limit. Unmatched sample names are *not* quarantine; matching happens at adoption.
- **Audit:** ingest events via `IAuditEventService.RecordSystemEventAsync` (`ActorType.System`, `SystemProcessName = "InstrumentAgent:{Code}"`, category `ImportExport`); no `ActorType` enum change. Generic capture stays on for Agent/Ingest/Version; `InstrumentIngestPayload` is excluded from generic capture (append-only by trigger, hashes carried by the explicit event).
- **Storage:** report original goes through `IFileStorageService` under a key containing its SHA-256 (never overwritten). Raw `.D` stays in the lab archive; the LIMS keeps the per-file hash manifest. Raw upload is Phase 6.
- **Supersession reason:** a newer report before adoption replaces the pending one with system reason "Newer report received before adoption". A newer report for an already-**Adopted** version requires a human reason at re-adoption (mirrors `HplcRunService.SupersedeEvidenceAsync`).
- **Typed vs ingested value:** analyst may edit an ingested value only with a reason; it is flagged to the reviewer (flag, not block — confirm in Phase 0).
- **Time:** `DateTime` UTC; `ILabClock` for received-at; verbatim local text + zone used + clock offset stored.

## Global Constraints

- Controllers, route `api/<resource>`, no `/v1`; envelope `ApiResponse<object>.Ok/.Fail` (`API/Controllers/HplcWorkspaceController.cs:13-35`).
- Domain/Persistence use `DateTime` (UTC); Application never reads the system clock (`ArchitectureTests/ClockUsageTests.cs:12`).
- Decimals: `HasPrecision(28,10)` (`Persistence/Configurations/HplcSstRecordConfiguration.cs:34-40`); store verbatim text beside every parsed number.
- Deactivate, never delete: `IsActive` + reason; status enums persisted as int, names never change (add a names test like `StatusLiteralTests.cs:17`).
- Every new controller action appears in `ArchitectureTests/authorization-matrix.txt`.
- All builds/artifacts/temp on `E:` (`dotnet test backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/ingest`); `dotnet ef migrations add … --configuration Release`; run the **Postgres** suite, not only InMemory.
- Work on local branch `feat/chemstation-ingest` cut from `origin/main` (OCR/Empower parser is already there via PR #97). Commit each tested slice locally; no push, no merge, no prod.
- Nothing is called "validated". Retrospective test evidence is not validation; Phase 5 produces a validation plan, not a claim.

## Review Focus

- Agent PC clock wrong or DST fold/gap → acquisition time lands an hour off; expected: drift beyond limit quarantines, fold/gap resolved by a defined rule and flagged.
- Two concurrent POSTs of the same report (outbox retry) → one version row, both get 200 with the same receipt.
- Truncated/corrupt upload → server-computed SHA-256 ≠ supplied → 400, nothing stored.
- Comma-decimal / non-US-locale export (`1.234,56`) → parsed correctly or quarantined, never silently ×1000.
- Ingest arrives when no open `HplcRun` exists, run is Closed, or run's equipment ≠ agent's equipment → stays PendingReview; adopt refuses with a clear message.
- Revoked/rotated key while the outbox is full → agent stops, keeps data, alerts; server records the rejected attempt in `SecurityAuditEvent`.

---

## Phase 0 — Unblock (no code). STOP at the end.

### Task 0: Branch, answers, sample files

- [ ] **Step 1:** `git switch -c feat/chemstation-ingest origin/main`; confirm `git grep EmpowerReportParser -- backend/MicroLIMS.API` still hits `ServiceCollectionExtensions.cs` (it did on origin/main at e7040ba6).
- [ ] **Step 2:** Mohamed answers (review file §5). **Blocks:** Phase 3 ← Q7 + 5-10 real exports; Phase 4 ← Q1, Q2, Q5, Q15; Phase 5 ← Q9, Q10, Q11, Q8; Phase 1-2 ← nothing (proceed now). Q4, Q12, Q13, Q14 are recorded as decisions in this file's header when answered.
- [ ] **Step 3:** Put anonymised sample exports in `E:/MicroLIMS/rls-tmp/chemstation-samples/` (report + one `.D` file listing) and copy the anonymised report files to `backend/MicroLIMS.Tests/Fixtures/ChemStation/`.
- **STOP:** Phases 1-2 may start before Step 2/3 finish; Phases 3+ may not.

---

## Phase 1 — Agent principal and authentication

### Task 1: `InstrumentAgent` entity + migration

**Files:** Create `Domain/Entities/InstrumentAgent.cs`, `Persistence/Configurations/InstrumentAgentConfiguration.cs`, migration `AddInstrumentAgents`; Modify `Persistence/DbContext/MicroLimsDbContext.cs` (DbSet), `Application/Abstractions/Persistence/IMicroLimsDbContext.cs`; Test `Tests/UnitTests/InstrumentAgentModelTests.cs`.

**Interfaces — Produces:** `InstrumentAgent : IVersionedEntity { int Id; uint Version; string Code; string Name; int EquipmentId; Equipment? Equipment; int SectionId; string TimeZoneId; string KeyHash; string KeyPrefix; bool IsActive; DateTime CreatedAt; int CreatedByUserId; DateTime? RevokedAt; int? RevokedByUserId; string? RevokeReason; DateTime? LastSeenAt; }`

- [ ] Test `Code_and_KeyHash_are_unique_and_Equipment_is_required` (Postgres): duplicate `Code` or `KeyHash` → unique violation; null equipment → FK error.
- [ ] Implement entity/config (`Code` max 50 unique, `KeyHash` max 64 unique, `TimeZoneId` max 100, FK `Equipment` Restrict); generate migration; confirm `Down` runs.
- [ ] Register `CreatedByUserId`/`RevokedByUserId` in `Application/Services/UserReferenceRegistry.cs` (Blocks) — the model-scan test fails otherwise.
- [ ] Run: `dotnet test … --filter InstrumentAgentModelTests` and `UserReferenceModelScanTests` → PASS. Commit.

### Task 2: Agent lifecycle service + admin endpoints

**Files:** Create `Application/Services/InstrumentAgentService.cs`, `API/Controllers/InstrumentAgentController.cs`; Test `Tests/UnitTests/InstrumentAgentServiceTests.cs`; Modify `authorization-matrix.txt`.

**Interfaces — Produces:**
`Task<(InstrumentAgentDto Agent, string PlainKey)> RegisterAsync(int equipmentId, string name, string timeZoneId, int adminUserId, CancellationToken ct)`;
`Task<(InstrumentAgentDto Agent, string PlainKey)> RotateKeyAsync(int agentId, int adminUserId, CancellationToken ct)`;
`Task RevokeAsync(int agentId, string reason, int adminUserId, CancellationToken ct)`.

- [ ] Tests: key shown once and only its SHA-256 hex stored; equipment must be `EquipmentType.Hplc`, active, in a section the admin can manage (`ISectionScopeService`); invalid `TimeZoneId` rejected (`TimeZoneInfo.FindSystemTimeZoneById`); Revoke requires non-blank reason, sets `IsActive=false`, old key stops working; each action writes `RecordUserEventAsync` (category `Security`).
- [ ] Implement; key format `lims_agent_{Code}_{32 random bytes, base64url}`; endpoints under `api/instrument-agents`, permission `Equipment.Manage`.
- [ ] Run tests, update matrix, commit.

### Task 3: `AgentKey` authentication scheme + `InstrumentAgent` policy

**Files:** Create `API/Authorization/AgentKeyAuthenticationHandler.cs`, `API/Extensions/AgentAuthenticationExtensions.cs`; Modify `API/Program.cs` (call extension), `API/Extensions/AuthorizationExtensions.cs` (register policy); Test `Tests/UnitTests/AgentKeyAuthenticationTests.cs` (same host pattern as `AccessTokenRevalidationTests`).

**Interfaces — Produces:** scheme name `"AgentKey"`; header `X-Agent-Key`; policy `"InstrumentAgent"`; claims `agent_id`, `agent_code`, `equipment_id`, `section_id` (deliberately **no** `NameIdentifier`).

- [ ] Tests: valid key → 200 on a probe endpoint `[Authorize(AuthenticationSchemes="AgentKey", Policy="InstrumentAgent")]`; revoked/unknown/missing key → 401; deactivated equipment → 401; a human JWT on the probe → 401/403; an agent key on `HplcWorkspaceController` → 401; failed attempts write `SecurityAuditEvent` (code `AGENT_AUTH_FAILED`, no key material stored).
- [ ] Implement: lookup by SHA-256 of header value; update `LastSeenAt` at most once/minute; per-agent rate-limit partition in `RateLimitingExtensions.cs`.
- [ ] Run, commit.

### Task 4: Audit attribution for agents

**Files:** Modify `Persistence/DbContext/MicroLimsDbContext.cs:13` (+ `BuildAuditLog` at `:355-377`), `API/Middleware/AuditMiddleware.cs:19-24`; Test `Tests/UnitTests/AgentAuditAttributionTests.cs`.

**Interfaces — Produces:** `MicroLimsDbContext.CurrentAgentCode : string?`.

- [ ] Test: with `CurrentAgentCode="HPLC1"` and no `CurrentUserId`, every generic `AuditLog` has `UserId == null`, `ActorType == System`, `SystemProcessName == "InstrumentAgent:HPLC1"`; with neither set, behavior is unchanged (existing tests stay green); no ingest-path row ever has `UserId == 0`.
- [ ] Implement; `AuditMiddleware` sets it from claim `agent_code`.
- [ ] Run full `ArchitectureTests` + `AuditTrailRecordIdsPostgresIntegrationTests`, commit.
- **STOP:** show Mohamed the auth design (key, rotation, revoke, audit rows). Needed before any agent installs anywhere.

---

## Phase 2 — Receipt, storage, idempotency (format-agnostic)

### Task 5: Ingest entities + migration + append-only protection

**Files:** Create `Domain/Entities/InstrumentIngest.cs` (3 classes), `Domain/Enums/InstrumentIngestStatus.cs`, configurations, migration `AddInstrumentIngest`; Modify `MicroLimsDbContext.cs:309-321` (exclude `InstrumentIngestPayload`); Test `Tests/UnitTests/InstrumentIngestModelTests.cs`.

**Interfaces — Produces:**
`InstrumentIngest { Id; InstrumentAgentId; EquipmentId; string SequenceName; int InjectionNumber; DateTime AcquiredAtUtc; }` unique on `(EquipmentId, SequenceName, InjectionNumber, AcquiredAtUtc)`.
`InstrumentIngestVersion { Id; InstrumentIngestId; int VersionNo; string ReportSha256; string ReportStorageKey; string ReportFileName; InstrumentIngestStatus Status; string? StatusReason; DateTime ReceivedAt; string AcquiredAtLocalText; string TimeZoneIdUsed; int ClockOffsetSeconds; string? OperatorAsReported; int? ResolvedByUserId; DateTime? ResolvedAt; }` unique on `(InstrumentIngestId, ReportSha256)` and `(InstrumentIngestId, VersionNo)`.
`InstrumentIngestPayload { Id; InstrumentIngestVersionId; string ParserVersion; string ManifestJson; string ParsedJson; DateTime CreatedAt; }` (jsonb).

- [ ] Tests: enum names equal `["Adopted","Dismissed","PendingReview","Quarantined","Superseded"]`; updating `ReportSha256`, `ReportStorageKey`, `AcquiredAtLocalText` on a version, or any update/delete of a payload row, raises the DB exception (Postgres).
- [ ] Implement trigger migration modelled on `20260902181230_AddAuditImmutabilityTriggers.cs` (column-level for Version, whole-row for Payload); `Down` drops them.
- [ ] Run, commit.

### Task 6: `InstrumentIngestService.ReceiveAsync`

**Files:** Create `Application/Services/InstrumentIngestService.cs`, `Application/DTOs/InstrumentIngestDtos.cs`; Test `Tests/UnitTests/InstrumentIngestReceiveTests.cs` + `Tests/IntegrationTests/InstrumentIngestPostgresIntegrationTests.cs`.

**Interfaces — Consumes:** `InstrumentAgentContext(int AgentId, string Code, int EquipmentId, int SectionId, string TimeZoneId)` (built from claims in Task 7), `IFileStorageService`, `ILabClock`, `IAuditEventService`.
**Produces:** `Task<IngestReceipt> ReceiveAsync(InstrumentAgentContext agent, IngestRequest request, CancellationToken ct)` with `IngestRequest(string SequenceName, int InjectionNumber, string AcquiredAtLocalText, string ReportSha256, string ReportFileName, byte[] ReportContent, string ManifestJson, DateTime AgentClockUtc, string? OperatorAsReported)` and `IngestReceipt(int IngestId, int VersionId, int VersionNo, InstrumentIngestStatus Status, bool AlreadyReceived)`.

- [ ] Tests (each one named after its rule): `Rejects_when_server_hash_differs`; `Same_hash_returns_existing_receipt_AlreadyReceived_true`; `Concurrent_same_hash_creates_one_version` (two tasks, Postgres, uses `TrySaveChangesAsync`); `New_hash_creates_next_version_and_supersedes_pending_previous`; `Drift_over_limit_quarantines` (setting `InstrumentIngest.MaxClockDriftSeconds`, default 120, in `ConfigurationSetting`); `DST_gap_and_fold_local_times_are_flagged` (zone of the registration, e.g. a zone with a gap — expected rule: gap → Quarantined "nonexistent local time", fold → earlier instant + StatusReason noting ambiguity); `Writes_system_audit_event_with_hashes_and_never_UserId_0`; `Storage_key_contains_sha256_and_is_never_overwritten`.
- [ ] Implement; manifest must parse as JSON array of `{path, sha256, bytes}` else Quarantined; no clock reads other than `ILabClock`.
- [ ] Run unit + Postgres tests, commit.

### Task 7: `InstrumentIngestController`

**Files:** Create `API/Controllers/InstrumentIngestController.cs`; Modify `authorization-matrix.txt`; Test `Tests/UnitTests/InstrumentIngestControllerTests.cs`.

**Interfaces — Produces:** `POST api/instrument-ingest/receive` (multipart: `report`, `manifest`, fields above) → 200 `ApiResponse<IngestReceipt>` (200 for duplicates, never 409); `GET api/instrument-ingest/receipt/{reportSha256}` → agent confirms an upload; both `[Authorize(AuthenticationSchemes="AgentKey", Policy="InstrumentAgent")]`; request limit 30 MB.

- [ ] Tests: multipart round trip through the Task 3 host; oversize → 413; human token → 401; matrix test passes with the agent actions annotated as agent-scheme (adapt `AuthorizationMatrixTests.AllowedRoles` to emit `(agent)` for them).
- [ ] Run full suite on Postgres, commit.
- **STOP:** manual `curl` with a registered test agent against local LIMSV2; show the `AuditLog` rows and stored object. Phase 2 is shippable on its own (stores originals, no parsing).

---

## Phase 3 — Server-side ChemStation parsing (**blocked on Task 0 samples**)

### Task 8: `ChemStationReportParser`

**Files:** Create `Application/Services/InstrumentImport/ChemStationReportParser.cs`; Test `Tests/UnitTests/ChemStationReportParserTests.cs` over `Tests/Fixtures/ChemStation/*`.

**Interfaces — Produces:** `ChemStationReportParser.Parse(ReportText text) -> ChemStationReport { ReportTextSource Source; IReadOnlyDictionary<ChemStationField, ExtractedField> Fields; IReadOnlyList<ChemStationPeak> Peaks; IReadOnlyList<string> Problems; bool RequiresVerification }`; `ChemStationPeak(int PeakNo, string RetentionTimeText, decimal RetentionTime, string AreaText, decimal Area, string HeightText, decimal Height, string? NameAsReported)`. Reuse `ExtractedField`/`IReportTextReader` from `EmpowerReportParser.cs:29` / `ReportText.cs:27`. Field set is fixed after reading the real exports (minimum: sequence name, line/injection, vial, sample name, acquired date, operator, method).

- [ ] Tests from real fixtures (expected values copied from the printed report by Mohamed): every field and peak per fixture; `Comma_decimal_export_parses_with_detected_culture` (`1.234,56` → 1234.56); `Unparseable_number_is_a_Problem_not_zero`; text-layer vs OCR sets `RequiresVerification`.
- [ ] Implement with invariant culture + explicit separator detection; verbatim text always kept.
- [ ] Run, commit.

### Task 9: Wire parser into receive + reparse

**Files:** Modify `InstrumentIngestService.cs`; Test `InstrumentIngestReceiveTests.cs`.

- [ ] Tests: parse success → payload row with `ParserVersion="chemstation-1"` and status `PendingReview`; parse failure → `Quarantined` with `StatusReason`, original still stored; `ReparseAsync(int versionId, int userId)` inserts a *new* payload row, never updates; system audit event for both.
- [ ] Implement, run, commit.
- **STOP (live-data check):** Mohamed compares parser output to ChemStation's printed report on ≥10 real runs, including one OCR'd/odd one. Mismatch = fix parser before Phase 4.

---

## Phase 4 — Match and adopt on `HplcRun` (**blocked on Q1, Q2, Q5, Q15**)

### Task 10: `InjectionMatcher`

**Files:** Create `Application/Services/InstrumentImport/InjectionMatcher.cs`; Test `Tests/UnitTests/InjectionMatcherTests.cs`.

**Interfaces — Produces:** `Propose(HplcRun run, ChemStationReport report) -> InjectionProposal { string Target /* "SstInjection"|"SampleReplicate"|"None" */; int? TargetId; string Reason }`. Convention comes from Q2 (e.g. `{Sample.ReferenceNumber}-R{n}`, SST by label); it only proposes.

- [ ] Tests from the confirmed convention: exact match; wrong replicate number → None; sample not on this run → None with reason; two injections claim one target → both flagged.
- [ ] Implement, run, commit.

### Task 11: Provenance columns on existing HPLC entries

**Files:** Modify `Domain/Entities/HplcRun.cs` (`HplcSstInjection`, `HplcReplicateResponse`), their configs, new migration `AddIngestProvenanceToHplcEntries`; Create `Domain/Enums/HplcValueSource.cs` (`Manual=0, Ingested=1`); Test `Tests/UnitTests/HplcIngestProvenanceTests.cs`.

**Interfaces — Produces:** on both entities: `HplcValueSource ValueSource; int? IngestVersionId; string? IngestedValueText; bool EditedAfterIngest; string? EditReason;`.

- [ ] Tests: existing rows migrate to `Manual`; editing an `Ingested` value without reason throws, with reason sets `EditedAfterIngest=true` and keeps `IngestedValueText`.
- [ ] Implement in the existing entry methods of `HplcRunService.cs`, run the whole HPLC test group, commit.

### Task 12: `HplcRunService.AdoptIngestAsync` / `DismissIngestAsync`

**Files:** Modify `Application/Services/HplcRunService.cs` (or a new partial `HplcRunService.Ingest.cs`, per `SourceFileSizeTests`); Test `Tests/UnitTests/HplcAdoptIngestTests.cs`.

**Interfaces — Produces:**
`Task AdoptIngestAsync(int runId, int versionId, IReadOnlyList<AdoptMapping> mappings, string? supersedeReason, int userId, CancellationToken ct)` with `AdoptMapping(int InjectionIndex, string Target, int TargetId)`;
`Task DismissIngestAsync(int versionId, string reason, int userId, CancellationToken ct)`.

- [ ] Tests: refuses when run not `Open`, when `version.EquipmentId != run.EquipmentId`, when user lacks section access; creates `HplcEvidence` from the stored original via the same path as `UploadEvidenceAsync` (`:586`); fills responses with `ValueSource=Ingested`; version → `Adopted`; adopting a newer version over an `Adopted` one requires `supersedeReason` and supersedes the old evidence (`:629` logic); Dismiss requires reason; **`InstrumentIngestService` and the adopt path never call `HplcMethodAssayRecorder`** (architecture test in the style of `LayerDependencyTests`).
- [ ] Implement; wrap in `UnitOfWork.RunAsync`; explicit audit events.
- [ ] Run HPLC + architecture groups, commit.

### Task 13: Endpoints + frontend panel

**Files:** Modify `API/Controllers/HplcWorkspaceController.cs`, `authorization-matrix.txt`; Create/Modify under `frontend/src/` the HPLC workspace page (one "Instrument data" panel); Test controller tests + one Vitest/RTL test.

**Interfaces — Produces:** `GET api/hplc-workspace/runs/{runId}/ingest-candidates`; `POST api/hplc-workspace/runs/{runId}/ingest/{versionId}/adopt`; `POST api/hplc-workspace/ingest/{versionId}/dismiss`; writes need `Hplc.Operate`. Panel shows proposal, verbatim vs parsed value, OCR/problem flags, `OperatorAsReported` next to the run's analyst, and a diff when a typed value was changed after ingest. Reviewer view shows `EditedAfterIngest`.

- [ ] Implement; `npm test`; commit.
- **STOP (E2E):** browser test on local with a real ChemStation export: ingest → adopt → submit with signature → review. Only then Phase 5.

---

## Phase 5 — Edge agent (**blocked on Q8-Q11**)

### Task 14: Agent project

**Files:** Create `agent/MicroLIMS.InstrumentAgent/` (+ `agent/MicroLIMS.InstrumentAgent.Tests/`), not part of `MicroLIMS.sln` backend layers.

**Interfaces — Produces:** `StabilityWatcher` (event when a set is complete: configured completion marker from Q9, file sizes unchanged for N seconds, exclusive open succeeds); `ManifestBuilder.Build(string dDirectory) -> string /* JSON array {path, sha256, bytes} */`; `ArchiveCopier.Copy(string src, string archiveRoot)` (verify hashes after copy; refuses to overwrite a differing file; never deletes source); `Outbox` (SQLite; states `Pending/Sent/Confirmed/Rejected`; row removed only after `GET receipt/{sha}` confirms); `IngestClient.SendAsync(OutboxItem) -> IngestReceipt`; key read from a DPAPI-protected file.

- [ ] Tests with temp dirs: file still growing → not emitted; manifest hashes match independent hashing; copy over a differing existing file → throws; kill mid-send then restart → resends, server shows one version; 401 (revoked key) → stops sending, keeps rows, logs a loud error; no parsing code exists in the project (assembly-reference test).
- [ ] Implement; commit per component.

### Task 15: Validation pack (documents, not claims)

**Files:** Create `docs/validation/chemstation-ingest/` (URS excerpt, risk assessment, IQ/OQ test scripts for agent install and ingest, trace matrix to Tasks 1-14, change-control note for the instrument PC).

- [ ] Draft only; Mohamed/QA own approval. 
- **STOP (shadow pilot):** agent runs against the real instrument in ingest-only mode (no adoption) for an agreed number of runs; compare ingested vs manually entered; record discrepancies before enabling adoption.

---

## Phase 6 — Deferred, separate plans

Raw `.D` upload to the LIMS; mTLS (only if hosting path can forward client certs, Q14); LIMS-generated sequence files; typed-vs-ingested *blocking* policy (if Q15 says block).

## Self-review (done)

- Review findings 1-30 map: 1,2→Decisions/Task 5; 3→Task 1,2; 5→Tasks 8-9 (parser server-side); 6,7→Decisions/Task 6; 8,9,10,11→Tasks 1-3,7; 12-15→Tasks 4,5,6; 16,17,18→Tasks 5,6,12; 19→Task 10; 20,25→Tasks 11-13; 21,22→Global Constraints/Task 6; 23→Task 1; 24→Task 12; 26→Task 0; 27→Phase 6; 28-30→Decisions/Task 6.
- Names consistent: `InstrumentIngest*`, `InstrumentAgentContext`, `IngestReceipt`, `AdoptMapping`, `HplcValueSource`.
- Open-by-design: Task 8 field list, Task 10 convention, Task 14 completion marker — each is explicitly gated on a Phase 0 answer.
