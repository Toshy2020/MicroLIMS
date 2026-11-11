# Instrument Connectivity (E1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Instrument readings with provenance for balances and pH meters (browser and agent routes), source marking on every connected field, balance checks and minimum weight, configurable formats, and export-file adoption for chromatography and ICP.

**Architecture:** An append-only `InstrumentReading` store fed by two routes (browser posts raw serial frames; agent posts raw frames and files with its `AgentKey`). Server-side `IFrameParser` implementations (MT-SICS, SBI, configured regex) and `IExportFileParser` implementations (configured CSV/XML mappings) read the data. A shared `CapturedValueService` validates a reading or a typed value for a field and returns a `ValueProvenance` that each existing recorder stores beside the number. The agent is a new .NET Worker Service in `agent/`.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; .NET Worker Service + SQLite (agent); React 19, TypeScript, Web Serial API, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-instrument-connectivity-design.md`

## Global Constraints

- `CLAUDE.md` binds every task (anti-fake tests, triad, `[PostgresFact]` must run, `TimeProvider` / `ILabClock`, `CancellationToken`, one migration per schema change + `has-pending-model-changes`, central package versions, no new `any`, `userEvent`, no external effects in transactions).
- Reuse, do not duplicate, the built agent server side: `InstrumentAgent` entity, `InstrumentAgentService` (`RegisterAsync`, `RotateKeyAsync`, `RevokeAsync`), `AgentKeyAuthenticationHandler`, `InstrumentIngestService`, `IntegrationsService.ListInstrumentAgentsAsync`, `frontend/src/modules/admin/IntegrationsPage.tsx`. Follow the ChemStation plan's decisions (header of `docs/superpowers/plans/2026-10-16-chemstation-ingest.md`) where they apply: system audit events with `SystemProcessName = "InstrumentAgent:{Code}"`, hash-keyed storage, statuses persisted as int with frozen names, verbatim text beside each parsed number, `HasPrecision(28,10)`, invariant-culture parsing.
- Parsing happens only on the server. The browser and the agent send raw bytes.
- No rounding on capture. Unit conversion only g↔mg↔µg by exact powers of ten; anything else refused.
- `ValueSource { Manual = 1, Instrument = 2, InstrumentOcr = 3 }`; manual reason 10–1000 characters.
- Calibration rule for balances and pH meters: same as `IncubatorEligibilityService` line 38, via the `EquipmentCalibration.IsCurrent` helper (introduced in D3 Task 5; create it here if D3 is not built).
- Agent: Windows service, outbound HTTPS only, key stored DPAPI-protected, SQLite outbox, never deletes source files.
- New permission codes: `Instruments.Configure` (System Administrator), `Instruments.Read` (Analyst, Section Head, Reviewer, QC Manager, QA), `Instruments.ManageChecks` (Section Head).

## Review Focus

1. One reading used for two weights (double click, two tabs) → `Reading_used_once_under_concurrency` (Task 2): unique index on the consuming link.
2. Comma-decimal or padded frames (`"S S      12,3456 g"`) → parsed by invariant rules or refused, never ×10⁴ → `Frame_with_comma_decimal_refused_or_parsed_exactly` (Task 3).
3. A typed weight bypasses the balance checks → `Typed_weight_on_unchecked_balance_refused` (Task 4).
4. Laboratory day boundary for the daily check uses UTC → `Daily_check_uses_lab_date` (Task 4).
5. Agent restarts mid-send of a file → `Outbox_resends_after_restart_and_server_keeps_one_copy` (Task 7).

## File Structure

| File | Responsibility |
|---|---|
| `Domain/Entities/Instruments/InstrumentReading.cs` | `InstrumentReading`, `InstrumentReadingUse` |
| `Domain/Entities/Instruments/InstrumentFormat.cs` | `InstrumentFormat`, `InstrumentFormatVersion` |
| `Domain/Entities/Instruments/BalanceCheck.cs` | `BalanceCheckConfig`, `BalanceCheckWeight`, `BalanceCheckRecord`, `BalanceCheckResult` |
| `Domain/ValueObjects/ValueProvenance.cs` | Owned type stored beside each connected number |
| `Application/Services/Instruments/FrameParsers/*.cs` | `IFrameParser`, `MtSicsFrameParser`, `SbiFrameParser`, `PatternFrameParser` |
| `Application/Services/Instruments/ExportParsers/*.cs` | `IExportFileParser`, `CsvMappedExportParser`, `XmlMappedExportParser` |
| `Application/Services/Instruments/InstrumentReadingService.cs`, `CapturedValueService.cs`, `BalanceCheckService.cs`, `InstrumentFormatService.cs`, `PendingInstrumentDataService.cs`, `InstrumentHealthService.cs` | Services |
| `API/Controllers/Instruments/*.cs` | Endpoints |
| `agent/MicroLIMS.Agent/*` | Worker Service |
| `frontend/src/services/instrumentService.ts`, `frontend/src/services/serialPort.ts`, `frontend/src/components/instruments/*` | Browser route, capture field, badges |

---

### Task 1: Instruments, formats, permissions

**Files:** `InstrumentFormat*`, migration, `PermissionConstants.cs`, `DbSeeder.cs`, `InstrumentAgentService.RegisterAsync` (lift the `EquipmentType.Hplc`-only rule to `{ Hplc, Balance, PhMeter, IcpOes, Gc }`; one agent may serve several equipment records → new `InstrumentAgentEquipment` join; keep `InstrumentAgent.EquipmentId` as the primary for existing rows), `Equipment` (`InstrumentFormatId?`, `MinimumWeight?`, `MinimumWeightUnit?`), `InstrumentFormatService`, controller; Test `IntegrationTests/InstrumentFormatPostgresTests.cs`

**Interfaces:** `InstrumentFormat { Id; Name; Kind (Frame | ExportCsv | ExportXml); IsActive }`; `InstrumentFormatVersion { Id; FormatId; VersionNo; ParserKey ("mt-sics" | "sbi" | "pattern" | "csv-map" | "xml-map"); ConfigJson; CreatedByUserId; CreatedAtUtc }` (append-only). `TestAsync(FormatDraft, IReadOnlyList<byte[]> samples)` → per sample parsed fields or error. `SaveAsync` creates a new version. Seed formats "Mettler Toledo MT-SICS" and "Sartorius SBI" (built-in parser keys, empty config).

- [ ] **Step 1:** Failing tests: register agent for balance and pH meter; agent serving two instruments; non-configurator refused; format save creates version 2 and keeps version 1; tester returns parsed fields and errors; audit rows.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(instruments): instrument types, multi-instrument agents and formats`.

### Task 2: Readings and the browser route

**Files:** `InstrumentReading*`, `InstrumentReadingService.cs`, `InstrumentReadingsController.cs` (`POST api/instrument-readings/frame` user-authenticated; `GET api/instrument-readings?equipmentId=&take=`), migration with append-only trigger on `InstrumentReadings` and `InstrumentReadingUses`; Test `IntegrationTests/InstrumentReadingPostgresTests.cs`

**Interfaces:**
- `InstrumentReading { Id; EquipmentId; Kind (Weight | Ph | Other); Route (Browser | Agent); AgentId?; SubmittedByUserId?; RawFrame (bytea); RawSha256; FormatVersionId; ParseStatus (Ok | Unstable | Unreadable | Overload | Underload | DeviceError); ValueText?; Value? (28,10); Unit?; InstrumentTimeText?; ReceivedAtUtc }`.
- `InstrumentReadingUse { ReadingId (unique); TargetEntity; TargetId; FieldKey; UsedByUserId; UsedAtUtc }`.
- `Task<InstrumentReadingDto> SubmitFrameAsync(int equipmentId, byte[] frame, InstrumentRoute route, int? userId, int? agentId, CancellationToken ct)` — section membership for user route; equipment has a format; parses with the format's current version; stores even when unreadable.

- [ ] **Step 1:** Failing tests: frame stored with hash, route, format version; unreadable frame stored with status; user of another laboratory refused; list newest first limited to 20; update and delete rejected by trigger; `Reading_used_once_under_concurrency` (two connections insert a use for the same reading → one fails).
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(instruments): readings and browser frame submission`.

### Task 3: Frame parsers

**Files:** `FrameParsers/*.cs`; Test `UnitTests/FrameParserTests.cs`

**Interfaces:** `FrameParseResult Parse(ReadOnlySpan<byte> frame, JsonElement config)` → `{ Status, ValueText, Value, Unit, Stable }`.
- MT-SICS: responses `S S <value> <unit>` (stable), `S D …` (dynamic → Unstable), `S I` (busy → Unstable), `S +` (Overload), `S -` (Underload), `ES`/`ET`/`EL` (DeviceError); also `SI` responses. Field separation by whitespace; value with optional sign.
- SBI: print line `<sign><value> <unit>` with optional identifier prefix and stability marked by unit presence per Sartorius documentation; read the official protocol description before coding (source-driven: cite the manual section in a comment) — do not infer from examples alone.
- Pattern: config `{ "regex": "...(?<value>...)...(?<unit>...)", "stableGroup"?: "...", "stableValue"?: "..." }`; timeout 100 ms; value parsed with `CultureInfo.InvariantCulture`; a comma in the value → Unreadable unless config sets `"decimalComma": true`.
- pH frames are read through the pattern parser (unit `pH`).

- [ ] **Step 1:** Failing tests: each MT-SICS case; SBI stable / unstable / negative; pattern parser with named groups; `Frame_with_comma_decimal_refused_or_parsed_exactly`; regex timeout → Unreadable; trailing CR/LF and padding.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(instruments): frame parsers`.

### Task 4: Captured values, balance checks, minimum weight

**Files:** `ValueProvenance.cs` (owned type: `Source`, `ReadingId?`, `EquipmentId?`, `Route?`, `ManualReason?`), `CapturedValueService.cs`, `BalanceCheck*`, `BalanceCheckService.cs`, controller; Test `IntegrationTests/CapturedValuePostgresTests.cs`, `IntegrationTests/BalanceCheckPostgresTests.cs`

**Interfaces:**
- Request shape used by every connected field: `CapturedValueInput { decimal? Value; string? Unit; int? ReadingId; int? EquipmentId; string? ManualReason }`.
- `Task<(decimal Value, ValueProvenance Provenance)> ResolveAsync(CapturedValueInput input, CapturedFieldSpec spec, string targetEntity, int targetId, string fieldKey, int userId, CancellationToken ct)` — `CapturedFieldSpec { Kind; TargetUnit; SectionId }`. Field connected = an active equipment of `spec.Kind` type in the section has a format. Connected + reading: checks of spec 5.1 and 5.7, converts unit, writes `InstrumentReadingUse`. Connected + typed: `EquipmentId` and `ManualReason` required, checks of 5.7 apply. Not connected: value as today, `Source = Manual`, no reason required.
- `BalanceCheckConfig : IVersionedEntity { EquipmentId (unique); DailyCheckEnabled; Weights[] }`, `BalanceCheckWeight { Nominal; Unit; LimitType (Absolute | Percent); Limit }`; `BalanceCheckRecord { EquipmentId; LabDate; PerformedByUserId; SignatureId; Passed; Results[] { WeightId, Value, Provenance, Passed } }`.
- `RecordCheckAsync(equipmentId, results[], password, userId)` — pass when `|value − nominal| ≤ limit` (Percent: `limit × nominal / 100`), all weights required; failed → notify Section Head after commit.
- `EnsureBalanceUsableAsync(equipmentId, decimal valueInBalanceUnit, ct)` — enabled and no passed record for `LabToday` → `"Balance {code} has no passed check today."`; below `MinimumWeight` → minimum weight message.
- Config and minimum weight edited by `Instruments.ManageChecks`, section configure scope.

- [ ] **Step 1:** Failing tests: reading path stores provenance and use; typed path requires equipment and reason on connected fields; not-connected path unchanged; unstable / unreadable / used / other-section / wrong-type / calibration expired refused; unit conversion g→mg exact and pH→g refused; `Typed_weight_on_unchecked_balance_refused`; check pass and fail (absolute and percent at the boundary); `Daily_check_uses_lab_date`; minimum weight refuses captured and typed; config audited.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(instruments): captured values, balance checks and minimum weight`.

### Task 5: Connect the fields

**Files:** for each field of spec 5.2 — `HplcRunService.Samples.cs` (`HplcReplicateInput.ActualWeightMg`, standard weight on `HplcRun`), `IcpRunService.Samples.cs` (`SaveIcpSampleEntryRequest.ActualWeightG`), `TitrationRecorder.cs` (`TitrationStandardInput.WeightMg`, `TitrationReplicateInput.SampleWeightMg`), `TestWorkflowContracts.cs:147` (`UvReplicateInput.WeightG`) and weight-variation payloads (`:205-218`), `MediaPreparationService.cs:16` (`TotalWeight`, `Ph`), `SolutionPreparationService.cs:19-20` (`MeasuredPh`); each entity gains an owned `ValueProvenance` per connected field (migration); request DTOs gain an optional `CapturedValueInput` beside the existing number (old clients keep working as Manual on non-connected fields; on connected fields a bare number is refused with "Select the balance and give a reason, or use a reading.").

- [ ] **Step 1:** Failing tests, one per field: reading path stores value and provenance; typed path on a connected field needs reason; bare number on a connected field refused; non-connected field unchanged; audit row contains provenance. Existing suites for these services pass unchanged with no instruments configured.
- [ ] **Step 2:** FAIL → implement → PASS; no pending model changes. Commit `feat(instruments): provenance on connected weight and pH fields`.

### Task 6: Browser route and capture UI

**Files:** `frontend/src/services/serialPort.ts` (thin wrapper over `navigator.serial`: `requestPort`, `open(baudRate, dataBits, parity, stopBits)`, `onFrame(cb)` splitting on CR/LF, `write(bytes)`; port settings from the equipment's format config), `services/instrumentService.ts`, `components/instruments/CapturedValueField.tsx` (balance/meter select, Read button, last readings list, typed value with reason, unstable message), `SourceBadge.tsx`, `BalanceCheckDialog.tsx`; wire into the HPLC, ICP, titration, UV, weight variation, media and solution screens; review screen badges and count of manual values; tests

- [ ] **Step 1:** Failing tests (`userEvent`; mock `services/serialPort` and `services/instrumentService` only): frame from the port is posted raw and the parsed value fills the field; unstable reading shows the message and is not used; typed value asks for balance and reason; refusal text from the server shown beside the field; balance check dialog blocks first use until passed; review shows badges and the manual count; browsers without Web Serial show "Connect through the agent or type with a reason".
- [ ] **Step 2:** FAIL → implement → lint, types, tests, debt check, build PASS. Commit `feat(ui): instrument capture in the browser`.

### Task 7: The agent

**Files:** `agent/MicroLIMS.Agent/MicroLIMS.Agent.csproj` (Worker Service; package versions in `backend/Directory.Packages.props` or a sibling `agent/Directory.Packages.props`), `SerialFrameListener.cs` (System.IO.Ports), `TcpFrameListener.cs`, `FolderCourier.cs` (stability watcher, manifest, archive copy — interfaces as ChemStation plan Task 14), `Outbox.cs` (SQLite; Pending / Sent / Confirmed / Rejected), `IngestClient.cs`, `Heartbeat.cs`, `agentsettings.json` (instruments: equipment code, port or host, format id; folders); server: `POST api/instrument-ingest/frame` and `POST api/instrument-ingest/heartbeat` (AgentKey scheme) in `InstrumentIngestController`, `InstrumentHealthService` (Online / Late / Offline, threshold setting default 5 minutes), Integrations page columns and notification on Late/Offline; `agent/MicroLIMS.Agent.Tests`; CI job building and testing the agent on `windows-latest`; Test also `IntegrationTests/AgentFramePostgresTests.cs`

- [ ] **Step 1:** Failing tests: agent unit — frame read and queued; `Outbox_resends_after_restart_and_server_keeps_one_copy`; partial file not sent; revoked key stops sending and keeps data; heartbeat interval. Server — frame from an agent for an instrument not registered to it refused; heartbeat updates health; Late and Offline computed from lab clock; notification once per state change.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(agent): instrument agent for frames, files and heartbeat`.

### Task 8: Export files, pending data, adoption

**Files:** `ExportParsers/*.cs`, `PendingInstrumentDataService.cs` (wire parsing into `InstrumentIngestService` for files whose agent folder has a format: `ParserVersion` = format version id, `ParsedJson` = rows), `InjectionMatcher` (proposals per spec 5.8), adoption methods on `HplcRunService` and `IcpRunService` that record through the existing entry paths with `ValueProvenance { Source = Instrument }` and the ingest version id, plus `InstrumentOcr` marking on `IcpRunService.ReportValues` adoption; frontend "Instrument data" panel on the HPLC and ICP run pages; Tests `IntegrationTests/ExportAdoptionPostgresTests.cs`, `UnitTests/ExportParserTests.cs`

**Interfaces:** `ExportParseResult Parse(byte[] file, JsonElement config)` → rows `{ SampleName, Batch?, TestOrderNumber?, Analyte, ResultText, Result?, Unit? }`; CSV map config `{ delimiter, headerRow, columns: { sampleName, batch?, testOrder?, analyte, result, unit? }, decimalComma }`; XML map config uses XPath per field. `ProposeAsync(ingestVersionId, runId)` → rows with candidate test orders (reference number in sample name, or batch equality; test order number narrows). `AdoptAsync(runId, ingestVersionId, confirmations[{ rowIndex, testOrderId, replicateNo }], password, userId)` — analyst confirms each row; runs through existing recorder; ingest status `Adopted`; nothing adopted without confirmation.

- [ ] **Step 1:** Failing tests: CSV and XML parsing with comma decimals and missing columns; proposals by reference number, by batch, narrowed by test order; ambiguous batch → several candidates; adoption records values with provenance and ingest link; unconfirmed rows not adopted; adoption on a closed run refused; equipment of the run ≠ agent's equipment refused (ChemStation review rule); OCR-adopted ICP values marked `InstrumentOcr`.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(instruments): export file parsing and adoption`.

### Task 9: Documents and verification

- [ ] **Step 1:** ChemStation plan: mark its Phase 1–2 as built, Phase 5 superseded by this plan's Task 7 (keep its open questions Q8–Q11 for the ChemStation folder settings), Phases 3–4 to run after its Phase 0 answers using this plan's formats and adoption.
- [ ] **Step 2:** `documents/` (if A1 exists): FS entries, risk rows, tags; instrument setup guide (`documents/UserManual/reference/` new chapter: registering an agent, installing it, formats, balance checks); SOP template note on typed values and balance checks.
- [ ] **Step 3:** Full verification per `CLAUDE.md` section 2 with PostgreSQL; agent tests on Windows CI; frontend checks. Report exact counts and that the hosting plan must not sleep for production use.
