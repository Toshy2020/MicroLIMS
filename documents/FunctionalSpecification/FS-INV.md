# FS-INV Functional Specification

| Field | Value |
|---|---|
| Document number | FS-INV |
| Title | Inventory materials and equipment register functional specification |
| Version | 0.1 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |

## Scope and reading notes

- This document covers the stock lot register (materials), the stock release gate, consumption of stock, and the equipment inventory register with its traceability queries. Material categories, material master entries and material documents (Certificates of Analysis) are in other controllers and were not read, except where a method here calls them. Media preparation and the use of stock in it are in FS-MED. Calibration planning, calibration records and maintenance logs for inventory equipment are in FS-EQP.
- Every statement was written from the code files named in each entry. Nothing was run.
- `Material` and `EquipmentInventory` have a version column. Where a method calls `RecordVersion.EnsureCurrent` this is stated; where it does not, the entry says "no stale-form version check". Audit rows for inserts and updates come from the automatic capture in `MicroLimsDbContext`; neither entity is on the exclusion list. Stock deduction is saved by the caller's save, so the deduction is audited as a change of the lot row.
- The code comment in `EquipmentInventory.cs` says nothing in the workflow engines reads the equipment register. This is no longer true: media storage, cryovial storage move and the HPLC and ICP availability checks read it. See FS-INV-011.

## Functions

### FS-INV-001
**Satisfies:** URS-INV-001
**Function:** Access to stock lots. `MaterialController` needs `Materials.Manage` on all actions. `GET` list, the print list and the usable reference standards list are filtered by `GetReadableSectionIdsAsync` (null means all sections). `POST` create resolves the owning section with `ResolveSectionForCreateAsync` (write access; the section can be sent by the user). `PUT` update calls `EnsureMaterialAccessAsync` (write) and then `RecordVersion.EnsureCurrent`. The type-options endpoint resolves a section the same way as create. The default-unit endpoint needs no scope.
**Code:** `backend/MicroLIMS.API/Controllers/MaterialController.cs` — `GetAll`, `Create`, `Update`; `backend/MicroLIMS.Application/Services/MaterialService.cs` — `GetAllAsync`, `CreateAsync`, `UpdateAsync`
**Verified by:** reading (not run)

### FS-INV-002
**Satisfies:** URS-INV-002, URS-INV-003
**Function:** Receive a stock lot. Inputs: material type, name, manufacturer, batch number, receiving date, expiry date, code, location, quantity received, unit, minimum stock level, organism, media product, section, purity, moisture, storage condition, master entry, reference organism. Rules: working standards cannot be received here (they are created by approving a qualification); legacy named types and free-text custom types are refused (receive as an Other material with a master entry). For a dehydrated media lot a media product is needed, must exist and be active; the lot's name and code are then taken from the product. The section is resolved (write access) and the material type must be one offered to that laboratory (`MaterialTypeRules.BuiltInTypesFor` by section code). Types that need a master entry (chemical, indicator, reference standard, primary standard, other) must have an active entry of the right category; for Other the category must be active and offered to the section; the lot name and code are taken from the entry. If the category requires an expiry date, one is needed. Unit is the one sent, else the category default, else the default for the type. Storage condition is the one sent (at most 100 characters), else the category default. A category with approval before use puts the lot at PendingReview; otherwise approval status stays empty. A stock ID is generated from a database sequence and the injected clock; it is never taken from the request. Remaining quantity starts equal to quantity received. Creator and time are stored. The method does not check that quantity received is above zero, that the expiry date is after the receiving date, or that the batch number is unique.
**Code:** `backend/MicroLIMS.Application/Services/MaterialService.cs` — `CreateAsync`, `ResolveMasterEntryAsync`, `EnsureTypeOfferedAsync`, `DefaultUnitFor`
**Verified by:** reading (not run)

### FS-INV-003
**Satisfies:** URS-INV-003
**Function:** Type-specific rules for lots. Lyophilized microorganism: a reference organism must be chosen, must exist and be active (an inactive one is accepted on update only when the lot already points at it); if an organism is also sent it must match the strain's organism; the organism, ATCC number, name and code of the lot are taken from the strain (values sent for them are ignored). Any other type may not have a reference organism. Purity: required and above 0 and not above 100 for reference standards and primary standards; refused for every other type (`ValidatePurity`). Moisture: allowed only for reference standards and must be at least 0 and below 100 (`ValidateMoisture`). These limits are literal values in the code.
**Code:** `backend/MicroLIMS.Application/Services/MaterialService.cs` — `ValidatePurity`, `ValidateMoisture`, `ResolveReferenceOrganismAsync`, `CreateAsync`
**Verified by:** reading (not run)

### FS-INV-004
**Satisfies:** URS-INV-004
**Function:** Edit a stock lot. Needs write section access and a matching version if one is sent (`RecordVersion.EnsureCurrent`). Working standards cannot be edited here or changed into. A move onto a legacy named type is refused. For a dehydrated media lot the media product may be changed only if no media lot has been prepared from the batch; linking a batch that has no product is always allowed. For a lyophilized microorganism lot with a strain, the strain may be changed only if no cryovial batch has been prepared from the lot. A change of material type is checked against the types offered to the lot's section. A free-text custom type cannot be added. A master entry may be replaced only by an entry of the same category, and for Other materials only of the same material category. Name and code are kept as the receipt snapshot while the entry is unchanged. Changing quantity received moves the remaining quantity by the same difference (it is not reset). Batch number, receiving date, expiry date, location, manufacturer, purity and moisture can be changed. No reason is asked, no password is asked, and approval status, approver and approval time are not touched by an edit. An expiry date that is removed is refused only for categories that require one when the lot had one or the entry changes. Last-modified user and time are set. Old and new values are in the automatic audit row.
**Code:** `backend/MicroLIMS.Application/Services/MaterialService.cs` — `UpdateAsync`
**Verified by:** reading (not run)

### FS-INV-005
**Satisfies:** URS-INV-005
**Function:** Consume stock. Called by media preparation, cryovial preparation, additives and the lyophilized disk choice. Inputs: lot ID, expected type (optional), quantity, user. Rules in order: the user needs write access to the lot (`EnsureMaterialAccessAsync`); the lot must exist; the type must match the expected type if one is given; the lot must not be expired (expiry date earlier than today's date refuses; a lot expiring today is accepted); the lot must not be awaiting release or rejected; if the type or category requires a Certificate of Analysis (always for dehydrated media and lyophilized microorganisms; otherwise as the category says), a document of type COA with status Current must exist; a category that does not track quantity returns without deduction; otherwise remaining quantity must be at least the requested quantity, and the quantity is taken off. Last-modified user and time are set. The method does not save: the caller saves the change with the record that used the stock. The method does not check that the requested quantity is above zero; the callers named in FS-MED-002 and FS-CRY-002 pass the total weight and the discs used from the request without such a check, and a negative value would raise the remaining quantity. The additive path checks the quantity first (FS-MED-003).
**Code:** `backend/MicroLIMS.Application/Services/MaterialService.cs` — `ConsumeAsync`
**Verified by:** reading (not run)

### FS-INV-006
**Satisfies:** URS-INV-007
**Function:** Stock queries. `GetAllAsync` returns lots in the readable sections, optionally of one type, ordered by type then name. `GetForPrintAsync` keeps only lots whose computed status is InStock (so expired and depleted lots are left out). `GetUsableReferenceStandardsAsync` returns reference standard and working standard lots in the readable sections with remaining quantity above zero, no expiry date or an expiry date not before today, and approval status empty or Approved. Primary standards are not in this list. `GetTypeOptionsAsync` returns the built-in types for the section and the custom type names already used there. The queries write nothing.
**Code:** `backend/MicroLIMS.Application/Services/MaterialService.cs` — `GetAllAsync`, `GetForPrintAsync`, `GetUsableReferenceStandardsAsync`, `GetTypeOptionsAsync`
**Verified by:** reading (not run)

### FS-INV-007
**Satisfies:** URS-INV-006
**Function:** Release or reject a stock lot. `POST api/materials/{id}/release` needs `Materials.Release`. Inputs: approved yes or no, password, optional comment. One transaction. The user needs write access to the lot; the version is checked if one is sent. Preconditions: approval status is not empty (otherwise the lot needs no release) and is PendingReview; a rejection needs a comment. Segregation of duties: the user must not be the user who received (created) the lot (`SegregationOfDutiesGuard.DidUserReceiveMaterialAsync`). A user who edited the lot after receipt is not excluded. The password is verified and an electronic signature with meaning Approved or Rejected and a review workflow event are written first (`ReviewGateService.SignAndLogAsync`; signature rules are in FS-SIG). Then approval status, approver and time are set. A held or rejected lot is refused by `ConsumeAsync` (FS-INV-005). No archived PDF is written for a stock release (unlike media and cryovial release).
**Code:** `backend/MicroLIMS.Application/Services/MaterialReleaseService.cs` — `DecideAsync`, `DecideCoreAsync`; `backend/MicroLIMS.API/Controllers/MaterialReleaseController.cs` — `Decide`
**Verified by:** reading (not run)

### FS-INV-008
**Satisfies:** URS-INV-001, URS-INV-008
**Function:** Access to the equipment register. `EquipmentInventoryController` needs `Equipment.Manage` on all actions. Lists (all, print, active, where-is-it) are filtered by `GetReadableSectionIdsAsync`; a row with no section is visible only when the scope is null (user can read all). Item reads (by ID, activities, history, status history) call `EnsureEquipmentInventoryAccessAsync` with read access. Update calls it with write access. Create and update both check the chosen section with `ResolveInventorySectionAsync`: the section must be sent, must exist and be active, and must be one of the sections returned by `GetMemberSectionIdsAsync` for the user, otherwise the method refuses with an unauthorized error. The print list contains only items with status InService.
**Code:** `backend/MicroLIMS.API/Controllers/EquipmentInventoryController.cs` — `Create`, `Update`, `GetById`; `backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs` — `ResolveInventorySectionAsync`, `GetAllAsync`, `GetForPrintAsync`
**Verified by:** reading (not run)

### FS-INV-009
**Satisfies:** URS-INV-008
**Function:** Create an equipment item. Inputs: instrument type, manufacturer, serial number, firmware version, code, location, calibration due date, status, section. Rules: the code must not already exist (exact text match); the serial number must not be used by another item of the same manufacturer (case-insensitive, ignoring retired items and placeholder serial values; `EquipmentSerialHelper.IsPlaceholder`); the section is resolved as in FS-INV-008. The item is saved with the status sent, with no comment needed even if the status sent is OutOfService or Retired, and no status-history row is written at creation. Creator and time are stored. The calibration due date is taken as sent; no evidence document is required and no calibration schedule is set here (FS-EQP-003). The method does not check that code, type or manufacturer are not empty.
**Code:** `backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs` — `CreateAsync`, `EnsureUniqueSerialNumberAsync`
**Verified by:** reading (not run)

### FS-INV-010
**Satisfies:** URS-INV-009
**Function:** Edit an equipment item. Needs write access to the item and a matching version if one is sent (`RecordVersion.EnsureCurrent`). Code and serial rules as in FS-INV-009. If the status changes, a non-blank comment is required, a status-history row is added (previous status, new status, comment, user, time from the injected clock), and the automatic out-of-service marker is cleared. If the item was set out of service automatically and a comment is sent without a status change, a history row is also added and the marker is cleared. All other fields, including the calibration due date and the section, are overwritten with the values sent; no reason is asked for a change of calibration due date. The status history is read through `GetStatusHistoryAsync` (read access); a change made by the system shows the system actor name.
**Code:** `backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs` — `UpdateAsync`, `GetStatusHistoryAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-126

### FS-INV-011
**Satisfies:** URS-INV-010
**Function:** Equipment traceability queries. Active equipment: every item in the readable sections is listed, with its calibration due date, its set point (taken from the equipment master record found by matching the register code to the master code without regard to letter case), the number of active incubation activities and a category. Activities: the incubations whose incubator is the master record matched by code; each is shown with sample reference, item, an activity type, media description, start, expected end, completion and whether it is active. The activity type is decided by text: a step name containing the letters "Pathogen" or a test code starting with "PAT" gives "Pathogen Test"; a step name containing "GPT" gives "GPT"; otherwise "Media Incubation". An item with no matching master record shows no activities. History accepts item code text and from and to dates. Where-is-it: the query text is matched, ignoring letter case, against sample reference, item name, media lot number, media name and step name, restricted to incubations in the user's readable sections (by test order section, or for a media-only incubation the section of its stock lot; an incubation with neither link is visible only when the scope is null). The category fallback for idle equipment is decided from the instrument type text and from the code prefixes INC, AUT and LAF. All read queries; nothing is written.
**Code:** `backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs` — `GetActiveEquipmentAsync`, `GetActiveActivitiesForEquipmentAsync`, `GetHistoricalActivitiesForEquipmentAsync`, `WhereIsItAsync`, `ClassifyEquipmentCategory`
**Verified by:** reading (not run)
**Conflicts:** CONF-127
