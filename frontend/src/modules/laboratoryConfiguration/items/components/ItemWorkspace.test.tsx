import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../../../theme";
import { ItemWorkspace } from "./ItemWorkspace";
import { Item } from "../services/ItemService";
import { ItemDocumentService } from "../services/ItemDocumentService";
import { ItemPreparationConfigurationService } from "../../../testPreparation/services/ItemPreparationConfigurationService";
import { NotApplicableTestService } from "../services/NotApplicableTestService";
import { SupplierAttributeService } from "../services/SupplierAttributeService";

vi.mock("../../../../contexts/AuthContext", () => ({
  useAuth: () => ({
    fullName: "QA Admin",
    username: "qaadmin",
    role: "Admin",
    permissions: ["tests.not-applicable.manage", "tests.not-applicable.final-approve", "MasterData.Manage"],
  }),
  useHasPermission: () => true,
}));

vi.mock("../services/ItemDocumentService", () => ({
  ItemDocumentService: {
    getDocumentsForItem: vi.fn().mockResolvedValue([]),
  },
  ItemDocumentType: { Sop: "Sop", VerificationReport: "VerificationReport" },
  MaterialDocumentStatus: { Current: "Current", Superseded: "Superseded", Voided: "Voided" },
}));

vi.mock("../../../testPreparation/services/ItemPreparationConfigurationService", () => ({
  ItemPreparationConfigurationService: {
    get: vi.fn().mockResolvedValue(null),
  },
}));

vi.mock("../services/NotApplicableTestService", () => ({
  NotApplicableTestService: {
    listForItem: vi.fn().mockResolvedValue([]),
    create: vi.fn(),
    headApprove: vi.fn(),
    finalApprove: vi.fn(),
    withdraw: vi.fn(),
  },
}));

vi.mock("../services/SupplierAttributeService", () => ({
  SupplierAttributeService: {
    listForItem: vi.fn().mockResolvedValue([]),
    create: vi.fn(),
    update: vi.fn(),
    delete: vi.fn(),
  },
}));

vi.mock("./ItemSpecificationsSection", () => ({
  ItemSpecificationsSection: () => <div data-testid="item-specs-section">Specifications Content</div>,
}));

vi.mock("./ItemPreparationConfigurationSection", () => ({
  ItemPreparationConfigurationSection: () => <div data-testid="prep-config-section">Prep Config Content</div>,
}));

vi.mock("../../../../components/ItemStmPanel", () => ({
  ItemStmPanel: () => <div data-testid="stm-panel">STM Panel</div>,
}));

describe("ItemWorkspace - Supporting Records Tab", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  const dummyItem: Item = {
    id: 42,
    name: "Paracetamol Raw Material",
    code: "RM-PARA-001",
    category: "RawMaterial",
    sopNumber: "SOP-RM-042",
    isActive: true,
    assignedTests: [
      { testCode: "TEST-01", displayName: "Assay by HPLC" },
      { testCode: "TEST-02", displayName: "Loss on Drying" },
    ],
    specifications: [
      {
        id: 1,
        testCode: "TEST-01",
        alertLimit: "",
        actionLimit: "",
        specLimit: "98.0 - 102.0%",
        grade: "USP",
      },
      {
        id: 2,
        testCode: "TEST-02",
        alertLimit: "",
        actionLimit: "",
        specLimit: "< 0.5%",
        grade: "BP",
      },
    ],
  };

  it("renders the Supporting Records tab and switches to it", async () => {
    render(
      <ThemeProvider theme={lightTheme}>
        <ItemWorkspace item={dummyItem} onClose={() => {}} />
      </ThemeProvider>
    );

    // Verify tab exists
    const supportingTab = screen.getByRole("tab", { name: "Supporting Records" });
    expect(supportingTab).toBeTruthy();

    // Click Supporting Records tab
    fireEvent.click(supportingTab);

    // Verify Not-Applicable Tests and Supplier-Stated Attributes sections are rendered
    await waitFor(() => {
      expect(screen.getByText("Not-Applicable Tests")).toBeTruthy();
      expect(screen.getByText("Supplier-Stated Attributes")).toBeTruthy();
    });

    // Verify service calls for itemId 42
    expect(NotApplicableTestService.listForItem).toHaveBeenCalledWith(42);
    expect(SupplierAttributeService.listForItem).toHaveBeenCalledWith(42);
  });
});
