// Physicochemical Test Master area helpers and types.
// A physicochemical test belongs to FinishedProduct, RawPackaging, or Both.

export type PageArea = "fp" | "rmpm";
export type PhyschemArea = "FinishedProduct" | "RawPackaging" | "Both";

/**
 * Returns the default PhyschemArea for a given page area.
 * fp -> "FinishedProduct", rmpm -> "RawPackaging"
 */
export function defaultAreaFor(page: PageArea): PhyschemArea {
  switch (page) {
    case "fp":
      return "FinishedProduct";
    case "rmpm":
      return "RawPackaging";
  }
}

/**
 * Checks if a test's PhyschemArea matches/includes the requested page area.
 * A test marked "Both" or with null/undefined area includes both "fp" and "rmpm".
 */
export function areaIncludes(testArea: PhyschemArea | null | undefined, page: PageArea): boolean {
  if (testArea === "Both" || testArea == null) {
    return true;
  }
  if (testArea === "FinishedProduct") {
    return page === "fp";
  }
  if (testArea === "RawPackaging") {
    return page === "rmpm";
  }
  return false;
}

/**
 * Toggles the "Both" state for a test based on the current page area and checkbox state.
 * When checked is true, sets "Both".
 * When checked is false, returns the default area for the page (fp -> FinishedProduct, rmpm -> RawPackaging).
 */
export function toggleBoth(
  _current: PhyschemArea | null | undefined,
  page: PageArea,
  checked: boolean
): PhyschemArea {
  if (checked) {
    return "Both";
  }
  return defaultAreaFor(page);
}

export function isBoth(area: PhyschemArea | null | undefined): boolean {
  return area === "Both";
}

/** Mirrors the server's PhyschemAreas.OfCategory: raw and packaging materials are rmpm, every other item category is fp. */
export function areaOfItemCategory(category: string | null | undefined): PageArea {
  return category === "RawMaterial" || category === "PackagingMaterial" ? "rmpm" : "fp";
}
