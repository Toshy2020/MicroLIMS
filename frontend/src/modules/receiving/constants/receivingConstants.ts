import { CategoryDefinition } from "../types/receivingTypes";

export const RECEIVING_CATEGORIES: CategoryDefinition[] = [
  {
    key: "product",
    label: "Product",
    apiCategory: "FinishedProduct",
    backendCategoryName: "FinishedProduct",
    description: "Finished pharmaceutical dosage forms (tablets, syrups, suspensions)"
  },
  {
    key: "rm",
    label: "Raw Material",
    apiCategory: "RawMaterial",
    backendCategoryName: "RawMaterial",
    description: "Active pharmaceutical ingredients (APIs), excipients, and raw inputs"
  },
  {
    key: "pm",
    label: "Packaging Material",
    apiCategory: "PackagingMaterial",
    backendCategoryName: "PackagingMaterial",
    description: "Primary & secondary containers, closures, foils, and labels"
  },
  {
    key: "water",
    label: "Water",
    apiCategory: null,
    backendCategoryName: "Water",
    description: "Purified water, WFI, potable water, and feed water sampling points"
  },
  {
    key: "em",
    label: "Environmental Monitoring",
    apiCategory: null,
    backendCategoryName: "EnvironmentalMonitoring",
    description: "Active air, settle plates, surface swabs, and personnel monitoring"
  },
  {
    key: "ac",
    label: "After Cleaning",
    apiCategory: null,
    backendCategoryName: "AfterCleaning",
    description: "Equipment swab rinse and surface contact plates after sanitization"
  }
];

export const SAMPLE_STATUS_OPTIONS = [
  { value: "ALL", label: "All Sample Statuses" },
  { value: "Received", label: "Received" },
  { value: "InTesting", label: "Under Testing" },
  { value: "PendingReview", label: "Pending Review" },
  { value: "Approved", label: "Approved" },
  { value: "Rejected", label: "Rejected" },
  { value: "RetestRequested", label: "Cancelled / Voided" }
];

export const TEST_STATUS_OPTIONS = [
  { value: "ALL", label: "All Test Statuses" },
  { value: "Waiting", label: "Not Started" },
  { value: "InProgress", label: "In Progress" },
  { value: "ResultEntered", label: "Result Entered" },
  { value: "UnderReview", label: "Under Review" },
  { value: "Reviewed", label: "Reviewed" },
  { value: "Approved", label: "Approved" },
  { value: "Rejected", label: "Rejected" }
];
