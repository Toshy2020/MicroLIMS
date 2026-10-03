# Working Standard Qualification — Design

Date: 2026-10-03. Closes open item O4 of `2026-09-29-hplc-chain-design.md`.

## 1. Purpose
Let the lab make an in-house working (secondary) standard from a raw material, qualify it by HPLC assay against a
primary reference standard, approve it with e-signatures, and then use it wherever a primary reference standard lot
is accepted today (solution preparations, HPLC SST). Requalify the same lot every 12 months.

## 2. Regulatory basis
- USP <11> (USP 35): covers official USP RS only — use per label (As Is / Dry Before Use / Determine Water Content),
  use only while "Current Lot" or within Valid Use Date. It does not address working standards.
- ICH Q7 §11.17–11.19: secondary standards are qualified against a primary standard, documented, and requalified
  on a schedule. This design follows that.
- USP <1251>: balance check before use; out of scope here (equipment module), noted for the SOP.

## 3. User decisions (2026-10-03)
| ID | Decision |
|---|---|
| D1 | Scope: qualification of in-house working standards. Daily standard solutions stay in Solution Preparation. |
| D2 | Potency assigned by HPLC assay against the primary RS. Site rule: exactly 6 replicates, RSD ≤ 2.0 %, potency = mean. |
| D3 | The qualification assay runs in the existing HPLC workspace. |
| D4 | Moisture measured once at qualification, stored on the WS lot until requalification. |
| D5 | Potency stored on the dried basis: `potency = mean as-is assay × 100 / (100 − MC)`. Both purity and moisture live on the lot and are applied at use exactly like a primary RS lot. |
| D6 | Valid 12 months from approval; then requalify or retire. Expired = blocked. |
| D7 | Sign-off: prepare (analyst) → review (reviewer) → approve (QC head), each with e-signature. Only an approved WS is usable. |
| D8 | Stock: one WS lot, grams remaining, deducted by solution preparations. |
| D9 | Source: either a received raw-material sample with an approved assay result, or a manually entered RM name + batch with its first test report attached. Nothing is deducted from the source (a received RM is a testing sample, data only). |
| D10 | Code `WS-nn/MM/yyyy`, same style as titrants (`VS-nn/MM/yyyy`). |
| D11 | Requalification extends the same lot (new potency, moisture, expiry); no new code. |
| D12 | An approved WS is accepted everywhere a primary RS is accepted now; no per-method flag. |

## 4. Data model

### 4.1 `MaterialType.WorkingStandard` (new enum value)
A WS lot is a `Material` row:
- `MaterialType = WorkingStandard`, `Code = WS-nn/MM/yyyy`, `Unit = Gram`.
- `MaterialMasterEntryId` = the primary standard's master entry (category ReferenceStandard). This is what makes
  D12 work without new code paths: Solution Preparation matches lots by master entry, and HPLC SST checks
  `LotUsability.Check(lot, methodAnalyte.StandardEntryId, …)`.
- `MaterialName`, `BatchNumber` copied from the source; `ManufacturerName` = "In-house"; `Location` entered.
- `Purity` = dried-basis potency (D5), `MoisturePercent` = MC (D4), `ExpiryDate` = approval date + 12 months (D6).
- `QuantityReceived` / `QuantityRemaining` = grams entered on the first qualification.
- Created only when the first qualification is approved. It is never created by the Material Stock receive form:
  `MaterialService` rejects `WorkingStandard` on receive and on type change.
- Rules updated: `MaterialTypeRules`, master-link-required types, master category mapping (→ ReferenceStandard),
  purity/moisture allowed for `WorkingStandard` as for `ReferenceStandard`, default unit Gram.
- `GetUsableReferenceStandardsAsync` returns both `ReferenceStandard` and `WorkingStandard`.

### 4.2 `WorkingStandardQualification` (new)
| Field | Notes |
|---|---|
| `Id`, `Version`, `SectionId` | section-scoped like other lab records |
| `Code` | `WSQ-nn/MM/yyyy` (record code; the lot keeps `WS-…`) |
| `Kind` | `Initial` / `Requalification` |
| `WorkingStandardMaterialId` | null for Initial until approval; set for Requalification |
| `MaterialMasterEntryId` | the primary standard's master entry (required, category ReferenceStandard) |
| `SourceSampleId` | received RM sample (Category RawMaterial) with an approved assay result; or null |
| `SourceMaterialName`, `SourceBatchNumber` | copied from the sample, or typed (manual source) |
| `QuantityGrams`, `Location` | Initial only |
| `MoisturePercent` | 0 ≤ x < 100, entered by the analyst |
| `HplcRunSampleId` | the run sample that carried the assay |
| `MeanAssayPercent`, `RsdPercent`, `PotencyPercent` | computed at submit (D2, D5) |
| `Passed`, `FailureReasons` | 6 replicates and RSD ≤ 2.0 % |
| `Status` | `Draft` → `Assayed` → `Reviewed` → `Approved`; or `Rejected` (reason required) |
| `PreparedBy/At + SignatureId`, `ReviewedBy/At + SignatureId`, `ApprovedBy/At + SignatureId` | e-signatures |
| `Evidence` | attachments (source first test report for manual source; moisture result); superseded, never replaced |

Every insert/update is audited by `MicroLimsDbContext.SaveChanges` (existing mechanism).

### 4.3 `HplcRunSample` change
- `TestOrderId` becomes nullable; new nullable `WorkingStandardQualificationId`.
- Check constraint: exactly one of the two is set.

### 4.4 Constants
`WorkingStandardRules`: `Replicates = 6`, `MaxRsdPercent = 2.0m`, `ValidityMonths = 12`, `DueSoonDays = 30`.
Fixed site rule (D2), not configurable.

## 5. Workflow

1. **Create (Draft)** — analyst with `WorkingStandards.Qualify`:
   - Initial: pick source (approved-assay RM sample, or manual name + batch + first test report upload), master
     entry, quantity (g), location.
   - Requalify: pick an existing WS lot (approved, may be expired); source = the lot itself.
   - Enter moisture %.
2. **Assign to HPLC run** — in the workspace, the candidate appears in the assign list beside test orders.
   Guards: run's SST passed; every SST analyte standard lot is `MaterialType.ReferenceStandard` (never qualify a WS
   against a WS); run's method has an analyte whose `StandardEntryId` = qualification's master entry; one open
   qualification per run sample.
3. **Enter replicates** — existing replicate entry. Exactly 6 replicates required for a qualification sample.
4. **Submit** — `HplcAssayCalculator.Calculate` (unchanged) gives as-is assay per replicate. Service computes mean,
   RSD (sample SD / mean × 100), potency = mean × 100 / (100 − MC), Passed. Written to the qualification
   (status `Assayed`) instead of `ParameterResult`. No test-order review flow is triggered. Analyst signs.
5. **Review** — reviewer signs or returns to Draft with reason (run sample can be re-entered per workspace rules).
6. **Approve** — QC head signs. Only allowed when `Passed`. On approval, in one transaction:
   - Initial: create the WS `Material` lot (4.1) with the next `WS-nn/MM/yyyy` code; set
     `WorkingStandardMaterialId`.
   - Requalification: update the lot's `Purity`, `MoisturePercent`, `ExpiryDate`.
7. **Reject** — reviewer or approver, reason required; a failed assay can only be rejected.

Codes: max+1 per month/year with a unique index and retry on conflict (same as titrant codes, S4 decision — no
sequence table).

## 6. Use and expiry
- Usable = existing `LotUsability` (in stock, not expired, master entry matches). No new gate.
- Working Standards page shows "due for requalification" for lots expiring within 30 days and "expired" for past.
- Retire: no action needed — an expired lot is already blocked; not requalifying it retires it.
- Reminder job: skipped; add if the list is not enough.

## 7. API and screens
- `api/working-standards`: list lots + qualifications, get, create, update draft, add evidence, review, approve,
  reject. Assign/replicates/submit go through the existing `api/hplc-workspace` endpoints with the qualification id.
- Permission `WorkingStandards.Qualify` (new, granted to the roles that have `Solutions.Prepare`); review uses
  `Samples.Review`, approve uses `Samples.Approve`; the reviewer and approver must differ from the preparer and
  from each other.
- Frontend page `/laboratory-configuration/working-standards`: lots table (code, material, batch, potency, MC,
  remaining, expiry, status badge), qualifications table, create/requalify dialog, review/approve panel. HPLC
  workspace assign list shows WS candidates with a "WS qualification" tag. Files kept small (split by component).
- No client-side potency/RSD math — the backend returns computed values.

## 8. Testing
- Unit: dried-basis potency, RSD, 6-replicate rule, pass/fail boundary (RSD exactly 2.0 % passes), hand-checked numbers.
- Postgres integration: approval creates the lot with code and expiry; requalify updates the same lot; WS rejected as
  SST standard on a run carrying a qualification; exactly-one FK constraint; code sequence under concurrency;
  approved WS appears in solution-prep lot match and the SST picker; expired WS blocked; source sample without an
  approved assay refused; manual source without a report refused; WorkingStandard refused in Material receive.
- Frontend: create dialog validation, assign list tag, approval panel.
- Browser E2E: RM sample → qualification → run with primary RS → 6 reps → review → approve → WS lot used in a
  solution preparation and as SST standard in another run.

## 9. Out of scope
- Balance check-weight log (<1251>), primary RS "Current Lot" catalog check, working standards for non-HPLC methods
  (titration/UV), mass-balance potency, aliquot vials.

## 10. Amendments (plan 2026-10-03)

These resolve details the spec left open.

1. The run link lives only on `HplcRunSample.WorkingStandardQualificationId` (no `HplcRunSampleId` on the qualification, which avoids a two-way FK). The "current" run sample is the `Assigned` one on a run that is not `Abandoned`.
2. WS entry uses its own workspace endpoints/page (`/hplc-workspace/:instrumentId/run/:runId/qualification/:runSampleId`) reusing `ReplicateEntryTable`; responses are entered only for the qualified analyte (the method analyte whose `StandardEntryId` equals the qualification's master entry), so multi-analyte methods work.
3. The page lives at `/working-standards` in the labs menu next to Solution Preparation (the `/laboratory-configuration/*` area is gated by `MasterData.Manage`, which analysts and reviewers lack).
4. `Material.MaterialName` of the WS lot is the master entry name (same as every master-linked lot); the source name stays on the qualification.
5. Attachments are `WorkingStandardDocument` rows (kinds `SourceReport`, `MoistureReport`); a new upload of the same kind supersedes the previous one.
6. Return (reviewer sends an `Assayed` qualification back to `Draft`) requires a reason, clears the computed results, and makes the replicates editable again even if the run is `Completed`.
