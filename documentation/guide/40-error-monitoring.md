## 40. Error Monitoring (Incidents and Error Log)

### 40.1 Technical Incident Telemetry Architecture

#### Purpose
The Error Monitoring module provides technical diagnostics and runtime exception telemetry for system administrators and IT support personnel. 

#### Operational Boundary: Technical Log, Not GxP Quality Event Management
MicroLIMS explicitly distinguishes technical system telemetry from formal pharmaceutical quality records:
- **Technical Log Only**: Error Monitoring captures diagnostic stack traces, HTTP errors, browser rendering exceptions, and database connection timeouts. As stated directly on the screen:
  > "Technical incidents captured from the application, the API and the database. Operational data only - this is not part of the GxP record."
- **Not a Deviation or CAPA Module**: Error Monitoring is not a quality incident, deviation, non-conformance, or Corrective and Preventive Action (CAPA) tracking module. Out-of-specification analytical investigations are managed separately in OOS Tracking (see §20), while formal deviations must be logged in organizational quality management systems.
- **Audit Exclusion**: In accordance with system audit capture rules, technical incident and error log tables are excluded from the immutable audit trail. Resolving, reopening, or modifying incident severity does not create audit trail rows; resolution history is maintained directly on the incident record.

#### Capture Scope and Exception Mapping
The system automatically groups runtime exceptions into incidents sharing a common Correlation ID:
- **Unhandled Server Exceptions**: Internal server failures (HTTP 500) return `"An unexpected error occurred."` and are recorded with Source **Backend** and severity **Critical**.
- **Business Rule Rejections**: Validation failures and invalid operations return HTTP 400 with the specific business rule message and are recorded with Source **Backend** and severity **Warning**.
- **Authorization Refusals**: Unauthorized access attempts return HTTP 403 and are captured with severity **Warning**.
- **Data Concurrency Conflicts**: Concurrency collisions return HTTP 409 (`"This record was changed by someone else while you were working on it. Nothing was saved - reload it and make your change again."`) and are captured with severity **Warning**.
- **Database Failures**: Database connection errors and query timeouts are captured with Source **Database**.
- **Frontend Client Errors**: Browser script crashes and user interface rendering failures are submitted via client telemetry and recorded with Source **Frontend** (severity **Critical** for full page render crashes; **Error** for component errors).

---

### 40.2 Reviewing, Triaging and Resolving Technical Incidents

<!-- WF-ERR-001 -->
#### Purpose
Inspect captured system exceptions, analyze diagnostic payloads, assign manual severity overrides, record resolution notes, and export incident datasets.

#### Who can use it
Requires the *View error log* permission (`System.ViewErrorLog`, by default: System Administrator).

#### Before you start
- Ensure you have the correlation ID or time range of the reported system issue.

#### What you see
[SCREENSHOT SS-401]
The **Error Monitoring** screen (`/error-monitoring`) presents:
- Heading: **Error Monitoring**
- Subtitle: "Technical incidents captured from the application, the API and the database. Operational data only - this is not part of the GxP record."
- Filter bar:
  - **Severity** dropdown (**All**, **Info**, **Warning**, **Error**, **Critical**)
  - **Source** dropdown (**All**, **Backend**, **Frontend**, **Database**)
  - **Status** dropdown (**All**, **Open**, **Resolved**)
  - Date inputs: **Active from** and **Active to** (applied to the Last Seen timestamp)
  - Search input: "Search summary or correlation id"
  - Action buttons: **Reset** and **Export CSV** (displays **Exporting...** during generation)
- Incidents table:
  - Columns: **Severity**, **Summary**, **Sources**, **Occurrences**, **First seen**, **Last seen**, and **Status**
  - Status indicators: **Open** (warning, filled chip) and **Resolved** (success, outlined chip)
- Expanded row detail:
  - Displays each child error entry: exception type, error message, full stack trace, request path, HTTP method, response status code, acting user ID, and timestamp.
  - Severity dropdown: **Override** (options: **Info**, **Warning**, **Error**, **Critical**, and **Auto ({severity})** to restore automatic computation).
  - Chip: `overridden from {severity}`
  - Text input: **Resolution notes**
  - Action buttons: **Mark resolved** and **Reopen**
  - Resolution audit stamp: `Resolved {time} by {user} - {notes}`

#### How to use it
1. Navigate to **System** > **Error Monitoring**.
2. Apply filter criteria to isolate relevant events (e.g., select **Severity** as **Critical** or search for a specific **Correlation ID**).
3. Click on an incident row to expand its diagnostic details.
4. Review the captured stack trace, request path, and exception message to diagnose the root cause.
5. If the automatic severity rating is inaccurate, select an alternative level from the **Override** dropdown.
6. Once the issue has been addressed, enter diagnostic findings into the **Resolution notes** field.
7. Click **Mark resolved**.
8. To download incident records for technical analysis, click **Export CSV**.

#### System behavior
- Incidents group individual error occurrences under a shared correlation ID.
- An incident's overall severity is automatically computed as the highest severity among its child entries (including active overrides).
- If a new error occurs with the same correlation ID as a previously resolved incident, the system automatically transitions the incident from **Resolved** back to **Open**.
- The CSV export downloads up to 5,000 incident rows matching the active filter criteria.

#### Status
- Statuses: **Open** and **Resolved**.

#### Important rules
- Resolving an incident does not delete error entries; diagnostic entries remain visible in the expanded row history.
- The date filters (**Active from** and **Active to**) evaluate the incident's **Last seen** timestamp.

#### Errors
- If incident loading fails: `"Could not load incidents."`
- If export generation fails: `"The export could not be generated."`
- If the incident no longer exists: `"Incident not found."`

#### What happens next
The incident table updates to reflect the resolved status, and support personnel can reference resolution notes for future occurrences.

> **Note for reviewers:** Error Monitoring is a technical log, not a deviation or investigation record; resolving an incident is not audited. (GAP-110)

> **Note for reviewers:** Business-rule rejections (HTTP 400) and authorization refusals (HTTP 403) are recorded as error incidents, so the list may contain routine operational refusals. (GAP-110)
