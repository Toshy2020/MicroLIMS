# E4 — Microbial Identification: Design Specification

**Roadmap package:** E4 (`improvement-roadmap.md`)
**Status:** Draft for user review
**Date:** 2026-11-11
**Builds on:** E1 (agent and formats). **Feeds:** F1 (environmental monitoring analytics uses the isolates and identities recorded here).

## 1. Purpose

Record which organism was actually found: each picked colony becomes an isolate, its identification (VITEK 2, manual kit, or Gram and simple tests) is stored with method and confidence, and the identity links to the controlled organism list.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Methods | VITEK 2 (biochemical cards); manual kits; Gram stain and simple tests |
| 2 | Isolates recorded | Pathogen tests on product; EM above alert or action level; water above alert or action level; all EM colonies for a flora library |
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
| EM | A location reading at alert or action level | The number of isolates set per room configuration (at least one) |
| Water | A sampling point reading at alert or action level | As for EM, per sampling point configuration |
| EM flora library | Every colony, for rooms where "identify all colonies" is switched on | Per room setting; off by default |

- Which readings count as alert or action level is decided by the existing evaluation; nothing new.
- For EM and water, a section of a sample cannot be **approved** while a required identification is open (see question 1).

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
| Approval with open required identification | Refused, naming the isolate or reading |
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
4. Required isolates per source; approval gate; pathogen consistency.
5. VITEK 2 through the agent with a configured format.
6. Reports; documents.

## 9. Questions for you

1. **Approval gate (EM and water).** For pathogen tests this is settled: identification is recorded inside the biochemical step. For EM and water: may a section be approved while a required identification is still open? I assumed **no**.
2. **Pathogen consistency.** Refuse "Not Detected" when an isolate is identified as the target organism, and require a reason for "Detected" when all isolates are other organisms. Agreed?
3. **VITEK 2 export.** Please provide two or three sample export files (or the LIS output description) from your VITEK 2 software; the format is configured from them.
4. **EM flora library.** Switched on per room, off by default. Agreed?
