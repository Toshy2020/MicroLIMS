export enum DiscussionCategory {
  Water = 1,
  Equipment = 2,
  EnvironmentalMonitoring = 3,
  Products = 4,
  MediaMaterials = 5,
  InternalDecisions = 6,
  ManagementRequirements = 7,
  EdaRequirements = 8,
  Iso17025 = 9,
  GmpRegulatory = 10,
  Other = 11
}

export interface CategoryOption {
  id: DiscussionCategory;
  name: string;
}

export const DISCUSSION_CATEGORIES: CategoryOption[] = [
  { id: DiscussionCategory.Water, name: "Water" },
  { id: DiscussionCategory.Equipment, name: "Equipment" },
  { id: DiscussionCategory.EnvironmentalMonitoring, name: "Environmental Monitoring (EM)" },
  { id: DiscussionCategory.Products, name: "Products" },
  { id: DiscussionCategory.MediaMaterials, name: "Media / Materials" },
  { id: DiscussionCategory.InternalDecisions, name: "Internal Decisions" },
  { id: DiscussionCategory.ManagementRequirements, name: "Management Requirements" },
  { id: DiscussionCategory.EdaRequirements, name: "EDA Requirements" },
  { id: DiscussionCategory.Iso17025, name: "ISO 17025" },
  { id: DiscussionCategory.GmpRegulatory, name: "GMP / Regulatory" },
  { id: DiscussionCategory.Other, name: "Other" }
];

export interface DiscussionAttachment {
  id: number;
  fileName: string;
  fileExtension: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedAt: string;
}

export interface DiscussionComment {
  id: number;
  postId: number;
  authorUserId: number;
  authorName: string;
  authorRole: string;
  content: string;
  isEdited: boolean;
  lastEditedAt?: string | null;
  createdAt: string;
}

export interface DiscussionPostSummary {
  id: number;
  title: string;
  contentPreview: string;
  category: DiscussionCategory;
  categoryName: string;
  authorUserId: number;
  authorName: string;
  authorRole: string;
  isImportant: boolean;
  currentVersion: number;
  isEdited: boolean;
  lastEditedAt?: string | null;
  createdAt: string;
  commentCount: number;
  attachmentCount: number;
  attachments: DiscussionAttachment[];
}

export interface DiscussionPostDetail {
  id: number;
  title: string;
  content: string;
  category: DiscussionCategory;
  categoryName: string;
  authorUserId: number;
  authorName: string;
  authorRole: string;
  isImportant: boolean;
  currentVersion: number;
  isEdited: boolean;
  lastEditedAt?: string | null;
  createdAt: string;
  attachments: DiscussionAttachment[];
  comments: DiscussionComment[];
  versionCount: number;
}

export interface DiscussionVersion {
  id: number;
  versionNumber: number;
  title: string;
  content: string;
  category: DiscussionCategory;
  categoryName: string;
  changedByUserId: number;
  changedByName: string;
  changedAt: string;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}
