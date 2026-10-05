# Physicochemical areas, primary standards, titration hardening - Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Primary Standard reagent type, harden titration (signed due-titrant override, reason for change), split the Physicochemical lab into FP and RM & PM areas (tests, workspaces, user access), then seed and browser-test RM titrations.

**Architecture:** One physicochemical section (`DocumentSection.Code == "FP"`) gains an *area* tag on tests and on user memberships. The area of a sample is derived from its category. Backend lanes run in parallel git worktrees (Sonnet); frontend lanes run in the main tree via agy against the fixed API contract below. The orchestrator merges, applies migrations to LIMSV2 and runs the suites.

**Tech Stack:** ASP.NET Core 8, EF Core + PostgreSQL, xUnit; React 18 + TypeScript + MUI, vitest/RTL.

**Spec:** `docs/superpowers/specs/2026-10-05-physchem-areas-design.md` (sections 0-7, including amendments A1-A5).

## Global Constraints

- Enums are stored as int: **append** new values, never renumber or insert.
- JSON enums travel as strings (frontend types use names such as `"Titration"`).
- Never hard-code section ids (prod FP = 3, local FP = 2). Resolve the physicochemical section by `DocumentSection.Code == "FP"`.
- Builds, test artifacts, temp files and npm cache go to `E:/MicroLIMS/rls-tmp/...`, never C:.
- Commit each tested task locally. Never push or merge.
- Commit messages end with:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01SDkmZyY6EQKT3VTkzwusjx
  ```
- Laboratory rules live in the backend Application layer. The frontend only reflects them.
- Keep files small. `TestMasterPage.tsx` (2762 lines) must grow only by prop wiring; new logic goes in new files.
- Backend tests in a worktree (the script in `.claude/scripts` is untracked and points at the main repo):
  ```bash
  pw="$(grep -o 'Password=[^;"]*' /e/MicroLIMS/MicroLIMS/backend/MicroLIMS.API/appsettings.Development.json | head -1 | cut -d= -f2-)"
  export MICROLIMS_TEST_POSTGRES="Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=$pw"
  dotnet build backend/MicroLIMS.Tests --artifacts-path E:/MicroLIMS/rls-tmp/art-<lane>
  dotnet test backend/MicroLIMS.Tests --no-build --artifacts-path E:/MicroLIMS/rls-tmp/art-<lane> --filter "<filter>"
  ```
  Never print the password.
- Frontend checks: `cd frontend && npx tsc -b && npx eslint <changed files> && npx vitest run <changed tests>`.

## Review Focus

1. A Primary Standard lot **without purity** (all 7 migrated prod lots) used for standardization: refused with a message naming the lot and telling the user to enter purity. Never computed at 100 %. Test in Task B1.3.
2. A **Both** test, then an edit that makes it FP-only while RM items still have specs on it: refused, listing the item codes. Test in Task B3.2.
3. A **user with area FP only** opening `/testorders/{id}?labSectionId=..&area=rmpm`: 403, not data. Test in Task B3.3.
4. **Due titrant used as the excess titrant** in a residual relative test (warning on the excess option, not the main titrant): the acknowledgement is still required. Test in Task B2.1.
5. Editing a titration test with **no titration field changed** (only the display name): no reason required. Test in Task B2.2.

---

## API contract (frontend lanes build against this; backend lanes must match it exactly)

**S1**
- `MaterialType` gains `"PrimaryStandard"`; `MaterialMasterCategory` gains `"PrimaryStandard"`.
- `GET /materials/type-options?sectionId=` returns `builtIn` with `"PrimaryStandard"` for the FP section.
- Material save/response: `purity` is required for `PrimaryStandard` (0 < p <= 100); `moisturePercent` stays ReferenceStandard-only.
- Solution master (Titrant, PrimaryStandard mode): `standardEntryId` must be a `PrimaryStandard` entry, otherwise 400 "The primary standard must be a Primary Standard entry in Reagents & Standards."

**S2**
- `RecordTitrationResultRequest` adds `dueTitrantAcknowledged: boolean | null` and `dueTitrantJustification: string | null`.
  - Required (true + 10-500 chars) when the chosen titrant or excess option has a `warning`; must be null/false otherwise.
- The titration snapshot (`TestAnalysis.ConditionsJson`) adds:
  - `engineVersion: "titration-1"`
  - `dueTitrantAcknowledgement: { justification: string, titrantCodes: string[], acknowledgedAt: string } | null`
- `SignatureMeaning` gains `"TitrantDueAcknowledged"`.
- `PUT test-definitions/{id}` body adds `changeReason: string | null`. Required (5-500 chars) when any titration field changes on a Titration test; otherwise 400 "A reason for change is required when titration settings change."

**S3**
- `PhyschemArea = "FinishedProduct" | "RawPackaging" | "Both"`.
- Test definition create/update request and response add `physchemArea: PhyschemArea | null`. It is required for tests in section FP and must be null for other sections (server clears it).
- Workspace: `GET /testorders/page`, `GET /testorders/counts` and `GET /testorders/{id}` accept `area=fp|rmpm`.
  - `area` is required when `labSectionId` is the FP section.
  - `fp` = FinishedProduct samples; `rmpm` = RawMaterial + PackagingMaterial samples.
  - 403 when the user's area access does not include it.
- `GET /org/my-sections`: each item adds `physchemAreas: ("fp"|"rmpm")[]`. For the FP section this is the user's areas (admin/department membership/null area = both); empty for other sections.
- Memberships `GET/PUT users/{id}/memberships` (existing): `UserOrgMembershipDto` adds `physchemArea: PhyschemArea | null`. Only allowed on a membership whose section is FP; null = Both.

---

## Lane B1 - S1 backend (Sonnet, worktree)

### Task B1.1: PrimaryStandard enums and material rules

**Files:**
- Modify: `backend/MicroLIMS.Domain/Enums/MaterialType.cs` (append `PrimaryStandard` after `WorkingStandard`)
- Modify: `backend/MicroLIMS.Domain/Enums/MaterialMasterCategory.cs` (append `PrimaryStandard`)
- Modify: `backend/MicroLIMS.Application/Services/MaterialService.cs` (`DefaultUnitFor`, `ValidatePurity`, `RequiresMasterEntry`, `CategoryFor`)
- Modify: `backend/MicroLIMS.Application/Services/MaterialTypeRules.cs` (add `PrimaryStandard` to `Physicochemical`)
- Modify: the material-master entry validation in `backend/MicroLIMS.Application/Services/MaterialMasterService.cs` (find with `git grep -n "MaterialMasterCategory" backend/MicroLIMS.Application`) so `PrimaryStandard` entries can be created and edited like ReferenceStandard entries.
- Test: `backend/MicroLIMS.Tests/UnitTests/PrimaryStandardMaterialTests.cs` (new)

**Interfaces:**
- Produces: `MaterialType.PrimaryStandard`, `MaterialMasterCategory.PrimaryStandard`, and `MaterialService.ValidatePurity(MaterialType, decimal?)` requiring purity for ReferenceStandard and PrimaryStandard.

- [ ] **Step 1: Write failing tests**

```csharp
public class PrimaryStandardMaterialTests
{
    [Fact] public void Purity_IsRequired_ForPrimaryStandard() =>
        Assert.Throws<InvalidOperationException>(() => MaterialService.ValidatePurity(MaterialType.PrimaryStandard, null));
    [Fact] public void Purity_IsAccepted_ForPrimaryStandard() =>
        MaterialService.ValidatePurity(MaterialType.PrimaryStandard, 99.95m);
    [Fact] public void Purity_StillRejected_ForChemical() =>
        Assert.Throws<InvalidOperationException>(() => MaterialService.ValidatePurity(MaterialType.Chemical, 99m));
    [Fact] public void PrimaryStandard_NeedsMasterEntry() =>
        Assert.True(MaterialService.RequiresMasterEntry(MaterialType.PrimaryStandard));
    [Fact] public void Physicochemical_Offers_PrimaryStandard_NotMicroTypes()
    {
        var t = MaterialTypeRules.BuiltInTypesFor("FP");
        Assert.Contains(MaterialType.PrimaryStandard, t);
        Assert.DoesNotContain(MaterialType.DehydratedMedia, t);
        Assert.DoesNotContain(MaterialType.LyophilizedMicroorganism, t);
    }
    [Fact] public void Microbiology_DoesNotOffer_PrimaryStandard() =>
        Assert.DoesNotContain(MaterialType.PrimaryStandard, MaterialTypeRules.BuiltInTypesFor("MICRO"));
}
```

Add an integration-style test in the same file, following the pattern of the existing material create tests (find with `git grep -ln "MaterialService(" backend/MicroLIMS.Tests | head`): creating a PrimaryStandard lot linked to a `ReferenceStandard`-category entry is refused with "is a ReferenceStandard, not a PrimaryStandard".

- [ ] **Step 2: Run, expect FAIL** (`--filter PrimaryStandardMaterialTests`; compile errors count as failing).
- [ ] **Step 3: Implement**

```csharp
// MaterialService
MaterialType.PrimaryStandard => MaterialUnit.Gram,   // in DefaultUnitFor
public static void ValidatePurity(MaterialType type, decimal? purity)
{
    if (type is MaterialType.ReferenceStandard or MaterialType.PrimaryStandard)
    {
        if (!purity.HasValue)
            throw new InvalidOperationException(type == MaterialType.PrimaryStandard
                ? "Purity is required for primary standards." : "Purity is required for reference standards.");
        if (purity.Value <= 0m || purity.Value > 100m)
            throw new InvalidOperationException("Purity must be greater than 0 and less than or equal to 100.");
    }
    else if (purity.HasValue)
        throw new InvalidOperationException("Purity is only allowed for reference and primary standards.");
}
public static bool RequiresMasterEntry(MaterialType t) =>
    t is MaterialType.Chemical or MaterialType.Indicator or MaterialType.ReferenceStandard or MaterialType.PrimaryStandard;
// CategoryFor: MaterialType.PrimaryStandard => MaterialMasterCategory.PrimaryStandard,
```

Check every `switch` over `MaterialType` / `MaterialMasterCategory` in Application and API (`git grep -n "MaterialMasterCategory\.\|case MaterialType\." backend/MicroLIMS.Application backend/MicroLIMS.API`) and add the new value where a missing arm would throw or misclassify. Fix any existing test that asserted the old message "Purity is only allowed for reference standards." to the new message.

- [ ] **Step 4: Run, expect PASS**, plus the full unit suite (`--filter "FullyQualifiedName~UnitTests"`).
- [ ] **Step 5: Commit** `feat(materials): primary standard reagent type with required purity`

### Task B1.2: Titrant primary standard must be a Primary Standard entry

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/SolutionMasterService.cs` (titrant validation near line 304, where `StandardEntryId` is checked)
- Test: `backend/MicroLIMS.Tests/UnitTests/PrimaryStandardTitrantTests.cs` (new; follow the existing SolutionMaster test setup, found with `git grep -ln "SolutionMasterService(" backend/MicroLIMS.Tests`)

- [ ] **Step 1: Failing tests:**
  - Creating a Titrant master in PrimaryStandard mode whose `StandardEntryId` points to a `Reagent` entry is refused with "The primary standard must be a Primary Standard entry in Reagents & Standards."
  - The same with a `PrimaryStandard` entry succeeds.
  - Updating an existing master to a Reagent entry is refused.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.** Where the master loads/validates `StandardEntryId` (create and update), add:

```csharp
var std = await _db.MaterialMasterEntries.FirstOrDefaultAsync(e => e.Id == r.StandardEntryId.Value, ct)
    ?? throw new InvalidOperationException("Primary standard entry not found.");
if (std.Category != MaterialMasterCategory.PrimaryStandard)
    throw new InvalidOperationException("The primary standard must be a Primary Standard entry in Reagents & Standards.");
```

  Reuse the existing entry load if one is already there; do not query twice.
- [ ] **Step 4: Run, expect PASS.** Run the existing SolutionMaster tests too. Update their seeds so primary-standard entries are created with `Category = MaterialMasterCategory.PrimaryStandard`.
- [ ] **Step 5: Commit** `feat(solutions): titrant primary standard must be a Primary Standard entry`

### Task B1.3: Standardization needs a Primary Standard lot with purity

**Files:**
- Modify: `backend/MicroLIMS.Application/Services/TitrantStandardizationService.cs` (PrimaryStandard branch around lines 111-134; `GetStandardLotOptionsAsync` around line 333)
- Modify: `backend/MicroLIMS.Application/Helpers/TitrationEngine.cs` (`PrimaryStandardFactor`: `decimal purityPercent`, not nullable)
- Test: `backend/MicroLIMS.Tests/UnitTests/TitrantStandardizationPrimaryStandardTests.cs` (new; reuse the setup of the existing standardization tests, found with `git grep -ln "TitrantStandardizationService" backend/MicroLIMS.Tests`)

- [ ] **Step 1: Failing tests:**
  - A lot of type `PrimaryStandard` with `Purity = null` gives "Replicate 1: standard lot (batch B1) has no purity - enter it in Materials Stock before standardizing." (Review Focus 1).
  - A lot of type `Chemical` gives "Replicate 1: standard lot (batch B1) is not a primary standard."
  - A PrimaryStandard lot with purity 99.95, W = 510.6 mg KHP, V = 25.00 mL, blank 0, E = 20.42 mg/mL gives factor `510.6*0.9995/(25.00*20.42)` = 0.99967... (assert to 6 dp).
  - `GetStandardLotOptionsAsync` lists only PrimaryStandard lots.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.** After `LotUsability.Check` in the PrimaryStandard branch:

```csharp
if (lot.MaterialType != MaterialType.PrimaryStandard)
    throw new InvalidOperationException($"Replicate {replicateNo}: standard lot (batch {lot.BatchNumber}) is not a primary standard.");
if (!lot.Purity.HasValue)
    throw new InvalidOperationException($"Replicate {replicateNo}: standard lot (batch {lot.BatchNumber}) has no purity - enter it in Materials Stock before standardizing.");
// ...
factor = TitrationEngine.PrimaryStandardFactor(input.StandardWeightMg.Value, lot.Purity.Value, input.TitrantVolumeMl, blank, snapshot.EquivalenceMgPerMl.Value);
```

  `TitrationEngine.PrimaryStandardFactor(decimal standardWeightMg, decimal purityPercent, ...)` validates `0 < purity <= 100` and uses it directly (remove `?? 100m`). Fix the callers and `TitrationEngineTests` that pass `null`.
- [ ] **Step 4: Run, expect PASS** (new tests, `TitrationEngineTests`, all standardization tests, `TitrationRecorderTests`).
- [ ] **Step 5: Commit** `fix(titrants): standardization requires a primary standard lot with purity`

### Task B1.4: Migration PrimaryStandardType (data only)

**Files:**
- Create: `backend/MicroLIMS.Persistence/Migrations/<timestamp>_PrimaryStandardType.cs` via `dotnet ef migrations add PrimaryStandardType --project backend/MicroLIMS.Persistence --startup-project backend/MicroLIMS.API --configuration Release`. Expect an empty Up/Down: no schema change, because both enums are int. Fill in the SQL below.
- Test: `backend/MicroLIMS.Tests/IntegrationTests/PrimaryStandardMigrationPostgresTests.cs` (new; copy the structure of an existing migration Postgres test, found with `git grep -ln "MigrateAsync\|GetPendingMigrations" backend/MicroLIMS.Tests/IntegrationTests | head -3`)

- [ ] **Step 1: Failing Postgres test.** Migrate to the migration before `PrimaryStandardType`, then seed:
  - a Reagent entry `RG-022` (KHP), referenced by a Titrant `SolutionMaster.StandardEntryId`
  - one Chemical lot of it
  - a Reagent entry `RG-100` not referenced by any titrant, with a Chemical lot

  Migrate up and assert:
  - RG-022 has category PrimaryStandard (3) and its lot has type PrimaryStandard (13)
  - RG-100 and its lot are unchanged

  Migrate down and assert RG-022 is back to Reagent (0) and the lot back to Chemical (6).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement**

```csharp
protected override void Up(MigrationBuilder mb)
{
    mb.Sql(@"
UPDATE ""MaterialMasterEntries"" e SET ""Category"" = 3
WHERE e.""Category"" = 0 AND EXISTS (SELECT 1 FROM ""SolutionMasters"" sm WHERE sm.""Type"" = 2 AND sm.""StandardEntryId"" = e.""Id"");
UPDATE ""Materials"" m SET ""MaterialType"" = 13
WHERE m.""MaterialType"" = 6 AND m.""MaterialMasterEntryId"" IN (SELECT ""Id"" FROM ""MaterialMasterEntries"" WHERE ""Category"" = 3);");
}
protected override void Down(MigrationBuilder mb)
{
    mb.Sql(@"
UPDATE ""Materials"" SET ""MaterialType"" = 6, ""Purity"" = NULL WHERE ""MaterialType"" = 13;
UPDATE ""MaterialMasterEntries"" SET ""Category"" = 0 WHERE ""Category"" = 3;");
}
```

  Before writing it, confirm the integer values against the enums (`MaterialType.PrimaryStandard` must be 13, `MaterialMasterCategory.PrimaryStandard` 3, `Chemical` 6, `Reagent` 0, `SolutionType.Titrant` 2), and confirm the table/column names against `MicroLimsDbContextModelSnapshot.cs`.
- [ ] **Step 4: Run, expect PASS.** Then build and run the full Postgres suite.
- [ ] **Step 5: Commit** `feat(materials): migrate titrant primary standards to the Primary Standard type`

---

## Lane B2 - S2 backend (Sonnet, worktree)

### Task B2.1: Due-titrant acknowledgement with its own signature

**Files:**
- Modify: `backend/MicroLIMS.Domain/Enums/SignatureMeaning.cs` (append `TitrantDueAcknowledged` after `TitrantStandardized`)
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TitrationRecorder.cs` (`TitrationPayload`, the checks after the preparation loads, the snapshot, signing)
- Modify: `backend/MicroLIMS.Application/Workflows/TestWorkflow/TestWorkflowSupport.cs` (`PersistTestAnalysisAndFinalizeAsync`: optional `Func<Task>? beforeSigned = null`, called before the result `SignAsync`)
- Modify: `RecordTitrationResultRequest` and its mapping to `TitrationPayload` in `backend/MicroLIMS.API/Controllers/TestWorkflowController.cs` (find the request record with `git grep -n "record RecordTitrationResultRequest" backend`)
- Test: `backend/MicroLIMS.Tests/UnitTests/TitrationRecorderTests.cs` (extend)

**Interfaces:**
- Produces: `TitrationPayload(..., bool? DueTitrantAcknowledged, string? DueTitrantJustification)` (append as the last two positional params, defaulting to null) and the snapshot fields from the API contract.

- [ ] **Step 1: Failing tests** (use the existing helpers in `TitrationRecorderTests` / `TitrationScenario.cs` that build Relative + Due setups, e.g. from `Relative_Due_IsUsableWithWarning`):
  - Relative + Due titrant, no acknowledgement: throws "The titrant is due for standardization - acknowledge it and give a justification to continue."
  - Relative + Due, acknowledged, justification "Restandardization booked for tomorrow; factor not used in relative method.": records the result.
    - Exactly two `ElectronicSignatures` exist for the order: meanings `ResultRecorded` and `TitrantDueAcknowledged`, same user, EntityType "TestOrder", EntityId = order id.
    - The snapshot has `dueTitrantAcknowledgement.justification` and `engineVersion == "titration-1"`.
  - Justification shorter than 10 characters is refused.
  - Valid titrant with an acknowledgement sent: throws "No titrant warning to acknowledge."
  - Review Focus 4: Relative is Direct-only, so today only the main titrant can carry a warning. Write the check generically over both options and unit-test the helper `TitrationRecorder.WarningsOf(TitrantPreparationOption main, TitrantPreparationOption? excess)`. It returns `["a"]` for (warning "a", null excess), `["b"]` for (no warning, excess warning "b"), and `[]` when neither has a warning.
  - Wrong password with acknowledgement: nothing persisted, and only the failed-attempt record that `SignAsync` normally writes exists (mirror `WrongPassword_LeavesNothingBehind`).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement**

```csharp
// TitrationRecorder
internal static List<string> WarningsOf(TitrantPreparationOption main, TitrantPreparationOption? excess) =>
    new[] { main.Warning, excess?.Warning }.Where(w => w != null).Select(w => w!).ToList();

// after excess is loaded:
var titrantWarnings = WarningsOf(titrantOpt, excess?.Opt);
object? ack = null;
if (titrantWarnings.Count > 0)
{
    if (p.DueTitrantAcknowledged != true)
        throw new InvalidOperationException("The titrant is due for standardization - acknowledge it and give a justification to continue.");
    var j = p.DueTitrantJustification?.Trim();
    if (string.IsNullOrEmpty(j) || j.Length < 10 || j.Length > 500)
        throw new InvalidOperationException("The justification must be 10 to 500 characters.");
    ack = new { justification = j, titrantCodes = new[] { titrantPrep.Code }.Concat(excess != null && excess.Value.Opt.Warning != null ? new[] { excess.Value.Prep.Code } : Array.Empty<string?>()).ToArray(), acknowledgedAt = nowUtc };
}
else if (p.DueTitrantAcknowledged == true || !string.IsNullOrWhiteSpace(p.DueTitrantJustification))
    throw new InvalidOperationException("No titrant warning to acknowledge.");
```

  The snapshot adds `engineVersion = EngineVersion` (`public const string EngineVersion = "titration-1";`) and `dueTitrantAcknowledgement = ack`.

  Signing: pass

```csharp
beforeSigned: ack == null ? null : async () => await _signatureService.SignAsync(
    userId, p.Password, SignatureMeaning.TitrantDueAcknowledged, "TestOrder", order.Id,
    $"Due titrant acknowledged: {((dynamic)ack).justification}", ipAddress)
```

  (use a local `string ackJustification` rather than `dynamic`). In `PersistTestAnalysisAndFinalizeAsync`, `if (beforeSigned != null) await beforeSigned();` goes immediately before the existing `SignAsync`. A wrong password then fails on the first call and the result signature is never attempted. Check that `_signatureService` is reachable from `TitrationRecorder` (it is on `TestWorkflowSupport`); if it is private, make it `protected`.

  API request: add `bool? DueTitrantAcknowledged = null, string? DueTitrantJustification = null` and map them.
- [ ] **Step 4: Run, expect PASS** (all `TitrationRecorderTests`, `TitrationPostgresIntegrationTests`).
- [ ] **Step 5: Commit** `feat(titration): signed acknowledgement to use a due titrant in relative mode`

### Task B2.2: Reason for change on titration settings

**Files:**
- Modify: `backend/MicroLIMS.Application/DTOs/MasterDataRequests.cs`. In the **body** of `UpdateTestDefinitionRequest` (not the positional list; lane B3 appends there), add `public string? ChangeReason { get; init; }`.
- Modify: `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs` (`UpdateTestDefinitionAsync`, line ~367; snapshot before, compare after `TitrationDefinitionRules.NormalizeAndValidateAsync` at ~738)
- Modify: `backend/MicroLIMS.Application/Services/MasterData/TitrationDefinitionRules.cs` (add `public static object Snapshot(TestDefinition t)` returning an anonymous object of every `Titration*` field + `TitrantSolutionMasterId` + `ReplicateCount`)
- Test: `backend/MicroLIMS.Tests/UnitTests/TitrationTestMasterReasonTests.cs` (new; follow the setup of `Master_ValidTitrationCreates_AndClearsIrrelevantFields` in `TitrationRecorderTests`)

- [ ] **Step 1: Failing tests:**
  - Changing `TitrationEquivalencyFactor` 64.03 to 64.04 without `ChangeReason` throws "A reason for change is required when titration settings change."
  - With `ChangeReason = "USP 2026 revision"` it saves, and an audit event `TestDefinition.TitrationChanged` exists with the reason and before/after JSON. Query the audit events table the way `HplcMethodService` history does (`git grep -n "HistoryAsync" backend/MicroLIMS.Application/Services/HplcMethodService.cs`).
  - Changing only `DisplayName` needs no reason (Review Focus 5).
  - A non-titration test never needs a reason.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement**

```csharp
// start of UpdateTestDefinitionAsync, after loading entity:
var titrationBefore = entity.WorkflowType == WorkflowType.Titration
    ? JsonSerializer.Serialize(TitrationDefinitionRules.Snapshot(entity), SnapshotJson.Options) : null;
// after NormalizeAndValidateAsync, before SaveChanges:
if (entity.WorkflowType == WorkflowType.Titration || titrationBefore != null)
{
    var titrationAfter = JsonSerializer.Serialize(TitrationDefinitionRules.Snapshot(entity), SnapshotJson.Options);
    if (titrationAfter != titrationBefore)
    {
        var reason = request.ChangeReason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length < 5 || reason.Length > 500)
            throw new InvalidOperationException("A reason for change is required when titration settings change.");
        pendingAudit = (titrationBefore, titrationAfter, reason);
    }
}
// after SaveChanges:
if (pendingAudit is var (b, a, why) && why != null)
    await _auditEventService.RecordUserEventAsync("TestDefinition.TitrationChanged", AuditActionCategory.Configuration,
        "TestDefinition", entityId: entity.Id.ToString(),
        changes: new[] { new AuditFieldChange("Titration", b, a) }, reason: why);
```

  Inject `IAuditEventService` if the service does not have it yet. Check how DI constructs it (`git grep -n "TestDefinitionMasterDataService(" backend`) and update the test constructors. Match the exact `RecordUserEventAsync` parameter names to `IAuditEventService` (the HPLC method service passes `reason:`).
- [ ] **Step 4: Run, expect PASS**, plus every test that constructs `TestDefinitionMasterDataService`.
- [ ] **Step 5: Commit** `feat(test-master): reason for change on titration settings`

### Task B2.3: Named titration regression tests

**Files:**
- Test: `backend/MicroLIMS.Tests/UnitTests/TitrationRegressionTests.cs` (new; reuse `TitrationScenario.cs`)

- [ ] **Step 1: Write the tests:**
  - **Citric acid:** UspFactor, Direct, AcidBase, N 1, factor 0.9990, F 64.03, PercentAsIs spec Range 99.5-100.5, W 551.2, V 8.58. `ReportedValue == 99.57m`, WithinLimits.
  - **Aspirin:** UspFactor, Residual, blank required, back titrant N 0.5 factor 1.002, excess master N 0.5 (factor any valid), F 90.08, blank 50.10, W 1505.0, V 16.80. `ReportedValue == 99.86m`.
  - **Failed standardization only** (one record, `Passed = false`): context option `usable == false`, blockReason "This titrant preparation has not been standardized."; recording throws.
  - **Restandardize after recording:** record citric, then add a new passed standardization with factor 1.0100 (later `StandardizedAt`). Reload: `ParameterResult.ReportedValue`, readings and `ConditionsJson` are byte-for-byte unchanged, and `factorUsed` in the snapshot is still 0.9990.
- [ ] **Step 2: Run.** These should PASS against the current code; if any fails, stop and report (do not change the engine).
- [ ] **Step 3: Commit** `test(titration): citric/aspirin golden values, failed standardization, restandardize after use`

---

## Lane B3 - S3 backend (Sonnet, worktree)

### Task B3.1: Area enum, columns, helper, migration

**Files:**
- Create: `backend/MicroLIMS.Domain/Enums/PhyschemArea.cs` (`FinishedProduct = 0, RawPackaging = 1, Both = 2`)
- Modify: `backend/MicroLIMS.Domain/Entities/TestDefinition.cs` (`public PhyschemArea? PhyschemArea { get; set; }`)
- Modify: `backend/MicroLIMS.Domain/Entities/UserOrgMembership.cs` (same property; comment: "Physicochemical section memberships only; null = both areas")
- Create: `backend/MicroLIMS.Application/Helpers/PhyschemAreas.cs`
- Create: migration `PhyschemAreas` (`dotnet ef migrations add PhyschemAreas ... --configuration Release`), plus the backfill SQL
- Test: `backend/MicroLIMS.Tests/UnitTests/PhyschemAreasTests.cs`, `backend/MicroLIMS.Tests/IntegrationTests/PhyschemAreasMigrationPostgresTests.cs`

**Interfaces:**
- Produces:

```csharp
public enum WorkspaceArea { Fp, RmPm }
public static class PhyschemAreas
{
    public const string SectionCode = "FP";
    public static WorkspaceArea OfCategory(SampleCategory c) =>
        c is SampleCategory.RawMaterial or SampleCategory.PackagingMaterial ? WorkspaceArea.RmPm : WorkspaceArea.Fp;
    public static bool Includes(PhyschemArea? testArea, WorkspaceArea area) => testArea switch
    {
        PhyschemArea.Both or null => true,
        PhyschemArea.FinishedProduct => area == WorkspaceArea.Fp,
        PhyschemArea.RawPackaging => area == WorkspaceArea.RmPm,
        _ => false,
    };
    public static IReadOnlyList<SampleCategory> CategoriesOf(WorkspaceArea a) => a == WorkspaceArea.RmPm
        ? new[] { SampleCategory.RawMaterial, SampleCategory.PackagingMaterial }
        : Enum.GetValues<SampleCategory>().Where(c => c is not (SampleCategory.RawMaterial or SampleCategory.PackagingMaterial)).ToArray();
    public static WorkspaceArea? Parse(string? s) => s?.ToLowerInvariant() switch
    { "fp" => WorkspaceArea.Fp, "rmpm" => WorkspaceArea.RmPm, null or "" => null,
      _ => throw new InvalidOperationException("area must be 'fp' or 'rmpm'.") };
    // Membership area (null = Both) -> workspace areas it grants.
    public static IReadOnlyList<WorkspaceArea> Grants(PhyschemArea? m) => m switch
    { PhyschemArea.FinishedProduct => new[] { WorkspaceArea.Fp }, PhyschemArea.RawPackaging => new[] { WorkspaceArea.RmPm },
      _ => new[] { WorkspaceArea.Fp, WorkspaceArea.RmPm } };
}
```

- [ ] **Step 1: Failing unit tests** for every helper branch (including `Parse("x")` throwing, and `Includes(null, …)` true for micro tests).
- [ ] **Step 2: Failing migration Postgres test.** Before the migration, seed section FP with tests:
  - `T-FP`: spec only on a FinishedProduct item
  - `T-RM`: spec only on a RawMaterial item
  - `T-BOTH`: specs on both
  - `T-NONE`: no specs

  Also seed section MICRO test `T-MIC`. After Up, expect `T-FP`=0, `T-RM`=1, `T-BOTH`=2, `T-NONE`=0, `T-MIC`=null. All memberships stay null. Down drops both columns.
- [ ] **Step 3: Implement.** In the migration Up, after the generated `AddColumn`s:

```sql
UPDATE "TestDefinitions" td SET "PhyschemArea" = CASE
  WHEN EXISTS (SELECT 1 FROM "Specifications" s JOIN "Items" i ON i."Id" = s."ItemId" WHERE s."TestCode" = td."Code" AND i."Category" IN (1,2))
   AND EXISTS (SELECT 1 FROM "Specifications" s JOIN "Items" i ON i."Id" = s."ItemId" WHERE s."TestCode" = td."Code" AND i."Category" NOT IN (1,2)) THEN 2
  WHEN EXISTS (SELECT 1 FROM "Specifications" s JOIN "Items" i ON i."Id" = s."ItemId" WHERE s."TestCode" = td."Code" AND i."Category" IN (1,2)) THEN 1
  ELSE 0 END
WHERE td."SectionId" IN (SELECT "Id" FROM "DocumentSections" WHERE "Code" = 'FP');
```

  Confirm `SampleCategory.RawMaterial = 1` and `PackagingMaterial = 2`, and check `Specifications.TestCode` / `TestDefinitions.Code` in the model snapshot.
- [ ] **Step 4: Run, expect PASS**, then the full Postgres suite.
- [ ] **Step 5: Commit** `feat(physchem): area on tests and memberships with backfill migration`

### Task B3.2: Area on the Test Master and in specification rules

**Files:**
- Modify: `backend/MicroLIMS.Application/DTOs/MasterDataRequests.cs`. Append `PhyschemArea? PhyschemArea = null` as the **last positional parameter** of `CreateTestDefinitionRequest` and of `UpdateTestDefinitionRequest`.
- Modify: `backend/MicroLIMS.Application/DTOs/Responses/ConfigurationResponses.cs` (`TestDefinitionResponse` + mapping: `PhyschemArea`)
- Modify: `backend/MicroLIMS.Application/Services/MasterData/TestDefinitionMasterDataService.cs` (create + update)
- Modify: specification create/update (`backend/MicroLIMS.Application/Services/SpecificationService.cs` around lines 196 and 381, where the item category is already loaded; also `SpecificationMasterDataService.cs` if it saves specs separately; confirm with `git grep -n "Specifications.Add\|_db.Specifications.Add" backend/MicroLIMS.Application`)
- Test: `backend/MicroLIMS.Tests/UnitTests/PhyschemAreaRulesTests.cs` (new)

- [ ] **Step 1: Failing tests:**
  - Creating a test in section FP without `PhyschemArea` throws "Choose the area of this test (FP, RM & PM or Both)."
  - Creating a test in section MICRO with `PhyschemArea = Both` saves with `PhyschemArea == null`.
  - A spec for a FinishedProduct item on an RawPackaging test throws "Test X is not used for finished products - it belongs to RM & PM." The mirror case is "...is not used for raw and packaging materials - it belongs to FP." A Both test accepts both.
  - Updating a Both test to FinishedProduct while a RawMaterial item has a spec on it throws "Items RM-CITRIC still have specifications on this test for RM & PM - remove them first." (comma-separated codes, max 10; Review Focus 2).
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.** A private helper in `TestDefinitionMasterDataService`:

```csharp
private async Task ApplyPhyschemAreaAsync(TestDefinition entity, PhyschemArea? requested)
{
    var code = await _db.DocumentSections.Where(s => s.Id == entity.SectionId).Select(s => s.Code).FirstAsync();
    if (code != PhyschemAreas.SectionCode) { entity.PhyschemArea = null; return; }
    if (!requested.HasValue) throw new InvalidOperationException("Choose the area of this test (FP, RM & PM or Both).");
    if (entity.Id != 0 && requested != PhyschemArea.Both)
    {
        var lost = requested == PhyschemArea.FinishedProduct ? WorkspaceArea.RmPm : WorkspaceArea.Fp;
        var cats = PhyschemAreas.CategoriesOf(lost);
        var items = await _db.Specifications.Where(s => s.TestCode == entity.Code && cats.Contains(s.Item!.Category))
            .Select(s => s.Item!.Code).Distinct().Take(10).ToListAsync();
        if (items.Count > 0)
            throw new InvalidOperationException($"Items {string.Join(", ", items)} still have specifications on this test for {(lost == WorkspaceArea.RmPm ? "RM & PM" : "FP")} - remove them first.");
    }
    entity.PhyschemArea = requested;
}
```

  Check that `Item` has a `Code` property (`git grep -n "public string Code" backend/MicroLIMS.Domain/Entities/Item.cs`); otherwise use its name field. Call the helper in create and update after `SectionId` is resolved. On update, use the old `Code` if the code changes in the same request.

  Spec rule: where the spec's item category is known and the test is resolved, add:

```csharp
if (!PhyschemAreas.Includes(test.PhyschemArea, PhyschemAreas.OfCategory(itemCategory)))
    throw new InvalidOperationException(PhyschemAreas.OfCategory(itemCategory) == WorkspaceArea.Fp
        ? $"Test {test.Code} is not used for finished products - it belongs to RM & PM."
        : $"Test {test.Code} is not used for raw and packaging materials - it belongs to FP.");
```

- [ ] **Step 4: Run, expect PASS**, plus existing Test Master and specification tests. Existing tests that create FP-section tests now need `PhyschemArea`: give the shared test helpers a default `PhyschemArea.Both` for FP-section tests rather than editing every test.
- [ ] **Step 5: Commit** `feat(physchem): test area on the test master and specification area rule`

### Task B3.3: Area access, workspace filtering, memberships

**Files:**
- Modify: `backend/MicroLIMS.Application/Interfaces/IUserSectionScopeService.cs` + `Services/UserSectionScopeService.cs`. Add `Task<IReadOnlyList<WorkspaceArea>> GetPhyschemAreasAsync(int userId, CancellationToken ct = default)`. Admin, or a department-level membership covering the FP section, gives both areas. A section membership on FP gives `PhyschemAreas.Grants(m.PhyschemArea)`, unioned over rows. Otherwise none.
- Modify: `backend/MicroLIMS.Application/Services/LaboratoryOrganizationService.cs`:
  - `LaboratorySectionDto` += `IReadOnlyList<string> PhyschemAreas` (`"fp"`, `"rmpm"`; empty for non-FP sections)
  - `UserOrgMembershipDto` += `PhyschemArea? PhyschemArea = null`
  - `ReplaceMembershipsAsync` stores it, refuses it on non-FP memberships ("An area can only be set on a Physicochemical Laboratory membership."), and treats a changed area on an existing row as an update
- Modify: `backend/MicroLIMS.Application/Services/TestingWorkspaceService.cs` + `Interfaces/ITestWorkspaceService.cs` + `backend/MicroLIMS.API/Controllers/TestingWorkspaceController.cs`:
  - add `string? area` to `TestingWorkspaceFilterDto` and to the `counts` / `{id}` actions
  - a private `ResolveAreaAsync(int? labSectionId, string? area, int? userId)` returns the categories to keep or null
  - Required when `labSectionId` is the FP section ("area is required for the Physicochemical Laboratory workspace.", 400).
  - Throws `ForbiddenAccessException` (find the 403 exception type used by the lab scope with `git grep -n "class .*Forbidden" backend/MicroLIMS.Shared backend/MicroLIMS.Application`) when the user's areas exclude it.
  - Narrow the sample queries with `.Where(s => cats.Contains(s.Category))` in `GetActiveSamplesAsync`, `GetWorkloadCountsAsync` and `GetSampleAsync`.
- Test: `backend/MicroLIMS.Tests/IntegrationTests/PhyschemAreaWorkspacePostgresTests.cs` (new; reuse the lab-separation workspace Postgres test setup, found with `git grep -ln "labSectionId\|LabSectionId" backend/MicroLIMS.Tests | head`)

- [ ] **Step 1: Failing tests:**
  - An FP-only user gets 403 on `GetSampleAsync(rmSampleId, user, fpSection, "rmpm")` (Review Focus 3).
  - A Both user: `area=fp` lists only FinishedProduct samples, `area=rmpm` only RM/PM, and counts per area add up to the lab total.
  - FP `labSectionId` with no area: 400.
  - MICRO `labSectionId` with no area: unchanged behaviour.
  - Membership round trip: setting `RawPackaging` on an FP membership persists and is returned; setting it on a MICRO membership is refused.
  - `GetMySectionsAsync` returns `["fp","rmpm"]` for null-area members and `["rmpm"]` for RawPackaging members.
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement** as described in Files.
- [ ] **Step 4: Run, expect PASS**, then the full Postgres suite.
- [ ] **Step 5: Commit** `feat(physchem): per-user area access and area-filtered workspace`

---

## Lane F1 - S1 frontend (agy, main tree)

Before any UI work, load the `ui-ux-pro-max` skill, then `frontend-design`. Do not commit; the orchestrator reviews and commits.

**Files:**
- `frontend/src/modules/inventory/materials/types/materialTypes.ts`: add `"PrimaryStandard"` to the `MaterialType` union and its label "Primary Standard".
- `frontend/src/modules/inventory/materials/components/MaterialFilterBar.tsx`: the type filter shows the lab's types (from `MaterialService.getTypeOptions(sectionId)`, the same call `AddMaterialDialog` uses) instead of the fixed `MATERIAL_TYPE_OPTIONS` micro list.
  - Keep the `MATERIAL_TYPE_OPTIONS` export only if other importers need it (`git grep -n MATERIAL_TYPE_OPTIONS frontend/src`).
  - `MaterialsPage.tsx` passes the active section.
- `AddMaterialDialog.tsx`: the initial type is the first `builtIn` type of the lab, not `"DehydratedMedia"`. Show and require purity for `PrimaryStandard` the same way as `ReferenceStandard` (moisture stays ReferenceStandard-only). The master-entry picker for `PrimaryStandard` lists `PrimaryStandard` entries.
- `frontend/src/modules/laboratoryConfiguration/masterDataSimple/MaterialMasterPage.tsx`: category option "Primary Standard".
- `frontend/src/modules/laboratoryConfiguration/masterDataSimple/solutionMaster/SolutionTitrantSection.tsx`: the primary-standard entry picker lists `PrimaryStandard` entries only.
- `frontend/src/modules/solutionPreparation/components/TitrantStandardizationDialog.tsx`: the lot picker shows each lot's purity, and the server message is shown verbatim when a lot has none.

**Checks:** `npx tsc -b`, `npx eslint` on changed files, and a vitest for the filter bar showing "Primary Standard" and not "Dehydrated Media" when type-options returns the FP list (mock the service).

## Lane F2 - S2 frontend (agy, main tree)

Load `ui-ux-pro-max`, then `frontend-design`. Do not commit.

**Files:**
- `frontend/src/modules/testingWorkspace/types/testWorkflowTypes.ts` + `services/TestWorkflowService.ts`: request fields `dueTitrantAcknowledged`, `dueTitrantJustification`.
- `frontend/src/modules/testingWorkspace/TitrationPanel.tsx`:
  - When the selected titrant (or excess) option has `warning`, show an amber Alert with the warning text, a required checkbox "I acknowledge this titrant is due for standardization", and a required justification field (10-500 chars, counter).
  - Submit is disabled until both are filled.
  - The SignatureDialog text says "Your signature records the result and your acknowledgement of the due titrant."
  - Send both fields only when a warning exists.
  - If TitrationPanel is over ~650 lines after the change, move the block into `TitrantDueAcknowledgement.tsx`.
- `utils/titrationSnapshot.ts`, `TestResultCards.tsx`, `SampleSummaryDialog.tsx`: show "Due titrant acknowledged: <justification>" as a warning line in the titration block when `dueTitrantAcknowledgement` is present.
- Tests: extend `TitrationPanel.test.tsx` (submit disabled until checkbox + 10-char justification; payload contains both) and `titrationSnapshot.test.ts` (parses the acknowledgement).

## Lane F3 - S2/S3 Test Master (agy, main tree)

Load `ui-ux-pro-max`, then `frontend-design`. Do not commit. Do **not** touch `menuConfig.ts` or `AppRoutes.tsx` (lane F4 owns them).

**Files:**
- Create `frontend/src/modules/laboratoryConfiguration/masterDataSimple/testMasterArea.ts`:
  - `type PageArea = "fp" | "rmpm"`
  - `areaIncludes(testArea, page)`
  - `defaultAreaFor(page)`: fp gives "FinishedProduct", rmpm gives "RawPackaging"
  - `toggleBoth(current, page, checked)`
  - with `testMasterArea.test.ts`
- Create `TestAreaField.tsx`: the checkbox "Also used for RM & PM" (on FP) / "Also used for FP" (on RM & PM), driven by `toggleBoth`.
- Create `TitrationChangeReasonField.tsx`: a required text field (5-500 chars), shown in the save step when an existing Titration test's titration fields differ from the loaded values.
- `TestMasterPage.tsx`:
  - prop `area?: PageArea`, used only when `lab === "fp"`
  - list filter via `areaIncludes`
  - create sends `physchemArea: defaultAreaFor(area)` merged with the Both checkbox
  - edit sends the current `physchemArea` and `changeReason`
  - page title "FP Test Master" / "RM & PM Test Master"
  - Only prop wiring and rendering of the two new components. No new logic inline.
- `frontend/src/services/masterDataOptions.ts` / `hooks/useTestDefinitions.ts`: carry `physchemArea` through.
- `frontend/src/modules/laboratoryConfiguration/items/components/SpecificationParameterDialog.tsx`: the test picker for a physicochemical item offers only tests whose area includes the item's category (FinishedProduct uses fp; RawMaterial/PackagingMaterial use rmpm). The server stays authoritative; show its message on save errors.

## Lane F4 - S3 workspaces, routes, menu (agy, main tree)

Load `ui-ux-pro-max`, then `frontend-design`. Do not commit.

**Files:**
- `frontend/src/services/laboratorySectionService.ts`: `LaboratorySection.physchemAreas: ("fp"|"rmpm")[]`.
- `frontend/src/hooks/useMyLabs.ts`: expose `physchemAreas` for the FP section. An admin gets both.
- `frontend/src/modules/receivingTesting/LabWorkspaceRoute.tsx`:
  - prop `area?: "fp" | "rmpm"`, required when `code === "FP"`
  - NotAMember message "You do not have access to the RM & PM workspace." (or FP) when the area is not granted
  - pass `lab.area` down
- `ReceivingTestingWorkspacePage.tsx`:
  - `WorkspaceLab` gains `area?`
  - title "FP Workspace" / "RM & PM Workspace"
  - every `ReceiveService` call passes `area`
  - `ALLOWED_CATEGORIES` for FP: area fp gives `["product"]`, area rmpm gives `["rm","pm"]`
- `frontend/src/modules/receiving/services/ReceiveService.ts`: `area` on `getRecordsPaged` filter, `getWorkloadCounts(labSectionId, area)`, `getSample(id, labSectionId, area)`.
- `frontend/src/routes/AppRoutes.tsx`:
  - `/physicochemical/workspace` uses `<LabWorkspaceRoute key="FP-fp" code="FP" area="fp" />`
  - new `/physicochemical/rm-pm-workspace` uses `area="rmpm"`
  - `/laboratory-configuration/fp-test-master` passes `area="fp"`
  - new `/laboratory-configuration/rm-pm-test-master` uses `<TestMasterPage key="rmpm" lab="fp" area="rmpm" />`
- `frontend/src/routes/menuConfig.ts`:
  - Physicochemical Laboratory: "FP Workspace" and "RM & PM Workspace", each shown only if `physchemAreas` includes it
  - Physicochemical Configuration: "FP Test Master" and "RM & PM Test Master"
  - Update `navigation.ts` highlighting if it keys on paths.
- Tests: vitest for menu items by area (fp only; rmpm only; both).

## Lane F5 - S3 user area (agy, main tree)

Load `ui-ux-pro-max`, then `frontend-design`. Do not commit.

**Files:**
- `frontend/src/modules/users/dialogs/UserSectionsDialog.tsx` and its service (find with `git grep -n "memberships" frontend/src/modules/users`): for a membership row whose section code is `FP`, an "Area" select (FP / RM & PM / Both; Both sends `null`). It is hidden for other sections. Show the server error verbatim.
- Test: vitest that the area select appears only for the FP section and that Both maps to null.

---

## Lane S4 - seed and E2E (Sonnet for the seed; orchestrator for the browser) - after all lanes merge

### Task S4.1: Seed

**Files:**
- Create: `docs/superpowers/plans/2026-10-05-titration-rmpm-test-data.sql` (idempotent; `WHERE NOT EXISTS` guards; section by `Code = 'FP'`; users by username)

Contents:
- Primary Standard entry `PS-KHP` (or reuse RG-022 now migrated), and a KHP lot of type PrimaryStandard with purity 99.95, expiry +2 years.
- Solution masters: `1 N Sodium hydroxide VS` (PrimaryStandard KHP, E = 204.22 mg/mL), `0.5 N Sulfuric acid VS` (against 1 N NaOH VS), `0.5 N Sodium hydroxide VS` (KHP, E = 102.11). Reuse the existing `0.1 N Iodine VS`.
- RM items `RM-CITRIC`, `RM-ASPIRIN`, `RM-ASCORBIC` (category RawMaterial, FP section).
- Tests, all PhyschemArea RawPackaging:
  - `TIT-CITRIC`: AcidBase, Direct, UspFactor, 1 N NaOH, F 64.03, blank off, 1 replicate, Visual phenolphthalein
  - `TIT-ASPIRIN`: AcidBase, Residual, blank on, back titrant 0.5 N H2SO4, excess 0.5 N NaOH 50 mL, F 90.08
  - `TIT-ASCORBIC`: Redox, Direct, UspFactor, 0.1 N iodine, F 88.06, starch
  - `TIT-REL`: Relative, Direct, iodine, standard entry = the ascorbic acid RS entry if present, else create RS `RS-ASCORBIC` with a lot of purity 99.8
- Specs PercentAsIs Range: citric 99.5-100.5; aspirin 99.5-100.5; ascorbic 99.0-100.5.

Apply to LIMSV2 only, after a backup `E:/MicroLIMS/backups/LIMSV2_before_rmpm_titration_seed_20261005.dump`. Commit `test(data): RM & PM titration seed`.

### Task S4.2: Browser E2E (orchestrator)

Spec section 4 E2E list. Restart the API and Vite first. Users e2e.head / e2e.analyst / e2e.reviewer; set e2e.analyst's FP membership to RawPackaging for the access check, and restore it afterwards.

---

## Orchestration (order and gates)

1. Start in parallel: B1, B2, B3 (Sonnet, `isolation: worktree`, separate artifact paths `art-b1/b2/b3`), and F1-F5 (agy delegate, main tree, disjoint files as listed).
2. Merge order into `feat/physchem-areas`: B1, then B2, then B3. Resolve `MasterDataRequests.cs` / `TestDefinitionMasterDataService.cs` overlaps by keeping both. If the migrations' ModelSnapshot conflicts, regenerate B3's migration on top.
3. After each merge: `dotnet build`, then the full Postgres suite. Back up LIMSV2, apply the migrations, and verify Down for each new migration.
4. Review agy changes file by file (diff the whole file; grep for unrelated edits), then tsc/eslint/vitest, then commit per lane.
5. S4 seed, then the browser E2E. Stop for user sign-off. Neon pre-merge checks before any PR (primary-standard component usage; area backfill counts).
