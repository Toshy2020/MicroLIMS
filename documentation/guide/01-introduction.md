## 1. Introduction

### 1.1 About This User Guide
This document is an end-user guide for the MicroLIMS laboratory information management system. It provides instructions and operational reference information for laboratory analysts, technical reviewers, Section Heads, laboratory administrators, Quality Assurance personnel, and Document Control specialists.

> **Important Notice:** This user guide is an AI-assisted draft describing the software as implemented in the codebase. It has not been formally validated against a live production deployment and requires thorough human review and operational verification before regulatory or operational adoption.

MicroLIMS is designed to support laboratory workflows by recording sample metadata, analytical measurements, test observations, electronic signatures, and lifecycle changes directly in an immutable audit trail. This guide describes observed system behaviors, user interface controls, permissions, and validation rules as implemented in the application source code. No statement in this guide constitutes a claim of regulatory compliance or formal system validation.

### 1.2 System Purpose and Core Functions
MicroLIMS is a web-based laboratory application built to manage testing operations across two distinct laboratory environments:
- **Microbiology Laboratory** (laboratory code `MICRO`)
- **Physicochemical Laboratory** (laboratory code `FP`), comprising the **Finished Product** area (`fp`) and the **Raw Material & Packaging Material** area (`rmpm`).

Within these operational areas, the system provides capabilities to:
- **Receive and register samples** across pharmaceutical finished products, raw materials, packaging materials, purified and process water, environmental monitoring locations, and after-cleaning equipment surfaces. Test orders configured for each item in the system masters are generated automatically upon sample registration.
- **Execute laboratory testing workflows**, including quantitative plate counts, qualitative microbial observations, structured pathogen test sessions, and physicochemical analyses such as HPLC, GC, ICP-OES spectroscopy, volumetric titrations, dissolution, disintegration, and physical measurements.
- **Perform secondary technical reviews and release decisions**, ensuring segregation between testing personnel, reviewing analysts, and approving Section Heads.
- **Manage laboratory materials and inventory**, tracking prepared media lots, reference strain cryovials, chemical reagents, volumetric solutions, reference standards, and laboratory equipment.
- **Maintain laboratory master data**, including items, test methods, specifications, microbial organisms, incubation conditions, and configuration tables.
- **Govern controlled documents**, providing full revision lifecycles, automated numbering, periodic review tracking, and read-and-understand training records.
- **Search, trend, and monitor quality metrics**, including out-of-specification (OOS) investigations, historical result trending, key performance indicators (KPIs), immutable audit trails, and technical error monitoring.

Every sensitive operational decision and quality status change in MicroLIMS requires the user to re-authenticate by entering their password. The system records the user identity, role, timestamp, IP address, and specific meaning statement in the electronic signature record and audit trail.
