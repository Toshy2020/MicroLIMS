import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { resetUiStateCache } from "../../../hooks/useUiState";
import { uiStateService } from "../../../services/uiStateService";
import { EquipmentPage } from "./EquipmentPage";
import { EquipmentConfigurationService } from "./services/EquipmentConfigurationService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import * as laboratorySectionService from "../../../services/laboratorySectionService";
import { AuthProvider } from "../../../contexts/AuthContext";
import { resetDraftDb, signInForTest } from "../../../services/drafts/draftTestUtils";
import { masterDataUsageService } from "../../../services/masterDataUsageService";
import { clickNext, clickSave } from "./equipmentWizard/equipmentWizard.testUtils";

vi.mock("../../../services/uiStateService", () => ({
  uiStateService: { getAll: vi.fn(), set: vi.fn(), remove: vi.fn() }
}));

vi.mock("./services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getConfiguredSummary: vi.fn(),
    getEquipmentList: vi.fn(),
    getAutoclavePrograms: vi.fn(),
    getSetPointHistory: vi.fn(),
    createEquipment: vi.fn(),
    updateEquipment: vi.fn(),
    updateSetPoint: vi.fn(),
    saveAutoclaveProgram: vi.fn()
  }
}));

vi.mock("../../../services/masterDataUsageService", () => ({
  masterDataUsageService: { getUsage: vi.fn() }
}));

vi.mock("../../inventory/equipment/services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: {
    getAll: vi.fn()
  }
}));

vi.mock("../../../services/laboratorySectionService", () => {
  const getSections = vi.fn();
  const getMySections = vi.fn();
  return {
    getSections,
    getMySections,
    laboratorySectionService: {
      getSections,
      getMySections
    }
  };
});

beforeEach(() => {
  resetUiStateCache();
  vi.mocked(uiStateService.getAll).mockResolvedValue({});
  vi.mocked(uiStateService.set).mockResolvedValue(undefined);
});

afterEach(() => {
  cleanup();
  resetUiStateCache();
  vi.clearAllMocks();
});

const renderPage = () =>
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <EquipmentPage />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );

describe("EquipmentPage", () => {
  beforeEach(async () => {
    await resetDraftDb();
    signInForTest(7);
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue({
      type: "Equipment",
      id: 10,
      openWorkCount: 0,
      historicalCount: 0,
      lines: []
    });
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology", departmentId: 1, departmentCode: "QC", departmentName: "QC" }
    ]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      { sectionId: 1, sectionCode: "MICRO", sectionName: "Microbiology", departmentId: 1, departmentCode: "QC", departmentName: "QC" }
    ]);
    vi.mocked(EquipmentConfigurationService.getConfiguredSummary).mockResolvedValue([
      {
        id: 10,
        version: 3,
        name: "Incubator 1",
        code: "INC-01",
        type: "Incubator",
        location: "Room 101",
        setPointTemperature: 32.5,
        calibrationDueDate: null,
        equipmentInventoryId: null,
        inventoryStatus: "InService",
        inventoryLocation: "Room 101",
        configuredProgramCount: 0,
        serialNumber: null,
        manufacturerName: "Binder",
        vendor: "Binder",
        sectionId: 1
      }
    ]);
    vi.mocked(EquipmentConfigurationService.getEquipmentList).mockResolvedValue([
      {
        id: 10,
        version: 3,
        name: "Incubator 1",
        code: "INC-01",
        type: "Incubator",
        location: "Room 101",
        setPointTemperature: 32.5,
        calibrationDueDate: null,
        sectionId: 1,
        equipmentInventoryId: null,
        vendor: "Binder",
        cdsSoftware: null,
        connectionSettings: null
      }
    ]);
    vi.mocked(EquipmentConfigurationService.getAutoclavePrograms).mockResolvedValue([]);
    vi.mocked(EquipmentConfigurationService.getSetPointHistory).mockResolvedValue([]);
    vi.mocked(EquipmentInventoryService.getAll).mockResolvedValue([
      {
        id: 42,
        version: 1,
        instrumentType: "Binder Incubator",
        manufacturerName: "Binder",
        serialNumber: "SN-42",
        firmwareVersion: null,
        code: "AST-INC-42",
        location: "Room 101",
        calibrationDueDate: null,
        status: "InService",
        isCalibrationOverdue: false,
        sectionId: 1,
        codeLocked: false
      }
    ]);
    vi.mocked(EquipmentConfigurationService.updateEquipment).mockResolvedValue({
      id: 10,
      version: 4,
      name: "Incubator 1",
      code: "INC-01",
      type: "Incubator",
      location: "Room 101",
      setPointTemperature: 32.5,
      calibrationDueDate: null,
      sectionId: 1,
      equipmentInventoryId: 42,
      vendor: "Binder",
      cdsSoftware: null,
      connectionSettings: null
    });
  });

  it("saves the chosen asset link", async () => {
    const user = userEvent.setup();
    renderPage();

    const editButton = await screen.findByRole("button", { name: "Edit Equipment" });
    expect(EquipmentInventoryService.getAll).toHaveBeenCalledWith(expect.any(AbortSignal));

    await user.click(editButton);

    const dialog = await screen.findByRole("dialog");
    await clickNext(user, dialog);
    await clickNext(user, dialog);
    const picker = within(dialog).getByRole("combobox", { name: "Asset register entry" });
    await user.click(picker);

    const listbox = await screen.findByRole("listbox");
    await user.click(within(listbox).getByText("AST-INC-42 — Binder Incubator"));

    await clickNext(user, dialog);
    await clickSave(user, dialog);

    await waitFor(() => {
      expect(EquipmentConfigurationService.updateEquipment).toHaveBeenCalledWith(
        10,
        expect.objectContaining({
          equipmentInventoryId: 42
        }),
        3
      );
    });
  });

  it("clears an existing asset link and surfaces inventory load failures", async () => {
    vi.mocked(EquipmentConfigurationService.getEquipmentList).mockResolvedValue([
      {
        id: 10,
        version: 3,
        name: "Incubator 1",
        code: "INC-01",
        type: "Incubator",
        location: "Room 101",
        setPointTemperature: 32.5,
        calibrationDueDate: null,
        sectionId: 1,
        equipmentInventoryId: 42,
        vendor: "Binder",
        cdsSoftware: null,
        connectionSettings: null
      }
    ]);
    vi.mocked(EquipmentConfigurationService.updateEquipment).mockResolvedValue({
      id: 10,
      version: 4,
      name: "Incubator 1",
      code: "INC-01",
      type: "Incubator",
      location: "Room 101",
      setPointTemperature: 32.5,
      calibrationDueDate: null,
      sectionId: 1,
      equipmentInventoryId: null,
      vendor: "Binder",
      cdsSoftware: null,
      connectionSettings: null
    });

    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Edit Equipment" }));

    const dialog = await screen.findByRole("dialog");
    await clickNext(user, dialog);
    await clickNext(user, dialog);
    const clearButton = within(dialog).getByRole("button", { name: "Clear" });
    await user.click(clearButton);
    await clickNext(user, dialog);
    await clickSave(user, dialog);

    await waitFor(() => {
      expect(EquipmentConfigurationService.updateEquipment).toHaveBeenCalledWith(
        10,
        expect.objectContaining({
          equipmentInventoryId: null
        }),
        3
      );
    });

    cleanup();
    vi.mocked(EquipmentInventoryService.getAll).mockRejectedValueOnce(new Error("Asset register unavailable"));
    renderPage();

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Asset register unavailable");
  });

  it("opens ChangeReasonDialog and retries EquipmentConfigurationService.updateEquipment when reason is required", async () => {
    vi.mocked(EquipmentConfigurationService.updateEquipment)
      .mockRejectedValueOnce({
        response: {
          status: 400,
          data: {
            message: "Reason required.",
            fieldErrors: [{ field: "reason", message: "Reason is required." }]
          }
        }
      })
      .mockResolvedValueOnce({
        id: 10,
        version: 4,
        name: "Incubator 1",
        code: "INC-01",
        type: "Incubator",
        location: "Room 101",
        setPointTemperature: 32.5,
        calibrationDueDate: null,
        sectionId: 1,
        equipmentInventoryId: null,
        vendor: "Binder",
        cdsSoftware: null,
        connectionSettings: null
      });

    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Edit Equipment" }));
    const dialog = await screen.findByRole("dialog");
    await clickNext(user, dialog);
    await clickNext(user, dialog);
    await clickNext(user, dialog);
    await clickSave(user, dialog);

    await user.type(await screen.findByLabelText(/Reason for change/i), "Recalibrated thermal probe");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(EquipmentConfigurationService.updateEquipment).toHaveBeenCalledTimes(2));
    expect(vi.mocked(EquipmentConfigurationService.updateEquipment).mock.calls[1][3]).toBe(
      "Recalibrated thermal probe"
    );
  });

  it("opens New from existing for the selected equipment with an empty code", async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "New from existing" }));
    const dialog = await screen.findByRole("dialog");
    expect(within(dialog).getByText(/New Equipment from "INC-01"/)).toBeTruthy();
    expect((within(dialog).getByLabelText(/^Equipment Code/) as HTMLInputElement).value).toBe("");
    expect((within(dialog).getByLabelText(/^Equipment Name/) as HTMLInputElement).value).toBe("Incubator 1");
    expect(EquipmentConfigurationService.createEquipment).not.toHaveBeenCalled();
  });

  it("lists the configured equipment and shows the selected incubator set point", async () => {
    renderPage();

    expect(await screen.findByRole("button", { name: "Edit Equipment" })).toBeTruthy();
    expect(screen.getAllByText("Incubator 1").length).toBeGreaterThan(0);
    expect(screen.getAllByText("INC-01").length).toBeGreaterThan(0);
    expect(screen.getByText("32.5 °C")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Edit Set Point" })).toBeTruthy();
  });

  it("changes an incubator set point only with a reason, through updateSetPoint", async () => {
    vi.mocked(EquipmentConfigurationService.updateSetPoint).mockResolvedValue();
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Edit Set Point" }));
    const dialog = await screen.findByRole("dialog");
    const setPoint = within(dialog).getByLabelText(/New Set Point/);
    await user.clear(setPoint);
    await user.type(setPoint, "35");

    await user.click(within(dialog).getByRole("button", { name: "Save Changes" }));
    expect(within(dialog).getByText("Reason for Change is required for audit and ALCOA+ compliance.")).toBeTruthy();
    expect(EquipmentConfigurationService.updateSetPoint).not.toHaveBeenCalled();

    await user.type(within(dialog).getByRole("textbox", { name: /Reason for Change/ }), "Method update");
    await user.click(within(dialog).getByRole("button", { name: "Save Changes" }));

    await waitFor(() => {
      expect(EquipmentConfigurationService.updateSetPoint).toHaveBeenCalledWith(10, {
        newSetPoint: 35,
        reason: "Method update"
      });
    });
  });

  it("adds an autoclave program through saveAutoclaveProgram", async () => {
    vi.mocked(EquipmentConfigurationService.getConfiguredSummary).mockResolvedValue([
      {
        id: 20,
        version: 1,
        name: "Autoclave 1",
        code: "AC-01",
        type: "Autoclave",
        location: "Media prep",
        setPointTemperature: null,
        calibrationDueDate: null,
        equipmentInventoryId: null,
        configuredProgramCount: 0,
        sectionId: 1
      }
    ]);
    vi.mocked(EquipmentConfigurationService.getEquipmentList).mockResolvedValue([]);
    vi.mocked(EquipmentConfigurationService.saveAutoclaveProgram).mockResolvedValue({
      id: 1,
      equipmentId: 20,
      autoclaveCode: "AC-01",
      autoclaveName: "Autoclave 1",
      programCode: "P01",
      programName: "Media",
      loadType: "Media",
      temperature: 121,
      cycleTimeMinutes: 15,
      isActive: true,
      createdByUserId: 1,
      createdAt: "2026-11-01T00:00:00Z",
      lastModifiedByUserId: 1,
      lastModifiedAt: "2026-11-01T00:00:00Z"
    });
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Add Program / Load" }));
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/Program \/ Load Code/), "P01");
    await user.type(within(dialog).getByLabelText(/Program \/ Load Name/), "Media");
    await user.click(within(dialog).getByRole("button", { name: "Save Program" }));

    await waitFor(() => {
      expect(EquipmentConfigurationService.saveAutoclaveProgram).toHaveBeenCalledWith(
        20,
        {
          id: undefined,
          equipmentId: 20,
          programCode: "P01",
          programName: "Media",
          loadType: "Media",
          temperature: 121,
          cycleTimeMinutes: 15,
          isActive: true,
          comment: "Initial program configuration"
        },
        undefined
      );
    });
  });

  it("switches between its tabs and remembers the choice", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByRole("button", { name: "Edit Equipment" });
    expect(screen.getByRole("tab", { selected: true }).textContent).toBe("Configured Equipment");
    await user.click(screen.getByRole("tab", { name: "Configuration History" }));
    expect(screen.getByRole("tab", { selected: true }).textContent).toBe("Configuration History");
    expect(screen.queryByRole("button", { name: "Edit Equipment" })).toBeNull();
    await waitFor(() => expect(uiStateService.set).toHaveBeenCalledWith("nav.tab.equipmentConfiguration", "history"), { timeout: 2000 });
  });

  it("opens the tab used last time", async () => {
    vi.mocked(uiStateService.getAll).mockResolvedValue({ "nav.tab.equipmentConfiguration": "history" });
    renderPage();

    await waitFor(() => expect(screen.getByRole("tab", { selected: true }).textContent).toBe("Configuration History"));
  });
});
