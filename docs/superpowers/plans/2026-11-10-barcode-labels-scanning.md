# Barcode Labels and Scanning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Print barcode labels for six record types, open a record by scanning its label, and select lots and instruments by scan during testing and preparation.

**Architecture:** The backend owns everything that decides: payload format, input normalisation, section scope, usability, print numbering and the label PDF. Labels are vector PDFs drawn by the existing `PdfCanvas`; ZXing.Net only computes the barcode module pattern. The frontend adds one print dialog, one shared `ScanField`, and a change to the Ctrl+K dialog; all three call two new endpoints.

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL, xUnit, ZXing.Net (new), SkiaSharp (existing, tests only here); React 19, TypeScript 6, MUI 9, Vitest, `barcode-detector` 3.2.3 (new).

**Spec:** `docs/superpowers/specs/2026-11-10-barcode-labels-scanning-design.md`. Read it together with `CLAUDE.md` before starting any task.

## Global Constraints

- Payload is `TYPE:CODE`. Prefixes: `S`, `MED`, `SP`, `CRY`, `MAT`, `EQ`. Code 128 for `S` and `EQ`; Data Matrix (ECC 200) for the rest.
- Number of labels per record: 1–50. The server rejects other values; it never clamps silently.
- Reprint reason: at least 10 characters after trimming.
- New permissions: `Labels.Reprint` (seeded to Section Head), `Labels.Configure` (seeded to Section Head).
- A record outside the caller's sections answers exactly like a record that does not exist.
- No clock reads outside the injected `TimeProvider` / `ILabClock`. Dates printed on labels use `ILabClock`.
- No organism name, test code or medium name in any string literal in Application, API or Domain code.
- Every async method takes and forwards `CancellationToken`.
- Package versions go in `backend/Directory.Packages.props` only.
- Every new or changed test follows `CLAUDE.md` section 4: separate contexts for arrange, act and assert; `AsNoTracking` in assert; `[PostgresFact]` for anything relational; `userEvent`, never `fireEvent`; mock only `services/` modules.
- Backend verification for every task that touches persistence:
  ```bash
  service postgresql start
  export Jwt__Key="$(openssl rand -base64 48)"
  export MICROLIMS_TEST_POSTGRES="Host=localhost;Port=5432;Database=postgres;Username=microlims_ci"
  dotnet build backend/MicroLIMS.sln --configuration Release
  dotnet test backend/MicroLIMS.Tests --configuration Release --no-build
  ```
  A skipped `[PostgresFact]` is a failed verification.
- Frontend verification for every frontend task: `cd frontend && npm run lint && npx tsc -b && npm run test && npm run debt:check`.

## Review Focus

1. **A code that contains the separator.** A solution code such as `MP-A:B 05/11/2026` contains `:`. Parsing must split on the first `:` only when the part before it is a known prefix. Test in Task 1.
2. **A bare code that exists in two record types.** Resolve must return an ambiguous result with both candidates, never the first one found. Test in Task 9.
3. **A scan that arrives while the previous resolve is still running.** The later scan must win; the earlier response must be discarded. Test in Task 12.
4. **A record whose text is longer than the label.** Long item or material names must be ellipsized to the label width, never overflow onto the barcode. Test in Task 5.
5. **A print request that mixes a first print and a reprint.** The reprint rules (permission and reason) apply to the whole request if any item is a reprint. Test in Task 7.

## File Structure

**Backend — new**

| File | Responsibility |
|---|---|
| `MicroLIMS.Domain/Enums/LabelEnums.cs` | `LabelEntityType`, `LabelSymbology`, `LabelLayout` |
| `MicroLIMS.Domain/Entities/LabelPrintLog.cs` | One row per record per print |
| `MicroLIMS.Domain/Entities/LabelFormat.cs` | Size, layout and default copies per label type |
| `MicroLIMS.Persistence/Configurations/LabelPrintLogConfiguration.cs`, `LabelFormatConfiguration.cs` | Mapping and indexes |
| `MicroLIMS.Application/Labels/LabelPayload.cs` | Build and parse `TYPE:CODE`; symbology per type |
| `MicroLIMS.Application/Labels/ScanInputNormalizer.cs` | Trim and Arabic-layout conversion |
| `MicroLIMS.Application/Labels/LabelUsability.cs` | Usable / blocked reason per record type |
| `MicroLIMS.Application/Labels/LabelDataSource.cs` | Loads code, text fields and section-guard for a record |
| `MicroLIMS.Application/Labels/LabelFormatService.cs` | Read and update formats |
| `MicroLIMS.Application/Labels/LabelPrintService.cs` | Permission, numbering, log, audit, render |
| `MicroLIMS.Application/Labels/ScanResolveService.cs` | Exact lookup with scope and usability |
| `MicroLIMS.Application/Interfaces/ILabelRenderer.cs`, `IBarcodeEncoder.cs` | Abstractions implemented in Infrastructure |
| `MicroLIMS.Infrastructure/Labels/ZXingBarcodeEncoder.cs` | Module patterns |
| `MicroLIMS.Infrastructure/Labels/LabelPdfRenderer.cs` | Draws labels with `PdfCanvas` |
| `MicroLIMS.API/Controllers/LabelController.cs`, `ScanController.cs` | Endpoints |

**Backend — modified**

`PdfCanvas.cs` (page size), `Equipment.cs` + configuration (asset link), `CryovialConfiguration.cs` (max length), `EquipmentInventoryService.cs` (code freeze), `MasterData/EquipmentMasterDataService.cs` (set link), `PermissionConstants.cs`, `DbSeeder.cs`, `AuditActionCategory.cs`, `MicroLimsDbContext.cs` (two `DbSet`s), `ServiceCollectionExtensions.cs`, `SampleController.cs` receipt response, `Directory.Packages.props`, `AppendOnlyTriggerCatalogPostgresTests.cs`.

**Frontend — new**

| File | Responsibility |
|---|---|
| `src/services/labelService.ts` | `printLabels`, `getLabelFormats`, `updateLabelFormat`, `resolveScan`, and their types |
| `src/components/labels/PrintLabelsDialog.tsx` | Number of labels per record, reprint reason, opens the PDF |
| `src/components/labels/PrintLabelButton.tsx` | Print / Reprint button with disabled reason |
| `src/components/labels/ScanField.tsx` | Scan input beside a picker |
| `src/components/labels/CameraScanDialog.tsx` | Camera capture |
| `src/components/labels/useScanResolve.ts` | Latest-wins resolve call |
| `src/modules/laboratoryConfiguration/labelFormats/LabelFormatsPage.tsx` | Edit sizes |

---

### Task 1: Payload registry and input normaliser

**Files:**
- Create: `backend/MicroLIMS.Domain/Enums/LabelEnums.cs`, `backend/MicroLIMS.Application/Labels/LabelPayload.cs`, `backend/MicroLIMS.Application/Labels/ScanInputNormalizer.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/LabelPayloadTests.cs`, `ScanInputNormalizerTests.cs`

**Interfaces — Produces:**
```csharp
public enum LabelEntityType { Sample = 1, MediaLot = 2, SolutionPreparation = 3, Cryovial = 4, MaterialStockLot = 5, Equipment = 6 }
public enum LabelSymbology { Code128 = 1, DataMatrix = 2 }
public enum LabelLayout { Standard = 1, Small = 2 }

public static class LabelPayload
{
    public static string PrefixOf(LabelEntityType type);            // S, MED, SP, CRY, MAT, EQ
    public static LabelSymbology SymbologyOf(LabelEntityType type);
    public static string Build(LabelEntityType type, string code);  // "MED:TSA/03/26"
    // true only when the text before the FIRST ':' is a known prefix (case-insensitive)
    public static bool TryParse(string value, out LabelEntityType type, out string code);
}

public readonly record struct NormalizedScan(string Value, bool LayoutCorrected);
public static class ScanInputNormalizer { public static NormalizedScan Normalize(string? raw); }
```

- [ ] **Step 1: Write failing tests**

```csharp
[Theory]
[InlineData(LabelEntityType.Sample, "FP1026001", "S:FP1026001")]
[InlineData(LabelEntityType.SolutionPreparation, "MP-VIT-C 05/11/2026", "SP:MP-VIT-C 05/11/2026")]
[InlineData(LabelEntityType.MaterialStockLot, "STK-2026-00123", "MAT:STK-2026-00123")]
public void Build_then_parse_round_trips(LabelEntityType type, string code, string payload)

[Fact] public void Parse_splits_on_first_colon_only()        // "SP:MP-A:B 05/11/2026" -> SolutionPreparation, "MP-A:B 05/11/2026"
[Fact] public void Parse_rejects_unknown_prefix()            // "XX:ABC" -> false ; "MP-A:B" -> false
[Fact] public void Parse_is_case_insensitive_on_prefix()     // "med:TSA/03/26" -> MediaLot
[Fact] public void Sample_and_equipment_use_code128_others_datamatrix()

[Fact] public void Normalize_trims_ends_and_keeps_inner_spaces()   // "  SP:MP-X 05/11/2026\r\n" -> "SP:MP-X 05/11/2026", false
[Fact] public void Normalize_converts_arabic_layout()              // "ض" -> "q"; full payload typed on Arabic 101 -> Latin, true
[Fact] public void Normalize_null_or_blank_returns_empty()
[Theory] /* one InlineData per key */ public void Arabic_101_key_maps_to_latin(string arabic, string latin)
```

- [ ] **Step 2: Run** `dotnet test backend/MicroLIMS.Tests --filter "FullyQualifiedName~LabelPayloadTests|FullyQualifiedName~ScanInputNormalizerTests"` — expect compile failure.
- [ ] **Step 3: Implement.** The Arabic map is a `static readonly Dictionary<char,char>` for the Arabic (101) layout covering every letter key, the digit row (Arabic-Indic digits `٠`–`٩` to `0`–`9`), and the keys that produce `/ - . :` on the Latin layout. The two-character key `لا` (Latin `b`) is handled before the single-character pass. Cite the layout source in a comment.
- [ ] **Step 4: Run** the same filter — expect PASS.
- [ ] **Step 5: Commit** `feat(labels): payload registry and scan input normaliser`.

---

### Task 2: Data model and migration

**Files:**
- Create: `LabelPrintLog.cs`, `LabelFormat.cs`, their two configurations, migration `AddLabelPrinting`
- Modify: `Equipment.cs`, `EquipmentConfiguration.cs`, `CryovialConfiguration.cs`, `AuditActionCategory.cs`, `PermissionConstants.cs`, `DbSeeder.cs`, `MicroLimsDbContext.cs`, `AppendOnlyTriggerCatalogPostgresTests.cs`
- Test: `backend/MicroLIMS.Tests/IntegrationTests/LabelPrintingSchemaPostgresTests.cs`

**Interfaces — Produces:**
```csharp
public class LabelPrintLog
{
    public int Id { get; set; }
    public LabelEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public string Code { get; set; } = string.Empty;        // max 250
    public int PrintNumber { get; set; }                    // 1 = first print
    public bool IsReprint { get; set; }
    public int Copies { get; set; }
    public int LabelFormatId { get; set; }
    public uint LabelFormatVersion { get; set; }
    public string? Reason { get; set; }                     // max 500
    public int PrintedByUserId { get; set; }
    public DateTime PrintedAt { get; set; }                 // UTC, set by the service
    public Guid BatchId { get; set; }                       // one per print request
}

public class LabelFormat : IVersionedEntity
{
    public int Id { get; set; }
    public uint Version { get; set; }
    public LabelEntityType EntityType { get; set; }         // unique
    public decimal WidthMm { get; set; }                    // 10–150
    public decimal HeightMm { get; set; }                   // 10–150
    public LabelLayout Layout { get; set; }
    public int DefaultCopies { get; set; }                  // 1–50
}

// Equipment
public int? EquipmentInventoryId { get; set; }
public EquipmentInventory? EquipmentInventory { get; set; }

// AuditActionCategory
Labelling = 10

// PermissionConstants
public const string LabelsReprint = "Labels.Reprint";
public const string LabelsConfigure = "Labels.Configure";
```

Schema decisions:
- `LabelPrintLogs`: unique index on (`EntityType`, `EntityId`, `PrintNumber`); index on `BatchId`.
- `LabelPrintLogs` gets `trg_labelprintlogs_immutable` (before UPDATE or DELETE, for each row, raising an exception) and `trg_labelprintlogs_no_truncate` (reusing `prevent_audit_truncate()`), following the SQL in migration `20261028011340_AddAccessReviews`. `Down()` drops the triggers before the table.
- `LabelFormats`: unique index on `EntityType`. Check constraints for the three ranges above.
- `Equipment.EquipmentInventoryId`: unique filtered index where not null; foreign key with `Restrict`.
- `Cryovial.Code`: `HasMaxLength(250)`. This cannot truncate: the prefix is at most a 200-character material name plus `/nn/yy`.
- Seed in the migration: six `LabelFormats` rows — Sample 50×25 Standard, Media lot 40×20 Standard, Solution preparation 50×25 Standard, Cryovial 25×10 Small, Material stock lot 50×25 Standard, Equipment 50×25 Standard; `DefaultCopies` 1. These are starting values the laboratory changes later.
- Seed in the migration and in `DbSeeder`: the two permissions and their grant to the Section Head role.

- [ ] **Step 1: Write failing tests** (`[PostgresFact]`, `[Collection("PostgresDatabaseCollection")]`, `PostgresTestFixture`)

```csharp
public async Task Duplicate_print_number_for_same_record_is_rejected()      // second insert -> DbUpdateException, 23505
public async Task Update_of_print_log_is_rejected_by_trigger()              // raw UPDATE -> PostgresException
public async Task Delete_of_print_log_is_rejected_by_trigger()
public async Task Truncate_of_print_log_is_rejected_by_trigger()
public async Task One_asset_cannot_link_to_two_workflow_equipment_rows()   // 23505
public async Task Six_label_formats_are_seeded_one_per_type()
public async Task Label_permissions_exist_and_are_granted_to_section_head()
public async Task Label_format_rejects_out_of_range_size()                 // WidthMm = 5 -> 23514
public async Task Cryovial_code_of_250_characters_is_accepted()
```
Add `["LabelPrintLogs"] = Update | Delete` to `ProtectedTables` in `AppendOnlyTriggerCatalogPostgresTests`.

- [ ] **Step 2: Run** `dotnet test backend/MicroLIMS.Tests --filter "FullyQualifiedName~LabelPrintingSchemaPostgresTests"` — expect compile failure.
- [ ] **Step 3: Add entities, configurations, `DbSet<LabelPrintLog> LabelPrintLogs`, `DbSet<LabelFormat> LabelFormats`, enum value, constants, seeder rows.**
- [ ] **Step 4: Generate the migration**
  ```bash
  cd backend/MicroLIMS.API
  dotnet ef migrations add AddLabelPrinting --project ../MicroLIMS.Persistence --startup-project .
  ```
  Then add the trigger SQL, check constraints and seed rows by hand. Inspect `Up()` for any `DropTable`, `RenameTable` or column-type change on a protected table; there must be none.
- [ ] **Step 5: Run** `dotnet ef migrations has-pending-model-changes --project ../MicroLIMS.Persistence --startup-project .` — expect "No changes".
- [ ] **Step 6: Run** the schema tests, `AppendOnlyTriggerCatalogPostgresTests` and `FreshDatabaseSeedPostgresTests` — expect PASS, 0 skipped.
- [ ] **Step 7: Commit** `feat(labels): print log, label formats, equipment asset link`.

---

### Task 3: Equipment code freeze and asset link

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/EquipmentInventoryService.cs` (`UpdateAsync`, around line 200), `backend/MicroLIMS.Application/Services/MasterData/EquipmentMasterDataService.cs`, their request/response DTOs, `frontend/src/modules/laboratoryConfiguration/masterDataSimple/EquipmentPage.tsx`
- Test: `backend/MicroLIMS.Tests/IntegrationTests/EquipmentLabelLinkPostgresTests.cs`

**Interfaces:**
- Consumes: `LabelPrintLog`, `Equipment.EquipmentInventoryId` (Task 2).
- Produces: the equipment master save request and response carry `int? EquipmentInventoryId`; the asset response carries `bool CodeLocked`.

Behaviour:
- `UpdateAsync` throws `BusinessRuleException("The code cannot be changed because a label has been printed for this instrument.")` when `r.Code != entity.Code` and a `LabelPrintLog` row exists for (`Equipment`, `id`).
- The master-data service accepts `EquipmentInventoryId`, verifies the caller can read that asset (`EnsureEquipmentInventoryAccessAsync`, `SectionAccess.Read`) and that it is not linked elsewhere, and saves it. Clearing the link is allowed.
- `EquipmentPage.tsx` gets an "Asset register entry" picker (labelled, clearable) in the equipment form, and the asset form shows the code field read-only with helper text "Locked: a label has been printed" when `codeLocked`.

- [ ] **Step 1: Write failing tests**
```csharp
public async Task Asset_code_can_change_before_any_label_is_printed()        // persisted code changed; AuditLogs row with old/new code
public async Task Asset_code_change_is_rejected_after_a_label_is_printed()   // BusinessRuleException; code unchanged in fresh context
public async Task Linking_an_asset_persists_and_is_audited()                 // Equipment.EquipmentInventoryId set; AuditLogs UserId/RecordId/NewValues
public async Task Linking_an_asset_already_linked_elsewhere_is_rejected()
public async Task Linking_an_asset_outside_callers_sections_is_rejected()
```
- [ ] **Step 2: Run** the filter — expect FAIL.
- [ ] **Step 3: Implement** the service changes and DTO fields.
- [ ] **Step 4: Run** — expect PASS.
- [ ] **Step 5: Frontend:** add the picker and the locked field; extend the existing `EquipmentPage` test file with "shows the code as locked with its reason" and "saves the chosen asset link" using `userEvent`. Run frontend verification.
- [ ] **Step 6: Commit** `feat(labels): freeze labelled asset codes and link workflow equipment to assets`.

---

### Task 4: Barcode encoder

**Files:**
- Modify: `backend/Directory.Packages.props`, `backend/MicroLIMS.Infrastructure/MicroLIMS.Infrastructure.csproj`
- Create: `backend/MicroLIMS.Application/Interfaces/IBarcodeEncoder.cs`, `backend/MicroLIMS.Infrastructure/Labels/ZXingBarcodeEncoder.cs`, `documents/Validation/ZXingNet-Qualification.md`
- Test: `backend/MicroLIMS.Tests/UnitTests/BarcodeEncoderTests.cs`

**Interfaces — Produces:**
```csharp
public interface IBarcodeEncoder
{
    bool[] EncodeCode128(string content);          // one entry per module, true = bar, no quiet zone
    bool[,] EncodeDataMatrix(string content);      // [row, column], true = dark, square, no quiet zone
}
```

- [ ] **Step 1: Add `ZXing.Net`** to `Directory.Packages.props` at the newest stable version on nuget.org that day, pinned exactly, and reference it without a version from the Infrastructure project and the Tests project. Note: its licence is Apache-2.0 (the recon report says MIT; that is wrong).
- [ ] **Step 2: Write failing tests.** Each test encodes, paints the modules into a SkiaSharp bitmap at 4 pixels per module with a 10-module white margin, decodes with ZXing's reader, and asserts the decoded text equals the input.
```csharp
[Theory] /* S:FP1026001 ; EQ:INC-F-ML-F-01-003 */            public void Code128_round_trips(string payload)
[Theory] /* MED:TSA/03/26 ; SP:MP-VIT-C 05/11/2026 ; MAT:STK-2026-00123 ; a 254-character CRY payload */
                                                               public void DataMatrix_round_trips(string payload)
[Fact] public void DataMatrix_is_square()
[Fact] public void Empty_content_throws_ArgumentException()
```
- [ ] **Step 3: Run** — expect FAIL.
- [ ] **Step 4: Implement** with `Code128Writer` and `DataMatrixWriter` (force square shape), requesting width and height 1 so the returned `BitMatrix` is one cell per module, and strip any margin the writer adds.
- [ ] **Step 5: Run** — expect PASS.
- [ ] **Step 6: Write the qualification record:** package, exact version, licence, what it is used for (module patterns only), what it is not used for, and the round-trip tests as its verification.
- [ ] **Step 7: Commit** `feat(labels): barcode module encoder on ZXing.Net`.

---

### Task 5: Label PDF renderer

**Files:**
- Modify: `backend/MicroLIMS.Infrastructure/Pdf/PdfCanvas.cs`
- Create: `backend/MicroLIMS.Application/Interfaces/ILabelRenderer.cs`, `backend/MicroLIMS.Infrastructure/Labels/LabelPdfRenderer.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/LabelPdfRendererTests.cs`

**Interfaces:**
- Consumes: `IBarcodeEncoder`, `LabelPayload`.
- Produces:
```csharp
public sealed record LabelField(string Name, string Value);
public sealed record LabelContent(LabelEntityType Type, string Code, IReadOnlyList<LabelField> StandardFields, IReadOnlyList<LabelField> SmallFields);
public sealed record LabelRenderItem(LabelContent Content, decimal WidthMm, decimal HeightMm, LabelLayout Layout,
                                     int Copies, int PrintNumber, DateOnly PrintDate);
public sealed class LabelDoesNotFitException(string message) : Exception(message);

public interface ILabelRenderer { byte[] Render(IReadOnlyList<LabelRenderItem> items); }

// PdfCanvas: new constructor; the parameterless one keeps A4
public PdfCanvas(double pageWidthPt, double pageHeightPt);
public double PageWidthPt { get; }
public double PageHeightPt { get; }
```

Rendering rules:
- One page per label; an item with `Copies = n` produces `n` identical pages. Page size = the item's size (1 mm = 2.83465 pt). Items of different sizes in one request are allowed only if `PdfCanvas` supports per-page size; otherwise the renderer throws `ArgumentException` and Task 7 groups a request by format. Choose per-page size support in `PdfCanvas` (`NewPage(double w, double h)`).
- Minimum module width `0.25 mm` for both symbologies; quiet zone 10 modules for Code 128 and 1 module for Data Matrix (ISO/IEC 15417 and 16022; cite in comments). Margin 1.5 mm.
- Layout: Data Matrix at the left, text to its right; Code 128 across the top, text below. The code is always printed as text in bold. Other fields are `Name: Value` lines in the order given, each ellipsized with `PdfCanvas.Ellipsize` to the text box width; lines that do not fit vertically are dropped from the end, but the code line and the "Printed {date}" line are never dropped.
- `PrintNumber > 1` prints `REPRINT {PrintNumber - 1}`.
- If the barcode at minimum module size plus quiet zone does not fit the label, throw `LabelDoesNotFitException("The code does not fit a {w}×{h} mm label. Increase the label size in Label Formats.")`.

- [ ] **Step 1: Write failing tests.** Read the produced PDF with PdfPig (already referenced) for page count, page size and text.
```csharp
public void Existing_A4_canvas_is_unchanged()                           // new PdfCanvas().Build(...) first page is 595.276 x 841.890
public void Three_copies_produce_three_pages_of_label_size()            // 50x25 mm -> 141.73 x 70.87 pt, ±0.1
public void Code_and_print_date_are_always_present()
public void Reprint_number_is_printed()                                 // PrintNumber 3 -> text contains "REPRINT 2"
public void Small_layout_prints_only_small_fields()
public void Long_field_value_is_ellipsized_within_the_text_box()        // 200-char name on 50x25: no text glyph x beyond page width - margin
public void Code_that_does_not_fit_throws_LabelDoesNotFitException()    // 254-char Data Matrix on 10x10 mm
public void Barcode_modules_are_drawn_as_filled_rectangles_at_min_size()// fake IBarcodeEncoder returning a known 2x2 pattern; content stream has exactly the dark-module rectangles, each 0.7087 pt wide
```
- [ ] **Step 2: Run** — expect FAIL.
- [ ] **Step 3: Implement** the `PdfCanvas` constructor and per-page size, then `LabelPdfRenderer`.
- [ ] **Step 4: Run** the renderer tests and every existing test whose name contains `Pdf` — expect PASS.
- [ ] **Step 5: Commit** `feat(labels): label PDF renderer with configurable page size`.

---

### Task 6: Label data source, usability rules and formats

**Files:**
- Create: `backend/MicroLIMS.Application/Labels/LabelDataSource.cs`, `LabelUsability.cs`, `LabelFormatService.cs`
- Modify: `ServiceCollectionExtensions.cs`
- Test: `backend/MicroLIMS.Tests/UnitTests/LabelUsabilityTests.cs`, `backend/MicroLIMS.Tests/IntegrationTests/LabelDataSourcePostgresTests.cs`

**Interfaces:**
- Consumes: Task 1 and Task 5 types; `IUserSectionScopeService`; `ILabClock`.
- Produces:
```csharp
public sealed record LabelRecord(LabelEntityType Type, int Id, string? Code, string DisplayName, string Status,
                                 bool Usable, string? BlockedReason, int? SampleId, string CreatePermission,
                                 LabelContent? Content, int? LinkedEquipmentId);

public interface ILabelDataSource
{
    // null when the record does not exist OR the user may not read it
    Task<LabelRecord?> FindByIdAsync(LabelEntityType type, int id, int userId, CancellationToken ct);
    Task<LabelRecord?> FindByCodeAsync(LabelEntityType type, string code, int userId, CancellationToken ct);
}

public static class LabelUsability   // pure; "now" passed in
{
    public static (bool Usable, string? Reason) Media(bool isReleasedForUse, DateTime? expiryUtc, DateTime nowUtc);
    public static (bool Usable, string? Reason) Solution(string effectiveStatus);
    public static (bool Usable, string? Reason) Cryovial(bool approved, int vialsRemaining);
    public static (bool Usable, string? Reason) StockLot(bool isUsable, bool expired, bool depleted);
    public static (bool Usable, string? Reason) Equipment(EquipmentOperationalStatus status);
}

public interface ILabelFormatService
{
    Task<IReadOnlyList<LabelFormatDto>> GetAllAsync(CancellationToken ct);
    Task UpdateAsync(int id, UpdateLabelFormatRequest request, uint expectedVersion, CancellationToken ct);
}
```

Decisions:
- Reuse the existing computed properties and helpers for status (`Media.IsReleasedForUse`, `SolutionPreparationResponse.EffectiveStatusOf`, `Material.StatusAt(nowUtc)`, cryovial approval gate). Do not re-derive their rules. `LabelUsability` only turns their results into a sentence.
- Section check: call the matching `Ensure*AccessAsync(userId, id, ct, SectionAccess.Read)` and convert its denial into `null`. For Equipment use `EnsureEquipmentInventoryAccessAsync`.
- Code lookups are exact and case-insensitive (`EF.Functions.ILike` with escaped input, or a lowered comparison), `AsNoTracking`, projected to the fields needed.
- `CreatePermission` per type: Sample → `Samples.ReceiveOwnLab`; Media lot → `Media.Prepare`; Solution preparation → `Solutions.Prepare`; Cryovial → `Cryovials.Manage`; Material stock lot → `Materials.Manage`; Equipment → `Equipment.Manage`. A user holding `Samples.Receive` also satisfies the Sample rule.
- `Content` fields follow the table in spec section 5.3. "Prepared by" is the initials of the user's full name. Dates are formatted `dd MMM yyyy` in lab local time.
- `LinkedEquipmentId` is the `Equipment.Id` whose `EquipmentInventoryId` equals the asset, else null.
- `LabelFormatService.UpdateAsync` validates the ranges, enforces `expectedVersion` (stale → the project's concurrency exception), and relies on automatic audit capture.

- [ ] **Step 1: Write failing tests**
```csharp
// unit
public void Media_not_released_is_blocked_with_reason()
public void Media_expired_at_now_is_blocked()               // expiry == now -> follow the existing domain rule; assert against it
public void Equipment_out_of_service_or_retired_is_blocked()
// Postgres
public async Task Finds_each_of_the_six_types_by_id_and_by_code()
public async Task Returns_null_for_a_record_in_another_section()
public async Task Solution_in_progress_has_null_code()
public async Task Legacy_stock_lot_without_stock_id_has_null_code()
public async Task Code_lookup_is_exact_not_contains()        // "TSA/03/2" does not find "TSA/03/26"
public async Task Asset_returns_linked_workflow_equipment_id()
public async Task Format_update_with_stale_version_is_rejected_and_nothing_changes()
public async Task Format_update_persists_and_writes_audit_row()   // OldValues/NewValues contain WidthMm
```
- [ ] **Step 2: Run** — expect FAIL.
- [ ] **Step 3: Implement** and register the three services as scoped.
- [ ] **Step 4: Run** — expect PASS, 0 skipped.
- [ ] **Step 5: Commit** `feat(labels): label data source, usability and formats`.

---

### Task 7: Print service and endpoints

**Files:**
- Create: `backend/MicroLIMS.Application/Labels/LabelPrintService.cs`, `backend/MicroLIMS.API/Controllers/LabelController.cs`
- Modify: `ServiceCollectionExtensions.cs`
- Test: `backend/MicroLIMS.Tests/IntegrationTests/LabelPrintPostgresTests.cs`

**Interfaces:**
- Consumes: `ILabelDataSource`, `ILabelRenderer`, `IAuditEventService.RecordUserEventAsync`, `LabelFormat`, `LabelPrintLog`.
- Produces:
```csharp
public sealed record PrintLabelItem(LabelEntityType EntityType, int EntityId, int Copies);
public sealed record PrintLabelsRequest(IReadOnlyList<PrintLabelItem> Items, string? Reason);
public sealed record LabelPrintResult(byte[] Pdf, string FileName, Guid BatchId);
public sealed record LabelPrintStatusDto(LabelEntityType EntityType, int EntityId, bool CanPrint, string? BlockedReason,
                                         int PrintCount, int DefaultCopies);

public interface ILabelPrintService
{
    Task<LabelPrintResult> PrintAsync(PrintLabelsRequest request, int userId, IReadOnlySet<string> permissions, CancellationToken ct);
    Task<IReadOnlyList<LabelPrintStatusDto>> GetStatusAsync(IReadOnlyList<(LabelEntityType Type, int Id)> records, int userId, CancellationToken ct);
}
```
Endpoints (all `[Authorize]`; permission checks are in the service because they depend on the record type):
- `POST /api/labels/print` → `application/pdf`.
- `POST /api/labels/status` → `ApiResponse<IReadOnlyList<LabelPrintStatusDto>>`.
- `GET /api/labels/formats` → formats; `PUT /api/labels/formats/{id}` with `[Authorize(Policy = PermissionConstants.LabelsConfigure)]` and the record-version header used elsewhere.

`PrintAsync` order:
1. Validate: 1–100 items, no duplicate (type, id), each `Copies` 1–50. Otherwise `ValidationException` with a field error per item.
2. Load every record through `ILabelDataSource.FindByIdAsync`. Any `null` → not-found for the whole request. Any `Code == null` → `BusinessRuleException` naming the record and why.
3. Check the caller holds each item's `CreatePermission` (or `Labels.Reprint` for an item that is a reprint). Missing → forbidden.
4. Count existing log rows per item. If any item has a count above zero the request is a reprint: require `Labels.Reprint` and a trimmed `Reason` of at least 10 characters.
5. In one `SaveChangesAsync`: add one `LabelPrintLog` per item (`PrintNumber = count + 1`, shared `BatchId`, `PrintedAt` from `TimeProvider`) and one audit event per item via `RecordUserEventAsync(actionCode: "LABEL_PRINTED" | "LABEL_REPRINTED", actionCategory: AuditActionCategory.Labelling, recordType: <entity name>, entityId: <id>, reason, sampleId, changes: Code, Copies, PrintNumber, LabelFormatVersion)`. Check `Database.CurrentTransaction` before opening a transaction; do not nest.
6. On a unique-index violation of the print number, reload the counts and retry once; a second violation → conflict.
7. After the save succeeds, render and return. Rendering is never inside the transaction. A `LabelDoesNotFitException` must be detected **before** step 5: do a dry render first, then save, then return the dry-render bytes only if print numbers did not change; otherwise render again.

- [ ] **Step 1: Write failing tests**
```csharp
public async Task First_print_writes_log_row_and_LABEL_PRINTED_audit_event()
// assert in a fresh context: PrintNumber 1, IsReprint false, Copies 3, PrintedByUserId, PrintedAt == fake clock;
// AuditLogs: ActionCode "LABEL_PRINTED", UserId, EntityId, SampleId for a sample, changes contain Copies=3
public async Task Returned_pdf_has_one_page_per_requested_label()            // items (2 copies, 3 copies) -> 5 pages
public async Task Reprint_without_permission_is_forbidden_and_writes_nothing()
public async Task Reprint_without_reason_or_with_short_reason_is_rejected()  // "too short" (9 chars) rejected; 10 accepted
public async Task Reprint_with_permission_and_reason_writes_print_number_2_and_LABEL_REPRINTED()
public async Task Mixed_first_print_and_reprint_request_follows_reprint_rules()
public async Task Copies_0_and_51_are_rejected_and_nothing_is_written()
public async Task Duplicate_record_in_one_request_is_rejected()
public async Task Record_in_another_section_gives_not_found_and_writes_nothing()
public async Task User_without_the_create_permission_is_forbidden()
public async Task Solution_in_progress_cannot_be_printed()
public async Task Label_too_small_fails_before_anything_is_logged()         // shrink the format to 10x10, print a long code -> no log row
public async Task Two_concurrent_first_prints_yield_print_numbers_1_and_2_or_one_conflict() // never two rows with PrintNumber 1
public async Task Status_reports_print_count_default_copies_and_blocked_reason()
```
- [ ] **Step 2: Run** — expect FAIL.
- [ ] **Step 3: Implement** service and controller; register the service and `ILabelRenderer`, `IBarcodeEncoder`.
- [ ] **Step 4: Run** — expect PASS, 0 skipped.
- [ ] **Step 5: Mutation self-check:** comment out the `SaveChangesAsync` call, confirm the first test fails, restore it.
- [ ] **Step 6: Commit** `feat(labels): print endpoint with print log and audit events`.

---

### Task 8: Creation endpoints return what they created

**Files:**
- Modify: `backend/MicroLIMS.API/Controllers/SampleController.cs` (`Receive`, line 91) and the multi-sample receipt action in the same controller; the receiving service result type
- Modify: `frontend/src/modules/receiving/dialogs/NewSampleDialog.tsx` (`onSuccess`, line 25 and 356), `MultiSampleEntryGrid.tsx`, `frontend/src/modules/receiving/ReceivingPage.tsx`
- Test: the existing receiving workflow test file for the service; `NewSampleDialog` test file

**Interfaces — Produces:**
```ts
export interface CreatedSampleRef { id: number; referenceNumber: string }
// NewSampleDialog prop
onSuccess: (created: CreatedSampleRef[]) => void;
```
The receipt responses must include `id` and `referenceNumber` for every sample created. Read the current response shape first; add the fields only if they are missing, and do not rename existing ones.

- [ ] **Step 1:** Backend test: `Receipt_response_lists_id_and_reference_number_of_each_created_sample`. Run — FAIL if fields are missing.
- [ ] **Step 2:** Add the fields. Run — PASS.
- [ ] **Step 3:** Frontend test: "after a successful receipt the parent receives the created samples" (`userEvent`, service module mocked). Change `onSuccess` and its callers; `ReceivingPage` keeps its existing success message using `created.length`.
- [ ] **Step 4:** Run backend and frontend verification.
- [ ] **Step 5: Commit** `feat(receiving): return created samples from receipt`.

---

### Task 9: Scan resolve service and endpoint

**Files:**
- Create: `backend/MicroLIMS.Application/Labels/ScanResolveService.cs`, `backend/MicroLIMS.API/Controllers/ScanController.cs`
- Test: `backend/MicroLIMS.Tests/IntegrationTests/ScanResolvePostgresTests.cs`

**Interfaces:**
- Consumes: `ScanInputNormalizer`, `LabelPayload`, `ILabelDataSource`.
- Produces:
```csharp
public enum ScanOutcome { Found = 1, NotFound = 2, Ambiguous = 3 }
public sealed record ScanMatchDto(LabelEntityType EntityType, int Id, string Code, string DisplayName, string Status,
                                  bool Usable, string? BlockedReason, string NavigationTarget, int? LinkedEquipmentId);
public sealed record ScanResolveResponse(ScanOutcome Outcome, bool LayoutCorrected, string NormalizedValue,
                                         ScanMatchDto? Match, IReadOnlyList<ScanMatchDto> Candidates, string? Hint);

public interface IScanResolveService { Task<ScanResolveResponse> ResolveAsync(string? value, int userId, CancellationToken ct); }
```
`GET /api/scan/resolve?value=` — `[Authorize]`, uses the existing per-user rate-limit policy. Always 200 with an outcome; a blank or over-300-character value is 400.

Decisions:
- With a prefix: one `FindByCodeAsync` for that type.
- Without a prefix: `FindByCodeAsync` for all six types; one hit → `Found`; several → `Ambiguous` with `Candidates`; none → `NotFound`.
- `Hint` on `NotFound` when `LayoutCorrected` is true: "The keyboard was in Arabic. Switch to English and scan again."
- `NavigationTarget` reuses the targets `GlobalSearchService` already builds for samples, materials, equipment inventory, media and solution preparations; for cryovials use the cryovial report route `/cryovials/{id}/report`. Extract the existing target builders into a shared helper so the two services cannot drift.

- [ ] **Step 1: Write failing tests**
```csharp
public async Task Prefixed_payload_resolves_each_of_the_six_types()
public async Task Bare_code_with_one_match_resolves()
public async Task Bare_code_matching_two_types_is_ambiguous_with_both_candidates()
public async Task Record_in_another_section_is_not_found()            // identical response to an unknown code, field by field
public async Task Partial_code_is_not_found()
public async Task Arabic_layout_input_resolves_and_sets_layout_corrected()
public async Task Unknown_arabic_layout_input_returns_hint()
public async Task Expired_media_lot_is_found_but_not_usable_with_reason()
public async Task Asset_scan_returns_linked_equipment_id()
public async Task Blank_value_is_bad_request()
```
- [ ] **Step 2: Run** — FAIL. **Step 3: Implement.** **Step 4: Run** — PASS, 0 skipped.
- [ ] **Step 5: Commit** `feat(labels): scan resolve endpoint`.

---

### Task 10: Frontend service and print UI

**Files:**
- Create: `frontend/src/services/labelService.ts`, `frontend/src/components/labels/PrintLabelsDialog.tsx`, `PrintLabelButton.tsx`, and their `.test.tsx`
- Modify: `frontend/src/routes/routes.ts` (`PERMISSIONS.LABELS_REPRINT`, `LABELS_CONFIGURE`)

**Interfaces — Produces:**
```ts
export type LabelEntityType = "Sample" | "MediaLot" | "SolutionPreparation" | "Cryovial" | "MaterialStockLot" | "Equipment";
export interface LabelTarget { entityType: LabelEntityType; entityId: number; code: string; displayName: string }
export interface LabelPrintStatus { entityType: LabelEntityType; entityId: number; canPrint: boolean; blockedReason: string | null; printCount: number; defaultCopies: number }

export const labelService: {
  getStatus(targets: LabelTarget[], signal?: AbortSignal): Promise<LabelPrintStatus[]>;
  print(items: { entityType: LabelEntityType; entityId: number; copies: number }[], reason?: string): Promise<Blob>;
  getFormats(signal?: AbortSignal): Promise<LabelFormat[]>;
  updateFormat(id: number, version: number, body: UpdateLabelFormat): Promise<void>;
  resolveScan(value: string, signal?: AbortSignal): Promise<ScanResolveResponse>;
};

<PrintLabelsDialog open targets={LabelTarget[]} onClose={() => void} />
<PrintLabelButton target={LabelTarget} />   // "Print label" or "Reprint label"
```

Dialog behaviour:
- Built on `FormDialog`. On open it loads status with an `AbortController`; shows `DialogBodySkeleton` while loading and `LoadErrorAlert` on failure.
- One row per target: code in `monospaceFontFamily`, name, and a "Number of labels" number field (`aria-label="Number of labels for {code}"`, `tabular-nums`) pre-filled with `defaultCopies`. Rows with `canPrint === false` show the reason and no field. A "Set all to" field appears when there are two or more rows.
- If any row has `printCount > 0`, a required "Reason for reprint" field appears, with helper text "At least 10 characters", and the title reads "Reprint labels".
- Client-side checks (1–50, reason length) only mirror the server; server field errors are shown inline next to the right row.
- On success: open the returned PDF blob in a new window (`window.open(URL.createObjectURL(blob))`) and revoke the URL after load; toast "Labels ready to print"; close. If the pop-up is blocked, show a "Open label PDF" link instead.
- `PrintLabelButton` is disabled with a `Tooltip` reason when the record has no code or the user lacks the permission.

- [ ] **Step 1: Write failing tests** (real `FormDialog`, `labelService` mocked, `userEvent`)
```
prefills number of labels from the default and sends the edited number
"Set all to" changes every row
rejects 0 and 51 with an inline message and does not call print
shows the reprint reason field only when a label was printed before, and requires 10 characters
shows a server field error next to its row
shows the blocked reason for a record that cannot be printed and excludes it from the request
shows the load error alert when status fails
```
- [ ] **Step 2: Run** `npm run test -- labels` — FAIL. **Step 3: Implement.** **Step 4:** frontend verification — PASS, debt counts not raised.
- [ ] **Step 5: Commit** `feat(labels): print dialog and button`.

---

### Task 11: Offer printing where records are created and viewed

**Files (modify):**
- `frontend/src/modules/receiving/ReceivingPage.tsx` — after receipt, open `PrintLabelsDialog` with the created samples.
- Sample summary: `frontend/src/modules/testingWorkspace/SampleSummaryDialog.tsx` — `PrintLabelButton`.
- Media lot: `frontend/src/modules/laboratoryConfiguration/media/dialogs/MediaPreparationDialog.tsx` (after completion) and the media lot detail view.
- Solution preparation: the completion step under `frontend/src/modules/solutionPreparation/` and the record view.
- Cryovial: the cryovial batch detail under `frontend/src/modules/laboratoryConfiguration/cryovials/`.
- Stock lot: `frontend/src/modules/inventory/materials/components/AddMaterialDialog.tsx` (after receipt) and `MaterialLotDetailsDialog.tsx`.
- Equipment asset: `frontend/src/modules/inventory/equipment/components/EquipmentDetailsDialog.tsx`.

Each creation flow must expose the created record's `id` and code to its page; where a flow does not, extend its success callback the way Task 8 did. Each detail view gets one `PrintLabelButton` in its action area.

- [ ] **Step 1:** For each of the six record types, add one test to the existing test file of the touched component: "offers label printing after {record} is created" or "shows the print label button with the record's code". Use `userEvent`; mock only service modules.
- [ ] **Step 2: Run** — FAIL. **Step 3: Implement** one record type at a time, running its test after each. **Step 4:** frontend verification.
- [ ] **Step 5: Commit** one commit per record type, `feat(labels): offer {record} labels`.

---

### Task 12: Scan to open in the Ctrl+K dialog

**Files:**
- Create: `frontend/src/components/labels/useScanResolve.ts`
- Modify: `frontend/src/components/search/GlobalSearchDialog.tsx` (`handleKeyDown`, around line 86)
- Test: `frontend/src/components/search/GlobalSearchDialog.test.tsx`

**Interfaces — Produces:**
```ts
// aborts the previous call; a superseded call resolves to null
export function useScanResolve(): { resolve(value: string): Promise<ScanResolveResponse | null>; pending: boolean };
```

Enter behaviour:
1. If the query is at least 2 characters, cancel the pending debounce timer and its request, then call `resolve(query)`.
2. `Found` → close and navigate to `match.navigationTarget`. If `layoutCorrected`, toast "Keyboard was in Arabic; the code was converted."
3. `Ambiguous` → show the candidates as the result list.
4. `NotFound` → run the normal search immediately (no debounce) and keep the dialog open; show `hint` if present.
5. A highlighted result chosen with the arrow keys still opens on Enter as today; resolve is used only when the user has not moved the selection.

- [ ] **Step 1: Write failing tests**
```
typing a code and pressing Enter at once opens the resolved record      (no wait for the debounce)
a second scan while the first resolve is pending opens the second record only
an ambiguous scan lists the candidates
an unknown value falls back to search results
arrow-key selection followed by Enter opens the selected result, not the resolve result
shows the Arabic keyboard notice when the layout was corrected
```
- [ ] **Step 2: Run** — FAIL. **Step 3: Implement.** **Step 4:** frontend verification. If the file's existing tests use `fireEvent`, migrate them and run `npm run debt:update`.
- [ ] **Step 5: Commit** `feat(labels): open a record by scan from global search`.

---

### Task 13: ScanField and the five pickers

**Files:**
- Create: `frontend/src/components/labels/ScanField.tsx`, `ScanField.test.tsx`
- Modify: `frontend/src/modules/testingWorkspace/pathogenSteps/StepLayout.tsx` (`MediaLotIncubatorFields`), `frontend/src/modules/hplcWorkspace/run/WizardMobilePhasesStep.tsx`, `frontend/src/modules/solutionPreparation/components/ComponentLotPicker.tsx`, `frontend/src/modules/laboratoryConfiguration/media/dialogs/MediaPreparationDialog.tsx`, `frontend/src/modules/laboratoryConfiguration/mediaEvaluation/components/ChallengeOrganismSourcePicker.tsx`

**Interfaces — Produces:**
```tsx
interface ScanFieldProps {
  label: string;                                   // e.g. "Scan media lot"
  expectedType: LabelEntityType;
  // ids the server already offered for this picker; for Equipment these are workflow Equipment ids
  offeredIds: readonly number[];
  onSelect: (id: number) => void;
  disabled?: boolean;
}
```
Behaviour on Enter:
- Calls `useScanResolve().resolve`, clears the input, keeps focus in the field.
- For `expectedType === "Equipment"` the id compared with `offeredIds` is `match.linkedEquipmentId`.
- Messages, in a `role="status"` region (`role="alert"` for refusals):
  - selected → "Selected {code}"
  - not usable → `blockedReason`
  - wrong type → "This is a {found type}, not a {expected type}."
  - not offered → "{code} is not available for this step."
  - equipment with no link → "{code} is not linked to a workflow instrument. Pick it from the list."
  - not found → "No record found for this code." plus `hint`
  - layout corrected → adds "Keyboard was in Arabic; the code was converted."
- It never calls `onSelect` with an id outside `offeredIds`.

- [ ] **Step 1: Write failing `ScanField` tests** — one per message above, plus "a second scan before the first returns selects only the second".
- [ ] **Step 2: Run** — FAIL. **Step 3: Implement `ScanField`.** **Step 4: Run** — PASS.
- [ ] **Step 5:** Add the field to each picker in the order listed, one commit each. For each, add to that component's existing test file: "scanning an offered {thing} selects it" and "scanning one that is not offered leaves the selection unchanged". Mount the real picker.
- [ ] **Step 6:** Backend regression, one `[PostgresFact]` in the existing test file of the media-selection service: `Selecting_an_expired_media_lot_is_still_rejected_by_the_service`. If such a test already exists, cite it in the commit message and add none.
- [ ] **Step 7:** frontend and backend verification.
- [ ] **Step 8: Commit** per picker, `feat(labels): scan to select {picker}`.

---

### Task 14: Camera scanning and label formats page

**Files:**
- Modify: `frontend/package.json` (add `barcode-detector` exactly `3.2.3`, MIT)
- Create: `frontend/src/components/labels/CameraScanDialog.tsx` + test, `frontend/src/modules/laboratoryConfiguration/labelFormats/LabelFormatsPage.tsx` + test, `documents/Validation/barcode-detector-Qualification.md`
- Modify: `ScanField.tsx`, `GlobalSearchDialog.tsx` (camera button), `frontend/src/routes/routes.ts`, `AuthenticatedRoutes.tsx`, `menuConfig.ts` (page under both Configuration sections, permission `LABELS_CONFIGURE`)

**Interfaces — Produces:**
```tsx
<CameraScanDialog open onClose={() => void} onDetected={(value: string) => void} />
```

Camera rules:
- The camera button is rendered only when `navigator.mediaDevices?.getUserMedia` exists. It has `aria-label="Scan with camera"`.
- Use the native `BarcodeDetector` when `"BarcodeDetector" in window` and it supports `data_matrix` and `code_128`; otherwise import the `barcode-detector` ponyfill lazily (`import("barcode-detector/ponyfill")`) so it is not in the main bundle.
- Formats requested: `data_matrix`, `code_128` only.
- On the first detection: stop every media track, call `onDetected`, close. Closing by Escape or the Close button also stops the tracks and returns focus to the button.
- Permission refused, no camera, or an insecure origin → an inline message naming the cause; no toast loop.
- The video element respects `prefers-reduced-motion` (no scanning animation).

Formats page: `LabPage` + `PageHeader` + `RegisterTable` with one row per label type; edit through `FormDialog` (width, height, layout, default number of labels); `EmptyState`, table skeleton and `LoadErrorAlert` states; sends the record version; a stale version shows "Someone else changed this format. Reload and try again."

- [ ] **Step 1: Write failing tests**
```
CameraScanDialog: reports the first detected value and stops the camera     (mock navigator.mediaDevices and a fake detector; comment above the mock states why: jsdom has no camera)
CameraScanDialog: shows the permission-refused message
CameraScanDialog: Escape closes and stops the camera
LabelFormatsPage: lists six formats and saves an edited width with its version
LabelFormatsPage: shows the stale-version message on 409
LabelFormatsPage: shows field errors for out-of-range sizes returned by the server
```
- [ ] **Step 2: Run** — FAIL. **Step 3: Implement.** **Step 4:** frontend verification and `npm run build`; confirm the ponyfill is a separate chunk in the build output.
- [ ] **Step 5:** Write the qualification record for `barcode-detector`.
- [ ] **Step 6: Commit** `feat(labels): camera scanning and label formats page`.

---

### Task 15: Final verification and documents

- [ ] **Step 1:** Run the full backend suite against PostgreSQL and report exact passed / failed / skipped. Skipped must be 0 for `[PostgresFact]`.
- [ ] **Step 2:** Run the suite once with `MICROLIMS_TEST_POSTGRES` unset and confirm every `[PostgresFact]` reports Skipped (mirrors CI's first run).
- [ ] **Step 3:** Run `dotnet ef migrations has-pending-model-changes` — "No changes".
- [ ] **Step 4:** Run full frontend verification including `npm run build`.
- [ ] **Step 5:** Start the application, and with Playwright: receive a sample, print 3 labels, confirm a 3-page PDF opens; reprint as a Section Head with a reason; open the sample by typing its payload into Ctrl+K and pressing Enter; check both themes. Save screenshots under `documents/Screenshots/labels/`.
- [ ] **Step 6:** Add to `documents/URS/` and `documents/FunctionalSpecification/` the label and scan requirements with IDs, and to `documents/SOP/` a draft "Label printing and handling" outline that states what the print log proves and does not prove.
- [ ] **Step 7:** Update `barcode-labels-recon-report.md` with a short "Superseded by" note pointing to the spec, and mark package B1 in `improvement-roadmap.md` as implemented, listing the manual gates still open (real printer test print, scanner readback, Arabic-layout scan, SOP approval, library qualification sign-off).
- [ ] **Step 8: Commit** `docs(labels): requirements, SOP outline and verification record`.

---

## Manual gates after this plan (need the laboratory)

1. Test print on the chosen thermal printer and label stock at 100% scale; adjust sizes on the Label Formats page.
2. Readback of all six label types with the handheld scanners and with a tablet camera.
3. A scan with the Arabic keyboard layout active on a real workstation.
4. QA approval of the label SOP and of the two library qualification records.
