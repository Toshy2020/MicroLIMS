# Configuration Specification — MicroLIMS

| Field | Value |
|---|---|
| **Document No.** | ML-SYS-CS-001 |
| **Version** | 0.1 — Draft for review |
| **Related documents** | ML-SYS-VP-001 Validation Plan (deliverable 8); ML-SYS-SD-001 System Description; ML-SOP-MDW-001 Master Data Workbook User Guide |
| **Source** | Written from the source code at branch `feat/master-data-workbook` (2026-10-08). Re-confirm against the frozen baseline before approval. |

---

## 1. Purpose and scope

This document defines the configuration of **one MicroLIMS deployment**. It has two parts:

1. **Environment configuration**: the settings of the hosting platforms and the API (ML-SYS-SD-001 §3.1 and §8).
2. **Lab master data**: the reference records the laboratories work with (organisms, rooms, water points, media products, equipment, items, specifications and so on).

The Installation Qualification (ML-SYS-IQ-001) verifies the installed environment against this document. The Performance Qualification (ML-SYS-PQ-001) runs on the lab master data defined here.

Out of scope: user accounts and their roles and section memberships. These are set up under the user management SOP and verified in IQ.

## 2. Environment configuration

### 2.1 Non-secret settings

Set as environment variables on the API host (Render). A double underscore (`__`) in a variable name stands for `:` in the setting name.

| Setting | Required PROD value | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | |
| `Storage:Provider` | `S3` | The default `Local` writes into the container, and files are lost on redeploy. |
| `Storage:S3:ServiceUrl` | Backblaze B2 endpoint, with `https://` | |
| `Storage:S3:Region` | Region part of the endpoint | |
| `Storage:S3:BucketName` | The private PROD bucket | Bucket private, encryption on, versioning kept (ML-SYS-SD-001 §3.1) |
| `Lab:TimeZoneId` | `Africa/Cairo` | Drives due dates and incubation windows. Record the value used: ______ |
| `Jwt:AccessTokenMinutes` | `15` | Allowed range 1–480 |
| `Frontend:Origin` | The PROD frontend URL only | Allowed CORS origins. No `localhost` in PROD. |
| `ForwardedHeaders:KnownNetworks` | CIDR of the hosting proxy | Needed so the audit trail and signatures record the real client IP address |
| `RateLimiting:Authentication:PermitLimit` | `20` (default) | Sign-in attempts per client per window |
| `MaterialDocuments:MaxFileSizeBytes` | 25 MB (default) | |
| `ErrorMonitoring:*` | Defaults from `appsettings.json` | Error-log retention only |
| `APPLY_MIGRATIONS` | `true` for the first start-up, then `false` | Database migrations run at start-up while `true` (DEPLOYMENT.md, step 2) |
| `Smtp:Host`, `Smtp:Port`, `Smtp:Username`, `Smtp:FromAddress`, `Smtp:EnableSsl` | As agreed with IT, or not set | Optional. When `Smtp:Host` is empty, password-reset and alert e-mails are not sent. |

### 2.2 Secrets

Secrets are set only in the hosting provider's environment settings. They are never stored in Git, in this document or in test evidence. IQ records only that each one **is set**.

| Secret | Purpose | Held by |
|---|---|---|
| `ConnectionStrings:Default` | Neon PostgreSQL connection string | System Administrator |
| `Jwt:Key` | Token signing key, at least 32 random characters | System Administrator |
| `Storage:S3:AccessKeyId`, `Storage:S3:SecretAccessKey` | Backblaze B2 application key | System Administrator |
| `Smtp:Password` | SMTP password or API key (if SMTP is used) | System Administrator |
| `Seed:InitialAdminPassword` | Password of the first `admin` account. First deployment only; must be changed at first sign-in. | System Owner |

## 3. Lab master data: what defines it

### 3.1 Records loaded by the master data workbook

The **approved master data workbook** is the specification for these 12 sheets:

| # | Sheet | Records |
|---|---|---|
| 1 | Organisms | Organisms (with ATCC numbers) |
| 2 | ProductionStages | Production stages offered on the receiving form, with their role |
| 3 | EmDepartments | Departments monitored by environmental monitoring |
| 4 | EmRooms | EM rooms per department |
| 5 | WaterDepartments | Water systems or areas |
| 6 | WaterPoints | Water sampling points and their tests |
| 7 | MaterialMaster | Reagents, standards, indicators and other materials, per lab section |
| 8 | MediaProducts | Culture media products (the code used in lot numbers) |
| 9 | Equipment | Equipment, with set-point temperature and calibration due date |
| 10 | Items | Products and materials received for testing, with the tests assigned on receipt |
| 11 | Specifications | Specification limits per item, test, parameter and stage |
| 12 | SpecificationStages | Stages of multi-stage specifications |

The workbook is identified by its **file name and SHA-256 hash**. Each load creates an import record in MicroLIMS (General Laboratory Configuration → Master Data Workbook, *Loads and verification*; table `MasterDataImports`). The import record keeps the hash, a copy of the file, the rows created, who loaded it and who verified it. **The hash on the import record must equal the approved hash recorded in §8.**

The workbook only **adds** records. Records that are already in MicroLIMS with the same values are reported as *unchanged*. A row that differs from an existing record is refused, because changes need a reason and a signature in the record's own screen.

### 3.2 Records configured in their screens

The following records are not in the workbook. Each is specified by a printout or export of its screen, approved together with the workbook.

| Record type | Screen (menu) | Owner |
|---|---|---|
| Microbiology test definitions and their workflow steps | Microbiology Configuration → Test Master | Process Owner — Microbiology |
| Finished product test definitions | Physicochemical Configuration → FP Test Master | Process Owner — Physicochemical |
| Raw material and packaging test definitions | Physicochemical Configuration → RM & PM Test Master | Process Owner — Physicochemical |
| Media configurations (incubation time and temperatures per medium) | Microbiology Configuration → Media Configurations | Process Owner — Microbiology |
| HPLC and GC methods (analytes, system suitability) | Physicochemical Configuration → HPLC Methods | Process Owner — Physicochemical |
| ICP methods (elements, conversion factors) | Physicochemical Configuration → ICP Methods | Process Owner — Physicochemical |
| Solution masters (volumetric solutions, titrants) | Physicochemical Configuration → Solutions | Process Owner — Physicochemical |
| Receiving reference lists | General Laboratory Configuration → Receiving Configuration | System Owner |

## 4. Procedure

The step-by-step instructions for lab users are in ML-SOP-MDW-001. In outline:

1. **Prepare** the workbook from the approved source documents: specifications, SOPs, the equipment list, the media list, and the EM and water sampling plans. Download the workbook from MicroLIMS and add the new rows to it.
2. **Check** the file in MicroLIMS (*Check file*). This is a dry run: everything a load would do is done and then rolled back. Fix every error listed.
3. **Load and sign.** A user with the Master Data Manage permission (Section Head or System Administrator) loads the workbook and signs (signature meaning: *master data changed*). If any row has an error, nothing is loaded. Items and material master entries are created **inactive**.
4. **Verify.** A **second person** with the same permission compares the load with the source documents and signs *Verify* (signature meaning: *reviewed*). This activates the items and material master entries. The system refuses a verification by the person who loaded the workbook. It also refuses, before signing, a verifier who is not a member of a lab section the load added material master entries to.
5. **Approve.** The Process Owner(s) and QA approve the workbook. Its file name and SHA-256 hash are recorded in §8.

Test definitions and methods (§3.2) must exist before the Specifications sheet can be loaded. The workbook is therefore usually loaded twice: sheets 1–10 first, then the specifications.

## 5. Moving configuration from VAL to PROD

1. In VAL, a **System Administrator** downloads the master data workbook once the configuration there is approved. Only a System Administrator's download contains every lab section's material master entries; a Section Head's download contains only their own sections. This file is the approved workbook. Record its hash in §8.
2. In PROD, configure the screen-based records of §3.2 the same way as in VAL, from the approved printouts.
3. In PROD, run *Check file* with the approved workbook. **Attach the report showing 0 errors** as IQ evidence.
4. A System Administrator loads and signs. A second person verifies, as in §4: a second System Administrator, or a Section Head who is a member of every section the load added material master entries to.
5. **Acceptance:** the hash on the PROD import record equals the approved hash recorded in §8. Any difference is a deviation.

Do not edit the approved workbook between VAL and PROD. Any change makes a new version, which needs approval again.

## 6. Verification evidence for IQ / PQ

For each load, record:

| Evidence | Where it comes from | Acceptance |
|---|---|---|
| File name and SHA-256 hash | Import record | Equal to §8 |
| Rows created per sheet | Import record (*New records*), compared with the rows added to the workbook | Equal |
| Load signature: who, when, meaning | Import record / audit trail | Signed by an authorised user |
| Verification signature: who, when, meaning | Import record / audit trail | A different authorised user |
| Spot check against the source documents | Tester's worksheet | No discrepancy |

**Spot-check size.** The risk assessment (ML-SYS-RA-001) sets the number of records checked per sheet:

- **Specifications and SpecificationStages: every record.** Specification evaluation is pre-classified High risk (ML-SYS-VP-001 §6).
- **All other sheets: 10 % of the records, and at least 5** (or all of them, if the sheet has fewer than 5).

For screen-based records (§3.2), compare the screen printout with the source document, record by record.

## 7. Changes after go-live

After go-live, master data is changed **in its screens**, under change control: every change needs a reason and an electronic signature, and appears in the audit trail. The workbook is used only to **add** new records, under the same check, load, verify and approve procedure. Every load is listed with its file on the Master Data Workbook page.

## 8. Approved workbook register

| Version | File name | SHA-256 | Approved by (Process Owner / QA) | Date | Loaded in PROD (import record no.) |
|---|---|---|---|---|---|
| | | | | | |

## 9. Approvals

| Role | Name | Signature | Date |
|---|---|---|---|
| Author (Developer / SME) | | | |
| System Owner | | | |
| Process Owner — Microbiology | | | |
| Process Owner — Physicochemical | | | |
| Quality Assurance | | | |
