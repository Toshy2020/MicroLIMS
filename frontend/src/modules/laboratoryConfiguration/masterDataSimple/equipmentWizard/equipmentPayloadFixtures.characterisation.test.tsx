import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { changeDateField } from "../../../../components/DateField.testUtils";
import { EquipmentPage, EQUIPMENT_TYPES } from "../EquipmentPage";
import { EquipmentConfigurationService } from "../services/EquipmentConfigurationService";
import { EquipmentInventoryService } from "../../../inventory/equipment/services/EquipmentInventoryService";
import * as laboratorySectionService from "../../../../services/laboratorySectionService";
import { DIALOG_EQUIPMENT_FIXTURES, type EquipmentPayloadFixture } from "./__fixtures__/equipmentPayloads";

// Characterisation: the body the Add/Edit Equipment dialog sends to the
// service, driven through the real page with user-event. Written and run
// against the dialog BEFORE it was moved onto equipmentFormModel, and kept to
// prove the move changed nothing.

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

type User = ReturnType<typeof userEvent.setup>;

function renderPage() {
  render(
    <PinnedLightTheme>
      <MemoryRouter>
        <EquipmentPage />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

async function setText(user: User, dialog: HTMLElement, label: RegExp | string, value: string) {
  const input = within(dialog).getByLabelText(label);
  await user.clear(input);
  if (value) await user.type(input, value);
}

// Enters every field of the fixture model into the open dialog.
async function fillDialog(user: User, dialog: HTMLElement, fixture: EquipmentPayloadFixture) {
  const { model } = fixture;
  await setText(user, dialog, /^Equipment Name/, model.name);
  await setText(user, dialog, /^Equipment Code/, model.code);

  await user.click(within(dialog).getByRole("combobox", { name: /Equipment Type/ }));
  const typeLabel = EQUIPMENT_TYPES.find((t) => t.value === model.type)?.label ?? model.type;
  await user.click(await screen.findByRole("option", { name: typeLabel }));

  await setText(user, dialog, "Vendor / Manufacturer", model.vendor);
  if (model.sectionId !== "" && !fixture.ctx.isEditing && fixture.ctx.sectionCount > 1) {
    await user.click(within(dialog).getByRole("combobox", { name: /Laboratory Section/ }));
    await user.click(await screen.findByRole("option", { name: /Environmental/ }));
  }
  await setText(user, dialog, "Location", model.location);
  if (model.equipmentInventoryId === 42) {
    await user.click(within(dialog).getByRole("combobox", { name: "Asset register entry" }));
    await user.click(await screen.findByText("AST-INC-42 — Binder Incubator"));
  }
  await setText(user, dialog, "Connection Settings (optional)", model.connectionSettings);
  await setText(user, dialog, /Set Point Temp/, model.setPointTemperature);
  if (model.calibrationDueDate) changeDateField("Calibration Due Date", model.calibrationDueDate);
}

describe("equipment dialog payload characterisation", () => {
  beforeEach(() => {
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
    await fillDialog(user, dialog, fixture);
    await user.click(within(dialog).getByRole("button", { name: "Create Equipment" }));

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
    await fillDialog(user, dialog, fixture);
    await user.click(within(dialog).getByRole("button", { name: "Save Changes" }));

    await waitFor(() => expect(EquipmentConfigurationService.updateEquipment).toHaveBeenCalledTimes(1));
    expect(vi.mocked(EquipmentConfigurationService.updateEquipment).mock.calls[0]).toStrictEqual([
      10,
      fixture.payload,
      3
    ]);
    expect(EquipmentConfigurationService.createEquipment).not.toHaveBeenCalled();
  });
});
