import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material";
import { MemoryRouter } from "react-router-dom";
import { lightTheme } from "../../../theme";
import { ItemsPage } from "./ItemsPage";
import { ItemService } from "./services/ItemService";

const perms = vi.hoisted(() => ({ list: [] as string[] }));
vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ fullName: "QC", username: "qc", role: "QcManager", permissions: perms.list }),
  useHasPermission: (code: string | string[]) =>
    (Array.isArray(code) ? code : [code]).some((c) => perms.list.includes(c)),
}));

const item = vi.hoisted(() => ({
  id: 1, name: "Riboflavin", code: "RM-1", category: "RawMaterial", sopNumber: "", isActive: true,
  assignedTests: [], specifications: [], version: 1,
}));
vi.mock("./services/ItemService", () => ({
  ItemService: { getAll: vi.fn().mockResolvedValue([item]), create: vi.fn(), update: vi.fn(), remove: vi.fn(), freeze: vi.fn(), unfreeze: vi.fn() },
}));
vi.mock("./services/ItemDocumentService", () => ({
  ItemDocumentService: { getDocumentsForItem: vi.fn().mockResolvedValue([]) },
}));

const renderPage = () => render(<MemoryRouter><ThemeProvider theme={lightTheme}><ItemsPage /></ThemeProvider></MemoryRouter>);

describe("ItemsPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(ItemService.getAll).mockResolvedValue([item] as never);
  });

  afterEach(cleanup);

  it("shows no create, edit, freeze or delete control to a user who can only give the final approval", async () => {
    perms.list = ["tests.not-applicable.final-approve"];
    renderPage();
    await waitFor(() => expect(screen.getByText("Riboflavin")).toBeTruthy());
    expect(screen.queryByText("Add Item")).toBeNull();
    expect(screen.queryByLabelText("Edit Riboflavin")).toBeNull();
    expect(screen.queryByLabelText("Freeze Riboflavin")).toBeNull();
    expect(screen.queryByLabelText("Delete Riboflavin")).toBeNull();
  });

  it("shows them to an item manager", async () => {
    perms.list = ["Items.Manage"];
    renderPage();
    await waitFor(() => expect(screen.getByText("Riboflavin")).toBeTruthy());
    expect(screen.getAllByText("Add Item").length).toBeGreaterThan(0);
    expect(screen.getByLabelText("Edit Riboflavin")).toBeTruthy();
    expect(screen.getByLabelText("Delete Riboflavin")).toBeTruthy();
  });

  it("opens ChangeReasonDialog for freeze, unfreeze, and delete", async () => {
    perms.list = ["Items.Manage"];
    vi.mocked(ItemService.getAll)
      .mockResolvedValueOnce([item] as never)
      .mockResolvedValueOnce([{ ...item, isActive: false }] as never)
      .mockResolvedValueOnce([{ ...item, isActive: true }] as never)
      .mockResolvedValueOnce([] as never);
    const user = userEvent.setup();
    renderPage();

    await waitFor(() => expect(screen.getByText("Riboflavin")).toBeTruthy());
    await user.click(screen.getByLabelText("Freeze Riboflavin"));
    await user.type(await screen.findByLabelText(/Reason for change/i), "Discontinued vendor lot 2026");
    await user.click(screen.getByRole("button", { name: "Freeze" }));
    await waitFor(() => expect(ItemService.freeze).toHaveBeenCalledWith(1, "Discontinued vendor lot 2026"));

    await waitFor(() => expect(screen.getByLabelText("Unfreeze Riboflavin")).toBeTruthy());
    await user.click(screen.getByLabelText("Unfreeze Riboflavin"));
    await user.type(await screen.findByLabelText(/Reason for change/i), "Supplier requalified batch");
    await user.click(screen.getByRole("button", { name: "Unfreeze" }));
    await waitFor(() => expect(ItemService.unfreeze).toHaveBeenCalledWith(1, "Supplier requalified batch"));

    await waitFor(() => expect(screen.getByLabelText("Delete Riboflavin")).toBeTruthy());
    await user.click(screen.getByLabelText("Delete Riboflavin"));
    await user.type(await screen.findByLabelText(/Reason for change/i), "Obsolete test item master");
    await user.click(screen.getByRole("button", { name: "Delete" }));
    await waitFor(() => expect(ItemService.remove).toHaveBeenCalledWith(1, "Obsolete test item master"));
  });
});
