# D3 — Stability Study Management: Design Specification

**Roadmap package:** D3 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11
**Depends on:** B4 (role pattern). **Related:** C1 (My Work), C5 (notifications), D1 (OOS), D2 (deviations).

## 1. Purpose

Plan stability studies as approved protocols, store and track the units in chambers, pull each time point on time as a normal laboratory sample, judge results against limits fixed in the protocol, and report the study with signatures.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Storage conditions | The ICH Q1A conditions shipped as an editable list; each study picks from it |
| 2 | Pull window | Stated per time point in each protocol; the system enforces the approved protocol. No built-in default |
| 3 | Statistics | No shelf-life calculation. Tables and trend charts only; any evaluation done outside is attached to the summary report |
| 4 | Limits | Copied into the protocol at approval; every pull is judged against them; changing them needs a signed amendment |

Decision D-5 of the roadmap is closed by 2–4.

## 3. Out of scope

- Statistical shelf-life evaluation (regression, poolability, extrapolation).
- Recording chamber temperature and humidity readings. Readings come from the chamber's own monitoring system; package E1 may connect it later. Excursions are entered by hand (5.3).
- Photostability and in-use studies as special types. They can be run as an ordinary study with a suitable condition.
- Stability of raw materials, solutions and media. Finished products only in this release.
- Production planning of stability batches.

## 4. Current state (from code, not run)

- No study, condition, time point, pull or chamber record exists.
- "Stability" exists as a production-stage role: a specification row can be marked for the stability stage, and a "Stability" stage and laboratory section are seeded.
- A sample is received for an item with a batch number, manufacture and expiry dates as text and dates on the sample; there is no batch record. Tests come from the item's assigned tests, or are chosen later when the cause of testing requires selection.
- Results are judged against the item's specification **at the time of entry** (decided in C7 for routine work). Recorded results keep a copy of the limits used.
- Equipment has no "stability chamber" type and no condition. Calibration due date and status exist.
- The monitoring plan has a schedule engine with windows and working-day rules; it is the pattern for pull schedules.
- Trend charts exist for environmental monitoring and water, by location; not by batch.

## 5. Design

### 5.1 Conditions

- A list of storage conditions: name, temperature and tolerance, relative humidity and tolerance (may be blank), and a short code. Shipped with: 25 °C ± 2 °C / 60 % RH ± 5 %, 30 °C ± 2 °C / 65 % RH ± 5 %, 30 °C ± 2 °C / 75 % RH ± 5 %, 40 °C ± 2 °C / 75 % RH ± 5 %, 5 °C ± 3 °C, −20 °C ± 5 °C.
- The R&D Manager maintains the list. A condition used by a study cannot be changed, only deactivated.

### 5.2 Protocol

A protocol is written by the Section Head of the stability laboratory and contains:

| Part | Content |
|---|---|
| Identity | Number (`STB-2026-001`), title, product (item), study type (long-term, intermediate, accelerated, ongoing, other), reason |
| Batches | One or more: batch number, manufacture date, expiry date, batch size and packaging description (text) |
| Conditions | One or more conditions from the list, each with pack and orientation (for example "Upright", "Inverted") |
| Time points | Per condition: list of time points in months (0 allowed), each with its pull window (days before and after) |
| Tests | Per time point: the tests to perform. Chosen from the item's specifications for the stability stage, or any test of the item |
| Limits | Copied from the item's stability-stage specification rows (or, where there is none, the general row) at approval; shown in the protocol and editable only by amendment |
| Units | Units needed per pull (per test and in total), plus spare units; total units to place per batch and condition |
| Start | Placement date per batch and condition: time zero for its schedule |

- **Approval:** the Section Head signs as author; the **R&D Manager** signs approval. Only an approved protocol creates a schedule.
- **Amendment:** any change after approval (add a time point, change a limit, add a test, discontinue a condition) is a numbered amendment with a reason, signed by the Section Head and the R&D Manager. Pulls already done keep the limits and tests they were done with. A limit amendment applies to pulls created after it.
- **Discontinue:** a study or one batch/condition can be stopped with a reason and the R&D Manager's signature; open pulls are cancelled.

### 5.3 Chambers and units

- A new equipment type **Stability chamber**. A chamber is assigned one condition at a time; the assignment is recorded with dates.
- **Placement:** after approval, the analyst records placing the units: chamber, batch, condition, number of units, date. The placement date starts that schedule. A chamber whose calibration or qualification is out of date, or whose condition differs from the protocol's, is refused.
- **Inventory:** per chamber, batch and condition — units placed, units pulled, units left, and units planned for the remaining pulls. A shortfall is shown as a warning on the study.
- **Moving units** to another chamber (same condition) is recorded with a reason.
- **Excursions:** a user records a chamber excursion (from, to, highest or lowest value, description). It lists the affected studies and opens a deviation (package D2) linked to them. Pulls are not blocked automatically; the deviation decides.
- At each pull the chamber's calibration status is checked again; if out of date, the pull is allowed only with a signed justification by the Section Head, and the sample shows a warning.

### 5.4 Schedule and reminders

- At placement the system creates one **pull** per time point: target date = placement date + months (calendar months), window from the protocol. If the target date falls on a non-working day, the target moves to the previous working day, inside the window.
- Pulls appear in My Work (package C1) for the stability laboratory a configurable number of working days before the window opens (setting per laboratory, empty = when the window opens), and in a study calendar.
- **Late pulls are not allowed.** A pull not done when its window closes becomes **Missed**: it can no longer be pulled, a deviation (package D2) is opened automatically and linked to the study and time point, and the Section Head, the R&D Manager and QA are notified. The units stay counted in the chamber.

### 5.5 Pulling

- The analyst opens the due pull, confirms the units taken from which chamber (inventory decreases), and the system **creates a normal sample** through the existing receiving process: the item, the batch number and dates from the protocol, production stage Stability, cause of testing "Stability", and **exactly the tests of that time point** from the protocol.
- The sample is linked to the study, batch, condition and time point and shows this on every screen and on its report.
- From then on it runs through the normal workflow: testing, review, approval, OOS investigation (package D1). Nothing in the testing engine is special for stability, except where its limits come from (5.6).
- Pulling before or after the window is refused.
- **Time zero:** a time point 0 can either be pulled like any other, or linked to an existing approved release sample of the same batch, chosen by the Section Head with a signature.

### 5.6 Limits for stability samples

- A test order created by a pull is judged against the **protocol's limits** for that test, not the item's current specification. The protocol limits are stored in the same structure as specification rows, so the existing evaluation applies unchanged; only the source of the row differs.
- The recorded result keeps its copy of the limits, as today.
- If a test has no protocol limit (for example a report-only test), it is recorded as "Limits not configured", as today.

### 5.7 Results and charts

- **Results table** per study: for each condition and batch, a grid of tests (rows) by time points (columns) showing the reported value, its limit, and its status; pending, missed and cancelled pulls shown as such; links to the sample and any OOS investigation.
- **Trend chart** per test: value against months, one line per batch, the limits drawn, separate charts or colours per condition. Display only; no fitted lines.
- Export of the table (CSV) is logged as other exports are.

### 5.8 Summary report

- Generated at any time as a draft; issued at completion or discontinuation.
- Contains the protocol and amendments, placements and inventory, the results tables and charts, deviations and OOS investigations linked, and attachments (for example an external statistical evaluation).
- **Conclusion** written by the Section Head (signed), approved by the R&D Manager (signed). The issued report is fixed and printable with every signature: printed name, date and time with time zone, meaning.
- A study is **Completed** when every pull is done, missed with a deviation, or cancelled, and the summary report is approved.

### 5.9 Permissions

| Permission | Default holders | Allows |
|---|---|---|
| `Stability.View` | Section Head, Reviewer, Analyst, QC Manager, QA, R&D Manager | Read studies of their laboratory (QA and R&D Manager: all) |
| `Stability.Manage` | Section Head | Write protocols and amendments, sign as author, record placements and chamber assignments, link time zero, write the conclusion |
| `Stability.Pull` | Analyst, Section Head | Pull, move units, record excursions |
| `Stability.Approve` | R&D Manager | Approve protocols, amendments, discontinuation, summary reports; maintain conditions |

- The author of a protocol or amendment cannot approve it. Section scope applies.
- **R&D Manager** is a new system role (like QA in package B4): it reads stability studies and their samples in all laboratories and holds `Stability.Approve`. It may not hold any permission to record, review or approve laboratory results, or to administer users; the server refuses such a combination, using the same rule as the QA role.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Placement in a chamber with the wrong condition or expired calibration | Refused with the reason |
| Pull before the window | Refused: "This pull opens on {date}." |
| Pull after the window | Refused: "This pull was missed on {date}." |
| Not enough units left for a pull | Refused; the shortfall is shown |
| Amendment to limits of a test already pulled | Allowed for future pulls only; earlier pulls unchanged |
| Condition or protocol changed by someone else meanwhile | Save refused; reload |
| No R&D Manager user | Protocols can be drafted, not approved |

## 7. Testing

- **Backend `[PostgresFact]`:** protocol draft, author signature, R&D Manager approval, author cannot approve; R&D Manager role independence; limits copied at approval and unaffected by a later specification change; schedule dates from placement with month arithmetic (end-of-month cases) and working-day adjustment; placement refusals; inventory arithmetic and shortfall; pull creates exactly one sample with the protocol's tests, batch, stage and cause; pull judged against protocol limits (result status and the copied limits); pull before window refused; missed pull refused and opening one deviation automatically; time-zero link; amendment applies to later pulls only; excursion opens a linked deviation; discontinue cancels open pulls; summary report signatures and immutability; scope and separation; state, audit and negative checks on every signed step.
- **Frontend (`userEvent`):** protocol editor (batches, conditions, time points, tests, units), approval and amendment flows, placement, due pulls in the list, pull dialog, results grid states, chart data passed to the chart wrapper, report view.
- **Manual:** one study with two batches and two conditions run through placement, three pulls, an amendment, an OOS at one time point, and the summary report.

## 8. Delivery stages

1. Conditions, chamber type and condition assignment.
2. Protocol, approval, limits snapshot, amendments.
3. Placement, inventory, schedule, reminders.
4. Pull to sample; evaluation against protocol limits; time zero link.
5. Missed and late pulls; excursions with deviations.
6. Results table and charts.
7. Summary report; completion and discontinuation.
8. Documents: requirements, manual chapter, SOP template note.

## 9. Decisions recorded 2026-11-11

1. Protocols, amendments, discontinuation and summary reports are approved by the R&D Manager, a new system role. QA reads everything and is notified of missed pulls.
2. Time point 0 may be linked to the batch's approved release sample instead of a new pull.
3. Units are counted, not tracked individually.
4. The reminder lead time is a per-laboratory setting in working days; empty means pulls appear when the window opens.
5. Late pulls are not allowed. A missed pull opens a deviation automatically.
