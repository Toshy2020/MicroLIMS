import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";

const auth = { permissions: ["Documents.Reassign"] as string[] };
vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => auth,
  useHasPermission: (code: string | string[]) => (Array.isArray(code) ? code : [code]).some((c) => auth.permissions.includes(c))
}));
vi.mock("sonner", () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const service = vi.hoisted(() => ({
  getQueue: vi.fn(),
  getCount: vi.fn(),
  getCandidates: vi.fn(),
  reassign: vi.fn()
}));
vi.mock("../services/reassignmentService", () => ({ reassignmentService: service }));

import { ReassignmentsPage } from "./ReassignmentsPage";
import { ReassignmentsButton } from "../components/ReassignmentsButton";

const item = (over: Record<string, unknown> = {}) => ({
  itemType: "ReviewTask", itemId: 1, departmentId: 1, departmentName: "Quality Control", documentMasterId: 10,
  documentCode: "SOP-001", documentTitle: "Cleaning of incubators", revisionNumber: "02",
  roleNeeded: "Technical reviewer", requiredPermission: "Documents.Review",
  holderLabel: "Inactive user", holderName: "Ivan Inactive",
  inactiveSince: new Date(Date.now() - 10 * 86_400_000).toISOString(), isOverdue: false, ...over
});

const queue = (items: ReturnType<typeof item>[], over: Record<string, unknown> = {}) => ({
  items, total: items.length, overdue: items.filter((i) => i.isOverdue).length, escalationDays: 7, ...over
});

function renderPage() {
  return render(<PinnedLightTheme><MemoryRouter><ReassignmentsPage /></MemoryRouter></PinnedLightTheme>);
}

describe("ReassignmentsPage", () => {
  beforeEach(() => {
    service.getQueue.mockResolvedValue(queue([
      item({ isOverdue: true }),
      item({ itemType: "Custodian", itemId: 10, documentCode: "SOP-002", documentTitle: "Gowning", revisionNumber: null, roleNeeded: "Custodian", requiredPermission: "Documents.DraftEdit", departmentId: 2, departmentName: "Production" })
    ]));
    service.getCandidates.mockResolvedValue([
      { userId: 7, fullName: "Rita Reviewer", jobTitle: "QA", roleName: "Reviewer" }
    ]);
    service.reassign.mockResolvedValue(undefined);
  });
  afterEach(() => { cleanup(); vi.clearAllMocks(); });

  it("lists the queue, showing the holder as an inactive user with the name for the person handing over", async () => {
    renderPage();
    const table = await screen.findByRole("table", { name: /needing reassignment/i });
    const rows = within(table).getAllByRole("row");
    expect(rows).toHaveLength(3); // header + 2
    expect(within(rows[1]).getByText("SOP-001")).toBeTruthy();
    expect(within(rows[1]).getByText("Technical reviewer")).toBeTruthy();
    expect(within(rows[1]).getByText("Inactive user")).toBeTruthy();
    expect(within(rows[1]).getByText("Ivan Inactive")).toBeTruthy();
    expect(within(rows[1]).getByText("Overdue")).toBeTruthy();
    expect(within(rows[2]).queryByText("Overdue")).toBeNull();
    expect(screen.getByText(/2 item\(s\) waiting, 1 overdue/)).toBeTruthy();
    expect(screen.getByText(/nothing is reassigned automatically/i)).toBeTruthy();
  });

  it("filters by department", async () => {
    renderPage();
    await screen.findByRole("table");
    fireEvent.mouseDown(screen.getByLabelText("Department"));
    fireEvent.click(await screen.findByRole("option", { name: "Production" }));
    const rows = within(screen.getByRole("table")).getAllByRole("row");
    expect(rows).toHaveLength(2);
    expect(screen.getByText("SOP-002")).toBeTruthy();
    expect(screen.queryByText("SOP-001")).toBeNull();
  });

  it("says so when nothing needs reassignment", async () => {
    service.getQueue.mockResolvedValue(queue([]));
    renderPage();
    expect(await screen.findByText("Nothing is waiting for reassignment.")).toBeTruthy();
    expect(screen.getByText("No responsibilities need reassignment.")).toBeTruthy();
  });

  it("reassigns to an eligible user with a reason", async () => {
    renderPage();
    const table = await screen.findByRole("table");
    fireEvent.click(within(within(table).getAllByRole("row")[1]).getByRole("button", { name: "Reassign" }));

    expect(await screen.findByText(/must be an active member of/i)).toBeTruthy();
    expect(service.getCandidates).toHaveBeenCalledWith("ReviewTask", 1);
    const dialog = screen.getByRole("dialog");
    const go = within(dialog).getByRole("button", { name: "Reassign" }) as HTMLButtonElement;
    expect(go.disabled).toBe(true);

    fireEvent.mouseDown(within(dialog).getByLabelText(/new holder/i));
    fireEvent.click(await screen.findByRole("option", { name: /Rita Reviewer/ }));
    expect(go.disabled).toBe(true); // still no reason
    fireEvent.change(within(dialog).getByLabelText(/^reason/i), { target: { value: "Ivan left the company" } });
    expect(go.disabled).toBe(false);
    fireEvent.click(go);

    await waitFor(() => expect(service.reassign).toHaveBeenCalledWith({ itemType: "ReviewTask", itemId: 1, newUserId: 7, reason: "Ivan left the company" }));
    await waitFor(() => expect(service.getQueue).toHaveBeenCalledTimes(2)); // reloaded
  });

  it("explains when nobody is eligible", async () => {
    service.getCandidates.mockResolvedValue([]);
    renderPage();
    const table = await screen.findByRole("table");
    fireEvent.click(within(within(table).getAllByRole("row")[1]).getByRole("button", { name: "Reassign" }));
    expect(await screen.findByText(/nobody is eligible/i)).toBeTruthy();
  });

  it("shows the server's refusal and keeps the dialog open", async () => {
    service.reassign.mockRejectedValue({ isAxiosError: true, message: "x", response: { data: { message: "User 'Rita' is not a member of the department." } } });
    renderPage();
    const table = await screen.findByRole("table");
    fireEvent.click(within(within(table).getAllByRole("row")[1]).getByRole("button", { name: "Reassign" }));
    const dialog = await screen.findByRole("dialog");
    fireEvent.mouseDown(await within(dialog).findByLabelText(/new holder/i));
    fireEvent.click(await screen.findByRole("option", { name: /Rita Reviewer/ }));
    fireEvent.change(within(dialog).getByLabelText(/^reason/i), { target: { value: "Because" } });
    fireEvent.click(within(dialog).getByRole("button", { name: "Reassign" }));
    expect(await screen.findByText("User 'Rita' is not a member of the department.")).toBeTruthy();
    expect(screen.getByRole("dialog")).toBeTruthy();
  });
});

describe("ReassignmentsButton", () => {
  beforeEach(() => { service.getCount.mockResolvedValue({ total: 4, overdue: 1 }); });
  afterEach(() => { cleanup(); vi.clearAllMocks(); });

  it("shows the count to people who can act", async () => {
    auth.permissions = ["Documents.Reassign"];
    render(<PinnedLightTheme><MemoryRouter><ReassignmentsButton /></MemoryRouter></PinnedLightTheme>);
    expect(await screen.findByText("4")).toBeTruthy();
    expect(screen.getByRole("button", { name: /needs reassignment/i })).toBeTruthy();
  });

  it("is absent, and asks nothing, for everyone else", () => {
    auth.permissions = ["Documents.Register"];
    render(<PinnedLightTheme><MemoryRouter><ReassignmentsButton /></MemoryRouter></PinnedLightTheme>);
    expect(screen.queryByRole("button", { name: /needs reassignment/i })).toBeNull();
    expect(service.getCount).not.toHaveBeenCalled();
  });
});
