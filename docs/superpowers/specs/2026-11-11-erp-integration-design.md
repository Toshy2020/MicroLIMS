# E2 — ERP (SAP QM) Integration: Design Specification

**Roadmap package:** E2 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11

## 1. Purpose

Receive inspection work from SAP so the laboratory does not retype it, and send the signed decision, the certificate and the results back, reliably and without ever calling SAP from inside a database transaction.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | ERP | SAP with the QM module |
| 2 | Transport | Not known yet: build both REST and files, chosen per site |
| 3 | Inbound | Raw and packaging material receipts; finished product batches; item master data (code and name) |
| 4 | Outbound | Usage decision after final approval; certificate PDF; result values |
| 5 | Usage decision | MicroLIMS sets it: the signed approval or rejection becomes the inspection lot's usage decision |
| 6 | Unknown material codes | Created as inactive items; a Section Head completes tests and specifications and activates them |
| 7 | Corrections after sending | Blocked and alerted: correcting or voiding a sample whose decision was sent needs QA approval, and QA corrects SAP by hand. Nothing automatic |
| 8 | Inspection lot number | Stored and shown on screens; not printed on the certificate |

Roadmap decision D-8 (SAP QM connector) is answered by 1 and 2: a neutral message format with a configurable mapping, delivered by REST or files; a direct SAP connector (IDoc / BAPI) is not built. The SAP side (or its middleware) maps the neutral messages.

## 3. Out of scope

- Direct SAP IDoc or BAPI calls, and SAP middleware configuration.
- Specifications, tests and limits from SAP. They stay in MicroLIMS (configuration drives behaviour).
- Stock movements, goods receipts and material consumption in SAP.
- Automatic correction or reversal of a usage decision.
- Planning data (dates, priorities) beyond what an inspection lot carries.

## 4. Current state (from code, not run)

- No outbox, inbox, ERP or webhook code exists. The Integrations page covers instrument agents, email and storage.
- A sample is created directly as "Received" by the receiving screen. There is no "expected" sample, and no link from a material lot in inventory to a sample.
- Items have a unique code and a name; no external code.
- A sample's final decision completes in one place on the server, which also archives the certificate PDF with its hash.
- No hook runs after a transaction commits; there is no event mechanism.
- Machine clients can authenticate only as instrument agents.

## 5. Design

### 5.1 Messages and mapping

Neutral messages, JSON for REST and XML or CSV for files, each with a message id, type, time and version:

| Direction | Message | Main content |
|---|---|---|
| In | Material master | SAP material number, name, category (raw material, packaging, finished product), plant |
| In | Inspection lot | Inspection lot number, origin (goods receipt or production), material number, batch, plant, quantity and unit, supplier and supplier batch (receipts), manufacture and expiry dates |
| In | Inspection lot cancelled | Inspection lot number, reason |
| Out | Usage decision | Inspection lot number, decision code, decided by (name), decided at (UTC), MicroLIMS sample number |
| Out | Results | Per reported result: inspection characteristic, value as reported, unit, specification text, conformance |
| Out | Certificate | The archived certificate PDF, its file name and hash |
| Out | Receipt status | Accepted / rejected for each inbound message, with reasons |

- **Mapping, configured by the administrator:** SAP plant → MicroLIMS laboratory section(s); material category → sample category; decision (approved / rejected) → SAP usage decision code; MicroLIMS test (and parameter) → SAP inspection characteristic. Unmapped results are not sent, and the screen lists them.

### 5.2 Transports

- **REST in:** SAP or its middleware posts messages to MicroLIMS with an integration client key (5.6). The answer is immediate acceptance or rejection with reasons.
- **REST out:** MicroLIMS posts messages to a configured SAP endpoint with configured authentication (secret kept in server configuration, never in the database or audit trail).
- **Files:** inbound files placed in a shared folder on site are carried by the instrument agent (package E1) to MicroLIMS; outbound messages are fetched by the agent and written to an outbound folder. Nothing on site needs to reach the cloud except the agent.
- Each site chooses REST or files per direction.

### 5.3 Inbound processing

- Every inbound message is stored first (inbox) with its message id; the same id received again is answered with the earlier result and processed once.
- **Material master:** an unknown material number creates an **inactive** item with the code and name and its SAP number as the external code; a known one updates the name. Tests and specifications are never touched. The Section Heads of the mapped laboratories see a list of new items to complete.
- **Inspection lot:** creates an **expected sample**: inspection lot number, item, batch, quantities, dates, supplier details, laboratory sections. Refused (with the reason sent back) when the material is unknown or its item is inactive, or the plant is not mapped; the message stays in the inbox and can be reprocessed after the item is completed.
- **Cancelled lot:** an expected sample not yet received is cancelled; a received sample is not changed and the Section Head is alerted.

### 5.4 Receiving an expected sample

- The receiving screen shows expected samples for the user's laboratories. Receiving one opens the normal receiving form pre-filled from the lot (item, batch, control number, dates); the analyst completes what only the laboratory knows (sampled by, sample quantity, cause of testing) and receives it as today.
- The sample keeps the inspection lot number and the link to the lot. Every rule of receiving applies unchanged.
- A sample can still be received without an expected sample, as today; it then has no ERP link and nothing is sent for it.

### 5.5 Outbound processing

- When a sample with an ERP link reaches its final decision, **in the same database transaction**, MicroLIMS writes outbox messages: usage decision, results, certificate. Nothing is sent inside the transaction.
- A worker delivers outbox messages after commit, in order per inspection lot, retrying with increasing waits (1, 5, 15 minutes, then hourly, up to a configurable limit). A message that still fails is marked **Failed**, the administrator and QA are alerted, and it can be retried from the monitoring screen.
- Each message carries a stable id, so a repeated delivery is recognised by the receiver.
- The certificate sent is the archived one, identified by its hash.

### 5.6 Integration clients

- An integration client (for example "SAP PI production") is registered by the administrator, gets a key shown once (stored hashed, rotate and revoke, like instrument agents), and is limited to the integration endpoints.
- Every inbound message and every delivery is attributed to the client or to the system process in the audit trail.

### 5.7 Corrections after sending

- Once a usage-decision message exists for a sample (pending, sent or failed), correcting a result, reopening, or voiding that sample needs **QA approval** (a QA signature in addition to the usual signature).
- On approval, any undelivered decision, results and certificate messages for the sample are put **on hold**, not sent; QA receives a task: "Correct SAP inspection lot {n} by hand", which QA closes with a comment once done.
- No reversal message is ever generated.

### 5.8 Monitoring

- An "ERP messages" page: inbound and outbound messages with status, time, inspection lot, sample, attempts and last error; filters; view of the payload (secrets never stored); retry for failed outbound; reprocess for refused inbound; hold release by QA.
- Integrations page tiles: messages waiting, failed, oldest pending, last successful delivery.

### 5.9 Permissions

| Permission | Holders | Allows |
|---|---|---|
| `Erp.Configure` | System Administrator | Clients, endpoints, mapping, transports |
| `Erp.Monitor` | System Administrator, QA, Section Head | View messages; retry and reprocess (System Administrator) |
| `Erp.ApproveCorrection` | QA | Approve correction of a sent sample; release held messages; close manual-correction tasks |

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Duplicate inbound message | Same answer, processed once |
| Unknown material / inactive item / unmapped plant | Refused with reason; kept for reprocessing |
| SAP endpoint down | Retries; Failed after the limit; alert |
| Unmapped result | Not sent; listed |
| Correction of a sent sample without QA approval | Refused with the reason |
| Lot cancelled after receipt | Sample unchanged; Section Head alerted |

## 7. Testing

- **Backend `[PostgresFact]`:** inbound dedupe; material creates inactive item and updates name only; inspection lot creates expected sample and refusals; receive from expected sample keeps lot link and applies all receiving rules; final decision writes outbox messages in the same transaction (and none when it rolls back); worker delivery, ordering per lot, retry schedule, Failed, manual retry; delivered message not resent; correction and void refused without QA approval; QA approval holds messages and creates the manual task; client key authentication, rotation and revocation; mapping of decisions and characteristics; secrets absent from payload and audit.
- **Transport tests:** REST out against a local test server; files in and out through the agent (E1).
- **Frontend (`userEvent`):** expected samples on receiving; monitoring filters, retry, reprocess, hold release; mapping screens.
- **Manual:** with the SAP team, one goods-receipt lot and one production lot end to end on the SAP test system.

## 8. Delivery stages

1. Inbox, outbox, worker, clients, monitoring.
2. Material master and inspection lots; expected samples; receiving.
3. Outbound decision, results and certificate; mapping.
4. Corrections after sending.
5. File transport through the agent.
6. Documents: interface specification for the SAP team, requirements, SOP note.

## 9. Open items

1. The SAP team must confirm the usage decision codes, inspection characteristics and plant codes for the mapping.
2. Which middleware (if any) sits between SAP and MicroLIMS; it decides REST or files per direction.

**Audit-capture decision (user, 2026-11-11):** approved — `IntegrationInboxMessage` and `IntegrationOutboxMessage` (message contents) are kept out of automatic audit capture, each added to `AuditCaptureExcludedTypes` with a justification comment. The business changes they cause (expected samples, items, held messages, QA approvals) stay audited; message status changes are visible on the monitoring page.
