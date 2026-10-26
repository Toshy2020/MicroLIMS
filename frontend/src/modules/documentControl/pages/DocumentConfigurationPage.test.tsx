import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";

const auth = { role: "SystemAdministrator", permissions: [] as string[] };
vi.mock("../../../contexts/AuthContext", () => ({ useAuth: () => auth }));

const service = vi.hoisted(() => ({
  getTypes: vi.fn(),
  getDepartments: vi.fn(),
  getNumberingConfig: vi.fn(),
  getSettings: vi.fn()
}));
vi.mock("../services/documentControlService", () => ({ documentControlService: service }));

import { lightTheme } from "../../../theme";
import { DocumentConfigurationPage } from "./DocumentConfigurationPage";

function renderPage() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <DocumentConfigurationPage />
    </ThemeProvider>
  );
}

describe("DocumentConfigurationPage access", () => {
  beforeEach(() => {
    service.getTypes.mockResolvedValue([]);
    service.getDepartments.mockResolvedValue([]);
    service.getNumberingConfig.mockResolvedValue({ prefix: "DOC-", numberFormat: "0000000", isEnabled: true });
    service.getSettings.mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  // The screen follows the Documents.ConfigManage permission the server checks, not the role name.
  it("is denied to a System Administrator whose role has not been granted Documents.ConfigManage", () => {
    auth.role = "SystemAdministrator";
    auth.permissions = ["Users.Manage"];

    renderPage();

    expect(screen.getByText(/Access Denied/)).toBeTruthy();
    expect(service.getTypes).not.toHaveBeenCalled();
  });

  it("is open to any role that holds Documents.ConfigManage", async () => {
    auth.role = "SectionHead";
    auth.permissions = ["Documents.ConfigManage"];

    renderPage();

    await waitFor(() => expect(service.getTypes).toHaveBeenCalled());
    expect(screen.queryByText(/Access Denied/)).toBeNull();
  });
});
