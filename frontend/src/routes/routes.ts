/**
 * Centralized Application Route Constants and Resolvers for MicroLIMS.
 * 
 * All routes match the active route tree in AppRoutes.tsx.
 * Do not invent routes or use deprecated paths.
 */

export const APP_ROUTES = {
  // Public
  LOGIN: "/login",

  // Core & Authenticated
  ROOT: "/",
  DASHBOARD: "/dashboard",
  PROFILE: "/profile",
  CHANGE_PASSWORD: "/change-password",
  REPORTS: "/reports",

  // Core Laboratory Workflows
  RECEIVING: "/receiving",
  TESTING_WORKSPACE: "/testing-workspace",

  // Laboratory Configuration & Master Data
  MEDIA_PREPARATION: "/laboratory-configuration/media",
  MEDIA_EVALUATION: "/laboratory-configuration/media-evaluation",
  CRYOVIALS: "/laboratory-configuration/cryovials",
  TEST_MASTER: "/laboratory-configuration/test-master",
  ORGANISMS: "/laboratory-configuration/organisms",
  ITEMS: "/laboratory-configuration/items",
  MEDIA_TYPES: "/laboratory-configuration/media-types",
  MEDIA_CHALLENGE_SPECS: "/laboratory-configuration/media-challenge-specs",
  WATER: "/laboratory-configuration/water",
  ENVIRONMENTAL_MONITORING: "/laboratory-configuration/environmental-monitoring",
  AFTER_CLEANING: "/laboratory-configuration/after-cleaning",
  CAUSE_OF_TESTING: "/laboratory-configuration/cause-of-testing",
  DILUENTS: "/laboratory-configuration/diluents",
  LAB_EQUIPMENT: "/laboratory-configuration/equipment",

  // Inventory & Stock
  INVENTORY_MATERIALS: "/inventory/materials",
  INVENTORY_EQUIPMENT: "/inventory/equipment",
  APPROVED_MEDIA: "/inventory/approved-media",
  APPROVED_CRYOVIALS: "/inventory/approved-cryovials",

  // Audit
  AUDIT_SEARCH: "/audit-search",

  // Administration
  USERS: "/users",
  ROLES: "/roles",

  // Printable Report Pages (standalone rendering outside MainLayout)
  SAMPLE_REPORT: (id: number | string) => `/samples/${id}/report`,
  MEDIA_REPORT: (id: number | string) => `/media/${id}/report`,
  CRYOVIAL_REPORT: (id: number | string) => `/cryovials/${id}/report`
} as const;

/**
 * Resolves an Audit Traceability navigation key or entity type to its
 * REAL existing MicroLIMS route. Returns null if no dedicated page exists.
 */
export function resolveTraceabilityRoute(
  navigationTarget?: string | null,
  nodeType?: string | null
): string | null {
  const target = navigationTarget?.toLowerCase().trim();
  const type = nodeType?.toLowerCase().trim();

  // 1. Check explicit navigationTarget
  if (target === "testing" || target === "samples" || target === "testorders" || target === "results") {
    return APP_ROUTES.TESTING_WORKSPACE;
  }
  if (target === "receiving") {
    return APP_ROUTES.RECEIVING;
  }
  if (target === "media") {
    return APP_ROUTES.MEDIA_PREPARATION;
  }
  if (target === "media-evaluation" || target === "mediaevaluation") {
    return APP_ROUTES.MEDIA_EVALUATION;
  }
  if (target === "materials") {
    return APP_ROUTES.INVENTORY_MATERIALS;
  }
  if (target === "cryovials") {
    return APP_ROUTES.CRYOVIALS;
  }
  if (target === "equipment") {
    return APP_ROUTES.INVENTORY_EQUIPMENT;
  }
  if (target === "items") {
    return APP_ROUTES.ITEMS;
  }
  if (target === "organisms") {
    return APP_ROUTES.ORGANISMS;
  }
  if (target === "water") {
    return APP_ROUTES.WATER;
  }
  if (target === "environmental-monitoring" || target === "em") {
    return APP_ROUTES.ENVIRONMENTAL_MONITORING;
  }
  if (target === "reports") {
    return APP_ROUTES.REPORTS;
  }

  // 2. Fallback to nodeType
  if (type === "sample" || type === "testorder" || type === "result" || type === "review" || type === "electronicsignature") {
    return APP_ROUTES.TESTING_WORKSPACE;
  }
  if (type === "item") {
    return APP_ROUTES.ITEMS;
  }
  if (type === "samplingpoint") {
    return APP_ROUTES.WATER;
  }
  if (type === "department") {
    return APP_ROUTES.ENVIRONMENTAL_MONITORING;
  }
  if (type === "media") {
    return APP_ROUTES.MEDIA_PREPARATION;
  }
  if (type === "mediaevaluation") {
    return APP_ROUTES.MEDIA_EVALUATION;
  }
  if (type === "material" || type === "materialdocument") {
    return APP_ROUTES.INVENTORY_MATERIALS;
  }
  if (type === "cryovial") {
    return APP_ROUTES.CRYOVIALS;
  }
  if (type === "organism") {
    return APP_ROUTES.ORGANISMS;
  }
  if (type === "equipment" || type === "equipmentdocument" || type === "equipmentstatushistory") {
    return APP_ROUTES.INVENTORY_EQUIPMENT;
  }

  return null;
}
