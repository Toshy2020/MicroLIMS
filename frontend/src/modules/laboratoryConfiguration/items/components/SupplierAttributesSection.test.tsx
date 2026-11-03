import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../../theme";
import { SupplierAttributesSection } from "./SupplierAttributesSection";
import { SupplierAttributeService } from "../services/SupplierAttributeService";
import { PERMISSIONS } from "../../../../routes/routes";

let mockPermissions: string[] = [];

vi.mock("../../../../contexts/AuthContext", () => ({
  useAuth: () => ({
    fullName: "Spec Editor",
    username: "speceditor",
    role: "Admin",
    permissions: mockPermissions,
  }),
  useHasPermission: (perm: string | string[]) => {
    if (Array.isArray(perm)) return perm.some((p) => mockPermissions.includes(p));
    return mockPermissions.includes(perm);
  },
}));

vi.mock("../services/SupplierAttributeService", () => ({
  SupplierAttributeService: {
    listForItem: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    delete: vi.fn(),
  },
}));

describe("SupplierAttributesSection", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPermissions = [];
  });

  afterEach(() => {
    cleanup();
  });

  const dummyGrades = ["USP", "BP"];

  it("Supplier add sends name, value and grade", async () => {
    mockPermissions = [PERMISSIONS.MASTER_DATA_MANAGE];
    vi.mocked(SupplierAttributeService.listForItem).mockResolvedValue([]);
    vi.mocked(SupplierAttributeService.create).mockResolvedValue({
      id: 201,
      itemId: 1,
      grade: "USP",
      name: "Loss on Drying",
      statedValue: "< 0.5%",
      sortOrder: 0,
    });

    render(
      <ThemeProvider theme={lightTheme}>
        <SupplierAttributesSection itemId={1} grades={dummyGrades} />
      </ThemeProvider>
    );

    // Look for Add button
    const addBtn = await screen.findByRole("button", { name: /Add Attribute|Add Supplier Attribute/i });
    fireEvent.click(addBtn);

    // Fill dialog fields
    fireEvent.change(screen.getByLabelText(/Name/i), { target: { value: "Loss on Drying" } });
    fireEvent.change(screen.getByLabelText(/Stated Value/i), { target: { value: "< 0.5%" } });
    fireEvent.change(screen.getByLabelText(/Grade/i), { target: { value: "USP" } });

    // Submit dialog
    const saveBtn = screen.getByRole("button", { name: /Save|Submit/i });
    fireEvent.click(saveBtn);

    await waitFor(() => {
      expect(SupplierAttributeService.create).toHaveBeenCalledWith(1, {
        name: "Loss on Drying",
        statedValue: "< 0.5%",
        grade: "USP",
      });
    });
  });

  it("Supplier edit updates name, value and grade", async () => {
    mockPermissions = [PERMISSIONS.MASTER_DATA_MANAGE];
    const existing = {
      id: 202,
      itemId: 1,
      grade: "BP",
      name: "Appearance",
      statedValue: "White crystalline powder",
      sortOrder: 1,
    };
    vi.mocked(SupplierAttributeService.listForItem).mockResolvedValue([existing]);
    vi.mocked(SupplierAttributeService.update).mockResolvedValue({
      ...existing,
      statedValue: "Off-white crystalline powder",
    });

    render(
      <ThemeProvider theme={lightTheme}>
        <SupplierAttributesSection itemId={1} grades={dummyGrades} />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByText("White crystalline powder")).toBeTruthy();
    });

    // Click Edit button
    const editBtn = screen.getByRole("button", { name: /Edit/i });
    fireEvent.click(editBtn);

    // Change value
    fireEvent.change(screen.getByLabelText(/Stated Value/i), {
      target: { value: "Off-white crystalline powder" },
    });

    // Click Save
    fireEvent.click(screen.getByRole("button", { name: /Save/i }));

    await waitFor(() => {
      expect(SupplierAttributeService.update).toHaveBeenCalledWith(202, {
        itemId: 1,
        name: "Appearance",
        statedValue: "Off-white crystalline powder",
        grade: "BP",
        sortOrder: 1,
      });
    });
  });

  it("Supplier delete calls delete service", async () => {
    mockPermissions = [PERMISSIONS.MASTER_DATA_MANAGE];
    vi.spyOn(window, "confirm").mockReturnValue(true);

    const existing = {
      id: 203,
      itemId: 1,
      grade: null,
      name: "Mesh size",
      statedValue: "80 mesh",
      sortOrder: 2,
    };
    vi.mocked(SupplierAttributeService.listForItem).mockResolvedValue([existing]);
    vi.mocked(SupplierAttributeService.delete).mockResolvedValue();

    render(
      <ThemeProvider theme={lightTheme}>
        <SupplierAttributesSection itemId={1} grades={dummyGrades} />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByText("Mesh size")).toBeTruthy();
    });

    // Click Delete button
    const deleteBtn = screen.getByRole("button", { name: /Delete/i });
    fireEvent.click(deleteBtn);

    await waitFor(() => {
      expect(SupplierAttributeService.delete).toHaveBeenCalledWith(203);
    });
  });

  it("Non-spec-editor cannot see add, edit, or delete buttons", async () => {
    mockPermissions = []; // no permissions
    const existing = {
      id: 204,
      itemId: 1,
      grade: null,
      name: "Color",
      statedValue: "White",
      sortOrder: 0,
    };
    vi.mocked(SupplierAttributeService.listForItem).mockResolvedValue([existing]);

    render(
      <ThemeProvider theme={lightTheme}>
        <SupplierAttributesSection itemId={1} grades={dummyGrades} />
      </ThemeProvider>
    );

    await waitFor(() => {
      expect(screen.getByText("Color")).toBeTruthy();
    });

    // Add button should not be present
    expect(screen.queryByRole("button", { name: /Add Attribute|Add Supplier Attribute/i })).toBeNull();
    // Edit and Delete buttons should not be present
    expect(screen.queryByRole("button", { name: /Edit/i })).toBeNull();
    expect(screen.queryByRole("button", { name: /Delete/i })).toBeNull();
  });
});
