# FS-<AREA> Functional Specification (template)

| Field | Value |
|---|---|
| Document number | FS-<AREA> |
| Title | <area name> functional specification |
| Version | 0.1 |
| Status | Draft |
| Author | <name> |
| Reviewer | <name> |
| Approver | <name, set by QA> |
| Effective date | <set when Approved> |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | <date> | <name> | First draft, written from the code as built. |

## Functions

Use one entry per endpoint group or rule. Name the service and method that the code uses. Each entry is a level-3 heading with the ID, then these fields in this order.

### FS-MIC-001
**Satisfies:** URS-MIC-001, URS-MIC-004
**Function:** inputs → rule → result; who may do it; what is audited.
**Code:** `backend/MicroLIMS.Application/Workflows/TestWorkflow/IncubationStepRecorder.cs` — `RecordCountTestAsync`
**Verified by:** reading (not run)
**Conflicts:** CONF-003
