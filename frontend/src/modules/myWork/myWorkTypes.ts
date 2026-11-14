export type MyWorkUrgency = "Overdue" | "Now" | "Today" | "Later" | "None";

export const MY_WORK_TARGET_TYPES = [
  "test-step",
  "incubation-read",
  "sample-review",
  "sample-approval",
  "sample-assign",
  "media-approval",
  "media-evaluation",
  "media",
  "solution",
  "cryovial",
  "document-review",
  "document-approval",
  "training-assignment",
  "equipment-task"
] as const;

export type MyWorkTargetType = (typeof MY_WORK_TARGET_TYPES)[number];

export interface MyWorkTarget {
  type: string;
  sampleId?: number;
  testOrderId?: number;
  incubationId?: number;
  documentId?: number;
  taskId?: number;
  equipmentId?: number;
  mediaId?: number;
}

export interface MyWorkItem {
  key: string;
  groupKey: string;
  identifier: string;
  title: string;
  context?: string;
  dueAtUtc?: string;
  urgency: MyWorkUrgency;
  locked: boolean;
  lockReason?: string;
  target: MyWorkTarget;
  actions: string[];
  sectionId?: number;
  sectionName?: string;
  lab?: string;
}

export interface MyWorkGroup {
  key: string;
  title: string;
  count: number;
  lockedCount: number;
  items: MyWorkItem[];
  hasMore: boolean;
}

export interface MyWorkResponse {
  serverTimeUtc: string;
  timeZoneId: string;
  groups: MyWorkGroup[];
  unavailableSources: string[];
}

export interface MyWorkSummary {
  total: number | null;
  overdue: number | null;
  now: number | null;
  complete: boolean;
}
