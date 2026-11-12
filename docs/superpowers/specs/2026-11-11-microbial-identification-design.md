# E4 — Microbial Identification: Design Specification

**Roadmap package:** E4 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11
**Builds on:** E1 (agent and formats). **Feeds:** F1 (environmental monitoring analytics uses the isolates and identities recorded here).

## 1. Purpose

Record which organism was actually found: each picked colony becomes an isolate, its identification (VITEK 2, manual kit, or Gram and simple tests) is stored with method and confidence, and the identity links to the controlled organism list.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Methods | VITEK 2 (biochemical cards); manual kits; Gram stain and simple tests |
| 2 | Isolates recorded | Pathogen tests on product (inside the biochemical step); EM and water plates whose reading is out of specification or out of trend — one identification per plate. No flora library of all colonies |
| 3 | Confidence | Threshold per system and method, set by the Section Head; below it the analyst chooses and the Section Head confirms. Nothing pre-filled |
| 4 | Organism list | Only Section Heads add organisms. Unknown names from a system are held until mapped |

## 3. Out of scope

- MALDI-TOF and sequencing (not used). The design leaves room for them as further methods.
- Antibiotic susceptibility testing.
- Trending, floor plans and adverse-trend rules (package F1).
- Changing how pathogen results or counts are calculated or judged.

## 4. Current state (from code, not run)

- **The identity of the organism found is never stored.** A pathogen test's biochemical step records free text, an attachment reference and Detected / Not Detected; the target organism is the one configured on the step.
- There is no isolate, colony or identification record, and no identification method field. Gram, catalase, oxidase, coagulase, antibiogram and "identification kit" exist only as the list of tests a step shows.
- Environmental monitoring and water record counts and growth per location, with no identification.
- **Any user can add a new organism to the master list from the picker, with no review.** The list is used by media challenges, reference strains and workflow targets.
- No equipment type for identification systems.

## 5. Design

### 5.1 Isolates

- An **isolate** is a colony picked for identification. It belongs to its source: a pathogen test's step, an EM location reading, or a water point reading. Fields: number (`ISO-2026-00001`), source, medium and plate, colony description (text), picked by and at.
- An isolate can be **sub-cultured**; the sub-culture links to its parent.
- An isolate is never deleted; a mistaken one is voided with a reason.

### 5.2 Identifications

Each isolate has one or more identification attempts. Each attempt: method, result, confidence, raw data, who and when.

| Method | How the result arrives | Result content |
|---|---|---|
| VITEK 2 | From the instrument through the agent (package E1) as an export file, read by a configured format; or typed with a reason when the connection is not available | Organism name as reported, card type, probability (%), confidence text, bionumber, raw file and its hash |
| Manual kit | Entered by the analyst | Kit name and lot, profile number, organism as read from the kit database, % identification and the kit's comment |
| Gram and simple tests | Entered by the analyst | Gram reaction and morphology, catalase, oxidase, coagulase, other tests configured for the step — each a separate result |

- The organism reported by VITEK 2 or a kit is **mapped** to the organism list. A name that has no mapping is held as "unmapped"; the Section Head adds the organism or maps the name to an existing one, and the attempt then proceeds. Mapped names are remembered per method.
- **Confidence:** the Section Head sets a threshold per method (and card type for VITEK 2), with nothing pre-filled. At or above it, the result is proposed and the analyst accepts it. Below it, or with no threshold set, the analyst chooses: repeat, another method, genus only, or not identified, with a reason, and a Section Head confirms.
- An isolate's **final identity** is the accepted attempt: an organism (species), a genus only, Gram and morphology only, or "not identified". It can be changed only by a new accepted attempt; earlier attempts remain.
- Gram and simple tests can be the final identity where the method used was only that.

### 5.3 Where isolates are required

| Source | When | Required |
|---|---|---|
| Pathogen test (product) | **Inside the existing biochemical (identification) step** of the pathogen workflow | The step itself is the identification: it cannot be submitted without at least one recorded identification (5.3.1) |
| EM | A plate whose location reading is out of specification (action limit or specification exceeded) or flagged out of trend | One identified isolate per such plate |
| Water | A plate whose sampling-point reading is out of specification or flagged out of trend | One identified isolate per such plate |

- Which readings are out of specification is decided by the existing evaluation. **Out of trend** is the flag set by package F1's adverse-trend rules (the code has no working out-of-trend flag today); until F1 is built, only out-of-specification plates require identification. A reading at alert level only does not require identification.
- **Identification is separate from approval** (decided by the user 2026-11-11): approving a sample against its specifications is not blocked by an open identification. Open identifications are listed for the laboratory (My Work and a register) until completed.

#### 5.3.1 Pathogen tests: identification in the biochemical step

- No new step and no separate gate is added. The existing biochemical step of the pathogen workflow becomes the place where identification is recorded (decided by the user 2026-11-11).
- The step form offers the methods: **VITEK 2**, **manual kit**, and **Gram and simple tests** (the simple tests listed are those already configured on the step). The analyst records one identification per colony examined; each colony is an isolate created by the step.
- VITEK 2 results waiting for the step's isolates are shown inside the step for the analyst to accept or choose (5.2).
- The step's existing outcome (Detected / Not Detected) is submitted together with the identifications, subject to the consistency rules of 5.4. The step's free-text result field stays and is filled with a readable summary of the identifications, which the analyst can extend.
- Because review requires all steps completed, a pathogen test cannot reach review or approval with its identification missing; no extra approval gate is needed.

### 5.4 Consistency with the pathogen decision

- When an isolate on a pathogen test's identification step has an accepted identity equal to the step's configured target organism, "Not Detected" is refused for that test.
- When all isolates are identified as other organisms, "Detected" needs a reason.
- The comparison is with the configured target, never with an organism name in code.
(See question 2.)

### 5.5 Organism list control

- Adding, renaming or removing organisms requires the master-data permission (Section Head). The picker no longer offers "Add to Organism list" to others; it says "Ask your Section Head to add this organism".
- Organism entries gain an optional genus field so genus-only identities can be recorded and trended.

### 5.6 Screens

- On the pathogen biochemical step (5.3.1), and on EM and water result screens: an "Isolates" panel — add isolate, record identification by method, see VITEK 2 results waiting for the isolate, accept or choose, final identity.
- VITEK 2 results arrive as pending instrument data (E1) and are matched to isolates by isolate number entered on the instrument; the analyst confirms the match.
- Section Head: unmapped names, low-confidence confirmations, thresholds per method.
- The sample report and certificate show identified organisms for pathogen tests where the certificate shows the test; EM and water reports list isolates and identities per location.

### 5.7 Permissions

Recording isolates and identifications uses the existing result-recording permissions and the competency gate (D4). `Identification.Configure` (Section Head): thresholds, mappings, room and point settings. The master-data permission controls the organism list.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Unmapped organism name | Attempt held; Section Head task created |
| Confidence below threshold or no threshold | Analyst must choose; Section Head confirms |
| Open required identification | Listed for the laboratory; approval not blocked |
| "Not Detected" with an isolate identified as the target | Refused |
| VITEK 2 result with an unknown isolate number | Kept as pending instrument data; listed for matching |
| Analyst tries to add an organism | Not offered; refused on the server |

## 7. Testing

- **Backend `[PostgresFact]`:** isolates for each source; sub-culture link; void; each method's fields; mapping, unmapped hold and release; threshold proposal and below-threshold path with Section Head confirmation; final identity history; required isolates per source and approval refusal; pathogen consistency rules (generic, using a configured target id); organism creation refused for non-holders; VITEK 2 file read by a configured format, matched, adopted; state, audit and negative checks.
- **Frontend (`userEvent`):** isolates panel; each method form; pending VITEK 2 results; Section Head queues; picker without "Add".
- **Manual:** with sample VITEK 2 export files from your instrument, and one kit identification.

## 8. Delivery stages

1. Organism list control and genus field.
2. Isolates and manual identifications (kits, Gram and simple tests).
3. Mapping and confidence thresholds.
4. Required isolates per source; open-identification list; pathogen consistency.
5. VITEK 2 through the agent with a configured format.
6. Reports; documents.

## 9. Decisions recorded 2026-11-11

1. Pathogen identification is recorded inside the existing biochemical step, with VITEK 2 offered there.
2. EM and water identification is separate from approval; it never blocks approving a sample against its specifications.
3. Consistency with the pathogen outcome as in 5.4 is agreed.
4. VITEK 2 sample export files are not available yet; the VITEK 2 import waits for them, and VITEK 2 results are typed with a reason until then.
5. Only plates with an out-of-specification or out-of-trend reading need identification, one per plate. No flora library.
