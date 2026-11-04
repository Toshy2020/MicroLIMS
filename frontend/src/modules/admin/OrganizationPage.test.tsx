import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";

const auth = { permissions: ["Documents.ConfigManage", "Users.Manage"] as string[] };
vi.mock("../../contexts/AuthContext", () => ({ useAuth: () => auth }));
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const dc = vi.hoisted(() => ({
  getDepartments: vi.fn(),
  createDepartment: vi.fn(),
  updateDepartment: vi.fn(),
  createSection: vi.fn(),
  updateSection: vi.fn(),
  getDepartmentImpact: vi.fn(),
  getSectionImpact: vi.fn()
}));
vi.mock("../documentControl/services/documentControlService", () => ({ documentControlService: dc }));

const users = vi.hoisted(() => ({ getAll: vi.fn() }));
vi.mock("../users/services/UserService", () => ({ UserService: users }));

// The memberships editor is the Users page's own dialog; here only that it opens for the right user matters.
vi.mock("../users/dialogs/UserSectionsDialog", () => ({
  UserSectionsDialog: ({ open, user }: { open: boolean; user: { username: string } | null }) =>
    open ? <div role="dialog" aria-label="Membership editor">memberships of {user?.username}</div> : null
}));

import { OrganizationPage } from "./OrganizationPage";

const departments = [
  {
    id: 1, version: 7, code: "QC", name: "Quality Control", isActive: true,
    sections: [
      { id: 11, version: 3, departmentId: 1, departmentName: "Quality Control", name: "Microbiology", isActive: true },
      { id: 12, version: 4, departmentId: 1, departmentName: "Quality Control", name: "Retired lab", isActive: false }
    ]
  },
  { id: 2, version: 1, code: "PD", name: "Production", isActive: false, sections: [] }
];

function renderPage() {
  return render(<PinnedLightTheme><OrganizationPage /></PinnedLightTheme>);
}

function fillReason(text = "Reorganisation approved") {
  fireEvent.change(screen.getByLabelText(/reason for change/i), { target: { value: text } });
}

describe("OrganizationPage", () => {
  beforeEach(() => {
    auth.permissions = ["Documents.ConfigManage", "Users.Manage"];
    dc.getDepartments.mockResolvedValue(departments);
    dc.createDepartment.mockResolvedValue({});
    dc.updateDepartment.mockResolvedValue({});
    dc.createSection.mockResolvedValue({});
    dc.updateSection.mockResolvedValue({});
    dc.getDepartmentImpact.mockResolvedValue({ activeDocuments: 0, activeMemberships: 0, openWork: 0, hasReferences: false });
    dc.getSectionImpact.mockResolvedValue({ activeDocuments: 0, activeMemberships: 0, openWork: 0, hasReferences: false });
    users.getAll.mockResolvedValue([
      { id: 5, fullName: "Jane Analyst", username: "jane", role: { id: 1, name: "Analyst", type: "Analyst" }, isActive: true },
      { id: 6, fullName: "Omar Reviewer", username: "omar", role: { id: 2, name: "Reviewer", type: "Reviewer" }, isActive: true }
    ]);
  });
  afterEach(() => { cleanup(); vi.clearAllMocks(); });

  it("lists departments and sections including inactive ones", async () => {
    renderPage();
    expect(await screen.findByText("QC: Quality Control")).toBeTruthy();
    expect(dc.getDepartments).toHaveBeenCalledWith(true);
    expect(screen.getByText("PD: Production")).toBeTruthy();
    const sections = screen.getByRole("table", { name: "Sections of Quality Control" });
    expect(within(sections).getByText("Microbiology")).toBeTruthy();
    expect(within(sections).getByText("Retired lab")).toBeTruthy();
    expect(within(sections).getAllByText("Inactive")).toHaveLength(1);
  });

  it("creates a department only with a reason", async () => {
    renderPage();
    await screen.findByText("QC: Quality Control");
    fireEvent.click(screen.getByRole("button", { name: /^add department$/i }));

    fireEvent.change(screen.getByLabelText(/^code/i), { target: { value: "MB" } });
    fireEvent.change(screen.getByLabelText(/^name/i), { target: { value: "Microbiology" } });
    const save = screen.getByRole("button", { name: "Save" }) as HTMLButtonElement;
    expect(save.disabled).toBe(true); // no reason yet

    fillReason();
    expect(save.disabled).toBe(false);
    fireEvent.click(save);
    await waitFor(() => expect(dc.createDepartment).toHaveBeenCalledWith({ code: "MB", name: "Microbiology", reason: "Reorganisation approved" }));
  });

  it("renames a section with the reason and the row version", async () => {
    renderPage();
    await screen.findByText("QC: Quality Control");
    fireEvent.click(screen.getByRole("button", { name: "Rename section Microbiology" }));
    fireEvent.change(screen.getByLabelText(/^name/i), { target: { value: "Microbiology Lab" } });
    fillReason("Naming convention");
    fireEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(dc.updateSection).toHaveBeenCalledWith(
      11, { name: "Microbiology Lab", isActive: true, reason: "Naming convention" }, 3));
  });

  it("adds a section under a department", async () => {
    renderPage();
    await screen.findByText("QC: Quality Control");
    fireEvent.click(screen.getAllByRole("button", { name: "Add section" })[0]);
    fireEvent.change(screen.getByLabelText(/^name/i), { target: { value: "Chemistry" } });
    fillReason();
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    await waitFor(() => expect(dc.createSection).toHaveBeenCalledWith({ departmentId: 1, name: "Chemistry", reason: "Reorganisation approved" }));
  });

  it("deactivates an unused department after a reason, without a confirmation flag", async () => {
    renderPage();
    await screen.findByText("QC: Quality Control");
    fireEvent.click(screen.getByRole("button", { name: "Deactivate department QC" }));

    expect(await screen.findByText(/nothing refers to it/i)).toBeTruthy();
    fillReason("Closed");
    fireEvent.click(screen.getByRole("button", { name: "Deactivate" }));
    await waitFor(() => expect(dc.updateDepartment).toHaveBeenCalledWith(
      1, { name: "Quality Control", isActive: false, reason: "Closed", confirmDeactivation: false }, 7));
  });

  it("warns with the counts when a department is still in use and sends the confirmation", async () => {
    dc.getDepartmentImpact.mockResolvedValue({ activeDocuments: 12, activeMemberships: 5, openWork: 3, hasReferences: true });
    renderPage();
    await screen.findByText("QC: Quality Control");
    fireEvent.click(screen.getByRole("button", { name: "Deactivate department QC" }));

    const warning = await screen.findByText(/still in use:/i);
    expect(warning.textContent).toContain("12");
    expect(warning.textContent).toContain("5");
    expect(warning.textContent).toContain("3");
    expect(warning.textContent).toMatch(/never be deleted/i);

    const confirm = screen.getByRole("button", { name: "Deactivate anyway" }) as HTMLButtonElement;
    expect(confirm.disabled).toBe(true); // reason first
    fillReason("Merged into Production");
    fireEvent.click(confirm);
    await waitFor(() => expect(dc.updateDepartment).toHaveBeenCalledWith(
      1, { name: "Quality Control", isActive: false, reason: "Merged into Production", confirmDeactivation: true }, 7));
  });

  it("checks a section's impact too", async () => {
    dc.getSectionImpact.mockResolvedValue({ activeDocuments: 2, activeMemberships: 0, openWork: 0, hasReferences: true });
    renderPage();
    await screen.findByText("QC: Quality Control");
    fireEvent.click(screen.getByRole("button", { name: "Deactivate section Microbiology" }));
    expect(await screen.findByText(/still in use:/i)).toBeTruthy();
    expect(dc.getSectionImpact).toHaveBeenCalledWith(11);
  });

  it("activates an inactive department with only a reason", async () => {
    renderPage();
    await screen.findByText("PD: Production");
    fireEvent.click(screen.getByRole("button", { name: "Activate department PD" }));
    fillReason("Reopened");
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    await waitFor(() => expect(dc.updateDepartment).toHaveBeenCalledWith(
      2, { name: "Production", isActive: true, reason: "Reopened", confirmDeactivation: false }, 1));
    expect(dc.getDepartmentImpact).not.toHaveBeenCalled();
  });

  it("shows the server's message when a change is refused", async () => {
    dc.createDepartment.mockRejectedValue({ isAxiosError: true, message: "x", response: { data: { message: "Department Code 'MB' already exists." } } });
    renderPage();
    await screen.findByText("QC: Quality Control");
    fireEvent.click(screen.getByRole("button", { name: /^add department$/i }));
    fireEvent.change(screen.getByLabelText(/^code/i), { target: { value: "MB" } });
    fireEvent.change(screen.getByLabelText(/^name/i), { target: { value: "Microbiology" } });
    fillReason();
    fireEvent.click(screen.getByRole("button", { name: "Save" }));
    expect(await screen.findByText("Department Code 'MB' already exists.")).toBeTruthy();
  });

  it("opens the Users page's membership editor for a user", async () => {
    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "Edit memberships of omar" }));
    expect(screen.getByRole("dialog", { name: "Membership editor" }).textContent).toContain("omar");
  });

  it("filters the user list", async () => {
    renderPage();
    await screen.findByText(/Jane Analyst/);
    fireEvent.change(screen.getByLabelText(/find a user/i), { target: { value: "omar" } });
    expect(screen.queryByText(/Jane Analyst/)).toBeNull();
    expect(screen.getByText(/Omar Reviewer/)).toBeTruthy();
  });

  it("explains that editing memberships needs Users.Manage and does not load users", async () => {
    auth.permissions = ["Documents.ConfigManage"];
    renderPage();
    expect(await screen.findByText(/needs the Users.Manage permission/i)).toBeTruthy();
    expect(users.getAll).not.toHaveBeenCalled();
    expect(screen.queryByRole("button", { name: /edit memberships/i })).toBeNull();
  });
});
