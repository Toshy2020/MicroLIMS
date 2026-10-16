## 35. Audit Trail

### 35.1 Audit Trail Architecture and Immutability

#### Purpose
MicroLIMS maintains an immutable, comprehensive record of all critical database transactions, master data adjustments, testing modifications, and administrative events across the laboratory.

#### Scope of Automatic Audit Capture
The system automatically captures every database addition, modification, and deletion at the moment changes are saved:
- **Action Type**: Records whether an entity was created (**Create**), modified (**Update**), or deleted (**Delete**).
- **Previous and New Values**: Stores complete JSON serialized representations of property values before and after each change.
- **Actor and Time**: Records the acting user's identifier and the precise UTC timestamp of the transaction.
- **Laboratory Identifiers**: Automatically indexes associated sample references, batch numbers, control numbers, media lot numbers, sample identifiers, and test order identifiers whenever present on the record.

#### Excluded Entity Types
To prevent circular logging and preserve system performance, nine technical entity types are excluded from the primary audit trail:
- Audit log tables (`AuditLog`, `AuditEventChange`)
- Document access logs (`MaterialDocumentAccessLog`, `EquipmentDocumentAccessLog`)
- Technical telemetry (`Incident`, `ErrorLog`)
- Session management artifacts (`RefreshToken`, `NotificationLog`, `SecurityAuditEvent`)

#### Immutability Controls
Audit records are protected against tampering or alteration. Database triggers enforce immutability by refusing all database `UPDATE` and `DELETE` operations on audit log and electronic signature tables.

#### Backend-Only Audit Logs
- **Login History**: Records all user authentication attempts (successful sign-in, unknown username, locked account, wrong password, inactive account) with client IP addresses. This endpoint is not exposed in the user interface.
- **Security Audit Events**: Captures 19 distinct security event codes covering session management, token lifecycles, and account modifications. This log is not exposed in the user interface.
- **Export Control**: The Audit Trail user interface does not provide an export or print function.

---

### 35.2 Searching the Audit Trail

<!-- WF-AUDIT-001 -->
#### Purpose
Search and review historical audit records using date ranges, action categories, entity types, or laboratory identifiers.

#### Who can use it
Requires the *View audit trail* permission (`Audit.View`, by default: Section Head, System Administrator).

#### Before you start
- Ensure you have the relevant record identifiers (such as a Sample Reference or Batch Number) if searching for specific laboratory events.

#### What you see
[SCREENSHOT SS-351]
The **Audit Trail** screen (`/audit-search`) presents:
- Heading: **Audit Trail**
- Subtitle: "Search and trace immutable change events, actor identities, and laboratory records across MicroLIMS."
- Filter bar:
  - **From Date** and **To Date**
  - **Action** dropdown with options: **All Actions**, **Create**, **Update**, **Delete**
  - **Entity Category** dropdown listing all tracked domain entities
  - Collapsible toggle: **More Laboratory Identifiers (Batch, Sample, Media...)** / **Hide Laboratory Identifiers** containing:
    - **Sample Reference**
    - **Batch Number**
    - **Control Number**
    - **Media Lot Number**
    - **Cryovial / RS Code**
    - **User ID**
  - Action buttons: **Reset** and **Search Audit Trail**
- Results table:
  - Header: **Audit Events (N)** (displays `", capped at 300 latest"` when 300 or more records match)
  - Columns: **Date / Time**, **User**, **Entity**, **Action**, **Changed Fields (Diff)**, **Record Ref**, and **Details**
  - Row action button: **Inspect Event & Traceability Chain**

#### How to use it
1. Navigate to **Audit & Compliance** > **Audit Search**.
2. Select desired search filters:
   - Enter **From Date** and **To Date** to specify a time window.
   - Choose an **Action** or **Entity Category** to narrow by transaction type.
   - Click **More Laboratory Identifiers (Batch, Sample, Media...)** to enter specific tracking numbers such as **Sample Reference** (e.g., "SMP-0001") or **Batch Number** (e.g., "Lot L-0001").
3. Click **Search Audit Trail** (or press Enter).
4. Review matching records in the results table.
5. Click **Reset** to clear all filter criteria.

#### System behavior
- The server retrieves matching audit events ordered with the newest records first.
- Search queries return up to 300 results. If more than 300 events match, the results header displays `", capped at 300 latest"`.

#### Validation
- Date filters validate that the entered dates represent valid calendar values.

#### Status
- The audit search interface is strictly read-only.

#### Important rules
- The **Action** filter offers only **Create**, **Update**, and **Delete**. Platform action codes written by system services (such as `USER_ROLE_CHANGED`, `USER_DISABLED`, `ROLE_PERMISSIONS_UPDATED`, or `ADMIN_PASSWORD_RECOVERY_USED`) cannot be selected directly in the Action filter dropdown.

#### Errors
- If retrieving audit events fails: `"Failed to fetch audit records from the server."`
- If no records match: `"No audit events found"`

#### What happens next
Users can inspect any event in detail to examine modified fields or trace linked records.

---

### 35.3 Inspecting Event Details and Entity History

#### Purpose
Examine property-level before-and-after differences, view raw event payloads, and access complete un-capped lifecycles for a selected entity.

#### Who can use it
Requires the *View audit trail* permission (`Audit.View`, by default: Section Head, System Administrator).

#### Before you start
- Perform an audit search as described in §35.2 and locate the target event row.

#### What you see
[SCREENSHOT SS-352]
Clicking **Inspect Event & Traceability Chain** opens the audit event details drawer containing:
- Event action chip (**CREATE**, **UPDATE**, or **DELETE**)
- Formatted diff viewer highlighting changed field names, previous values, and new values
- Raw JSON viewer displaying complete before-and-after payloads
- The traceability chain associated with the record (see §36)
- Action button: **View Complete History for {entityName} #{entityId}**

#### How to use it
1. In the audit results table, click **Inspect Event & Traceability Chain** on the desired row.
2. Review the diff viewer to verify which specific fields were modified during the transaction.
3. To inspect the entity across its entire lifecycle without the 300-row search cap, click **View Complete History for {entityName} #{entityId}**.
4. Close the drawer using the close button.

#### System behavior
- The drawer loads property diffs and raw event payloads for the selected event.
- Clicking **View Complete History** initiates a dedicated entity history query that retrieves all audit rows for that specific entity identifier without search limits.

#### Important rules
- Audit records cannot be deleted, cleared, or archived from the user interface.

> **Note for reviewers:** Audit Trail shows 300 events at most, has no export, cannot filter on user/role actions, and does not show login history or security events. (GAP-030, GAP-101)

> **Note for reviewers:** Some records are not audited (incidents, error log entries, refresh tokens, notification state). (GAP-111)

> **Note for reviewers:** The role-change audit entry shows the new role id as the previous value. (GAP-112)
