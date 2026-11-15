import type { RecentRecordDto } from "./services/recentRecordsService";

// The routes the server sends for each type (RecordNavigationTargets).
function serverRoute(recordType: string, recordId: number): string {
  switch (recordType) {
    case "sample":
      return `/receiving-testing?sampleId=${recordId}`;
    case "testOrder":
      return `/receiving-testing?sampleId=900&testOrderId=${recordId}`;
    case "item":
      return `/laboratory-configuration/items?item=${recordId}`;
    case "document":
      return `/document-control/documents/${recordId}`;
    case "equipment":
      return `/inventory/equipment?equipmentId=${recordId}`;
    case "material":
      return `/inventory/materials?materialId=${recordId}`;
    case "media":
      return `/laboratory-configuration/media?mediaId=${recordId}`;
    default:
      return `/preparation/${recordId}`;
  }
}

export function recent(
  recordType: string,
  recordId: number,
  identifier: string,
  title: string,
  minutesAgo = recordId,
  route = serverRoute(recordType, recordId)
): RecentRecordDto {
  return {
    recordType,
    recordId,
    identifier,
    title,
    route,
    openedAtUtc: new Date(Date.UTC(2026, 10, 15, 10, 0) - minutesAgo * 60_000).toISOString()
  };
}
