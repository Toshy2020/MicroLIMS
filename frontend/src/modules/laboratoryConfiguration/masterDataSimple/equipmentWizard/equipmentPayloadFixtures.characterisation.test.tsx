import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { EquipmentPage } from "../EquipmentPage";
import { EquipmentConfigurationService } from "../services/EquipmentConfigurationService";
import { EquipmentInventoryService } from "../../../inventory/equipment/services/EquipmentInventoryService";
import * as laboratorySectionService from "../../../../services/laboratorySectionService";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { resetDraftDb, signInForTest } from "../../../../services/drafts/draftTestUtils";
import { masterDataUsageService } from "../../../../services/masterDataUsageService";
import { DIALOG_EQUIPMENT_FIXTURES } from "./__fixtures__/equipmentPayloads";
import { clickSave, fillWizardToReview } from "./equipmentWizard.testUtils";

// Characterisation: the body the Add/Edit Equipment wizard sends to the
// service, driven through the real page with user-event. Written and run
// against the old dialog BEFORE it was moved onto equipmentFormModel, then retargeted
// at the wizard with the same fixtures to prove the move changed nothing.

// Service-boundary mocks only (CLAUDE.md Rule 4.5).
vi.mock("../services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getConfiguredSummary: vi.fn(),
    getEquipmentList: vi.fn(),
    getAutoclavePrograms: vi.fn(),
    getSetPointHistory: vi.fn(),
    createEquipment: vi.fn(),
    updateEquipment: vi.fn()
  }
}));

vi.mock("../../../../services/masterDataUsageService", () => ({
  masterDataUsageService: { getUsage: vi.fn() }
}));

vi.mock("../../../inventory/equipment/services/EquipmentInventoryService", () => ({
  EquipmentInventoryService: { getAll: vi.fn() }
}));

vi.mock("../../../../services/laboratorySectionService", () => {
  const getSections = vi.fn();
  const getMySections = vi.fn();
  return { getSections, getMySections, laboratorySectionService: { getSections, getMySections } };
});

const microSection = {
  sectionId: 1,
  sectionCode: "MICRO",
  sectionName: "Microbiology",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};
const secondSection = { ...microSection, sectionId: 2, sectionCode: "ENV", sectionName: "Environmental" };

const seededEquipment = {
  id: 10,
  version: 3,
  name: "Incubator 1",
  code: "INC-01",
  type: "Incubator",
  location: "Room 101",
  setPointTemperature: 32.5,
  calibrationDueDate: null,
  equipmentInventoryId: null,
  sectionId: 1,
  vendor: "Binder",
  cdsSoftware: null,
  connectionSettings: null
};

function renderPage() {
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <EquipmentPage />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
}

describe("equipment wizard payload characterisation", () => {
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
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([microSection]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([microSection]);
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
    vi.mocked(EquipmentConfigurationService.getEquipmentList).mockResolvedValue([seededEquipment]);
    vi.mocked(EquipmentConfigurationService.getAutoclavePrograms).mockResolvedValue([]);
    vi.mocked(EquipmentConfigurationService.getSetPointHistory).mockResolvedValue([]);
    vi.mocked(EquipmentConfigurationService.createEquipment).mockResolvedValue({
      ...seededEquipment,
      id: 99
    });
    vi.mocked(EquipmentConfigurationService.updateEquipment).mockResolvedValue(seededEquipment);
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
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  const createCases = Object.entries(DIALOG_EQUIPMENT_FIXTURES).filter(([, f]) => !f.ctx.isEditing);

  it.each(createCases)("create %s sends the captured body", async (_name, fixture) => {
    if (fixture.ctx.sectionCount > 1) {
      vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([microSection, secondSection]);
    }
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Add Equipment" }));
    const dialog = await screen.findByRole("dialog");
    await fillWizardToReview(user, dialog, fixture);
    await clickSave(user, dialog);

    await waitFor(() => expect(EquipmentConfigurationService.createEquipment).toHaveBeenCalledTimes(1));
    expect(vi.mocked(EquipmentConfigurationService.createEquipment).mock.calls[0]).toStrictEqual([
      fixture.payload
    ]);
    expect(EquipmentConfigurationService.updateEquipment).not.toHaveBeenCalled();
  });

  it("edit sends the captured body with the record version", async () => {
    const fixture = DIALOG_EQUIPMENT_FIXTURES.incubatorEdit;
    const user = userEvent.setup();
    renderPage();

    await user.click(await screen.findByRole("button", { name: "Edit Equipment" }));
    const dialog = await screen.findByRole("dialog");
    await fillWizardToReview(user, dialog, fixture);
    await clickSave(user, dialog);

    await waitFor(() => expect(EquipmentConfigurationService.updateEquipment).toHaveBeenCalledTimes(1));
    expect(vi.mocked(EquipmentConfigurationService.updateEquipment).mock.calls[0]).toStrictEqual([
      10,
      fixture.payload,
      3
    ]);
    expect(EquipmentConfigurationService.createEquipment).not.toHaveBeenCalled();
  });
});
