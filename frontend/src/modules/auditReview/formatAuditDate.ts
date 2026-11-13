import type { AuditReviewScopeKind } from "../../services/auditTrailReviewService";

export function formatDateTimeWithZone(iso: string | null | undefined): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return String(iso);
  return d.toLocaleString("en-GB", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZoneName: "short"
  });
}

export function formatReviewScope(
  scopeKind: AuditReviewScopeKind,
  entityName: string | null,
  recordId: string | null
): string {
  if (scopeKind === "EntityName") return `Record type: ${entityName ?? "—"}`;
  if (scopeKind === "Record") return `Record: ${entityName ?? "—"} #${recordId ?? "—"}`;
  return "All";
}
