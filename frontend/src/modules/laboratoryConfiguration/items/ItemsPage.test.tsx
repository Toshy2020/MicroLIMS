import { describe, it, expect, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../theme";
import { ItemsPage } from "./ItemsPage";

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

const renderPage = () => render(<ThemeProvider theme={lightTheme}><ItemsPage /></ThemeProvider>);

describe("ItemsPage", () => {
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
});
