## 1. Introduction

### 1.1 About This User Guide
This document is an end-user guide for the MicroLIMS laboratory information management system. It provides instructions and operational reference information for laboratory analysts, technical reviewers, Section Heads, system administrators, Quality Assurance personnel, and Document Control users.

> **Important Notice:** This user guide is an AI-assisted draft describing the software as implemented. The application was not run to prepare it, and it has not been formally verified against a live deployment. It requires thorough human review and operational verification before regulatory or operational adoption.

MicroLIMS is designed to support laboratory workflows by recording sample information, analytical measurements, test observations, electronic signatures, and changes to records in an audit trail. This guide describes the behavior, screen controls, permissions, and validation messages of the application. No statement in this guide is a claim of regulatory compliance or formal system validation.

### 1.2 System Purpose and Core Functions
MicroLIMS is a web-based laboratory application used in two laboratories:
- The **Microbiology Laboratory**.
- The **Physicochemical Laboratory**, which has two areas: **FP** (finished product) and **RM & PM** (raw material and packaging material).

Within these laboratories, the system provides the following capabilities:
- **Receive and register samples**: finished products, raw materials, packaging materials, water, environmental monitoring, and after-cleaning samples. The system creates the test orders configured for the item automatically after the sample is received.
- **Run laboratory testing workflows**: microbiology count tests, qualitative observations, pathogen tests, environmental and water tests, and physicochemical tests (HPLC, GC, ICP-OES, titration, dissolution, disintegration, weight variation, measurement, gravimetric, and qualitative tests).
- **Review and approve results**: an analyst enters results, a reviewer performs a technical review, and a Section Head records the release decision, with checks that stop a person from reviewing or approving work they did themselves.
- **Prepare and track materials**: media lots, reference strain cryovials, reagents and materials, equipment, solutions, and working standards.
- **Maintain master data**: items, tests, specifications, organisms, media, equipment, methods, and solutions, and part of this data can be loaded from a workbook.
- **Control documents**: controlled documents, revisions, review, approval, periodic review, and reading and training records in Document Control.
- **Report and monitor**: reports and trend pages, KPIs, out-of-specification (OOS) tracking, the audit trail, and a technical Error Monitoring page.
- **Collaborate**: direct and group messages and discussions.

Many sensitive actions ask the user to type their login password again as an electronic signature. The system records the user, role, time, and meaning of the signature with the action. Not every action is signed: for example, microbiology plate-count entry and pathogen test steps are not.

> **Note for reviewers:** Whether the microbiology count entry and pathogen test steps should carry an electronic signature has not been confirmed. (GAP-010)

<!-- Review log (Sonnet): 7 corrections; invented items removed: "immutable" audit trail wording, laboratory/area code names, claim that every sensitive decision requires re-authentication; callouts added: GAP-010 -->
