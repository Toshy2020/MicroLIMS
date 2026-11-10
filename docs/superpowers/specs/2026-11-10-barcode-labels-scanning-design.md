# B1 — Barcode Labels and Scanning: Design Specification

**Roadmap package:** B1 (`improvement-roadmap.md`)
**Date:** 2026-11-10
**Status:** Design approved in conversation on 2026-11-10. This written spec awaits review.
**Inputs:** `barcode-labels-recon-report.md` (2026-10-28) and the current code. Where the two differ, this document follows the code and says so.

---

## 1. Purpose

Analysts identify samples, media lots, solutions, cryovials, stock lots and equipment by reading a code and typing or picking it. That is slow and is a source of transcription errors.

This package lets the laboratory:

1. print a barcode label for each of those six record types,
2. open a record by scanning its label, and
3. choose a lot or an instrument during testing or preparation by scanning it, in place of picking from a list.

**Success looks like:** a sample is received, labelled, and opened by scan; a media lot and its incubator are selected by scan during a test; every label print is attributable; and the server still rejects an unusable lot whichever way it was chosen.

## 2. Decisions taken

| # | Decision | Choice |
|---|---|---|
| 1 | Printer | Thermal label printer, fed a PDF through its driver |
| 2 | Scanners | USB 2D imagers (keyboard wedge), plus tablet camera |
| 3 | Symbology | Code 128 for Sample and Equipment; Data Matrix (ECC 200) for the other four |
| 4 | Records labelled | Sample, Media lot, Solution preparation, Cryovial, Material stock lot, Equipment |
| 5 | Reprint control | Reason plus the `Labels.Reprint` permission; no e-signature |
| 6 | Equipment sticker | Carries the asset register code (`EquipmentInventory.Code`) |
| 7 | Print timing | Offered after creation; never automatic |
| 8 | Libraries | One qualified open-source encoder on the backend; one decoder on the frontend for the camera |
| 9 | Stock lot identifier | The existing `Material.StockId` |
| 10 | Arabic keyboard layout | Handled: converted back and the user is told |
| 11 | Label text | Standard English set; the small template shows code and expiry only |

## 3. Out of scope

- A label designer that lets users lay out their own templates.
- ZPL or any other printer language.
- Arabic text on labels (`PdfCanvas` draws Latin text only).
- Automatic printing without a user action.
- Merging the two equipment registers.
- Labels for HPLC/ICP runs, system suitability records, chromatography columns, water sampling points and items. The lookup can be extended to them later.
- Recording individual scans in the audit trail. A scan is a read; the action that follows it is already audited.

## 4. Current state

Confirmed in the code on 2026-11-10:

- No barcode, label or print-audit code exists. No barcode package is referenced.
- **Recon gap G1 is closed.** `Material.StockId` (`STK-{year}-{number}`, `StockIdGenerator`) is system-generated at receipt, never edited, and has a unique filtered index (`IX_Materials_StockId`). It is null only on legacy lots that were already expired or depleted when stock IDs were introduced.
- `EquipmentInventory.Code` is unique in the database (max 80) but can be edited.
- `Equipment` (the workflow list that tests pick incubators and autoclaves from) has no link to `EquipmentInventory`.
- `Cryovial.Code` is unique but has no maximum length.
- `GlobalSearchDialog` waits 300 ms before searching and acts on Enter immediately, so a scanner's "code + Enter" is lost.
- `NewSampleDialog` reports only a count on success, not the samples it created.
- `PdfCanvas` is a dependency-free PDF writer with a fixed A4 page.
- `AuditEventService.RecordUserEventAsync` writes explicit audit events. `IUserSectionScopeService` provides per-record `Ensure*AccessAsync` guards.

## 5. Design

### 5.1 Label types and payload

The barcode holds `TYPE:CODE`. The same code is printed as text beside it.

| Record | Type prefix | Code field | Symbology | Example payload |
|---|---|---|---|---|
| Sample | `S` | `Sample.ReferenceNumber` | Code 128 | `S:FP1026001` |
| Media lot | `MED` | `Media.LotNumber` | Data Matrix | `MED:TSA/03/26` |
| Solution preparation | `SP` | `SolutionPreparation.Code` | Data Matrix | `SP:MP-VIT-C 05/11/2026` |
| Cryovial | `CRY` | `Cryovial.Code` | Data Matrix | `CRY:ECOLI/02/26` |
| Material stock lot | `MAT` | `Material.StockId` | Data Matrix | `MAT:STK-2026-00123` |
| Equipment | `EQ` | `EquipmentInventory.Code` | Code 128 | `EQ:INC-F-ML-F-01-003` |

Rules:

- The type registry is one backend class. Adding a type later means adding one entry and one template.
- A record with no code cannot be labelled: a solution preparation that is still In Progress, and a legacy stock lot without a `StockId`. The Print button is disabled with the reason shown, and the server refuses the request.
- A working standard is a stock lot and is labelled by its `StockId`, with its `WS-…` code printed as text.

### 5.2 Identifier changes

1. **Cryovial code length.** Add a maximum length to `Cryovial.Code`. The value is chosen in the implementation plan from the longest code in existing data, with room to spare; the migration must not truncate any existing row.
2. **Equipment code freeze.** `EquipmentInventoryService` refuses a change to `Code` once any label has been printed for that asset. The error names the reason. Before the first print the code stays editable.
3. **Equipment link.** `Equipment` gains an optional `EquipmentInventoryId`. The Section Head sets it on the equipment master-data page. One asset links to at most one workflow record (unique filtered index). A workflow record without a link cannot be selected by scan; picking from the list still works.

This link changes nothing about how tests validate equipment today. Using it to check calibration status during a test is a separate decision and is not part of this package.

### 5.3 Label formats

A `LabelFormat` record exists per label type and holds:

- width and height in millimetres,
- layout: `Standard` or `Small`,
- default number of copies.

Formats are versioned and audited like other configuration. They are edited by holders of a new `Labels.Configure` permission. Seed data provides one starting format per type so the feature works before sizes are tuned; the laboratory sets the real sizes after choosing label stock.

Fields printed by the fixed templates:

| Record | Standard layout | Small layout |
|---|---|---|
| Sample | reference number, item name, batch number, received date | reference number |
| Media lot | lot number, media name, prepared date, expiry, prepared by | lot number, expiry |
| Solution preparation | code, solution name, prepared date, expiry, prepared by, storage condition | code, expiry |
| Cryovial | code, organism name, prepared date, expiry | code, expiry |
| Material stock lot | stock ID, material name, supplier batch, received date, expiry, storage condition | stock ID, expiry |
| Equipment | asset code, instrument type, location | asset code |

- "Prepared by" prints the user's initials.
- Dates use the laboratory clock (`ILabClock`), the same clock that builds the codes.
- Every label prints the print date. A reprint also prints `REPRINT n`.
- A label shows values as they were at print time. The scan always returns the live status (5.6).

### 5.4 Rendering

- `ILabelRenderer` is an Application interface. Its Infrastructure implementation draws with `PdfCanvas`.
- `PdfCanvas` takes its page size as a parameter. Existing A4 callers keep their behaviour.
- Barcodes are drawn as filled rectangles, so they are vector and sharp at any printer resolution.
- **ZXing.Net** computes the module pattern for both Code 128 and Data Matrix. It is used for nothing else. It is added in `Directory.Packages.props` and gets a qualification record under `documents/Validation/`.
- The renderer refuses a payload that does not fit the configured label size at a readable module size, and says so. It never shrinks a barcode below the minimum.
- Minimum module size and quiet zone are constants in the renderer, with their source cited in a comment.

### 5.5 Printing and its record

**Endpoint.** `POST /api/labels/print` with `{ entityType, entityId, copies, reason? }` returns the PDF.

**Flow.**
1. Check the permission and the record's section scope.
2. Decide first print or reprint from the print log.
3. Write the print log row.
4. Render the PDF from committed data and return it.

Rendering happens after the database work, never inside a transaction.

**Permissions.**
- First print: the permission that allows creating that record (`Samples.Receive` or `Samples.ReceiveOwnLab`, `Media.Prepare`, `Solutions.Prepare`, `Cryovials.Manage`, `Materials.Manage`, `Equipment.Manage`).
- Reprint: `Labels.Reprint`, seeded to Section Heads, and a reason of at least 10 characters.
- Copies are clamped to 1–50 by the server.

**Print log.** A new `LabelPrintLog` entity records each print: label type, record ID, the code printed, format version, copies, print number, whether it is a reprint, reason, user, and UTC time from the injected clock.

- The print number is 1 for the first print and increases by one per reprint. A unique index on (label type, record ID, print number) stops two simultaneous requests taking the same number; the loser gets a conflict and retries.
- The table is append-only: a PostgreSQL trigger rejects `UPDATE`, `DELETE` and `TRUNCATE`. It is added to `AppendOnlyTriggerCatalogPostgresTests.ProtectedTables` and gets its own rejection test.
- Each insert is captured by the automatic audit trail. In addition, an explicit event `LABEL_PRINTED` or `LABEL_REPRINTED` is written through `AuditEventService` in the same transaction, so prints appear in Audit Search by sample.

**What the log proves.** It proves that the system produced a label PDF for that user. It cannot prove the paper came out of the printer, because the browser's print dialog can be cancelled. The label SOP must state this.

**Where printing is offered.**
- After sample receipt: the receipt endpoint returns the IDs and reference numbers it created, and the dialog offers "Print labels" for them.
- After a media lot, solution preparation or cryovial batch is completed, and after a stock lot is received.
- A "Print label" button on each record's detail view. If a label was printed before, it becomes "Reprint label" and opens a dialog that asks for the reason.

The PDF opens in a new window for printing, which avoids the application's global A4 print rule.

### 5.6 Scan lookup

**Endpoint.** `GET /api/scan/resolve?value=…` returns:

```
{ entityType, id, code, displayName, status, usable, blockedReason, navigationTarget }
```

**Matching.**
1. Normalise the input (5.7).
2. If it has a known type prefix, look up that type by exact code.
3. If it has no prefix, look for an exact code match across all six types. One match is returned. More than one returns an "ambiguous" result listing the candidates, and the user chooses.
4. Matching is exact and case-insensitive. It never uses "contains".

**Scope.** The endpoint runs the matching `IUserSectionScopeService` guard with read access. A record outside the user's sections returns the same "not found" as a code that does not exist.

**Usability.** `usable` and `blockedReason` are computed by the backend from the same rules the services enforce:

| Record | Usable when |
|---|---|
| Sample | Always opens; cancelled and voided samples show a status banner |
| Media lot | Released for use and within expiry |
| Solution preparation | Effectively Prepared (not expired, discarded or cancelled) |
| Cryovial | Approved and vials remain |
| Material stock lot | Not expired and not depleted |
| Equipment | In service |

The lookup informs the user. It is not the gate: the existing service validation on save remains the final check.

**Rate limit.** The endpoint is partitioned by authenticated user, like the other limits.

### 5.7 Input normalisation

Done in the backend so there is one implementation:

1. Trim leading and trailing whitespace and control characters. Inner spaces are kept, because solution codes contain them.
2. If the input contains Arabic-layout characters, map each back to the Latin key at the same position on the Arabic (101) keyboard layout, and set `layoutCorrected: true` in the response. The frontend then shows "Keyboard was in Arabic; the code was converted."
3. If the converted value matches nothing, the response is "not found" with a hint to switch the keyboard to English.

The mapping table is a fixed constant with a unit test for every key that can appear in a code (letters, digits, `/`, `-`, `.`, `:` and space).

### 5.8 Scan to open

- The Ctrl+K dialog calls the resolve endpoint when Enter is pressed. If it returns one record, the dialog navigates to it. Otherwise the dialog falls back to the normal search results.
- Enter cancels the pending debounced search and waits for the resolve call, so a fast "code + Enter" from a scanner is never lost or matched to a stale result.
- Typing behaves as it does today.

### 5.9 Scan to select

One shared component, `ScanField`, sits beside a picker. On a scan it calls the resolve endpoint and then:

- selects the option if the returned record is among the options the server already offered for that picker;
- shows `blockedReason` if the record is not usable;
- shows "This is a {type}, not a {expected type}" if the type is wrong;
- shows "Not available for this step" if the record is valid but not among the offered options.

It never adds an option that the server did not offer.

Pickers that get a `ScanField`, in this order:

1. `MediaLotIncubatorFields` — media lot and incubator. One component, used by four test screens. The incubator scan resolves the asset, then selects the workflow equipment linked to it (5.2).
2. `WizardMobilePhasesStep` — HPLC mobile phase.
3. `ComponentLotPicker` — solution component stock lots.
4. `MediaPreparationDialog` — dehydrated media stock lot and autoclave.
5. Cryovial selection in growth promotion and reference strain steps.

The HPLC column step is not included, because columns are not labelled in this package.

**Camera.** `ScanField` and the Ctrl+K dialog have a camera button on devices with a camera. It uses the browser's `BarcodeDetector` where available and one qualified decoder library elsewhere. The library is chosen in the implementation plan after checking licence and maintenance status. The camera needs HTTPS and the user's permission; if either is missing, the button explains why and typing or a handheld scanner still works.

### 5.10 Accessibility and UI rules

- `ScanField` has a programmatic label, and its result messages use `role="status"` or `role="alert"`.
- The camera view closes on Escape and returns focus to the field.
- The reprint dialog uses `FormDialog`; the reason field has inline validation.
- A disabled Print button always states its reason.
- Status colours come from `theme.custom.status`; no raw hex values.

## 6. Data changes

| Change | Kind |
|---|---|
| `LabelPrintLog` table, unique index, append-only trigger | New |
| `LabelFormat` table with seed rows | New |
| `Equipment.EquipmentInventoryId`, nullable, unique filtered index | New column |
| `Cryovial.Code` maximum length | Altered column |
| Permissions `Labels.Reprint`, `Labels.Configure` with role mapping | Seed |
| `AuditActionCategory.Labelling` | New enum value, appended |

Each change ships in an explicit migration. `dotnet ef migrations has-pending-model-changes` must report none.

## 7. Error handling

| Situation | Behaviour |
|---|---|
| Record has no code yet | 409 with the reason; button disabled in the UI |
| Reprint without permission | 403 |
| Reprint without a reason | 400 with a field error |
| Record outside the user's sections | 404, identical to "does not exist" |
| Payload does not fit the label size | 422 naming the label format to adjust |
| Two prints race for the same print number | One succeeds; the other retries once, then returns 409 |
| Equipment code edit after a label was printed | 409 with the reason |
| Scan matches several records | "Ambiguous" result; the user picks |
| Camera unavailable or refused | Inline message; other input methods unaffected |

All errors reach the user through `sonner` toasts or inline helper text.

## 8. Testing

**Unit**
- Type registry: payload build and parse for all six types, including codes with spaces, `/`, `.` and `-`.
- Arabic-layout mapping for every character that can occur in a code.
- Usability rules per record type, with "now" passed in.

**Rendering**
- Encode each payload, render the PDF, rasterise it, decode it, and compare with the input. This runs for both symbologies and for both layouts.
- The renderer refuses an oversized payload.

**PostgreSQL integration (`[PostgresFact]`, zero skipped)**
- First print writes the log row and the `LABEL_PRINTED` audit event with the right user, record and values.
- A reprint without `Labels.Reprint` is rejected; without a reason is rejected; with both it writes print number 2 and `LABEL_REPRINTED`.
- The unique index rejects a duplicate print number.
- `UPDATE`, `DELETE` and `TRUNCATE` on `LabelPrintLog` are rejected; the trigger catalog test lists the table.
- A user in section A resolving a section B code gets "not found".
- The equipment code can be edited before a print and is rejected after one.
- One asset cannot be linked to two workflow equipment records.
- The cryovial migration keeps every existing code.

Each test uses separate contexts for arrange, act and assert, and reads with `AsNoTracking`.

**Frontend**
- `ScanField` with the real picker around it: select on match, each of the four refusal messages, and the layout-corrected notice. `userEvent` only; only the service module is mocked.
- Ctrl+K: a code typed and Enter pressed within 50 ms opens the record.
- Reprint dialog: reason required; the request carries it.

**Manual gates (cannot be automated)**
- Test print on the real printer and label stock, at 100% scale.
- Readback of every label type with the real handheld scanners and with a tablet camera.
- A scan with the Arabic keyboard layout active.

## 9. Delivery stages

Each stage is shippable and ends with its tests passing.

1. **Identifiers and data** — migrations, equipment link and freeze, cryovial length, permissions.
2. **Rendering** — `PdfCanvas` page size, `ILabelRenderer`, ZXing.Net, six templates, label formats.
3. **Print and record** — print endpoint, print log, audit events, Print and Reprint in the UI, labels offered after creation.
4. **Scan to open** — resolve endpoint, normalisation, Ctrl+K change.
5. **Scan to select** — `ScanField` and the five pickers.
6. **Camera** — decoder library and camera button.

## 10. Open items before go-live

These do not block writing the implementation plan.

| Item | Needed for |
|---|---|
| Printer model and resolution | Test-print gate |
| Label stock sizes per record type | Final `LabelFormat` values |
| Handheld scanner model | Readback gate |
| Label handling SOP, including what the print log does and does not prove | Go-live |
| Qualification records for ZXing.Net and the camera decoder | Go-live |
