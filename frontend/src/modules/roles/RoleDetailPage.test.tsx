import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { RoleDetailPage } from "./RoleDetailPage";
import { RoleService, RoleDetail } from "./services/RoleService";
import { UserService } from "../users/services/UserService";

vi.mock("./services/RoleService", () => ({
  RoleService: {
    getById: vi.fn(), getAllPermissions: vi.fn(), getSodRules: vi.fn(),
    update: vi.fn(), updatePermissions: vi.fn(), remove: vi.fn()
  }
}));
vi.mock("../users/services/UserService", () => ({ UserService: { getAll: vi.fn() } }));
vi.mock("../../contexts/AuthContext", () => ({
  useAuth: () => ({ userId: 1, fullName: "Admin One", username: "admin", role: "SystemAdministrator" })
}));
// A matrix with one toggle is enough to change the role's permissions.
vi.mock("./components/PermissionMatrix", () => ({
  PermissionMatrix: ({ onToggle }: { onToggle: (code: string, checked: boolean) => void }) => (
    <button onClick={() => onToggle("Audit.View", true)}>Grant audit view</button>
  )
}));

const role: RoleDetail = {
  id: 9, version: 3, name: "QC Trainee", description: "d", type: "Analyst",
  isSystemRole: false, isActive: true, permissionCodes: []
};

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/roles/9"]}>
      <Routes>
        <Route path="/roles/:id" element={<RoleDetailPage />} />
        <Route path="/roles" element={<div>Roles list</div>} />
      </Routes>
    </MemoryRouter>
  );
}

beforeEach(() => {
  vi.mocked(RoleService.getById).mockResolvedValue(role);
  vi.mocked(RoleService.getAllPermissions).mockResolvedValue([]);
  vi.mocked(RoleService.getSodRules).mockResolvedValue({ administrationCodes: [], gxpExecutionAndApprovalCodes: [] });
  vi.mocked(RoleService.update).mockResolvedValue(role);
  vi.mocked(RoleService.updatePermissions).mockResolvedValue(role);
  vi.mocked(RoleService.remove).mockResolvedValue();
  vi.mocked(UserService.getAll).mockResolvedValue([]);
});
afterEach(() => { cleanup(); vi.clearAllMocks(); });

describe("RoleDetailPage reasons and signatures", () => {
  it("will not save an edit until a reason is given", async () => {
    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "Grant audit view" }));

    const save = screen.getByRole("button", { name: "Save Changes" }) as HTMLButtonElement;
    expect(save.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText(/Reason for Change/), { target: { value: "Training access" } });
    expect(save.disabled).toBe(false);
  });

  it("signs a permission edit with the reason and password", async () => {
    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "Grant audit view" }));
    fireEvent.change(screen.getByLabelText(/Reason for Change/), { target: { value: "Training access" } });
    fireEvent.click(screen.getByRole("button", { name: "Save Changes" }));

    expect(RoleService.updatePermissions).not.toHaveBeenCalled();
    await screen.findByText("Electronic Signature");
    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "my-password" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => expect(RoleService.updatePermissions).toHaveBeenCalledWith(9, ["Audit.View"], "Training access", "my-password"));
  });

  it("renames with only the reason, no signature", async () => {
    renderPage();
    const name = await screen.findByLabelText("Name");
    fireEvent.change(name, { target: { value: "QC Trainee 2" } });
    fireEvent.change(screen.getByLabelText(/Reason for Change/), { target: { value: "Naming standard" } });
    fireEvent.click(screen.getByRole("button", { name: "Save Changes" }));

    await waitFor(() => expect(RoleService.update).toHaveBeenCalledWith(9, "QC Trainee 2", "d", "Naming standard", 3));
    expect(screen.queryByText("Electronic Signature")).toBeNull();
    expect(RoleService.updatePermissions).not.toHaveBeenCalled();
  });

  it("deletes a role only with a reason and a signature", async () => {
    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "Delete Role" }));
    const dialog = await screen.findByRole("dialog");
    const next = within(dialog).getByRole("button", { name: "Continue to Sign" }) as HTMLButtonElement;
    expect(next.disabled).toBe(true);

    fireEvent.change(within(dialog).getByLabelText(/Reason for Deletion/), { target: { value: "Role retired" } });
    fireEvent.click(next);

    expect(RoleService.remove).not.toHaveBeenCalled();
    await screen.findByText("Electronic Signature");
    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "my-password" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => expect(RoleService.remove).toHaveBeenCalledWith(9, "Role retired", "my-password"));
  });
});
