import { AxiosError, AxiosHeaders } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { EquipmentPage } from "./EquipmentPage";
import { EquipmentConfigurationService, type EquipmentResponse } from "./services/EquipmentConfigurationService";
import { EquipmentInventoryService } from "../../inventory/equipment/services/EquipmentInventoryService";
import * as laboratorySectionService from "../../../services/laboratorySectionService";

vi.mock("./services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getConfiguredSummary: vi.fn(),
    getEquipmentList: vi.fn(),
    getAutoclavePrograms: vi.fn(),
    getSetPointHistory: vi.fn(),
    createEquipment: vi.fn(),
    createEquipmentBatch: vi.fn(),
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

function makeBatchError(
  fieldErrors: Array<{ field: string; message: string; row?: number | null }>
): AxiosError {
  return new AxiosError(
    "Request failed with status code 400",
    "ERR_BAD_REQUEST",
    undefined,
    undefined,
    {
      data: {
        success: false,
        message: "Some rows need correction.",
        data: null,
        errors: fieldErrors.map((e) => e.message),
        fieldErrors
      },
      status: 400,
      statusText: "Bad Request",
      headers: {},
      config: { headers: new AxiosHeaders() }
    }
  );
}

const renderPage = () =>
  render(
    <PinnedLightTheme>
      <MemoryRouter>
        <EquipmentPage />
      </MemoryRouter>
    </PinnedLightTheme>
  );

async function pasteEquipment(user: ReturnType<typeof userEvent.setup>, tsv: string) {
  await user.click(await screen.findByRole("button", { name: "Paste equipment" }));
  const pasteBox = screen.getByLabelText("Paste equipment rows");
  await user.click(pasteBox);
  await user.paste(tsv);
}

const previewTable = () => screen.findByRole("table", { name: /paste preview/i });
const acceptButton = () => screen.getByRole("button", { name: "Accept" }) as HTMLButtonElement;

describe("EquipmentPage paste", () => {
  beforeEach(() => {
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      {
        sectionId: 1,
        sectionCode: "MICRO",
        sectionName: "Microbiology",
        departmentId: 1,
        departmentCode: "QC",
        departmentName: "Quality Control"
      },
      {
        sectionId: 2,
        sectionCode: "EM",
        sectionName: "Environmental Monitoring",
        departmentId: 1,
        departmentCode: "QC",
        departmentName: "Quality Control"
      }
    ]);
    vi.mocked(laboratorySectionService.getMySections).mockResolvedValue([
      {
        sectionId: 1,
        sectionCode: "MICRO",
        sectionName: "Microbiology",
        departmentId: 1,
        departmentCode: "QC",
        departmentName: "Quality Control"
      },
      {
        sectionId: 2,
        sectionCode: "EM",
        sectionName: "Environmental Monitoring",
        departmentId: 1,
        departmentCode: "QC",
        departmentName: "Quality Control"
      }
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
    vi.mocked(EquipmentConfigurationService.createEquipmentBatch).mockResolvedValue([] as EquipmentResponse[]);
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("paste opens preview with the screen's columns and documents post-create configuration in helper text", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText("INC-01: Incubator 1");
    await pasteEquipment(
      user,
      "Incubator 2\tINC-02\tIncubator\tMemmert\tMICRO\tRoom 102\tAST-INC-42\tCOM3\t37.0\t2027-06-15\n"
    );

    expect(screen.getByText(/Add set points and programs after creating the equipment/i)).toBeTruthy();

    const table = await previewTable();
    const headers = within(table)
      .getAllByRole("columnheader")
      .map((h) => h.textContent?.trim());
    expect(headers).toEqual([
      "Row",
      "Equipment name",
      "Equipment code",
      "Equipment type",
      "Vendor / Manufacturer",
      "Laboratory section",
      "Location",
      "Asset register entry",
      "Connection settings",
      "Set point temp (°C)",
      "Calibration due date",
      "Problem"
    ]);
    expect(within(table).getByText("Microbiology")).toBeTruthy();
    expect(within(table).getByText("AST-INC-42 — Binder Incubator")).toBeTruthy();
    expect(within(table).getByText("2027-06-15")).toBeTruthy();
    expect(acceptButton().disabled).toBe(false);
  });

  it("unknown choice names and unsupported equipment types are preview errors", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText("INC-01: Incubator 1");
    await pasteEquipment(
      user,
      [
        "Bad Eq 1\tBAD-01\tNoSuchType\tBinder\tNoSuchSec\tLab\tAST-999\t\tabc\t",
        "HPLC 1\tHPLC-01\tHPLC\tShimadzu\tMICRO\tLab\t\t\t\t"
      ].join("\n")
    );

    const table = await previewTable();
    expect(within(table).getByText(/unknown equipment type 'NoSuchType'/i)).toBeTruthy();
    expect(within(table).getByText(/unknown laboratory section 'NoSuchSec'/i)).toBeTruthy();
    expect(within(table).getByText(/unknown asset register entry 'AST-999'/i)).toBeTruthy();
    expect(within(table).getByText(/Set point temp \(°C\):.*number/i)).toBeTruthy();
    expect(within(table).getByText(/not supported by paste/i)).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
  });

  it("existing code is flagged and duplicate code inside the paste is flagged on both rows", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText("INC-01: Incubator 1");
    await pasteEquipment(
      user,
      [
        "Existing Inc\tINC-01\tIncubator\tBinder\tMICRO\tRoom 101\t\t\t\t",
        "Dup One\tEQ-DUP\tAutoclave\tTuttnauer\tMICRO\tRoom 102\t\t\t\t",
        "Dup Two\teq-dup\tWater Bath\tMemmert\tEM\tRoom 103\t\t\t\t"
      ].join("\n")
    );

    const table = await previewTable();
    const rows = within(table).getAllByRole("row");
    expect(within(rows[1]).getByText(/Already exists\./i)).toBeTruthy();
    expect(within(rows[2]).getByText(/Duplicate equipment code in paste\./i)).toBeTruthy();
    expect(within(rows[3]).getByText(/Duplicate equipment code in paste\./i)).toBeTruthy();
    expect(acceptButton().disabled).toBe(true);
  });

  it("Accept then Save sends one batch call with the exact payload and reloads", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText("INC-01: Incubator 1");
    await pasteEquipment(
      user,
      [
        "Incubator 2\tINC-02\tIncubator\tMemmert\tMicrobiology\tRoom 102\tAST-INC-42 — Binder Incubator\tCOM3\t32.5\t2027-08-01",
        "Autoclave 2\tAUTO-02\tAutoclave\t\tEM\tWash Room\t\t\t\t"
      ].join("\n")
    );
    await previewTable();
    await user.click(acceptButton());

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(EquipmentConfigurationService.createEquipmentBatch).not.toHaveBeenCalled();

    const pendingTable = screen.getByRole("table", { name: /pending equipment/i });
    expect(within(pendingTable).getByText("INC-02")).toBeTruthy();
    expect(within(pendingTable).getByText("AUTO-02")).toBeTruthy();

    const callsBeforeSave = vi.mocked(EquipmentConfigurationService.getConfiguredSummary).mock.calls.length;
    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(EquipmentConfigurationService.createEquipmentBatch).toHaveBeenCalledTimes(1)
    );
    expect(vi.mocked(EquipmentConfigurationService.createEquipmentBatch).mock.calls[0][0]).toEqual([
      {
        name: "Incubator 2",
        code: "INC-02",
        type: "Incubator",
        vendor: "Memmert",
        sectionId: 1,
        location: "Room 102",
        equipmentInventoryId: 42,
        connectionSettings: "COM3",
        setPointTemperature: 32.5,
        calibrationDueDate: "2027-08-01"
      },
      {
        name: "Autoclave 2",
        code: "AUTO-02",
        type: "Autoclave",
        vendor: null,
        sectionId: 2,
        location: "Wash Room",
        equipmentInventoryId: null,
        connectionSettings: null,
        setPointTemperature: null,
        calibrationDueDate: null
      }
    ]);
    await waitFor(() =>
      expect(screen.queryByRole("table", { name: /pending equipment/i })).toBeNull()
    );
    await waitFor(() =>
      expect(vi.mocked(EquipmentConfigurationService.getConfiguredSummary).mock.calls.length).toBeGreaterThan(
        callsBeforeSave
      )
    );
  });

  it("server row error appears on that pending row and nothing is cleared", async () => {
    const user = userEvent.setup();
    vi.mocked(EquipmentConfigurationService.createEquipmentBatch).mockRejectedValueOnce(
      makeBatchError([
        { field: "equipmentInventoryId", message: "This asset is already linked to another instrument.", row: 1 }
      ])
    );

    renderPage();
    await screen.findByText("INC-01: Incubator 1");
    await pasteEquipment(
      user,
      [
        "Incubator 2\tINC-02\tIncubator\tMemmert\tMICRO\tRoom 102\t\t\t\t",
        "Incubator 3\tINC-03\tIncubator\tMemmert\tMICRO\tRoom 103\tAST-INC-42\t\t\t"
      ].join("\n")
    );
    await previewTable();
    await user.click(acceptButton());
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());

    await user.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() => expect(screen.getByRole("alert")).toBeTruthy());
    expect(screen.getByRole("alert").textContent).toMatch(/1 problem in 1 row/i);

    const pendingTable = screen.getByRole("table", { name: /pending equipment/i });
    const rows = within(pendingTable).getAllByRole("row");
    expect(within(rows[1]).getByText("None")).toBeTruthy();
    expect(within(rows[2]).getByText("This asset is already linked to another instrument.")).toBeTruthy();
    expect(within(pendingTable).getByText("INC-02")).toBeTruthy();
    expect(within(pendingTable).getByText("INC-03")).toBeTruthy();
  });

  it("Cancel and Discard send nothing", async () => {
    const user = userEvent.setup();
    renderPage();

    await screen.findByText("INC-01: Incubator 1");
    await pasteEquipment(user, "Incubator 2\tINC-02\tIncubator\t\tMICRO\t\t\t\t\t\n");
    await previewTable();
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.queryByRole("table", { name: /pending equipment/i })).toBeNull();
    expect(EquipmentConfigurationService.createEquipmentBatch).not.toHaveBeenCalled();

    const pasteBox = screen.getByLabelText("Paste equipment rows");
    await user.click(pasteBox);
    await user.paste("Incubator 2\tINC-02\tIncubator\t\tMICRO\t\t\t\t\t\n");
    await previewTable();
    await user.click(acceptButton());
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(screen.getByRole("table", { name: /pending equipment/i })).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Discard" }));
    expect(screen.queryByRole("table", { name: /pending equipment/i })).toBeNull();
    expect(EquipmentConfigurationService.createEquipmentBatch).not.toHaveBeenCalled();
  });
});
