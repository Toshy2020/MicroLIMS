import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { AuthProvider } from "../../contexts/AuthContext";
import { CreateRolePage } from "./CreateRolePage";
import { RoleService, type PermissionRecord, type RoleDetail, type RoleRecord } from "./services/RoleService";

// Only the service layer is mocked; the real wizard, PermissionMatrix,
// SignatureDialog and AuthProvider run (CLAUDE.md Rule 4.5).
vi.mock("./services/RoleService", () => ({
  RoleService: {
    getAll: vi.fn(),
    getById: vi.fn(),
    getAllPermissions: vi.fn(),
    getSodRules: vi.fn(),
    getQaForbiddenCodes: vi.fn(),
    create: vi.fn(),
    updatePermissions: vi.fn()
  }
}));

const existingRoles: RoleRecord[] = [
  { id: 1, name: "System Administrator", description: null, type: "SystemAdministrator", isSystemRole: true, isActive: true },
  { id: 4, name: "Analyst", description: null, type: "Analyst", isSystemRole: true, isActive: true },
  { id: 5, name: "QC Manager", description: null, type: "QcManager", isSystemRole: true, isActive: true },
  { id: 6, name: "QA", description: null, type: "Qa", isSystemRole: true, isActive: true }
];

const permissions: PermissionRecord[] = [
  { code: "Samples.Approve", description: "Approve sample results.", isEnforced: true },
  { code: "Documents.Approve", description: "Approve controlled documents.", isEnforced: true },
  { code: "Audit.Review", description: "Perform and sign audit trail reviews.", isEnforced: true }
];

const qaDetail: RoleDetail = {
  id: 6,
  version: 1,
  name: "QA",
  description: null,
  type: "Qa",
  isSystemRole: true,
  isActive: true,
  permissionCodes: ["Documents.Approve", "Audit.Review"]
};

const analystDetail: RoleDetail = {
  id: 4,
  version: 1,
  name: "Analyst",
  description: null,
  type: "Analyst",
  isSystemRole: true,
  isActive: true,
  permissionCodes: ["Samples.Approve"]
};

function renderPage() {
  return render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter initialEntries={["/roles/new"]}>
          <Routes>
            <Route path="/roles/new" element={<CreateRolePage />} />
            <Route path="/roles/:id" element={<div>Role detail page</div>} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

const permissionCheckbox = (code: string) =>
  screen.getByRole("checkbox", { name: new RegExp(`^${code.replace(/\./g, "\\.")}`) }) as HTMLInputElement;

async function advanceToBaseTypeStep(user: ReturnType<typeof userEvent.setup>, roleName = "QA Reviewer") {
  const nameInput = await screen.findByLabelText(/Role Name/);
  await user.type(nameInput, roleName);
  await user.click(screen.getByRole("button", { name: "Next" }));
}

describe("CreateRolePage QA base type", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    localStorage.setItem("microlims_full_name", "Ada Admin");
    localStorage.setItem("microlims_username", "ada");
    localStorage.setItem("microlims_role", "SystemAdministrator");

    vi.mocked(RoleService.getAll).mockResolvedValue(existingRoles);
    vi.mocked(RoleService.getAllPermissions).mockResolvedValue(permissions);
    vi.mocked(RoleService.getSodRules).mockResolvedValue({
      administrationCodes: [],
      gxpExecutionAndApprovalCodes: ["Samples.Approve", "Documents.Approve"]
    });
    vi.mocked(RoleService.getQaForbiddenCodes).mockResolvedValue(["Samples.Approve"]);
    vi.mocked(RoleService.getById).mockImplementation(async (id: number) =>
      id === 6 ? qaDetail : analystDetail
    );
    vi.mocked(RoleService.create).mockResolvedValue({
      id: 99,
      version: 1,
      name: "QA Reviewer",
      description: null,
      type: "Qa",
      isSystemRole: false,
      isActive: true,
      permissionCodes: ["Documents.Approve", "Audit.Review"]
    });
    vi.mocked(RoleService.updatePermissions).mockResolvedValue({
      id: 99,
      version: 2,
      name: "QA Reviewer",
      description: null,
      type: "Qa",
      isSystemRole: false,
      isActive: true,
      permissionCodes: ["Documents.Approve", "Audit.Review"]
    });
  });

  afterEach(cleanup);

  it("QA base type disables forbidden permissions with the reason", async () => {
    const user = userEvent.setup();
    renderPage();

    await advanceToBaseTypeStep(user);
    expect(screen.getByRole("radio", { name: "QC Manager" })).toBeTruthy();
    await user.click(screen.getByRole("radio", { name: "Quality Assurance" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    const forbidden = permissionCheckbox("Samples.Approve");
    const allowed = permissionCheckbox("Documents.Approve");

    expect(forbidden.disabled).toBe(true);
    expect(forbidden.checked).toBe(false);
    const describedById = forbidden.getAttribute("aria-describedby");
    expect(describedById).toBeTruthy();
    expect(document.getElementById(describedById!)?.textContent).toBe("Not available to a Quality Assurance role");
    expect(screen.getByText("Not available to a Quality Assurance role")).toBeTruthy();

    expect(allowed.disabled).toBe(false);
  });

  it("QA base type cannot toggle a forbidden permission", async () => {
    const user = userEvent.setup();
    const directClickUser = userEvent.setup({ pointerEventsCheck: 0 });
    renderPage();

    await advanceToBaseTypeStep(user);
    await user.click(screen.getByRole("radio", { name: "Quality Assurance" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    const forbidden = permissionCheckbox("Samples.Approve");
    await directClickUser.click(forbidden);
    forbidden.focus();
    await user.keyboard(" ");
    expect(forbidden.checked).toBe(false);

    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.type(screen.getByLabelText(/Reason for Creating This Role/), "Add QA delegate");
    await user.click(screen.getByRole("button", { name: "Create Role" }));

    await user.type(await screen.findByLabelText(/^Password/), "admin-pw");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => expect(RoleService.updatePermissions).toHaveBeenCalled());
    const [, sentCodes] = vi.mocked(RoleService.updatePermissions).mock.calls[0];
    expect(sentCodes).not.toContain("Samples.Approve");
    expect(sentCodes).toContain("Documents.Approve");
  });

  it("non-QA base type leaves those permissions enabled", async () => {
    const user = userEvent.setup();
    renderPage();

    await advanceToBaseTypeStep(user, "Senior Analyst");
    await user.click(screen.getByRole("radio", { name: "Analyst" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    expect(permissionCheckbox("Samples.Approve").disabled).toBe(false);
    expect(permissionCheckbox("Documents.Approve").disabled).toBe(false);
    expect(screen.queryByText("Not available to a Quality Assurance role")).toBeNull();
  });

  it("server refusal on save is shown", async () => {
    const user = userEvent.setup();
    vi.mocked(RoleService.updatePermissions).mockRejectedValue({
      response: { data: { message: "A Quality Assurance role cannot hold: Samples.Approve." } }
    });
    renderPage();

    await advanceToBaseTypeStep(user);
    await user.click(screen.getByRole("radio", { name: "Quality Assurance" }));
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.click(screen.getByRole("button", { name: "Next" }));
    await user.type(screen.getByLabelText(/Reason for Creating This Role/), "Add QA delegate");
    await user.click(screen.getByRole("button", { name: "Create Role" }));

    await user.type(await screen.findByLabelText(/^Password/), "admin-pw");
    await user.click(screen.getByRole("button", { name: "Sign" }));

    expect(await screen.findByText("A Quality Assurance role cannot hold: Samples.Approve.")).toBeTruthy();
    expect(screen.queryByText("Role detail page")).toBeNull();
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    expect(await screen.findByRole("button", { name: "Create Role" })).toBeTruthy();
  });

  it("forbidden-codes load failure is reported and the page still works", async () => {
    const user = userEvent.setup();
    vi.mocked(RoleService.getQaForbiddenCodes).mockRejectedValue(new Error("Network error"));
    renderPage();

    expect(await screen.findByText(/Could not load/i)).toBeTruthy();

    await advanceToBaseTypeStep(user);
    await user.click(screen.getByRole("radio", { name: "Quality Assurance" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    expect(permissionCheckbox("Documents.Approve").disabled).toBe(false);
  });
});
