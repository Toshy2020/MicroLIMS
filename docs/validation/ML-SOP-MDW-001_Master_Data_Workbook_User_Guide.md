# Master Data Workbook — User Guide

| Field | Value |
|---|---|
| **Document No.** | ML-SOP-MDW-001 |
| **Version** | 0.1 — Draft for review |
| **Related documents** | ML-SYS-CS-001 Configuration Specification (§4 Procedure); ML-SYS-VP-001 Validation Plan |
| **Applies to** | Setting up lab master data in a new MicroLIMS deployment, and adding new master data records later |

---

## 1. Who may do it

- Loading the workbook and verifying a load need the **Master Data Manage** permission. Section Heads and System Administrators have it.
- **Two people are always needed.** One person loads the workbook and signs. A **second person** checks the load against the source documents and signs *Verify*. MicroLIMS refuses a verification by the person who loaded the workbook.
- You can only add records for **your own lab sections**. For example, a Physicochemical Section Head cannot load Microbiology specifications or Microbiology equipment. Ask a Section Head of that lab.
- The verifier must also be a member of every lab section the load added material master entries to. System Administrators can verify any load.
- Both signatures use your own MicroLIMS password. Never sign for someone else.

## 2. Gather the sources

Before you start, have the **approved, current** versions of:

- product and material specifications (for the Items, Specifications and SpecificationStages sheets);
- the equipment list (Equipment);
- the media list (MediaProducts);
- the reagent, standard and indicator list per lab section (MaterialMaster);
- the environmental monitoring sampling plan (EmDepartments, EmRooms);
- the water sampling plan (WaterDepartments, WaterPoints);
- the reference organism list (Organisms).

Every row you add must come from one of these documents. The second person checks the rows against the same documents.

## 3. Download the workbook

1. Open **General Laboratory Configuration → Master Data Workbook**.
2. Click **Download workbook**. The file is named `MicroLIMS-master-data-<date>.xlsx`.
3. Open it in Excel and read the **Instructions** sheet first.

The workbook already lists every record that is in MicroLIMS. On the MaterialMaster sheet, a Section Head sees only the entries of their own sections; a System Administrator sees all of them. **Do not change or delete the listed rows.** Add your new rows underneath them.

The limit per upload is **5,000 new rows**. Rows that are already in MicroLIMS do not count towards it.

## 4. Fill sheets 1–10

The workbook only **adds** records. To change an existing record, use its screen in MicroLIMS (see step 10).

General rules for every sheet:

- Row 1 is the header. Do not rename, reorder or delete header cells.
- Where a column has a drop-down list, use it.
- Separate lists with `;`, for example `TAMC;E.coli`.
- Use a **dot** for decimals: `32.5`, not `32,5`.
- Write dates as `yyyy-MM-dd`, for example `2027-03-31`.
- **Required** columns must not be blank.

Fill the sheets **in this order**. A later sheet can refer to a record added on an earlier sheet in the same workbook.

### 4.1 Organisms

| Column | Required | What to enter |
|---|---|---|
| ScientificName | Yes | Full scientific name, e.g. *Escherichia coli*. Must be unique. |
| AtccNumber | | ATCC number without the prefix, e.g. `8739`. Must be unique when given. |
| CommonName | | |
| Description | | |

### 4.2 ProductionStages

| Column | Required | What to enter |
|---|---|---|
| Name | Yes | Stage name shown on the receiving form, e.g. Bulk. |
| Role | Yes | What the stage means to the system: `Bulk`, `InProcess`, `Finished`, `Stability` or `Other`. |

### 4.3 EmDepartments

| Column | Required | What to enter |
|---|---|---|
| Name | Yes | Production department monitored by EM, e.g. Sterile Filling. |
| Class | | Cleanroom classification, e.g. Grade B. |
| TestingFrequency | | Information only, e.g. Weekly. |

### 4.4 EmRooms

| Column | Required | What to enter |
|---|---|---|
| Department | Yes | Name of a department on the EmDepartments sheet. |
| Name | Yes | Room name, unique within its department. |
| GradeClassification | | e.g. Grade C. |

### 4.5 WaterDepartments

| Column | Required | What to enter |
|---|---|---|
| Name | Yes | Water system or area, e.g. Purified Water Loop 1. |

### 4.6 WaterPoints

| Column | Required | What to enter |
|---|---|---|
| Code | Yes | Sampling point code, unique, e.g. PW-01. |
| Location | Yes | |
| TestingFrequency | | e.g. Daily. |
| TestCodes | | Test codes from Test Master, separated by `;`, e.g. `TAMC;PSEUDO`. |
| WaterDepartment | | A name on the WaterDepartments sheet. |

### 4.7 MaterialMaster

| Column | Required | What to enter |
|---|---|---|
| SectionCode | Yes | Code of the lab section that owns the entry. You must be a member of that section. |
| Code | Yes | Unique within the section, e.g. RG-011. |
| Name | Yes | |
| Category | Yes | From the drop-down list. |
| Grade | | |
| Source | | |
| BaseUnit | Yes | From the drop-down list. |
| WorkingConcentration | | Indicators only. |
| Solvent | | Indicators only. |
| TransitionRangeFrom | | Indicators only, pH. |
| TransitionRangeTo | | Indicators only, pH. |
| ColourChange | | Indicators only. |
| IndicatorUse | | Indicators only. |

Material master entries are loaded **inactive**. They become usable when the second person verifies the load (step 9).

### 4.8 MediaProducts

| Column | Required | What to enter |
|---|---|---|
| Code | Yes | Short unique code used in lot numbers, e.g. TSA. |
| Name | Yes | e.g. Tryptic Soy Agar. |

### 4.9 Equipment

| Column | Required | What to enter |
|---|---|---|
| Code | Yes | Asset or equipment ID, e.g. INC-03. |
| Name | Yes | |
| Type | Yes | From the drop-down list. |
| SectionCode | | Lab section that uses it. You must be a member of that section. |
| Location | | |
| SetPointTemperature | | Incubators: °C, dot decimal, e.g. `32.5`. |
| CalibrationDueDate | | `yyyy-MM-dd`. |
| Vendor | | |
| CdsSoftware | | Chromatography and ICP instruments only. |

### 4.10 Items

| Column | Required | What to enter |
|---|---|---|
| Code | Yes | Product or material code, unique. |
| Name | Yes | |
| Category | Yes | `FinishedProduct`, `RawMaterial` or `PackagingMaterial`. |
| SopNumber | | |
| TestCodes | | Tests assigned on receipt, from Test Master, separated by `;`. **Every test you will give a specification for must be listed here.** |

Items are loaded **inactive**. They cannot be received for testing until the second person verifies the load (step 9).

Leave the Specifications and SpecificationStages sheets for step 8.

## 5. Check the file and fix the errors

1. On the Master Data Workbook page, click **Choose file** and select your workbook.
2. Click **Check file**. Nothing is saved: MicroLIMS tries every row and then undoes everything.
3. The report shows, per sheet, how many rows are **New**, how many are **Unchanged** (already in MicroLIMS with the same values), and how many have **Errors**.
4. Below the counts, every problem is listed with its **sheet, row and column**. Fix each one in Excel, save, choose the file again and check again.
5. Repeat until the report shows **0 problems**. **Load and sign** stays disabled until then.

The five most common errors:

| Error message (example) | Cause | Fix |
|---|---|---|
| *No EM department named 'Filling'.* | The row refers to a record that does not exist: a misspelt name, or a record on a sheet that is not filled in. | Correct the spelling to match the other sheet or MicroLIMS exactly, or add the missing record on its sheet. |
| *SetPointTemperature '32,5' uses a comma. Use a dot as the decimal separator, for example 0.5.* | A comma was used as the decimal separator. Excel may do this automatically with some regional settings. | Type `32.5`. If Excel keeps changing it, format the column as Text. |
| *The same record appears more than once on this sheet.* | Two rows have the same key (for example the same item code). Upper and lower case count as the same. | Delete the duplicate row. |
| *Already in MicroLIMS with different values in: Name.* | The record already exists, and your row has different values. The workbook cannot change existing records. | Put the row back to the values in MicroLIMS (download a fresh workbook to see them). Make the change in the record's screen instead (step 10). |
| *Missing column(s): Code.* | A header cell was renamed or deleted. | Restore the header exactly as in a freshly downloaded workbook. |

Other errors you may see:

- *You are not a member of the selected laboratory section.* (column SectionCode): ask a Section Head of that lab to load these rows.
- *Unknown sheet 'Sheet1'.*: remove sheets you added yourself, or rename them back.

## 6. Load and sign

1. When the check shows 0 problems, click **Load and sign**.
2. Enter a comment (for example the source document numbers) and your password, then click **Sign**.
3. MicroLIMS checks every row again. **If any row fails, nothing is loaded**, and the report shows what to fix.
4. When the load succeeds, it appears under **Loads and verification** with the status **Waiting for verification**. The loaded file is kept, and anyone with the permission can download it from the list.

## 7. Create the test definitions and methods

Specifications refer to tests, and some tests refer to methods. Create these in their screens **before** step 8:

| Record | Screen |
|---|---|
| Microbiology tests and their workflow steps | Microbiology Configuration → Test Master |
| Media configurations (incubation per medium) | Microbiology Configuration → Media Configurations |
| Finished product tests | Physicochemical Configuration → FP Test Master |
| Raw material and packaging tests | Physicochemical Configuration → RM & PM Test Master |
| HPLC and GC methods | Physicochemical Configuration → HPLC Methods |
| ICP methods | Physicochemical Configuration → ICP Methods |
| Solution masters | Physicochemical Configuration → Solutions |

Then check that every item has its tests in **TestCodes** (step 4.10).

## 8. Fill Specifications and SpecificationStages, then load again

1. Download a fresh workbook, so it contains the items and tests created so far.
2. Fill the **Specifications** sheet: one row per item, test and reported parameter. If a test needs stages (limit type `MultiStage`), add those on the **SpecificationStages** sheet in the same workbook.
3. Check, fix, load and sign as in steps 5 and 6.

Columns of the Specifications sheet:

| Column | Required | What to enter |
|---|---|---|
| ItemCode | Yes | |
| TestCode | Yes | Code from Test Master. The test must exist and be assigned to the item. |
| ParameterName | Yes | What is reported, e.g. TAMC or Assay of Paracetamol. Unique per item, test and stage. |
| ProductionStageRole | | Blank means the specification applies to every stage. |
| DisplayOrder | | |
| LimitType | Yes | From the drop-down list (see the examples below). |
| LowerLimit, UpperLimit | | Numbers, dot decimal. |
| LowerInclusive, UpperInclusive | | `Yes` or `No`. Default `Yes`. |
| Target, Tolerance, ToleranceMode | | For target ± tolerance limits. |
| AlertLimit, ActionLimit, SpecLimit | | Count-tiered tests: limits as text, e.g. `50`. For other types, SpecLimit is the limit text printed on reports. |
| Unit | | e.g. CFU/g, %. |
| DilutionFactor | | |
| ExpectedResultText | | Qualitative tests: the text that passes. |
| ExpectedState | | Presence/absence tests: `Absence` or `Presence`. |
| ReferenceStandard | | e.g. USP <61>. |
| SampleQuantity, SampleQuantityUnit | | |
| ResultBasis | | e.g. `PercentLabelClaim`, `MgPerUnit`, `Ppm`. |
| LabelClaim, LabelClaimUnit | | |
| DosageForm | | |
| HplcAnalyte | | Method abbreviation / analyte name, e.g. `PCM / Paracetamol`. The method must exist. |
| IcpElement | | Method abbreviation / element symbol, e.g. `ICP1 / Zn`. The method must exist. |

Columns of the SpecificationStages sheet:

| Column | Required | What to enter |
|---|---|---|
| ItemCode, TestCode, ParameterName, ProductionStageRole | Yes (ProductionStageRole optional) | The same values as the specification's row on the Specifications sheet. |
| StageNumber | Yes | 1, 2, 3 … |
| StageLabel | Yes | e.g. S1. |
| AcceptanceCriteria | Yes | The criterion for that stage, as text. |

Stages load **only together with their new specification row** in the same workbook. To change the stages of an existing specification, use the Specifications screen.

### 8.1 Worked examples

These rows were checked with *Check file* on 2026-10-08. Columns not shown are left blank. The test codes are examples; use the codes from your own Test Master.

**Example 1. Count-tiered microbiology limit: TAMC not more than 1000 CFU/g**

| ItemCode | TestCode | ParameterName | LimitType | SpecLimit | Unit | ReferenceStandard |
|---|---|---|---|---|---|---|
| EX-RM-01 | TAMC | Total Aerobic Microbial Count | CountTiered | 1000 | CFU/g | USP <61> |

If the test has alert and action levels, enter them in AlertLimit and ActionLimit as well.

**Example 2. Range assay: 95.0–105.0 % of label claim by HPLC**

| ItemCode | TestCode | ParameterName | LimitType | LowerLimit | UpperLimit | SpecLimit | Unit | ResultBasis | HplcAnalyte |
|---|---|---|---|---|---|---|---|---|---|
| EX-FP-01 | HPLC-APAP | Assay of Acetaminophen | Range | 95.0 | 105.0 | 95.0 - 105.0 | % | PercentLabelClaim | APAP / Acetaminophen |

**Example 3. Presence/absence: *E. coli* absent in 1 g**

| ItemCode | TestCode | ParameterName | LimitType | SpecLimit | Unit | ExpectedState | ReferenceStandard |
|---|---|---|---|---|---|---|---|
| EX-RM-01 | E.coli | Escherichia coli | PresenceAbsence | Absent in 1 g | 1 g | Absence | USP <62> |

**Example 4. Dissolution with S1/S2 stages: Q = 80 %**

Dissolution tests must use limit type **`DissolutionQ`**. Enter Q in LowerLimit. MicroLIMS applies the USP <711> stage criteria (S1, S2, S3) itself, so **do not** add SpecificationStages rows. A dissolution row with any other limit type is refused (*Only DissolutionQ specifications are allowed for Dissolution tests.*).

| ItemCode | TestCode | ParameterName | LimitType | LowerLimit | SpecLimit | Unit | ReferenceStandard | LabelClaim | LabelClaimUnit |
|---|---|---|---|---|---|---|---|---|---|
| EX-FP-01 | DISS-UI | Dissolution | DissolutionQ | 80 | Q = 80 % | % | USP <711> | 500 | mg |

**Example 5. Other multi-stage limits**

Use `MultiStage` for tests whose acceptance criteria are written per stage, other than dissolution. Give the specification row on the Specifications sheet, and one row per stage on SpecificationStages:

| Sheet | ItemCode | TestCode | ParameterName | LimitType / StageNumber | StageLabel | AcceptanceCriteria |
|---|---|---|---|---|---|---|
| Specifications | EX-FP-01 | *your test* | *parameter* | MultiStage | | |
| SpecificationStages | EX-FP-01 | *your test* | *parameter* | 1 | S1 | *criterion for stage 1* |
| SpecificationStages | EX-FP-01 | *your test* | *parameter* | 2 | S2 | *criterion for stage 2* |

## 9. Second person: verify the load

1. Sign in as yourself. You must not be the person who loaded the workbook. If the load added material master entries, you must be a member of their lab sections (or a System Administrator). Otherwise MicroLIMS refuses before you sign.
2. Open **General Laboratory Configuration → Master Data Workbook**. Under **Loads and verification**, find the load marked **Waiting for verification**.
3. Click the file name to download the loaded workbook. Compare **every new row** with the source documents from step 2. *New records* shows how many rows were added.
4. If everything is correct, click **Verify**, enter a comment (for example "checked against SPEC-xxx rev. n") and your password, and click **Sign**. The status becomes **Verified by** *your name*. The items and material master entries from that load become active.
   - If the load added items that have no specification yet, the signature dialog lists them. Make sure that is intended before you sign: verified items can be received for testing.
5. **If something is wrong, do not verify.** Correct the record in its own screen, giving a reason (each change is signed and kept in the audit trail). Then verify the load, mentioning the correction in your comment.

## 10. Changing data later

After a record has been loaded, **always change it in its screen**, never by loading the workbook again. The workbook refuses changed rows.

This is deliberate. In the screens, every change asks for a **reason** and an **electronic signature**, and the audit trail records the old and new values. The workbook is a bulk *add* tool. If it could overwrite records, a change could slip through without its own reason and review.

Use the workbook again only to **add new records**, with the same check, load, verify and approve procedure.
