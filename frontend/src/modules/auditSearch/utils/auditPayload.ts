import type { AuditSearchFilterState } from "../types/auditTypes";

export const INITIAL_AUDIT_FILTERS: AuditSearchFilterState = {
  fromDate: "",
  toDate: "",
  batchNumber: "",
  controlNumber: "",
  sampleReferenceNumber: "",
  mediaLotNumber: "",
  referenceStrainCode: "",
  cryovialCode: "",
  entityName: "",
  action: "",
  userId: "",
  actionCategory: "",
  actorType: "",
  correlationId: "",
  reason: "",
  negativeEntityId: false
};

// Builds the request body for POST /admin/audit/search and /export from the filter form.
// The cursor (BeforeId) is added by the caller; an export sends the filter without it.
export function buildAuditPayload(f: AuditSearchFilterState, take?: number): Record<string, unknown> {
  const payload: Record<string, unknown> = {};
  if (take !== undefined) payload.Take = take;

  if (f.fromDate) payload.FromDate = new Date(f.fromDate).toISOString();
  if (f.toDate) {
    const d = new Date(f.toDate);
    d.setHours(23, 59, 59, 999);
    payload.ToDate = d.toISOString();
  }
  if (f.batchNumber.trim()) payload.BatchNumber = f.batchNumber.trim();
  if (f.controlNumber.trim()) payload.ControlNumber = f.controlNumber.trim();
  if (f.sampleReferenceNumber.trim()) payload.SampleReferenceNumber = f.sampleReferenceNumber.trim();
  if (f.mediaLotNumber.trim()) payload.MediaLotNumber = f.mediaLotNumber.trim();
  if (f.referenceStrainCode.trim()) payload.ReferenceStrainCode = f.referenceStrainCode.trim();
  if (f.cryovialCode.trim()) payload.CryovialCode = f.cryovialCode.trim();
  if (f.entityName.trim()) payload.EntityName = f.entityName.trim();
  if (f.action.trim()) payload.Action = f.action.trim();
  if (f.userId.trim()) {
    const uid = parseInt(f.userId.trim(), 10);
    if (!isNaN(uid)) payload.UserId = uid;
  }
  if (f.actionCategory) payload.ActionCategory = f.actionCategory;
  if (f.actorType) payload.ActorType = f.actorType;
  if (f.correlationId.trim()) payload.CorrelationId = f.correlationId.trim();
  if (f.reason.trim()) payload.Reason = f.reason.trim();
  if (f.negativeEntityId) payload.NegativeEntityId = true;
  return payload;
}
