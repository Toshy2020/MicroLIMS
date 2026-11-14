import type { MyWorkTarget } from "./myWorkTypes";

function workspaceBase(lab?: string): string {
  const normalized = lab?.trim().toUpperCase();
  if (normalized === "FP" || normalized === "PHYSICOCHEMICAL") {
    return "/physicochemical/workspace";
  }
  return "/microbiology/workspace";
}

function buildWorkspaceUrl(
  base: string,
  params: Record<string, string | number | undefined>
): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") {
      search.set(key, String(value));
    }
  }
  const qs = search.toString();
  return qs ? `${base}?${qs}` : base;
}

/**
 * Maps a backend MyWorkTarget to an existing frontend route.
 * Returns null when no route fits, so the row renders as plain text
 * with "Open from the workspace".
 */
export function routeForTarget(t: MyWorkTarget, lab?: string): string | null {
  const ws = workspaceBase(lab);

  switch (t.type) {
    case "test-step":
      return buildWorkspaceUrl(ws, {
        sampleId: t.sampleId,
        testOrderId: t.testOrderId,
        open: "step"
      });
    case "incubation-read":
      return buildWorkspaceUrl(ws, {
        sampleId: t.sampleId,
        testOrderId: t.testOrderId,
        open: "incubation",
        incubationId: t.incubationId
      });
    case "sample-review":
      return buildWorkspaceUrl(ws, {
        sampleId: t.sampleId,
        open: "review"
      });
    case "sample-approval":
      return buildWorkspaceUrl(ws, {
        sampleId: t.sampleId,
        open: "approval"
      });
    case "sample-assign":
      return buildWorkspaceUrl(ws, {
        sampleId: t.sampleId,
        testOrderId: t.testOrderId,
        open: "assign"
      });
    case "media-approval":
    case "media":
      return t.mediaId != null
        ? `/laboratory-configuration/media?mediaId=${t.mediaId}`
        : "/laboratory-configuration/media";
    case "media-evaluation":
      return "/laboratory-configuration/media-evaluation";
    case "solution":
      return t.taskId != null ? `/preparation/${t.taskId}` : "/preparation";
    case "cryovial":
      return "/laboratory-configuration/cryovials";
    case "document-review":
    case "document-approval":
      return t.documentId != null
        ? `/document-control/documents/${t.documentId}`
        : "/document-control/library";
    case "training-assignment":
      return "/document-control/my-reading-list";
    case "equipment-task":
      return "/inventory/equipment";
    default:
      return null;
  }
}

/**
 * Maps a My Work group key to the filtered workspace or list route
 * used by the "Show all {n}" overflow link.
 */
export function routeForGroup(groupKey: string, lab?: string): string {
  const ws = workspaceBase(lab);
  switch (groupKey) {
    case "readyToRead":
      return `${ws}?workload=readyToRead`;
    case "returned":
    case "endingToday":
    case "next24Hours":
    case "toPerform":
      return `${ws}?scope=mine`;
    case "review":
      return `${ws}?status=UnderReview`;
    case "approval":
      return `${ws}?status=UnderApproval`;
    case "available":
    case "unassigned":
      return `${ws}?workload=unassigned`;
    case "documents":
      return "/document-control/my-reading-list";
    case "equipment":
      return "/inventory/equipment";
    case "materials":
      return "/laboratory-configuration/media";
    default:
      return "/receiving-testing";
  }
}
