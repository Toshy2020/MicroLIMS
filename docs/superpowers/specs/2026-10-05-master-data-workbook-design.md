# Master Data Workbook — Design

Date: 2026-10-05. Status: draft for approval.

## Goal

A lab setting up a new MicroLIMS deployment loads its flat master data and its
specifications from one Excel workbook instead of typing every record into the
screens. The load is checked before anything is saved, signed, audited and kept
as evidence, and a second person verifies it. The same workbook format exports
the current data, so one approved workbook becomes the Configuration
Specification (ML-SYS-CS-001) and moves configuration from VAL to PROD.

## Scope

**In (first slice):** the flat lists and specifications.

| Sheet (load order) | Creates | Existing service called |
|---|---|---|
| 1 Organisms | `Organism` | `OrganismMasterDataService.CreateOrganismAsync` |
| 2 ProductionStages | `ProductionStage` | `ReferenceListMasterDataService.CreateProductionStageAsync` |
| 3 EmDepartments | `Department` | `EnvironmentalMonitoringMasterDataService.CreateDepartmentAsync` |
| 4 EmRooms | `Room` | `...CreateRoomAsync` (department by name) |
| 5 WaterDepartments | `WaterDepartment` | `WaterMasterDataService.CreateWaterDepartmentAsync` |
| 6 WaterPoints | `WaterSamplingPoint` | `...CreateWaterSamplingPointAsync` (test codes `;`-separated) |
| 7 MaterialMaster | `MaterialMasterEntry` | `MaterialMasterService.CreateAsync` (section by code) |
| 8 MediaProducts | `MediaProduct` | `MediaProductService.CreateAsync` |
| 9 Equipment | `Equipment` | `EquipmentMasterDataService.CreateEquipmentAsync` (section by code) |
| 10 Items | `Item` + assigned tests | `ItemService.CreateAsync` (tests `;`-separated) |
| 11 Specifications | `Specification` | `SpecificationMasterDataService.CreateSpecificationAsync` (item by code) |
| 12 SpecificationStages | `SpecificationStage` | attached to the sheet-11 row with the same item code + test code + parameter |

**Out (stay in the screens for now):** test definitions and workflow steps,
media configurations and incubation conditions, HPLC/GC/ICP methods, solution
masters, users. Specifications that need an HPLC analyte or ICP element name it
(`MethodAbbreviation / Analyte`); that method must already exist.

So the lab's order is: workbook sheets 1–10 → test definitions and methods in
the screens → workbook sheets 11–12. One workbook can be uploaded more than
once; rows already loaded are reported as "unchanged".

## Key decisions

1. **Reuse the existing create services, inside one transaction.** No
   import-only rules. The import opens a transaction, calls each row's create
   service in sheet order, and either rolls back (dry run) or commits. A row
   that a service rejects records the service's message against that sheet and
   row; the load continues to collect every error, then rolls back. The data
   layer already joins an outer transaction (`MicroLimsDbContext.SaveChangesAsync`,
   `UnitOfWork`), and audit rows are written inside it.
2. **References by code or name, never by database id.** Item code, test code,
   section code, department name, `Method / Analyte`. Resolved after earlier
   sheets have been created in the same transaction, so a workbook can
   reference rows it creates itself.
3. **Create only.** A row whose key (code, or name where there is no code)
   already exists is "unchanged" if every column matches, otherwise an error:
   "exists with different values — change it in the screens". Changing data
   already in use needs reasons and signatures that the screens already ask for.
4. **XLSX via ClosedXML** (MIT). The template has one sheet per type, a
   header row, drop-down lists for enum columns and for codes already in the
   database, and an Instructions sheet. Enum values are written by name.
5. **Evidence.** A commit needs an e-signature (`MasterDataChanged`). It writes
   a `MasterDataImport` record: file name, SHA-256 of the file, uploaded by,
   time, counts created/unchanged per sheet, and status. The uploaded file is
   kept in file storage (B2 in production). Every created row also gets its
   normal audit-trail entry.
6. **Second-person verification, enforced through activation.** Items and
   material master entries are created **inactive**. A different user, after
   checking the import, signs `Reviewed` on the import, and that activates
   them. Samples cannot be received against an inactive item, so its
   specifications cannot be used before verification. The same user who
   committed cannot verify (`SegregationOfDutiesGuard`). Organisms, rooms,
   departments, water points, media products, stages and equipment are
   active at once; they are only used through items, tests or later screens.
7. **Export.** "Download current data" writes the same workbook from the
   database. Its sheets 1–12 round-trip: re-uploading an export gives all
   "unchanged".
8. **Permission.** `MasterData.Manage` for template, export, dry run and
   commit; verification also needs `MasterData.Manage` and a different user.

## API

| Method | Route | Does |
|---|---|---|
| GET | `api/masterdata/workbook/template` | Blank workbook with drop-downs |
| GET | `api/masterdata/workbook/export` | Current data in the same format |
| POST | `api/masterdata/workbook/validate` | Multipart file → dry run report (nothing saved) |
| POST | `api/masterdata/workbook/import` | File + password + comment → commit, returns import record |
| GET | `api/masterdata/workbook/imports` | Import history |
| POST | `api/masterdata/workbook/imports/{id}/verify` | Password + comment → second-person verification, activates items |
| GET | `api/masterdata/workbook/imports/{id}/file` | Download the uploaded file |

Report shape: per sheet `{ sheet, created, unchanged, errors: [{ row, column?, message }] }`.
The commit re-runs the dry run first and refuses if there are any errors.

## Frontend

One page, **Laboratory Configuration → Master Data Workbook**:

1. Buttons: download template, download current data.
2. Upload → shows the dry-run report: a count row per sheet and an error table
   (sheet, row, column, message). "Load" is enabled only with zero errors and
   opens the existing signature dialog.
3. Import history table: date, user, file, counts, status (Loaded / Verified),
   download file, and "Verify" (signature dialog) for other users.

## Limits

- File size 10 MB, at most 5,000 data rows per upload (ponytail: whole import
  in one request and one transaction; split the workbook if a lab exceeds it).
- Rows are read as text and parsed with invariant culture; decimals typed with
  a comma are rejected with a clear message rather than guessed.

## Testing

- Unit: workbook parsing (headers, enum names, `;` lists, blank rows, bad
  decimals), reference resolution, unchanged/conflict detection.
- PostgreSQL integration: dry run leaves no rows and no audit rows; commit
  creates rows + audit + signature + import record; a failing row rolls the
  whole import back; export → re-import gives all unchanged; verify by the same
  user is refused, by another user activates items.
- Frontend: report rendering and Load enabled only with zero errors.

## Phases (stop after each for review)

1. **Backend read side:** ClosedXML, template + export for sheets 1–12, parser
   with unit tests.
2. **Backend write side:** dry run + commit + `MasterDataImport` entity and
   migration + signature + file storage + verify; integration tests.
3. **Frontend page** + browser check with a real workbook.
4. **Validation tie-in:** add the workbook procedure to the Configuration
   Specification and IQ/PQ outline.
