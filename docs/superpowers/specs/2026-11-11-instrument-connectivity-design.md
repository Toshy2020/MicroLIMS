# E1 — Instrument Connectivity: Design Specification

**Roadmap package:** E1 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11
**Builds on:** the ChemStation ingest plan (`docs/superpowers/plans/2026-10-16-chemstation-ingest.md`) and its review (`docs/reviews/chemstation-integration-review.md`).

## 1. Purpose

Values from balances, pH meters and chromatography / ICP systems arrive in MicroLIMS with the instrument's identity and time instead of being typed, every value shows whether it was captured or typed, and typing remains possible with a reason.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Instruments | Analytical balances, pH meters, finishing HPLC ChemStation, and other chromatography data systems and ICP through structured export files |
| 2 | Makes | Mettler Toledo, Sartorius, and others; the design stays generic with configurable formats |
| 3 | Connection | Both: a browser connection for a balance or meter plugged into the bench PC, and a small agent on a lab PC for networked instruments and chromatography PCs |
| 4 | Typing | Allowed with a reason; the value is marked "manually entered" and the reviewer sees it |

## 3. Out of scope

- Controlling instruments (starting sequences, taring by command from MicroLIMS).
- Raw chromatography data upload and reprocessing (ChemStation plan Phase 6).
- Instruments not named in section 2 (Karl Fischer, UV, titrators, dissolution testers). The generic format support makes them a later configuration exercise, not this release.
- Continuous logging (chamber temperatures, incubator probes).

## 4. Current state (from code, not run)

- **Every weight and pH is typed.** Weights are typed in HPLC sample and standard preparation, ICP, titration (standard and sample), UV, weight variation and media preparation; pH in solution and media preparation. Only the weight-variation screen offers an optional balance selection; nothing checks the balance's type or calibration. No pH meter is recorded anywhere.
- **No value says where it came from.** Results have no "captured / typed" marker and no instrument reference.
- **The instrument agent exists only on the server side.** Registration with a hashed key, key rotation and revocation, a receive endpoint for ChemStation reports with hashing, idempotency and a clock-drift check, and an Integrations page listing agents are built. The agent program itself is not in the repository; its plan is blocked on open questions about the instrument PC. Received reports are stored but **not parsed and not turned into results**.
- An Empower report parser and an ICP report parser (text and OCR) exist; ICP report values can be shown to the analyst on demand. The Empower parser is not used.
- Agents are registered only for HPLC equipment. Agent health is "Active" once seen, with no staleness limit and no heartbeat.
- The API runs on Render; the documented free tier sleeps after 15 minutes idle, so the first push after a quiet period waits for the service to start.

## 5. Design

### 5.1 Instrument readings and provenance

- Every value that comes from an instrument is first stored as an **instrument reading**: instrument (equipment record), kind (weight, pH, chromatography result, ICP result), value as text exactly as sent, unit, stability flag where the instrument sends one, the instrument's own time where sent, the time received, the route (browser or agent and which agent), the raw message, and a hash of the raw message.
- Readings are never edited or deleted.
- A field that accepts an instrument value receives either:
  - a **reading** chosen by the analyst: the server checks the reading exists, belongs to an instrument of the right type in the analyst's laboratory, is stable (weights), has not already been used for another value, and that the instrument's calibration is current. It then copies the value with a link to the reading; or
  - a **typed value with a reason** (at least 10 characters), marked "manually entered". For a weight or pH on a connected field, the analyst still selects the balance or meter used, so the checks of 5.7 apply to typed values too.
- Every stored value carries its source: **Instrument** (with reading, instrument and route) or **Manual** (with reason). Values recorded before this package show "Source not recorded".
- **Units:** a reading in g for a field in mg (or the reverse) is converted by exact decimal scaling; any other unit mismatch is refused. The number of decimals the instrument sent is kept; nothing is rounded on capture.
- The review screen and the sample report show the source of every captured or typed value, and the reviewer's list shows a count of manually entered values per test.

### 5.2 Fields covered

| Area | Fields |
|---|---|
| HPLC | Sample replicate weight, standard weight |
| ICP | Sample weight |
| Titration | Standard weight, sample weight |
| UV | Replicate weight |
| Weight variation | Each unit weight |
| Media preparation | Total weight, pH |
| Solution preparation | Measured pH |

A field is connected for a laboratory when at least one instrument of the right type is set up for it; otherwise it behaves as today, with no reason required.

### 5.3 Balances and pH meters in the browser

- On a bench PC with Chrome or Edge, the analyst connects a USB or serial balance or meter once per session (the browser asks permission). The page listens.
- When the analyst presses the instrument's print or transfer key (or the page's "Read" button, which sends the instrument's read command), the browser sends the **raw message** to the server. The server, not the browser, reads the value, unit and stability using the instrument's configured format (5.5) and stores the reading.
- The reading appears in the field at once; the analyst confirms it.
- The route is recorded as "browser" with the PC's session. This route is weaker than the agent (the browser is under the user's control), and is labelled as such in the reading.

### 5.4 The agent

- A small Windows service installed on a lab PC. It authenticates with its own key, as the server side already supports.
- **Balances and meters on the network or on the agent PC's ports:** the agent listens for print messages and sends each raw message to the server, which stores the reading as in 5.3. The analyst picks the reading in MicroLIMS from the list of the last readings of the selected instrument (newest first, with time and value).
- **Chromatography and ICP PCs:** the agent watches the configured export folders, waits until a file is complete, and sends the original file with its hash, exactly as the ChemStation plan describes (file courier; all parsing on the server; outbox that keeps unsent files across restarts and outages).
- One agent can serve several instruments; each instrument is registered to the agent by the administrator. Registration is extended from HPLC to all instrument types in section 2.
- **Health:** the agent sends a heartbeat every minute. The Integrations page shows each agent and instrument as Online, Late (no heartbeat for a configurable time, default 5 minutes) or Offline, with the last message time; a Late or Offline agent notifies the administrator and the laboratory's Section Head.
- **Hosting:** because the agent keeps unsent data, a sleeping API only delays delivery. The production service should not use a plan that sleeps; this is noted for the hosting decision, not built.

### 5.5 Formats by configuration

- Each instrument model has a **format**: for balances and meters, how to find value, unit and stability in a message (built in: Mettler Toledo MT-SICS, Sartorius SBI; others by a pattern the administrator enters and tests against sample messages before saving); for export files, the file type and a mapping from columns or fields to sample identifier, analyte, response or result, and units.
- A format is versioned; a reading or file records the format version used to read it. Editing a format creates a new version and never re-reads old data.
- A format can be tested in the screen against pasted sample messages or an uploaded sample file; the result is shown before saving.

### 5.6 Chromatography and ICP results

- **ChemStation:** finish the existing plan — server-side parser (Phase 3), matching and adoption onto the HPLC run (Phase 4), the agent (Phase 5) — using this package's agent and formats where they overlap. Its open questions (identifier convention, the instrument PC's operating system, export settings, sample files) still have to be answered before those phases.
- **Other systems (Empower, Chromeleon, OpenLab) and ICP:** structured export files read by a configured format into **pending instrument data**. The analyst adopts it onto the open HPLC or ICP run: the system proposes the match by the configured identifier, the analyst confirms each row, and the adopted values are recorded through the existing run entry with source "Instrument" and a link to the file and its hash. Nothing is accepted automatically.
- The existing OCR import remains for reports that have no structured export, and its values are marked "Instrument (read from report image)" so the reviewer can see the lower confidence.

### 5.7 Balance checks and minimum weight

- **Daily check, configurable per balance:** on or off; the check weights (nominal value and unit) and the acceptance limit for each (absolute or percent), entered by the Section Head for that balance. Nothing is pre-filled.
- When on, the first use of the balance each laboratory day needs a recorded check: each check weight is weighed (reading from the balance, or typed with a reason), and the server compares it with the configured limit. The check is signed by the analyst. A failed check blocks the balance for the day and notifies the Section Head; a new check can be recorded.
- **Minimum weight, per balance:** optional value entered by the Section Head. Any weight from that balance below it, captured or typed, is refused.
- Configuration changes are audited; the check results are kept with the balance's record.

### 5.8 Matching instrument rows to samples

- An export row is proposed for a sample when its sample name contains the sample's reference number, or its batch field equals the sample's batch number. A test order number in the row, when present, narrows the match to that test order.
- Because batch numbers are not unique, a match is a proposal; the analyst confirms each row before it is adopted.

### 5.9 Permissions

| Permission | Holders | Allows |
|---|---|---|
| `Instruments.Configure` | System Administrator | Register agents and instruments, keys, formats |
| `Instruments.ManageChecks` | Section Head | Balance check weights, limits and minimum weight |
| `Instruments.Read` | Analyst, Section Head, Reviewer | See readings and pending instrument data of their laboratory |
| existing result permissions | as today | Use readings and adopt pending data in their own work |

Formats and registrations are system configuration and are audited; a format change in use also notifies the Section Heads of the laboratories that use it.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Unstable weight | Not usable; "Wait for a stable reading." |
| Reading already used, wrong instrument type, other laboratory | Refused with the reason |
| Balance not checked today (when its daily check is on) or check failed | Refused: "Balance {code} has no passed check today." |
| Weight below the balance's minimum weight | Refused: "{value} is below the minimum weight of {min} for balance {code}." |
| Instrument calibration not current | Refused; typing with a reason remains possible and is flagged |
| Message the format cannot read | Stored as an unreadable reading with the raw message; the analyst is told; not usable |
| Unit not convertible | Refused |
| Agent offline | Readings stop; typing with a reason; the Integrations page and notifications show it |
| Export file cannot be read | Kept as quarantined with the reason, as the ChemStation plan defines |
| Same file sent twice | One copy kept; the second send is answered with the same receipt |

## 7. Testing

- **Backend `[PostgresFact]`:** reading stored with hash and route; MT-SICS and SBI parsing of stable, unstable, overload, underload and error messages; custom pattern formats; unit conversion; each refusal in section 6; reading used once; manual value requires reason; source stored on each field in 5.2; format versioning; heartbeat statuses; export file → pending data → adoption with source and hash; no automatic acceptance; audit of configuration changes; state, audit and negative checks.
- **Agent:** outbox survives restart; duplicate send; key revoked; clock drift; partial file not sent.
- **Frontend (`userEvent`):** connect and read (Web Serial wrapped behind a service so tests mock the boundary only); pick from recent readings; typed value asks for a reason; source badges on review; format tester.
- **Manual:** one Mettler and one Sartorius balance through the browser and through the agent; one pH meter; one Empower or OpenLab export and one ICP export adopted; agent unplugged and reconnected.

## 8. Delivery stages

1. Readings, provenance on the fields of 5.2, typed-with-reason, review display.
2. Formats (built-in MT-SICS and SBI, custom patterns, tester).
3. Browser connection for balances and meters.
4. Agent: heartbeat, health, balances and meters; registration for all instrument types.
5. Export files: agent folder courier, file formats, pending data and adoption for HPLC and ICP.
6. ChemStation phases 3–5 once its open questions are answered.
7. Documents: requirements, instrument setup guide, SOP note on typed values.

## 9. Decisions recorded 2026-11-11

1. Daily balance checks are configurable per balance (on/off, check weights, limits); no limits are pre-filled.
2. A minimum weight per balance is enforced when entered.
3. No age limit on readings.
4. Browser-captured readings count as instrument values; the route is recorded.
5. Rows are matched by sample name (reference number) or batch number; a test order number is optional and narrows the match. The analyst confirms every match.
