# E3 — Public API and Webhooks: Design Specification

**Roadmap package:** E3 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11
**Builds on:** E2 (integration clients, outbox and delivery worker).

## 1. Purpose

Let in-house systems, reporting tools and customers read approved laboratory information through a stable, versioned, read-only API with scoped keys, and be told by webhooks when something they care about happens.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Users | Other in-house systems, in-house reporting / BI tools, and customers or contract givers |
| 2 | Writing | Read-only |
| 3 | Webhook events | Sample approved / rejected; OOS investigation opened / concluded; document revision effective; deviation or CAPA opened / closed |
| 4 | Webhook content | Identifiers only; details are read through the API with the receiver's own key |

## 3. Out of scope

- Writing through the public API (samples, results, decisions). Expected samples come only through the ERP interface (E2).
- A customer web portal. Customers use the API through their own systems.
- Unapproved or in-progress data for customers.
- OAuth client-credentials; keys are used (section 5.2).

## 4. Current state (from code, not run)

- The interactive API documentation is switched on in development only.
- Machine access exists for instrument agents (and, after E2, integration clients) with hashed keys. No general API key, no scopes, no webhooks.
- Rate limits exist for sign-in, token refresh, client errors and agents.
- **There is no customer in the data model.** Samples do not record who they were tested for.

## 5. Design

### 5.1 API shape

- A separate, versioned route: `api/public/v1/…`. The internal API used by the screens stays free to change; the public one changes only by adding fields or a new version, with at least 6 months' notice before an old version is switched off.
- Read endpoints:

| Resource | Content |
|---|---|
| Samples | Number, category, item code and name, batch, control number, received and decided dates, status, decision, customer (if any) |
| Results | Per sample: reported value as reported, unit, specification text, conformance, test name; only for **approved** sections |
| Certificates | The archived certificate PDF of a decided sample, with its hash |
| Items | Code, name, category, active |
| OOS investigations | Number, sample, tests, status, phase, conclusion (no free-text notes) |
| Deviations, CAPAs | Number, type, status, classification, laboratory, dates (no free text) |
| Documents | Effective controlled documents: code, title, revision, effective date; the controlled PDF when the scope allows |
| KPIs | The values shown on the KPI dashboard, by month and laboratory |

- Lists are paged (at most 100 per page) and filtered by change time (`changedSince`) so a reporting tool can read only what changed.
- Every response gives times in UTC with the time zone stated.
- The specification and every endpoint are published as an OpenAPI document reachable with a valid key in production.

### 5.2 Service accounts and keys

- An administrator creates a **service account** (name, owner, purpose, contact) and issues one or more keys. A key is shown once, stored hashed, can be rotated (old key valid for a chosen overlap of up to 7 days) and revoked, and can have an expiry date.
- Each service account has **scopes**: which resources (samples, results, certificates, items, quality records, documents, KPIs), which laboratories, and — for customers — exactly one customer.
- A **customer** account only ever sees samples marked for that customer, and only decided samples (results and certificates); it never sees quality records, documents (unless explicitly allowed), KPIs or other customers' data. This is enforced on the server in the query, not by filtering afterwards.
- Accounts and keys are configuration and audited; key values never appear in the audit trail.

### 5.3 Customers

- New master data: **customer** (code, name, active), maintained by the Section Head or administrator.
- A sample can be marked for a customer at receipt (optional field on the receiving form and on expected samples). The mark is audited and can be corrected with a reason before the sample is decided; after the decision only with QA approval, like other post-decision corrections.
- The customer appears on the sample report; whether it is printed on the certificate is a setting (off by default).

### 5.4 Webhooks

- A service account can register webhook subscriptions: event types, a target HTTPS address, and a secret used to sign each delivery (HMAC-SHA256 of the body, with a timestamp header to prevent replay).
- Event message: event type, event id, time, record type and number, record id. Nothing else.
- A subscription only receives events for records its account can read (same scopes).
- Events are written to the outbox in the same transaction as the change (E2's outbox) and delivered after commit with the same retry schedule; a subscription failing for 24 hours is paused and its owner contact and the administrator are told.
- The monitoring page of E2 shows webhook deliveries; an administrator can resend an event.

### 5.5 Limits and logging

- Rate limit per key (default 60 requests per minute, configurable per account), answered with "too many requests" and a retry time.
- **Access log:** every public API call is recorded with account, key prefix, endpoint, parameters (without secrets), result count, status and time. Kept for a configurable period (default 2 years). Viewable and exportable by the administrator and QA. Certificate and document downloads are also recorded in the existing data-export log.

### 5.6 Permissions

| Permission | Holders | Allows |
|---|---|---|
| `Api.Manage` | System Administrator | Service accounts, keys, scopes, webhooks |
| `Api.ViewAccessLog` | System Administrator, QA | Access log |
| `Customers.Manage` | Section Head, System Administrator | Customer master data |

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Missing, expired, revoked key | 401; recorded as a security event |
| Out-of-scope resource | 404 (not 403), so existence is not revealed |
| Too many requests | 429 with retry time |
| Undecided sample requested by a customer key | 404 |
| Webhook target unreachable | Retries; paused after 24 hours; owner told |

## 7. Testing

- **Backend `[PostgresFact]`:** each endpoint's content and paging; `changedSince`; scope by resource, laboratory and customer (another customer's sample → 404; undecided → 404; quality records → 404 for customers); key rotation overlap; expiry; revocation; rate limit per key; access log entries; webhook event written with the change and not on rollback; signature and timestamp; only scoped events delivered; pause after 24 hours; customer mark audited and post-decision correction gated.
- **Contract test:** the OpenAPI document of v1 is committed; CI fails when a field is removed or renamed.
- **Frontend (`userEvent`):** service account screens; key shown once; scopes; webhooks; access log; customer field on receiving.

## 8. Delivery stages

1. Service accounts, keys, scopes, rate limit, access log.
2. Read endpoints: samples, results, certificates, items.
3. Customers and customer scope.
4. Quality records, documents, KPIs.
5. Webhooks.
6. Documents: API guide for consumers, requirements, SOP note on granting external access.

## 9. Decisions recorded 2026-11-11

1. A customer list and an optional customer on each sample at receipt are added.
2. The customer is not printed on the certificate unless the setting is switched on.
3. The access log is kept for 2 years by default.
