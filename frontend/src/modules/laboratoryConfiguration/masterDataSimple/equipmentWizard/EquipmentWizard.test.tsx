import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { i18n } from "../../../../i18n";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { resetDraftDb, signInForTest } from "../../../../services/drafts/draftTestUtils";
import { masterDataUsageService } from "../../../../services/masterDataUsageService";
import type { LaboratorySection } from "../../../../services/laboratorySectionService";
import type { EquipmentItem } from "../../../inventory/equipment/types/equipmentTypes";
import {
  EquipmentConfigurationService,
  type ConfiguredEquipmentSummary,
  type EquipmentResponse
} from "../services/EquipmentConfigurationService";
import { EquipmentWizard, type EquipmentWizardProps } from "./EquipmentWizard";
import { chooseType, clickNext, clickSave } from "./equipmentWizard.testUtils";

// Service-boundary mocks only (CLAUDE.md Rule 4.5).
vi.mock("../services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: { createEquipment: vi.fn(), updateEquipment: vi.fn(), getEquipmentById: vi.fn() }
}));

vi.mock("../../../../services/masterDataUsageService", () => ({
  masterDataUsageService: { getUsage: vi.fn() }
}));

const micro: LaboratorySection = {
  sectionId: 1,
  sectionCode: "MICRO",
  sectionName: "Microbiology",
  departmentId: 1,
  departmentCode: "QC",
  departmentName: "Quality Control"
};
const env: LaboratorySection = { ...micro, sectionId: 2, sectionCode: "ENV", sectionName: "Environmental" };

const asset: EquipmentItem = {
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
};

const source: ConfiguredEquipmentSummary = {
  id: 10,
  version: 3,
  name: "Incubator 1",
  code: "INC-01",
  type: "Incubator",
  location: "Room 101",
  setPointTemperature: 32.5,
  calibrationDueDate: "2027-05-01",
  equipmentInventoryId: 42,
  inventoryStatus: "InService",
  inventoryLocation: "Room 101",
  configuredProgramCount: 0,
  serialNumber: "SN-42",
  manufacturerName: "Binder",
  vendor: "Binder",
  connectionSettings: null,
  sectionId: 1
};

const saved: EquipmentResponse = {
  id: 10,
  version: 4,
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

function renderWizard(props: Partial<EquipmentWizardProps> = {}) {
  const onSaved = vi.fn();
  const onClose = vi.fn();
  render(
    <PinnedLightTheme>
      <AuthProvider>
        <MemoryRouter>
          <EquipmentWizard
            open
            mySections={[micro]}
            inventory={[asset]}
            onSaved={onSaved}
            onClose={onClose}
            {...props}
          />
        </MemoryRouter>
      </AuthProvider>
    </PinnedLightTheme>
  );
  return { onSaved, onClose };
}

describe("EquipmentWizard", () => {
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
    vi.mocked(EquipmentConfigurationService.createEquipment).mockResolvedValue({ ...saved, id: 99 });
    vi.mocked(EquipmentConfigurationService.updateEquipment).mockResolvedValue(saved);
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("shows only the chosen type's settings", async () => {
    const user = userEvent.setup();
    renderWizard();
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Equipment Name/), "Incubator 4");
    await user.type(within(dialog).getByLabelText(/^Equipment Code/), "INC-04");

    await clickNext(user, dialog);
    expect(within(dialog).getByLabelText(/Set Point Temp/)).toBeTruthy();
    expect(within(dialog).getByLabelText("Connection Settings (optional)")).toBeTruthy();
    expect(within(dialog).queryByLabelText(/CDS Software/)).toBeNull();

    await user.click(within(dialog).getByRole("button", { name: /^Back$/ }));
    await chooseType(user, dialog, "Autoclave");
    await clickNext(user, dialog);
    expect(within(dialog).getByLabelText("Connection Settings (optional)")).toBeTruthy();
    expect(within(dialog).queryByLabelText(/Set Point Temp/)).toBeNull();

    await user.click(within(dialog).getByRole("button", { name: /^Back$/ }));
    await chooseType(user, dialog, "LafCabinet");
    await clickNext(user, dialog);
    expect(within(dialog).queryByLabelText(/Set Point Temp/)).toBeNull();
    expect(within(dialog).queryByLabelText(/CDS Software/)).toBeNull();
    // Connection settings stay available for every type, as in the old dialog.
    expect(within(dialog).getByLabelText("Connection Settings (optional)")).toBeTruthy();
  });

  it("drops a set point typed for an incubator when the type changes to one without it", async () => {
    const user = userEvent.setup();
    renderWizard();
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Equipment Name/), "Cabinet");
    await user.type(within(dialog).getByLabelText(/^Equipment Code/), "LAF-9");
    await clickNext(user, dialog);
    await user.type(within(dialog).getByLabelText(/Set Point Temp/), "37");
    await user.click(within(dialog).getByRole("button", { name: /^Back$/ }));
    await chooseType(user, dialog, "LafCabinet");
    await clickNext(user, dialog);
    await clickNext(user, dialog);
    await clickNext(user, dialog);
    await clickSave(user, dialog);

    await waitFor(() => expect(EquipmentConfigurationService.createEquipment).toHaveBeenCalledTimes(1));
    expect(vi.mocked(EquipmentConfigurationService.createEquipment).mock.calls[0][0]).toMatchObject({
      type: "LafCabinet",
      setPointTemperature: null
    });
  });

  it("blocks Next with the required-fields message", async () => {
    const user = userEvent.setup();
    renderWizard();
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Equipment Name/), "Incubator 4");
    await clickNext(user, dialog);

    expect(within(dialog).getByRole("alert").textContent).toBe("Name, Code, and Type are required.");
    expect(within(dialog).getByLabelText(/^Equipment Code/).getAttribute("aria-invalid")).toBe("true");
    expect(within(dialog).queryByLabelText(/Set Point Temp/)).toBeNull();
  });

  it("requires a section when the user has several, and not when editing", async () => {
    const user = userEvent.setup();
    renderWizard({ mySections: [micro, env] });
    const dialog = await screen.findByRole("dialog");
    await user.type(within(dialog).getByLabelText(/^Equipment Name/), "Incubator 4");
    await user.type(within(dialog).getByLabelText(/^Equipment Code/), "INC-04");
    await clickNext(user, dialog);
    expect(within(dialog).getByText("Laboratory section is required.")).toBeTruthy();
    expect(within(dialog).queryByLabelText(/Set Point Temp/)).toBeNull();

    await user.click(within(dialog).getByRole("combobox", { name: /Laboratory Section/ }));
    await user.click(await screen.findByRole("option", { name: /Environmental/ }));
    await clickNext(user, dialog);
    expect(within(dialog).getByLabelText(/Set Point Temp/)).toBeTruthy();

    cleanup();
    renderWizard({ mySections: [micro, env], editing: source });
    const editDialog = await screen.findByRole("dialog");
    expect(within(editDialog).queryByRole("combobox", { name: /Laboratory Section/ })).toBeNull();
    await clickNext(user, editDialog);
    expect(within(editDialog).getByLabelText(/Set Point Temp/)).toBeTruthy();
  });

  it("requires CDS software for HPLC equipment on the settings step", async () => {
    const user = userEvent.setup();
    renderWizard({ editing: { ...source, type: "Hplc", cdsSoftware: null, setPointTemperature: null } });
    const dialog = await screen.findByRole("dialog");
    await clickNext(user, dialog);
    await clickNext(user, dialog);
    expect(within(dialog).getByText("CDS Software is required for HPLC equipment.")).toBeTruthy();
    expect(within(dialog).queryByLabelText(/Calibration Due Date/)).toBeNull();
  });

  it("New from existing pre-fills type and settings, leaves code, calibration date and documents empty, and saves only after confirmation", async () => {
    const user = userEvent.setup();
    const { onSaved } = renderWizard({ copyFrom: source });
    const dialog = await screen.findByRole("dialog");

    expect(within(dialog).getByRole("combobox", { name: /Equipment Type/ }).textContent).toBe("Incubator");
    expect((within(dialog).getByLabelText(/^Equipment Code/) as HTMLInputElement).value).toBe("");
    expect((within(dialog).getByLabelText("Vendor / Manufacturer") as HTMLInputElement).value).toBe("Binder");
    await user.type(within(dialog).getByLabelText(/^Equipment Code/), "INC-02");

    await clickNext(user, dialog);
    expect((within(dialog).getByLabelText(/Set Point Temp/) as HTMLInputElement).value).toBe("32.5");
    await clickNext(user, dialog);

    const dateGroup = within(dialog).getByRole("group", { name: "Calibration Due Date" });
    expect((dateGroup.querySelector("input") as HTMLInputElement).value).toBe("");
    expect((within(dialog).getByRole("combobox", { name: "Asset register entry" }) as HTMLInputElement).value).toBe("");
    await clickNext(user, dialog);

    // Nothing is saved until the user confirms on Review.
    expect(EquipmentConfigurationService.createEquipment).not.toHaveBeenCalled();
    await clickSave(user, dialog);

    await waitFor(() => expect(EquipmentConfigurationService.createEquipment).toHaveBeenCalledTimes(1));
    expect(vi.mocked(EquipmentConfigurationService.createEquipment).mock.calls[0][0]).toStrictEqual({
      name: "Incubator 1",
      code: "INC-02",
      type: "Incubator",
      location: "Room 101",
      vendor: "Binder",
      cdsSoftware: null,
      connectionSettings: null,
      setPointTemperature: 32.5,
      calibrationDueDate: null,
      equipmentInventoryId: null
    });
    expect(EquipmentConfigurationService.updateEquipment).not.toHaveBeenCalled();
    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(99));
  });

  it("asks for a reason when equipment with open work is edited, then sends it with the update", async () => {
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue({
      type: "Equipment",
      id: 10,
      openWorkCount: 2,
      historicalCount: 5,
      lines: [{ key: "incubations", label: "Incubations in progress", count: 2, isOpenWork: true, listRoute: null }]
    });
    const user = userEvent.setup();
    renderWizard({ editing: source });
    const dialog = await screen.findByRole("dialog");
    await user.clear(within(dialog).getByLabelText("Location"));
    await user.type(within(dialog).getByLabelText("Location"), "Room 202");
    await clickNext(user, dialog);
    await clickNext(user, dialog);
    await clickNext(user, dialog);
    await clickSave(user, dialog);

    expect(EquipmentConfigurationService.updateEquipment).not.toHaveBeenCalled();
    await user.type(await screen.findByLabelText(/Reason for change/i), "Moved to Room 202");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(EquipmentConfigurationService.updateEquipment).toHaveBeenCalledTimes(1));
    const call = vi.mocked(EquipmentConfigurationService.updateEquipment).mock.calls[0];
    expect(call[0]).toBe(10);
    expect(call[1]).toMatchObject({ location: "Room 202" });
    expect(call[2]).toBe(3);
    expect(call[3]).toBe("Moved to Room 202");
  });

  it("renders wizard title and step labels in Arabic when Arabic is active", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["common", "masterData"]);
    try {
      renderWizard();
      const dialog = await screen.findByRole("dialog");
      expect(within(dialog).getByText("إضافة معدات")).toBeTruthy();
      expect(within(dialog).getByLabelText(/^اسم المعدات/)).toBeTruthy();
    } finally {
      await i18n.changeLanguage("en");
    }
  });

  it("after 409 conflict, reloads version 8 and sends version 8 on retry", async () => {
    const conflictErr = new AxiosError("Conflict", "ERR_BAD_REQUEST", undefined, undefined, {
      status: 409,
      statusText: "Conflict",
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: { message: "Modified by another user" },
    });
    vi.mocked(EquipmentConfigurationService.updateEquipment)
      .mockRejectedValueOnce(conflictErr)
      .mockResolvedValueOnce({ ...saved, version: 9 });
    vi.mocked(EquipmentConfigurationService.getEquipmentById).mockResolvedValue({
      ...saved,
      version: 8,
      location: "Room 999",
    });

    const user = userEvent.setup();
    const { onSaved } = renderWizard({ editing: source });
    const dialog = await screen.findByRole("dialog");
    await user.clear(within(dialog).getByLabelText("Location"));
    await user.type(within(dialog).getByLabelText("Location"), "Room 202");
    await clickNext(user, dialog);
    await clickNext(user, dialog);
    await clickNext(user, dialog);
    await clickSave(user, dialog);

    await screen.findByRole("alert");
    await user.click(screen.getByRole("button", { name: "Review and save again" }));
    await clickSave(user, dialog);

    await waitFor(() => expect(EquipmentConfigurationService.updateEquipment).toHaveBeenCalledTimes(2));
    expect(vi.mocked(EquipmentConfigurationService.updateEquipment).mock.calls[0][2]).toBe(3);
    expect(vi.mocked(EquipmentConfigurationService.updateEquipment).mock.calls[1][2]).toBe(8);
    expect(onSaved).toHaveBeenCalledWith(null);
  });

  it("sends the restored draft's starting version so a newer server record triggers 409 conflict flow", async () => {
    const user = userEvent.setup();
    const first = render(
      <PinnedLightTheme>
        <AuthProvider>
          <MemoryRouter>
            <EquipmentWizard
              open
              mySections={[micro]}
              inventory={[asset]}
              editing={source}
              onSaved={vi.fn()}
              onClose={vi.fn()}
            />
          </MemoryRouter>
        </AuthProvider>
      </PinnedLightTheme>
    );
    const firstDialog = await screen.findByRole("dialog");
    await user.clear(within(firstDialog).getByLabelText("Location"));
    await user.type(within(firstDialog).getByLabelText("Location"), "Room Draft");
    await new Promise((r) => setTimeout(r, 500));
    first.unmount();

    const conflictErr = new AxiosError("Conflict", "ERR_BAD_REQUEST", undefined, undefined, {
      status: 409,
      statusText: "Conflict",
      headers: {},
      config: { headers: new AxiosHeaders() },
      data: { message: "Modified by another user" },
    });
    vi.mocked(EquipmentConfigurationService.updateEquipment)
      .mockRejectedValueOnce(conflictErr)
      .mockResolvedValueOnce({ ...saved, version: 7 });
    vi.mocked(EquipmentConfigurationService.getEquipmentById).mockResolvedValue({
      ...saved,
      version: 6,
      location: "Room Colleague",
    });

    const { onSaved } = renderWizard({ editing: { ...source, version: 6, location: "Room Colleague" } });
    const dialog = await screen.findByRole("dialog");
    const notice = await within(dialog).findByRole("region", { name: "Unsaved entry found" });
    await user.click(within(notice).getByRole("button", { name: "Restore entry" }));
    expect((within(dialog).getByLabelText("Location") as HTMLInputElement).value).toBe("Room Draft");

    await clickNext(user, dialog);
    await clickNext(user, dialog);
    await clickNext(user, dialog);
    await clickSave(user, dialog);

    await screen.findByRole("alert");
    expect(vi.mocked(EquipmentConfigurationService.updateEquipment).mock.calls[0][2]).toBe(3);

    const reviewBtn = screen.getByRole("button", { name: "Review and save again" }) as HTMLButtonElement;
    await waitFor(() => expect(reviewBtn.disabled).toBe(false));
    await user.click(reviewBtn);
    await clickSave(user, dialog);

    await waitFor(() => expect(EquipmentConfigurationService.updateEquipment).toHaveBeenCalledTimes(2));
    expect(vi.mocked(EquipmentConfigurationService.updateEquipment).mock.calls[1][1]).toMatchObject({ location: "Room Draft" });
    expect(vi.mocked(EquipmentConfigurationService.updateEquipment).mock.calls[1][2]).toBe(6);
    expect(onSaved).toHaveBeenCalledWith(null);
  });
});
