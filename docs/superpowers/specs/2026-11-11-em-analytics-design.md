# F1 — Environmental Monitoring Analytics: Design Specification

**Roadmap package:** F1 (`improvement-roadmap.md`)
**Status:** Draft for user review
**Date:** 2026-11-11
**Builds on:** E4 (isolates and identities), D1 (excursion review engine), D2 (deviations), C5 (notifications).

## 1. Purpose

Show the state of each cleanroom at a glance on the site's own floor plan, trend results per room, grade and season, show which organisms were found where, and detect adverse trends with rules the Section Head chooses, opening an excursion review when one fires.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Level of detail | Room level only; no sampling points inside rooms |
| 2 | Floor plan | An uploaded drawing per area with room markers placed on it |
| 3 | Trend rules | The system offers every rule type; the Section Head chooses which to use and sets their numbers |
| 4 | When a rule fires | The Section Head and QA are notified and an excursion review opens automatically |

## 3. Out of scope

- Sampling points within rooms, and limits per point.
- Particle counters, temperature, humidity and pressure monitoring.
- Changing how counts are evaluated against alert and action limits.
- Changing the monitoring plan or its deviations (they are linked, not replaced).

## 4. Current state (from code, not run)

- Rooms have a name, a free-text grade ("A", "B", "Grade C" all occur), a kind (room or laminar-air-flow unit inside a room) and a sampling frequency. Each room has one configuration per test type (passive air, surface, active air, compressed air, drains) with alert, action and specification limits.
- Results are stored per sample location with copies of the limits and a status (within, alert, action, out of specification).
- **"Out of trend" is never set anywhere.** A room-monitoring table with an out-of-trend flag exists but nothing writes to it. Package E4 assumed this flag; it must come from this package instead (5.5).
- Control charts with eight run rules exist for EM and water, for display; trend-based limit review can update a room's limits with a signature.
- No floor plan, image or position data. No season grouping.
- Monitoring-plan deviations open automatically for action-level results of planned occurrences.

## 5. Design

### 5.1 Grades

- Grade becomes a controlled list (name and order, for example A, B, C, D), maintained by the Section Head. Existing free-text values are mapped to the list once during upgrade by the Section Head; a room cannot be saved with a grade that is not on the list.

### 5.2 Floor plans

- An **area** (for example "Sterile block, ground floor") has an uploaded drawing (PNG, JPEG or PDF first page converted to an image), kept with its version history.
- The Section Head places **room markers** on the drawing by dragging; water sampling points can also be placed. Each marker stores its position relative to the drawing, so the plan scales to any screen.
- The plan shows each room's state for a chosen period (default: last 30 days) using **colour plus an icon and a text label**, never colour alone:

| State | Shown when |
|---|---|
| No data | No result in the period |
| Within limits | All results within limits |
| Alert | At least one alert-level result |
| Action | At least one action-level or out-of-specification result |
| Adverse trend | A trend rule fired in the period (5.4) |
| Overdue | A planned sampling is overdue (from the monitoring plan) |

- Selecting a room opens its panel: latest results per test type, trend chart, organisms found, open excursion reviews and deviations.
- The plan has a keyboard-accessible list view with the same information, for screen readers and small screens.

### 5.3 Trends and flora

- **Trend views** by room, by grade, by test type and by period (month, quarter, and **seasons defined by the site** as month ranges, for example "Summer: June–September"). Charts show counts against limits; tables show counts of alert and action results, and the percentage of samples with any growth.
- **Flora view** from the E4 identifications: organisms (or genus, or Gram group where only that was identified) found per room, grade and period, with links to the isolates and samples. Only plates that required identification appear (out of specification or out of trend), so the view says so.
- Exports to CSV and to the PDF report used by the existing control-chart report.

### 5.4 Adverse-trend rules

- The system offers these rule types, **none active until the Section Head turns one on** and enters its numbers:

| Rule type | Numbers the Section Head sets |
|---|---|
| Consecutive alert-level results | N results in a row at or above alert level, for a room and test type |
| Count within a window | X results at or above alert level within the last Y samples or Y days, for a room and test type |
| Contamination recovery rate | Maximum percentage of samples with any growth per grade, test type and period |
| Same organism repeating | The same organism (or genus) identified in the same room K times within D days |
| Control-chart rules | Any of the existing eight run rules, applied to a room and test type |

- Each rule is set per grade (default for all rooms of that grade) and can be narrowed to a room or test type. Rules are versioned: a change applies from its effective date and never re-judges earlier results.
- Rules are evaluated when a result is approved and nightly for window and rate rules. Each firing is recorded once (rule, version, room, test type, the results involved), so the same pattern does not fire repeatedly.

### 5.5 What a firing does

- The readings involved are flagged **out of trend** (this is the flag E4 uses: an out-of-trend plate then needs one identification).
- The Section Head of the laboratory and QA are notified.
- An **excursion review** (package D1's one-phase review) opens automatically, linked to the room, the rule and the readings. QA can raise a deviation (D2) from it.
- If the same rule fires again for the same room while its review is open, the new readings are added to that review instead of opening another.
- Action-level results keep their existing behaviour (monitoring-plan deviation for planned occurrences); they are shown on the plan and linked from the review when a rule also fires.

### 5.6 Permissions

| Permission | Holders | Allows |
|---|---|---|
| `EmAnalytics.View` | Section Head, Reviewer, Analyst, QC Manager, QA | Floor plans, trends, flora |
| `EmAnalytics.Configure` | Section Head | Areas, drawings, markers, grades, seasons, trend rules |

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Drawing too large or wrong type | Refused with the limits (20 MB; PNG, JPEG, PDF) |
| Room without marker | Listed beside the plan as "Not placed" |
| Rule fires for a room with an open review from the same rule | Readings added to that review |
| Rule edited | New version from its effective date; earlier firings unchanged |
| Grade not on the list | Room save refused |

## 7. Testing

- **Backend `[PostgresFact]`:** grade list and mapping; area drawing versions and marker positions; room state per period for each state; trend aggregations by grade, month, quarter and site seasons; recovery rate; flora from isolates; each rule type at its boundaries; rule versioning and effective date; one firing per pattern; out-of-trend flag set on the readings; excursion review opened and joined; notifications after commit; nothing evaluated for inactive rules.
- **Frontend (`userEvent`):** marker drag with keyboard alternative (arrow keys move a marker); state shown with icon and text; list view equals the plan; room panel; rule editor with versions; season editor.
- **Manual:** one area with a real drawing; one rule of each type turned on with test data.

## 8. Delivery stages

1. Grades list; areas, drawings, markers.
2. Room states on the plan and list view.
3. Trends, seasons, flora.
4. Rule types, versions, evaluation, firing records.
5. Out-of-trend flag, notifications, excursion reviews.
6. Documents; E4 updated to use this package's out-of-trend flag.

## 9. Open items

1. The numbers for each rule are the Section Head's, from your EM SOP; none ship.
2. Seasons are entered by the site; none ship.
