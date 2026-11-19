import type { TFunction } from "i18next";
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
  recordId: string | null,
  t?: TFunction<"auditReview">
): string {
  if (scopeKind === "EntityName") return t ? t("scope.entityName", { name: entityName ?? "—" }) : `Record type: ${entityName ?? "—"}`;
  if (scopeKind === "Record") return t ? t("scope.record", { name: entityName ?? "—", id: recordId ?? "—" }) : `Record: ${entityName ?? "—"} #${recordId ?? "—"}`;
  return t ? t("scope.all") : "All";
}
