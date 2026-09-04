/**
 * Release 1c WP3 Acknowledgement Engine & Evidentiary Recording Types
 */

export interface AcknowledgementPresentationDto {
  assignmentId: number;
  documentMasterId: number;
  companyDocumentCode: string;
  documentTitle: string;
  documentRevisionId: number;
  revisionNumber: string;
  assignedUserId: number;
  assignedUserName: string;
  assignmentStatus: string;
  dueDateUtc?: string | null;
  controlledFileId?: number | null;
  controlledFileName?: string | null;
  controlledFileSha256?: string | null;
  legalStatementText: string;
  canAcknowledge: boolean;
  validationMessage?: string | null;
}

export interface AcknowledgementSubmissionRequest {
  assignmentId: number;
  documentRevisionId: number;
  confirmedLegalStatement: boolean;
  comments?: string | null;
  clientIpAddress?: string | null;
  userAgent?: string | null;
}

export interface AcknowledgementResultDto {
  acknowledgementRecordId: number;
  assignmentId: number;
  documentRevisionId: number;
  status: string;
  acknowledgedAtUtc: string;
  legalStatementFrozen: string;
  isIdempotentReplay: boolean;
}

export interface ReadingProgressUpdateRequest {
  assignmentId: number;
  documentRevisionId: number;
  progressPercentage: number;
  lastPageRead?: number | null;
}

export interface ReadingProgressResultDto {
  assignmentId: number;
  progressPercentage: number;
  status: string;
  updatedAtUtc: string;
}
