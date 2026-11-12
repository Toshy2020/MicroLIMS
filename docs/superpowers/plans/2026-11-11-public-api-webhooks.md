# Public API and Webhooks (E3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Versioned read-only public API with scoped, hashed keys, customer scoping, rate limits and an access log, plus signed identifier-only webhooks delivered through the E2 outbox.

**Architecture:** `ServiceAccount` + `ServiceAccountKey` + `ServiceAccountScope`; an `ApiKey` authentication scheme; a `PublicApiScope` service applied inside every public query (resource, sections, customer); controllers under `API/Controllers/PublicV1`; webhooks as `WebhookSubscription` rows fed by event writers into E2's `IntegrationOutboxMessage` (type `Webhook`) and delivered by the same worker with an HMAC transport.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit `[PostgresFact]`; React 19, TypeScript, Vitest.

**Spec:** `docs/superpowers/specs/2026-11-11-public-api-webhooks-design.md`

## Global Constraints

- Prerequisite: E2 Tasks 1 and 3 (outbox, worker). D1, D2 for their endpoints and events; if not built, those endpoints and events are left out and listed.
- `CLAUDE.md` binds every task (anti-fake tests, triad, `[PostgresFact]`, `AsNoTracking` + projection for reads, page size clamp 1–100, `CancellationToken`, no external call inside a transaction, rate limiting partitioned by authenticated identity — here the key id).
- Routes `api/public/v1/...`; header `X-Api-Key`; key format `lims_api_{accountCode}_{32 random bytes base64url}`; stored SHA-256 hex with `[AuditRedacted]`; prefix = first 8 chars of the random part.
- Out-of-scope or undecided → 404, never 403.
- Webhook headers: `X-MicroLIMS-Event`, `X-MicroLIMS-Delivery` (event id), `X-MicroLIMS-Timestamp` (unix seconds), `X-MicroLIMS-Signature: sha256=<hex HMAC of "{timestamp}.{body}">`. Secrets stored encrypted with the B2 `ISecretProtector` (if B2 not built: ASP.NET Data Protection) — never in plain text or audit.
- Event types (frozen): `sample.decided`, `oos.opened`, `oos.concluded`, `document.effective`, `quality.opened`, `quality.closed`.
- Rate limit default 60/min per key, `ServiceAccount.RequestsPerMinute` override.
- Access log retention default 730 days (`PublicApi:AccessLogRetentionDays`), cleanup in an existing housekeeping worker pattern.
- Permission codes: `Api.Manage`, `Api.ViewAccessLog`, `Customers.Manage`.

## Review Focus

1. A list endpoint applies the customer filter after paging (leaks counts or empty pages) → `Customer_filter_is_in_the_query_and_counts_match` (Task 3).
2. Results of a sample with one approved and one undecided section → only approved section results returned → `Partially_decided_sample_returns_only_approved_sections` (Task 2).
3. Rotated key keeps working forever → `Old_key_stops_after_overlap` (Task 1).
4. Webhook for a record outside the subscriber's laboratories delivered → `Event_not_queued_for_out_of_scope_subscription` (Task 5).
5. Removing a field from a v1 DTO passes CI → `OpenApi_v1_contract_is_unchanged` (Task 2).

## File Structure

| File | Responsibility |
|---|---|
| `Domain/Entities/PublicApi/*.cs` | `ServiceAccount`, `ServiceAccountKey`, `ServiceAccountScope`, `ApiAccessLogEntry`, `WebhookSubscription`, `Customer` |
| `Domain/Entities/Sample.cs` (modify) | `CustomerId?` |
| `API/Authorization/ApiKey*.cs` | Scheme and handler |
| `Application/Services/PublicApi/*.cs` | `ServiceAccountService`, `PublicApiScope`, `PublicSampleQueries`, `PublicQualityQueries`, `PublicDocumentQueries`, `PublicKpiQueries`, `WebhookService`, `WebhookEventWriter`, `ApiAccessLogService`, `CustomerService` |
| `API/Controllers/PublicV1/*.cs` | Endpoints |
| `API/Middleware/ApiAccessLogMiddleware.cs` | Access log |
| `Infrastructure/Erp/WebhookTransport.cs` | Signed delivery |
| `backend/MicroLIMS.Tests/Contracts/public-api-v1.json` | Committed OpenAPI |
| `frontend/src/modules/publicApi/*`, `modules/customers/*` | Screens |

---

### Task 1: Accounts, keys, scopes, rate limit, access log

**Interfaces:** `ServiceAccount { Id; Code; Name; Owner; Purpose; Contact; Kind (Internal|Customer); CustomerId?; RequestsPerMinute?; IsActive }`; `ServiceAccountKey { Id; AccountId; KeyHash; KeyPrefix; CreatedAtUtc; ExpiresAtUtc?; RevokedAtUtc?; SupersededAtUtc?; ValidUntilUtc? }`; `ServiceAccountScope { AccountId; Resource (Samples|Results|Certificates|Items|Quality|Documents|DocumentFiles|Kpis); SectionId? }` (null section = all). `IssueKeyAsync(accountId, expiresAtUtc?)`, `RotateKeyAsync(keyId, overlapDays 0–7)`, `RevokeKeyAsync(keyId, reason)`. Handler sets claims: account id, kind, customer id, key id. Rate limiter policy `PublicApi` partitioned by key id. `ApiAccessLogMiddleware` (scoped services in `InvokeAsync`) writes after the response: account, key prefix, path, query (allow-listed parameters), status, result count (from an `HttpContext.Items` value set by controllers), duration, time. Customer accounts must have exactly `CustomerId` set and may only hold Samples, Results, Certificates, Items (DocumentFiles only if explicitly allowed).

- [ ] **Step 1:** Failing tests: key shown once, hash stored, redacted; `Old_key_stops_after_overlap`; expired and revoked keys 401 with security event; customer account with Quality scope refused at save; rate limit 429 after the per-key limit with `Retry-After`; access log row per call without the key value; audit of account changes.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(api): service accounts, keys, scopes, rate limit and access log`.

### Task 2: Samples, results, certificates, items

**Interfaces:** `GET samples?changedSince&status&itemCode&batch&page&pageSize`, `GET samples/{number}`, `GET samples/{number}/results`, `GET samples/{number}/certificate` (archived PDF from `ArchivedRecord` with `X-Content-SHA256`; logged in `DataExportLog`), `GET items?changedSince`. DTOs in `Application/DTOs/PublicV1/` (separate from internal DTOs). Results projected from the approved-result path the sample report uses; sections not approved omitted. `PublicApiScope.ApplySamples(IQueryable<Sample>, ApiPrincipal)` adds resource, section and customer predicates **before** count and paging. Committed OpenAPI: a test generates the v1 document (Swashbuckle document filter limited to `api/public/v1`) and compares to `Contracts/public-api-v1.json`, failing on removed or renamed members (additions allowed, then the file is updated).

- [ ] **Step 1:** Failing tests: content per endpoint; `changedSince` boundary; scope by resource and section (404); `Partially_decided_sample_returns_only_approved_sections`; invalidated results never returned; certificate hash header and export log; `OpenApi_v1_contract_is_unchanged`.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(api): public v1 samples, results, certificates and items`.

### Task 3: Customers

**Files:** `Customer { Id; Code (unique); Name; IsActive }`, `CustomerService`, `Sample.CustomerId`, receiving request and `ExpectedSample` (E2) gain optional `CustomerId`; correction before decision with reason; after decision through E2's `ErpCorrectionGate` pattern (QA approval) — reuse the gate generalised to `PostDecisionCorrectionGate` if E2 is built, else create it here; setting `Certificates:ShowCustomer` (false); report and certificate mapper changes.

- [ ] **Step 1:** Failing tests: customer key sees only its samples (`Customer_filter_is_in_the_query_and_counts_match`), only decided ones; another customer's sample 404; customer set at receipt audited; change after decision without QA approval refused; certificate shows customer only when the setting is on.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(api): customers and customer scope`.

### Task 4: Quality records, documents, KPIs

**Interfaces:** `GET oos-investigations`, `GET quality-records` (type filter), `GET documents` (effective only), `GET documents/{code}/file` (scope `DocumentFiles`; logged), `GET kpis?month&sectionId` (from the existing KPI services, same numbers as the dashboard). No free-text fields in quality DTOs.

- [ ] **Step 1:** Failing tests: content and no free text; scope by section; customer keys 404 on all four; KPI values equal the dashboard service for a fixed data set; document file download logged.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(api): quality, documents and KPI endpoints`.

### Task 5: Webhooks

**Interfaces:** `WebhookSubscription { Id; AccountId; Url (https only); Events[]; SecretProtected; IsActive; PausedAtUtc?; ConsecutiveFailuresSinceUtc? }`; `WebhookService.CreateAsync` returns the secret once; `WebhookEventWriter.WriteAsync(string eventType, string recordType, int recordId, string recordNumber, int? sectionId, int? customerId, CancellationToken ct)` called in the same transaction as: `SampleApprovalService.FinalizeIfClosedAsync`, D1 open / conclude, `DocumentEffectiveDateService` revision effective, D2 open / close; it adds one outbox row per matching active subscription whose scopes cover the record. `WebhookTransport` signs and posts; 2xx = delivered; pause when failing continuously for 24 hours and notify owner contact (email via existing email service, after commit) and administrators; resend from the monitor.

- [ ] **Step 1:** Failing tests: event written with the change and absent after rollback; `Event_not_queued_for_out_of_scope_subscription`; customer subscription only for its decided samples; signature verifies with the secret and timestamp; body contains only the allowed fields; pause after 24 hours of failures; resend; secret never in DB plain text or audit.
- [ ] **Step 2:** FAIL → implement → PASS. Commit `feat(api): signed webhooks`.

### Task 6: Screens and documents

**Files:** `frontend/src/modules/publicApi/ServiceAccountsPage.tsx`, `KeysDialog.tsx`, `ScopesEditor.tsx`, `WebhooksPage.tsx`, `AccessLogPage.tsx`; `modules/customers/CustomersPage.tsx`; customer field on receiving and expected-sample forms; `documents/Interfaces/Public-API-v1-Guide.md` (authentication, scopes, paging, `changedSince`, errors, webhooks and signature verification example, versioning policy); tests

- [ ] **Step 1:** Failing tests (`userEvent`, `services/` mocked): key shown once and then masked; scope editor prevents quality scopes for customer accounts; webhook secret shown once; access log filters and export; customer field saved on receipt.
- [ ] **Step 2:** FAIL → implement → full checks PASS. Commit `feat(ui): public API administration and customers`.
- [ ] **Step 3:** Full verification per `CLAUDE.md`; report counts.

## Cross-plan corrections (whole-programme review, 2026-11-11)

These corrections come from reviewing all plans together against the code. Where they differ from the text above, they win. Shared names are defined once in `docs/superpowers/plans/2026-11-11-build-order.md` (section "Programme conventions").

1. Service accounts are E2's `IntegrationClient` with `Kind = ServiceAccount` plus `ServiceAccountScope` rows; there is no separate `ServiceAccount` table or `ApiKey` scheme. Header `X-Api-Key` (E2's `ClientKey` scheme).
2. Webhooks use E2's shared outbox with `Channel = Webhook`, `OrderingKey = subscription id`; the 24-hour pause is a webhook rule applied by the transport, independent of the ERP 'Failed after N attempts' rule.
3. Use `PostDecisionCorrectionGate` from E2 (do not create it here).
4. Grant rows: `Api.Manage` → System Administrator; `Api.ViewAccessLog` → System Administrator and QA; `Customers.Manage` → Section Head and System Administrator. Migration `AddPublicApi`.

## Review corrections (independent review, 2026-11-11)

From the independent review of the build order. These win over the text above and over the earlier corrections section where they differ.

1. Accounts: keys are E2's `IntegrationClientKey` rows (several per account, overlap rotation, expiry). E3's account field is named `AccountType (Internal|Customer)` with `CustomerId?` and `RequestsPerMinute?`, stored on `IntegrationClient` (separate from `Kind`).
2. **`ApiAccessLogEntry` is kept out of automatic audit capture** (user decision 2026-11-11); account, key, scope and webhook configuration stay audited.
3. Customer changes after approval: normal rules (Section Head, reason); only SAP-linked samples also pass E2's `PostDecisionCorrectionGate`.

**Translation (build order 2.7):** if F4 Task 4 is merged when this package is built, every new screen uses translation keys and every new refusal carries a message code.
