import type { ComponentType } from "react";
import { render } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { vi } from "vitest";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { AuthProvider } from "../../../contexts/AuthContext";
import type { QualityRecordDetailDto } from "../../../services/qualityService";
import type { QualityTypeActionsProps } from "../typeActions";

// Helpers for the tests of the step forms (not imported by production code): the real forms rendered with the real
// signature dialog, the real lookups and only the HTTP client replaced.

export type Permissions = QualityTypeActionsProps["permissions"];

export const NONE: Permissions = { raise: false, investigate: false, labConclude: false, approve: false };
export const QA: Permissions = { raise: true, investigate: false, labConclude: false, approve: true };
export const SECTION_HEAD: Permissions = { raise: true, investigate: true, labConclude: true, approve: false };

export const ok = <T,>(data: T) => Promise.resolve({ data: { success: true, data } });

export interface HttpMock {
  get: ReturnType<typeof vi.fn>;
  post: ReturnType<typeof vi.fn>;
  put: ReturnType<typeof vi.fn>;
  delete: ReturnType<typeof vi.fn>;
}

export const USERS = [
  { id: 7, fullName: "Ann Analyst", username: "aanalyst", jobTitle: null, roleName: "Analyst", isActive: true },
  { id: 8, fullName: "Sam Head", username: "shead", jobTitle: null, roleName: "Section Head", isActive: true },
  { id: 9, fullName: "Quinn QA", username: "qqa", jobTitle: null, roleName: "QA", isActive: true },
  { id: 10, fullName: "Left Person", username: "lperson", jobTitle: null, roleName: "Analyst", isActive: false }
];

export const CLASSIFICATIONS = [
  { id: 1, name: "Minor", order: 1, isActive: true, version: 1 },
  { id: 2, name: "Major", order: 2, isActive: true, version: 1 },
  { id: 3, name: "Retired", order: 3, isActive: false, version: 1 }
];

export const CAUSE_CATEGORIES = {
  items: [
    { id: 4, name: "Equipment failure", isActive: true, isNoCauseFound: false, version: 1 },
    { id: 5, name: "Operator error", isActive: true, isNoCauseFound: false, version: 1 }
  ],
  totalCount: 2,
  page: 1,
  pageSize: 100
};

// Answers the GETs the step forms make for their choice lists; `extra` answers any other URL.
export function serveLookups(http: HttpMock, extra: Record<string, () => Promise<unknown>> = {}) {
  http.get.mockImplementation((url: string) => {
    if (extra[url]) return extra[url]();
    if (url === "/users/directory") return ok(USERS);
    if (url === "/quality/config/classifications") return ok(CLASSIFICATIONS);
    if (url === "/oos-templates/categories") return ok(CAUSE_CATEGORIES);
    return Promise.reject(new Error(`unexpected GET ${url}`));
  });
}

// The signed-in person: the signature dialog shows their name; the id is passed to the form as a prop.
export function seedSigner(fullName = "Quinn QA") {
  localStorage.setItem("microlims_username", "qqa");
  localStorage.setItem("microlims_full_name", fullName);
}

function LocationProbe() {
  const location = useLocation();
  return <p data-testid="location">{location.pathname}</p>;
}

export interface RenderStepOptions {
  permissions?: Permissions;
  currentUserId?: number | null;
  onChanged?: () => Promise<void>;
}

// Renders one type's actions component the way the record page does, plus a probe of the current route so a test
// can see where the form navigated.
export function renderStep(Component: ComponentType<QualityTypeActionsProps>, record: QualityRecordDetailDto, options: RenderStepOptions = {}) {
  seedSigner();
  const onChanged = options.onChanged ?? vi.fn().mockResolvedValue(undefined);
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/quality/1"]}>
          <Routes>
            <Route
              path="/quality/:id"
              element={
                <>
                  <LocationProbe />
                  <Component
                    record={record}
                    permissions={options.permissions ?? QA}
                    currentUserId={options.currentUserId === undefined ? 9 : options.currentUserId}
                    onChanged={onChanged}
                  />
                </>
              }
            />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
  return { onChanged };
}

export const versionHeader = (version: number) => ({ headers: { "If-Match": `"${version}"` } });
