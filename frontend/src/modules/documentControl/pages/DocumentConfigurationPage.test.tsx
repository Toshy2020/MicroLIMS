import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material";
import { MemoryRouter } from "react-router-dom";

const service = vi.hoisted(() => ({
  getTypes: vi.fn(),
  getDepartments: vi.fn(),
  getNumberingConfig: vi.fn(),
  getSettings: vi.fn(),
  createType: vi.fn(),
  updateType: vi.fn()
}));
vi.mock("../services/documentControlService", () => ({ documentControlService: service }));

import { lightTheme } from "../../../theme";
import { AuthProvider, type Role } from "../../../contexts/AuthContext";
import { DocumentConfigurationPage } from "./DocumentConfigurationPage";

function seedAuth(permissions: string[], role: Role = "SystemAdministrator") {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, "");
  const token = `${b64({ alg: "none" })}.${b64({ role, permission: permissions })}.x`;
  localStorage.setItem("microlims_token", token);
  localStorage.setItem("microlims_full_name", "Ada Admin");
  localStorage.setItem("microlims_username", "ada");
  localStorage.setItem("microlims_user_id", "1");
}

function renderPage() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <DocumentConfigurationPage />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

describe("DocumentConfigurationPage access", () => {
  beforeEach(() => {
    localStorage.clear();
    service.getTypes.mockResolvedValue([]);
    service.getDepartments.mockResolvedValue([]);
    service.getNumberingConfig.mockResolvedValue({ prefix: "DOC-", numberFormat: "0000000", isEnabled: true });
    service.getSettings.mockResolvedValue([]);
    service.createType.mockResolvedValue({
      id: 10,
      version: 1,
      code: "SOP",
      name: "Standard Operating Procedure",
      defaultReviewCycleMonths: 24,
      isActive: true,
      requiresQaApprover: true
    });
    service.updateType.mockResolvedValue({
      id: 1,
      version: 2,
      code: "SOP",
      name: "Standard Operating Procedure",
      defaultReviewCycleMonths: 24,
      isActive: true,
      requiresQaApprover: true
    });
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  // The screen follows the Documents.ConfigManage permission the server checks, not the role name.
  it("is denied to a System Administrator whose role has not been granted Documents.ConfigManage", () => {
    seedAuth(["Users.Manage"], "SystemAdministrator");

    renderPage();

    expect(screen.getByText(/Access Denied/)).toBeTruthy();
    expect(service.getTypes).not.toHaveBeenCalled();
  });

  it("is open to any role that holds Documents.ConfigManage", async () => {
    seedAuth(["Documents.ConfigManage"], "SectionHead");

    renderPage();

    await waitFor(() => expect(service.getTypes).toHaveBeenCalled());
    expect(screen.queryByText(/Access Denied/)).toBeNull();
  });

  // One editor for the organisation: Administration > Organization (reasons, deactivation checks, memberships).
  it("sends the Departments & Sections tab to Administration > Organization instead of editing them twice", async () => {
    const user = userEvent.setup();
    seedAuth(["Documents.ConfigManage"], "SystemAdministrator");
    service.getDepartments.mockResolvedValue([{ id: 1, code: "QC", name: "Quality Control", isActive: true, sections: [] }]);

    renderPage();
    await user.click(await screen.findByRole("tab", { name: /Departments & Sections \(1\)/ }));

    const link = screen.getByRole("link", { name: "Open Organization" });
    expect(link.getAttribute("href")).toBe("/admin/organization");
    expect(screen.queryByRole("button", { name: /add department/i })).toBeNull();
  });

  it("saves a document type with the QA approver switch on", async () => {
    const user = userEvent.setup();
    seedAuth(["Documents.ConfigManage"], "SystemAdministrator");

    renderPage();
    await user.click(await screen.findByRole("button", { name: /Add Document Type/i }));

    await user.type(screen.getByLabelText(/Type Code/i), "SOP");
    await user.type(screen.getByLabelText(/Type Name/i), "Standard Operating Procedure");

    expect(
      screen.getByText("When on, a revision of this type can only be approved by a Quality Assurance user.")
    ).toBeTruthy();

    const qaSwitch = screen.getByRole("switch", { name: /Requires a Quality Assurance approver/i });
    await user.click(qaSwitch);
    await user.click(screen.getByRole("button", { name: /^Save$/i }));

    await waitFor(() => {
      expect(service.createType).toHaveBeenCalledWith({
        code: "SOP",
        name: "Standard Operating Procedure",
        defaultReviewCycleMonths: 24,
        requiresQaApprover: true
      });
    });
  });

  it("lists whether a document type requires a QA approver", async () => {
    seedAuth(["Documents.ConfigManage"], "SystemAdministrator");
    service.getTypes.mockResolvedValue([
      {
        id: 1,
        version: 1,
        code: "SOP",
        name: "Standard Operating Procedure",
        defaultReviewCycleMonths: 24,
        isActive: true,
        requiresQaApprover: true
      },
      {
        id: 2,
        version: 1,
        code: "FRM",
        name: "Blank Form",
        defaultReviewCycleMonths: 12,
        isActive: true,
        requiresQaApprover: false
      }
    ]);

    renderPage();

    expect(await screen.findByText("SOP")).toBeTruthy();
    expect(screen.getByText("Required")).toBeTruthy();
    expect(screen.getByText("Not required")).toBeTruthy();
  });
});
