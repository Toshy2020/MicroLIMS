# FS-OOS Functional Specification

| Field | Value |
|---|---|
| Document number | FS-OOS |
| Title | Out-of-specification tracking functional specification |
| Version | 0.1 |
| Status | Draft |
| Author | Claude (AI agent), draft from code |
| Reviewer | To be assigned by QA |
| Approver | To be assigned by QA |
| Effective date | Set when Approved |

## Change history

| Version | Date | Author | Change |
|---|---|---|---|
| 0.1 | 2026-11-12 | Claude (AI agent), draft from code | First draft, written from the code as built. |

## Scope and reading notes

- This document covers the read-only OOS tracking list (`OosTrackingController`, `OosTrackingService`). Nothing in it creates or changes an OOS record. The OOS group code is set on samples elsewhere (the approval decision, see FS-REV); that code was not read for this document. `OosInvestigationDocumentController` was not in the files listed for this area and was not read.
- Every statement was written from the code files named in each entry. Nothing was run.
- Audit. The two methods read data only and write nothing, so there is no audit entry and no concurrency check to describe.

## Functions

### FS-OOS-001
**Satisfies:** URS-OOS-001, URS-OOS-002
**Function:** `GET api/oos-tracking`. The controller carries the policy `Oos.Manage` (`PermissionConstants.OosManage`); no other role or permission is tested. The controller passes the caller's readable sections (`GetReadableSectionIdsAsync`; null for a role that holds `Sections.ViewAll`) to the service. The method has no inputs from the client. It returns a list of OOS groups. A user without the permission is refused by the policy; a user with the permission but no section membership gets an empty list when the scope list is empty (the filter then matches no sample). The list is not paged and has no row limit.
**Code:** `backend/MicroLIMS.API/Controllers/OosTrackingController.cs` — `Get`
**Verified by:** reading (not run)
**Conflicts:** CONF-162

### FS-OOS-002
**Satisfies:** URS-OOS-001, URS-OOS-002
**Function:** Grouping rule, as coded. Every sample with an OOS group code is a candidate. With a scope list, a group is shown only when at least one of its retest samples (a sample with an origin sample) has a test order in the viewer's sections; the group's samples are then limited to the samples that have at least one test order in those sections. Per group: the root sample is the sample without an origin sample, else the one with the lowest ID among the samples loaded. Every other sample in the group is a retest entry, ordered by received date: new sample ID, reference, status, origin sample ID, reference and status (taken from the origin sample, else from the root), display name (item name; for after-cleaning the machine; for water the sampling point code; for environmental monitoring the department), category, batch number, retest type, distinct test codes, distinct assigned-analyst names ("User #id" when the name is not found) and the received date as the opening date. The retest type is the approval decision of the origin sample as text, or empty. The group header carries the root's ID, reference, status, name, category, batch, received date as opening date, and a flag that is true when an investigation document with a status other than Voided exists for the group code (compared without regard to case). Groups are ordered by opening date, newest first. Because the scope filter removes samples with no test in the viewer's sections, a viewer who has no test on the real root sample sees the lowest-ID remaining sample as the root. The method loads sample entities with their related entities without `AsNoTracking`.
**Code:** `backend/MicroLIMS.Application/Services/OosTrackingService.cs` — `GetOosGroupsAsync`, `ResolveDisplayName`
**Verified by:** reading (not run)
**Conflicts:** CONF-162
