## 2. Purpose and Scope

### 2.1 Intended Audience and Scope
This guide covers the day-to-day operations of laboratory personnel working within MicroLIMS. It is written for:
- **Laboratory Analysts** executing sample preparations, microbiological tests, instrument runs, volumetric titrations, solution preparations, and inventory updates.
- **Technical Reviewers** conducting secondary verification of test results, calculations, observations, and controlled document drafts.
- **Section Heads** managing laboratory workload distribution, test assignments, operational overrides, sample release authorizations, media and cryovial approvals, and controlled document governance.
- **System Administrators** configuring organizational memberships, managing user accounts, adjusting role permissions, reviewing audit trails, and monitoring system technical incidents.
- **Document Controllers and Quality Personnel** overseeing the lifecycle, periodic review, and training compliance of controlled Standard Operating Procedures and laboratory specifications.

### 2.2 Operational Scope
MicroLIMS manages laboratory operations across two distinct sections:
1. **Microbiology Laboratory (`MICRO`)**: Managing bioburden and count testing, pathogen screening sessions, environmental monitoring (EM), purified and process water sampling, after-cleaning (AC) surface monitoring, media preparation, growth promotion testing (GPT), and reference strain cryovials.
2. **Physicochemical Laboratory (`FP`)**: Managing chemical and physical quality control across two dedicated workspaces:
   - **Finished Product Workspace (`fp`)**: Managing final drug product release testing, assay calculations, dissolution, disintegration, and physical uniformity measurements.
   - **Raw Material & Packaging Material Workspace (`rmpm`)**: Managing incoming active pharmaceutical ingredients, excipients, and primary packaging materials.

### 2.3 Backend Capabilities Not Exposed in the User Interface
Certain system modules and administrative endpoints exist strictly within the application backend or database architecture and do not have dedicated frontend screens. Because these features cannot be accessed directly by end users in the browser, they are not given operational procedures in this guide:
- **Login History (`MOD-AUDIT-002`)**: The backend records every sign-in attempt, success, lockout, and failure (`GET /api/admin/audit/login-history`), but no user interface screen displays this history.
- **Security Audit Events (`MOD-SECAUDIT-001`)**: Security events tracking authentication, session lifecycle, and password modifications are captured in database tables, but the viewing permission is not enforced and no frontend viewer exists.
- **Global Signature Trail (`MOD-SIG-002`)**: An administrative endpoint exists to query electronic signatures (`GET /api/signatures`), but no separate signature query screen is provided in the web interface.
- **Historical Reporting Backfill (`MOD-REPORTS-002`)**: An administrative backfill utility (`POST /api/admin/reporting/backfill`) exists for database maintenance without a user-facing screen.
- **Analyst Availability and KPI Settings (`MOD-KPI-002`)**: Backend configuration routes exist for section availability and target parameters, but are maintained via service interfaces without independent user screens.
- **Document Escalations (`MOD-DC-005`)**: Backend background workers handle overdue training notifications without a dedicated user interface console.
- **Results Query Service (`MOD-RESULT-001`)**: A general result retrieval endpoint (`GET /api/results`) exists in the backend without a separate standalone search page.
- **Instrument Report Parsers (`MOD-INSTIMPORT-001`)**: File parsers and OCR utilities exist in the application code but are not registered to any user interface screen or live instrument feed.

### 2.4 Features Not Implemented in the Software
Based on verification of the application codebase, the following functions and modules are not implemented in MicroLIMS. No procedures are provided for them in this user guide:
- MicroLIMS does not have a separate Final Release step or permission; sample authorization by the Section Head serves as the release decision.
- MicroLIMS does not have a distinct sterility test module or result entry screen.
- MicroLIMS does not have an environmental monitoring (EM) annual plan or automated sampling schedule generator.
- MicroLIMS does not have water sampling frequencies or automated water sampling plans.
- MicroLIMS does not have reference-strain passage tracking.
- MicroLIMS does not have equipment qualification records (IQ, OQ, or PQ).
- MicroLIMS does not have a laboratory incident, deviation, or CAPA module; the Error Monitoring screen records only technical system errors.
- MicroLIMS does not have a separate standalone Titration Methods master page; titration settings are configured inside Test Master.
- MicroLIMS does not have an integration with SAP or any other enterprise resource planning (ERP) system.
- MicroLIMS does not have a local AI assistant.
- MicroLIMS does not have live automated instrument data feeds; instrument results are entered manually with supporting evidence file attachments.
- MicroLIMS does not have dedicated Reception or QA role types; receiving is performed by laboratory analysts, reviewers, or Section Heads holding receiving permissions.
