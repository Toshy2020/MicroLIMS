import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { EquipmentPage } from "./EquipmentPage";
import { EquipmentConfigurationService } from "./services/EquipmentConfigurationService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import * as laboratorySectionService from "../../../services/laboratorySectionService";

vi.mock("./services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getConfiguredSummary: vi.fn(),
    getEquipmentList: vi.fn(),
    getAutoclavePrograms: vi.fn(),
    getSetPointHistory: vi.fn(),
    createEquipment: vi.fn(),
    updateEquipment: vi.fn()
  }
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

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const renderPage = () =>
  render(
    <PinnedLightTheme>
      <MemoryRouter>
        <EquipmentPage />
      </MemoryRouter>
    </PinnedLightTheme>
  );

describe("EquipmentPage", () => {
  beforeEach(() => {
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
    const picker = within(dialog).getByRole("combobox", { name: "Asset register entry" });
    await user.click(picker);

    const listbox = await screen.findByRole("listbox");
    await user.click(within(listbox).getByText("AST-INC-42 — Binder Incubator"));

    await user.click(within(dialog).getByRole("button", { name: "Save Changes" }));

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
    const clearButton = within(dialog).getByRole("button", { name: "Clear" });
    await user.click(clearButton);
    await user.click(within(dialog).getByRole("button", { name: "Save Changes" }));

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
    await user.click(within(dialog).getByRole("button", { name: "Save Changes" }));

    await user.type(await screen.findByLabelText(/Reason for change/i), "Recalibrated thermal probe");
    await user.click(screen.getByRole("button", { name: "Save Change" }));

    await waitFor(() => expect(EquipmentConfigurationService.updateEquipment).toHaveBeenCalledTimes(2));
    expect(vi.mocked(EquipmentConfigurationService.updateEquipment).mock.calls[1][3]).toBe(
      "Recalibrated thermal probe"
    );
  });
});
