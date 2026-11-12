# Conflicts

This list records every place where the code, the business rules, the guide or another source disagree, or where a rule is doubtful. Each entry has an ID (`CONF-nnn`), an area, a status and the facts on both sides. An entry stays here after it is decided, so the decision and its date remain on record.

Entries CONF-001 to CONF-004 are the four known items from section 5.5 of the specification-set design. Entries CONF-005 to CONF-008 come from the whole-programme review of 2026-11-11 (cross-plan corrections, item 1).

For the eight entries below, the code has not been read yet for this list. Each "Location" is to be recorded when the area's functional specification (FS) is written.

### CONF-001
- **Area:** To be assigned when the area's FS is written
- **Status:** Open
- **What the code does:** The count preview on screen rounds differently from the server. Location: to be recorded when the area's FS is written.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Which rounding should the count preview follow, the screen rounding or the server rounding?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-002
- **Area:** To be assigned when the area's FS is written
- **Status:** Open
- **What the code does:** My Tasks and the workspace use different "ready to read" rules. Location: to be recorded when the area's FS is written.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Which "ready to read" rule is correct, and should My Tasks and the workspace use the same rule?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-003
- **Area:** To be assigned when the area's FS is written
- **Status:** Decided
- **What the code does:** Incubator selection checks the temperature range but not calibration status. Location: to be recorded when the area's FS is written.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Should incubator selection check calibration status as well as the temperature range?
- **Decision:** Decided by the user 2026-11-11: equipment with no calibration due date counts as in calibration.
- **Date:** 2026-11-11

### CONF-004
- **Area:** To be assigned when the area's FS is written
- **Status:** Open
- **What the code does:** Sample numbers can collide under concurrent receipt. Location: to be recorded when the area's FS is written.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** How should sample numbers be kept unique when two receipts happen at the same time?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-005
- **Area:** To be assigned when the area's FS is written
- **Status:** Open
- **What the code does:** Grade matching differs between services: some are case-insensitive, some lower-case the value, and some use exact matching. Location: to be recorded when the area's FS is written.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Which grade matching rule should every service use?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-006
- **Area:** To be assigned when the area's FS is written
- **Status:** Open
- **What the code does:** Limit comparison is implemented in several places: `SpecLimitParser`, `SpecificationService`, `PathogenSessionService` and `SpecificationEvaluator`. Location: to be recorded when the area's FS is written.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Should one place perform limit comparison, and which one?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-007
- **Area:** To be assigned when the area's FS is written
- **Status:** Open
- **What the code does:** Specification lookups bypass `SpecificationLookup` in five services. Location: to be recorded when the area's FS is written.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Should all five services use `SpecificationLookup`, and does the bypass change any result?
- **Decision:** Not decided.
- **Date:** Not applicable

### CONF-008
- **Area:** To be assigned when the area's FS is written
- **Status:** Open
- **What the code does:** Conformance is decided privately in `SampleApprovalService` and separately in `ReportDocumentMapper`. Location: to be recorded when the area's FS is written.
- **What the other source says:** Not recorded yet. Location: to be recorded when the area's FS is written.
- **Question:** Should one shared rule decide conformance for approval and for reports?
- **Decision:** Not decided.
- **Date:** Not applicable
