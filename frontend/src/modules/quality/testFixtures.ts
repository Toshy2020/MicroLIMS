import type {
  QualityActionDetailDto,
  QualityAttachmentDto,
  QualityCommentDto,
  QualityExtensionDto,
  QualityLinkDto,
  QualityPathStepDto,
  QualityRecordDetailDto,
  QualityRecordListItemDto,
  QualitySignedTimelineEntryDto
} from "../../services/qualityService";

// Builders for the tests of the quality screens: a complete DTO with every field the server sends, so a
// test only names what it is about. Not imported by production code.

export function listItem(overrides: Partial<QualityRecordListItemDto> = {}): QualityRecordListItemDto {
  return {
    id: 1,
    number: "DEV-2026-0001",
    type: "Deviation",
    title: "Incubator temperature drift",
    status: "Investigation",
    sectionId: 1,
    sectionName: "Microbiology",
    classificationId: 2,
    classificationName: "Major",
    raisedByUserId: 7,
    raisedByName: "Ann Analyst",
    raisedAtUtc: "2026-11-20T08:00:00Z",
    ownerUserId: 8,
    ownerName: "Sam Head",
    dueDate: "2026-12-01",
    isOverdue: false,
    isCancelled: false,
    version: 3,
    ...overrides
  };
}

export function pathStep(overrides: Partial<QualityPathStepDto> = {}): QualityPathStepDto {
  return {
    stepKey: "raised",
    label: "Raised",
    isCompleted: false,
    isCurrent: false,
    completedAtUtc: null,
    completedByName: null,
    ...overrides
  };
}

export function timelineEntry(overrides: Partial<QualitySignedTimelineEntryDto> = {}): QualitySignedTimelineEntryDto {
  return {
    stepId: 1,
    kind: "DeviationAssessed",
    signerFullName: "Sam Head",
    username: "shead",
    role: "Section Head",
    signedAtUtc: "2026-11-21T10:15:00Z",
    signedAtUtcText: "2026-11-21 10:15:00 UTC",
    meaning: "DeviationAssessed",
    meaningText: "Deviation assessed",
    comment: null,
    ...overrides
  };
}

export function actionDetail(overrides: Partial<QualityActionDetailDto> = {}): QualityActionDetailDto {
  return {
    id: 11,
    kind: "Corrective",
    description: "Recalibrate the incubator probe",
    responsibleUserId: 7,
    responsibleName: "Ann Analyst",
    dueDate: "2026-12-05",
    doneAtUtc: null,
    doneByUserId: null,
    doneByName: null,
    evidence: null,
    evidenceLinkId: null,
    version: 1,
    ...overrides
  };
}

export function attachment(overrides: Partial<QualityAttachmentDto> = {}): QualityAttachmentDto {
  return {
    id: 21,
    fileName: "temperature-log.pdf",
    contentType: "application/pdf",
    size: 2048,
    uploadedByUserId: 7,
    uploadedAtUtc: "2026-11-21T09:00:00Z",
    voidedAtUtc: null,
    voidedByUserId: null,
    voidReason: null,
    isVoided: false,
    ...overrides
  };
}

export function link(overrides: Partial<QualityLinkDto> = {}): QualityLinkDto {
  return { id: 31, number: "S-2026-0010", label: "Amoxicillin 500mg", route: "/samples/10", isRestricted: false, ...overrides };
}

export function comment(overrides: Partial<QualityCommentDto> = {}): QualityCommentDto {
  return { id: 41, text: "Probe replaced on the night shift.", authorUserId: 7, createdAtUtc: "2026-11-21T11:00:00Z", ...overrides };
}

export function extension(overrides: Partial<QualityExtensionDto> = {}): QualityExtensionDto {
  return {
    id: 51,
    recordId: 1,
    actionId: null,
    requestedDueDate: "2026-12-20",
    reason: "Supplier delivers the probe late",
    requestedByUserId: 8,
    requestedAtUtc: "2026-11-22T08:00:00Z",
    decision: null,
    decidedByUserId: null,
    decidedAtUtc: null,
    decisionComment: null,
    signatureId: null,
    version: 1,
    recordVersion: 3,
    ...overrides
  };
}

export function recordDetail(overrides: Partial<QualityRecordDetailDto> = {}): QualityRecordDetailDto {
  return {
    id: 1,
    number: "DEV-2026-0001",
    type: "Deviation",
    title: "Incubator temperature drift",
    description: "Incubator 3 held 37.8 C for six hours.",
    status: "Investigation",
    sectionId: 1,
    sectionName: "Microbiology",
    classificationId: 2,
    classificationName: "Major",
    raisedByUserId: 7,
    raisedByName: "Ann Analyst",
    raisedAtUtc: "2026-11-20T08:00:00Z",
    ownerUserId: 8,
    ownerName: "Sam Head",
    dueDate: "2026-12-01",
    isOverdue: false,
    isCancelled: false,
    cancelReason: null,
    version: 3,
    deviation: {
      status: "Investigation",
      occurredAtUtc: "2026-11-19T22:00:00Z",
      location: "Incubator room 2",
      immediateAction: "Moved the plates to incubator 4.",
      impactAssessment: "Plates of two batches were affected.",
      rootCauseCategoryId: null,
      rootCauseCategoryName: null,
      rootCauseDescription: null,
      proposedActions: null,
      capaRequired: null,
      rejectionReason: null
    },
    capa: null,
    changeControl: null,
    complaint: null,
    pathSteps: [
      pathStep({ stepKey: "raised", label: "Raised", isCompleted: true, completedAtUtc: "2026-11-20T08:00:00Z", completedByName: "Ann Analyst" }),
      pathStep({ stepKey: "investigation", label: "Investigation", isCurrent: true }),
      pathStep({ stepKey: "closed", label: "Closed" })
    ],
    timeline: [timelineEntry()],
    actions: [],
    attachments: [],
    linksCount: 0,
    ...overrides
  };
}
