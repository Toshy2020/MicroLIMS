export interface SopCapture {
  status: "Captured" | "NoSopNumber" | "NotFound" | "NoEffectiveRevision" | "Ambiguous";
  documentMasterId: number | null;
  documentCode: string | null;
  title: string | null;
  revisionId: number | null;
  revisionNumber: string | null;
  effectiveDateUtc: string | null;
  capturedAtUtc: string | null;
}

export interface NewerSopRevision {
  revisionNumber: string;
  effectiveDateUtc: string;
}

export interface TestOrderSop {
  sopCapture?: SopCapture | null;
  newerSopRevision?: NewerSopRevision | null;
}
