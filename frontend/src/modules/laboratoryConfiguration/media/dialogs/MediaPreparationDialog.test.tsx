import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../../contexts/AuthContext";
import { renderWithTheme } from "../../../../components/bench/benchTestUtils";
import { draftStore } from "../../../../services/drafts/draftStore";
import { makeDraft, rawStoredDrafts, resetDraftDb, signInForTest } from "../../../../services/drafts/draftTestUtils";
import {
  labelService,
  type ScanMatch,
  type ScanResolveResponse
} from "../../../../services/labelService";
import { masterDataOptions } from "../../../../services/masterDataOptions";
import { entryDefaultsService } from "../../../../services/entryDefaultsService";
import { LAST_CHOICE_CAPTION } from "../../../../components/entry/LastChoiceField";
import { MaterialService } from "../../../inventory/materials/services/MaterialService";
import type { MaterialItem } from "../../../inventory/materials/types/materialTypes";
import {
  EquipmentConfigurationService,
  type EquipmentResponse
} from "../../masterDataSimple/services/EquipmentConfigurationService";
import { MediaPreparationService } from "../services/MediaPreparationService";
import { MediaPreparationDialog } from "./MediaPreparationDialog";
import { i18n } from "../../../../i18n";
import { changeDateField } from "../../../../components/DateField.testUtils";

vi.mock("../../../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

vi.mock("../../../../services/entryDefaultsService", () => ({
  entryDefaultsService: { get: vi.fn() }
}));

vi.mock("../../../../services/masterDataOptions", () => ({
  masterDataOptions: {
    getEquipment: vi.fn()
  }
}));

vi.mock("../../../inventory/materials/services/MaterialService", () => ({
  MaterialService: {
    getAll: vi.fn()
  }
}));

vi.mock("../../masterDataSimple/services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getAutoclavePrograms: vi.fn()
  }
}));

vi.mock("../services/MediaPreparationService", () => ({
  MediaPreparationService: {
    getUsableSolutions: vi.fn(),
    prepare: vi.fn()
  }
}));

const dehydratedMediaItems: MaterialItem[] = [
  {
    id: 301,
    sectionId: 1,
    materialType: "DehydratedMedia",
    customType: null,
    materialName: "Tryptic Soy Agar Powder",
    manufacturerName: "Oxoid",
    batchNumber: "OX-2601",
    receivingDate: "2026-01-10",
    expiryDate: "2028-01-10",
    code: "DM-001",
    location: "Shelf A",
    mediaProductId: 9,
    organismId: null,
    atccNumber: null,
    quantityReceived: 500,
    quantityRemaining: 400,
    unit: "Gram",
    minimumStockLevel: 50,
    status: "InStock"
  }
];

const autoclaveOptions: EquipmentResponse[] = [
  {
    id: 41,
    name: "Autoclave Alpha",
    code: "AC-01",
    type: "Autoclave",
    location: null,
    setPointTemperature: null,
    calibrationDueDate: null,
    vendor: null,
    cdsSoftware: null,
    connectionSettings: null,
    sectionId: 1
  }
];

function makeResolveResponse(match: Partial<ScanMatch>): ScanResolveResponse {
  return {
    outcome: "Found",
    layoutCorrected: false,
    normalizedValue: match.code ?? "",
    match: {
      entityType: "MaterialStockLot",
      id: 301,
      code: "MAT-DM-001",
      displayName: "Tryptic Soy Agar Powder",
      status: "InStock",
      usable: true,
      blockedReason: null,
      navigationTarget: "/inventory/materials?id=301",
      linkedEquipmentId: null,
      ...match
    },
    candidates: [],
    hint: null
  };
}

const sampleProgram = {
  id: 55,
  equipmentId: 41,
  autoclaveCode: "AC-01",
  autoclaveName: "Autoclave Alpha",
  programCode: "P1",
  programName: "Liquid Standard",
  loadType: "Liquid",
  temperature: 121,
  cycleTimeMinutes: 15,
  isActive: true,
  createdByUserId: 1,
  createdAt: "2026-01-01T00:00:00Z",
  lastModifiedByUserId: 1,
  lastModifiedAt: "2026-01-01T00:00:00Z"
};

function renderDialog(bench = false, onClose: () => void = vi.fn()) {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter>
        <MediaPreparationDialog open={true} onClose={onClose} onSuccess={vi.fn()} />
      </MemoryRouter>
    </AuthProvider>,
    { bench }
  );
}

describe("MediaPreparationDialog", () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    localStorage.clear();
    await resetDraftDb();
    signInForTest(7);
    vi.mocked(entryDefaultsService.get).mockResolvedValue({ defaults: [] });
    vi.mocked(masterDataOptions.getEquipment).mockResolvedValue(autoclaveOptions);
    vi.mocked(MaterialService.getAll).mockImplementation(async (type?: string) =>
      type === "DehydratedMedia" ? dehydratedMediaItems : []
    );
    vi.mocked(MediaPreparationService.getUsableSolutions).mockResolvedValue([]);
    vi.mocked(EquipmentConfigurationService.getAutoclavePrograms).mockResolvedValue([]);
  });

  afterEach(() => {
    cleanup();
    localStorage.clear();
  });

  it("pre-selects the autoclave the server offers, labelled, with its programs loaded", async () => {
    vi.mocked(entryDefaultsService.get).mockResolvedValue({
      defaults: [{ field: "autoclaveEquipmentId", id: 41, text: null, label: "AC-01", lastUsedAtUtc: "2026-11-13T08:00:00Z" }]
    });
    renderDialog();

    expect(await screen.findByText(LAST_CHOICE_CAPTION)).toBeTruthy();
    expect(screen.getByRole("combobox", { name: "Autoclave" }).textContent).toContain("AC-01");
    await waitFor(() => expect(EquipmentConfigurationService.getAutoclavePrograms).toHaveBeenCalledWith(41, true));
    expect(entryDefaultsService.get).toHaveBeenCalledWith({ context: "mediaPreparation" }, expect.anything());
  });

  it("removes the caption when the analyst picks a different autoclave value", async () => {
    const user = userEvent.setup();
    vi.mocked(entryDefaultsService.get).mockResolvedValue({
      defaults: [{ field: "autoclaveEquipmentId", id: 41, text: null, label: "AC-01", lastUsedAtUtc: "2026-11-13T08:00:00Z" }]
    });
    renderDialog();
    await screen.findByText(LAST_CHOICE_CAPTION);

    await user.click(screen.getByRole("combobox", { name: "Autoclave" }));
    await user.click(await screen.findByRole("option", { name: /Select Autoclave/ }));

    await waitFor(() => expect(screen.queryByText(LAST_CHOICE_CAPTION)).toBeNull());
  });

  it("leaves the autoclave empty when nothing is offered or the offer is not in the list", async () => {
    vi.mocked(entryDefaultsService.get).mockResolvedValue({
      defaults: [{ field: "autoclaveEquipmentId", id: 999, text: null, label: "AC-99", lastUsedAtUtc: "2026-11-13T08:00:00Z" }]
    });
    renderDialog();
    await waitFor(() => expect(entryDefaultsService.get).toHaveBeenCalled());
    await waitFor(() => expect(masterDataOptions.getEquipment).toHaveBeenCalled());
    expect(screen.queryByText(LAST_CHOICE_CAPTION)).toBeNull();
    expect(screen.getByRole("combobox", { name: "Autoclave" }).textContent).not.toContain("AC-");
  });

  it("scanning an offered dehydrated media stock lot selects it", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ entityType: "MaterialStockLot", id: 301, code: "MAT-DM-001" })
    );

    renderDialog();
    await screen.findByLabelText("Scan dehydrated media stock lot");
    await waitFor(() => {
      expect(MaterialService.getAll).toHaveBeenCalledWith("DehydratedMedia");
    });

    await user.type(screen.getByLabelText("Scan dehydrated media stock lot"), "MAT:MAT-DM-001{Enter}");

    await waitFor(() => {
      expect(
        screen.getByRole("combobox", { name: "Dehydrated Media Stock (Inventory)" }).textContent
      ).toContain("Tryptic Soy Agar Powder");
    });
    expect(screen.getByText(/Manufacturer: Oxoid/)).toBeTruthy();
    expect(screen.getByRole("status").textContent).toContain("Selected MAT-DM-001");
  });

  it("scanning a dehydrated media stock lot that is not offered leaves the selection unchanged", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({ entityType: "MaterialStockLot", id: 999, code: "MAT-DM-999" })
    );

    renderDialog();
    await screen.findByLabelText("Scan dehydrated media stock lot");
    await waitFor(() => {
      expect(MaterialService.getAll).toHaveBeenCalledWith("DehydratedMedia");
    });

    await user.type(screen.getByLabelText("Scan dehydrated media stock lot"), "MAT:MAT-DM-999{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("MAT-DM-999 is not available for this step.");
    expect(
      screen.getByRole("combobox", { name: "Dehydrated Media Stock (Inventory)" }).textContent
    ).toContain("Dehydrated Media Stock (Inventory) *");
  });

  it("scanning an offered autoclave selects it", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        entityType: "EquipmentAsset",
        id: 801,
        linkedEquipmentId: 41,
        code: "AC-01",
        displayName: "Autoclave Alpha"
      })
    );

    renderDialog();
    await screen.findByLabelText("Scan autoclave");
    await waitFor(() => {
      expect(masterDataOptions.getEquipment).toHaveBeenCalledWith("Autoclave");
    });

    await user.type(screen.getByLabelText("Scan autoclave"), "EQ:AC-01{Enter}");

    await waitFor(() => {
      expect(screen.getByRole("combobox", { name: "Autoclave" }).textContent).toContain("AC-01");
      expect(EquipmentConfigurationService.getAutoclavePrograms).toHaveBeenCalledWith(41, true);
    });
    expect(screen.getByRole("status").textContent).toContain("Selected AC-01");
  });

  it("scanning an autoclave that is not offered leaves the selection unchanged", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        entityType: "EquipmentAsset",
        id: 802,
        linkedEquipmentId: 99,
        code: "AC-99",
        displayName: "Autoclave 99"
      })
    );

    renderDialog();
    await screen.findByLabelText("Scan autoclave");
    await waitFor(() => {
      expect(masterDataOptions.getEquipment).toHaveBeenCalledWith("Autoclave");
    });

    await user.type(screen.getByLabelText("Scan autoclave"), "EQ:AC-99{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("AC-99 is not available for this step.");
    expect(screen.getByRole("combobox", { name: "Autoclave" }).textContent).toContain("Select Autoclave *");
    expect(EquipmentConfigurationService.getAutoclavePrograms).not.toHaveBeenCalled();
  });

  it("rejects an expiry date before today and does not save", async () => {
    const user = userEvent.setup();
    vi.mocked(EquipmentConfigurationService.getAutoclavePrograms).mockResolvedValue([sampleProgram]);
    vi.mocked(MediaPreparationService.prepare).mockResolvedValue({ id: 900 } as never);

    renderDialog();
    await screen.findByRole("combobox", { name: "Dehydrated Media Stock (Inventory)" });

    await user.click(screen.getByRole("combobox", { name: "Dehydrated Media Stock (Inventory)" }));
    await user.click(await screen.findByRole("option", { name: /Tryptic Soy Agar Powder/ }));

    await user.type(screen.getByLabelText(/^Total Weight/i), "40");
    await user.type(screen.getByLabelText(/^Total Volume/i), "1000 mL");

    await user.click(screen.getByRole("combobox", { name: "Autoclave" }));
    await user.click(await screen.findByRole("option", { name: /AC-01: Autoclave Alpha/ }));

    await waitFor(() => {
      expect(EquipmentConfigurationService.getAutoclavePrograms).toHaveBeenCalledWith(41, true);
    });
    await user.click(screen.getByRole("combobox", { name: "Autoclave program" }));
    await user.click(await screen.findByRole("option", { name: /P1: Liquid Standard/ }));

    await user.type(screen.getByLabelText(/^Cycle Number/i), "3");
    await user.type(screen.getByLabelText(/^pH/i), "7.3");
    changeDateField(/Expiry Date/i, "2020-01-01");

    await user.click(screen.getByRole("button", { name: "Save Prepared Lot" }));

    expect((await screen.findByRole("alert")).textContent).toContain("cannot be before today");
    expect(MediaPreparationService.prepare).not.toHaveBeenCalled();
  });

  it("media payload equals the current twelve fields (materialId ... additives)", async () => {
    const user = userEvent.setup();
    vi.mocked(EquipmentConfigurationService.getAutoclavePrograms).mockResolvedValue([sampleProgram]);
    vi.mocked(MediaPreparationService.prepare).mockResolvedValue({ id: 900 } as never);

    renderDialog();
    await screen.findByRole("combobox", { name: "Dehydrated Media Stock (Inventory)" });

    await user.click(screen.getByRole("combobox", { name: "Dehydrated Media Stock (Inventory)" }));
    await user.click(await screen.findByRole("option", { name: /Tryptic Soy Agar Powder/ }));

    await user.type(screen.getByLabelText(/^Total Weight/i), "40");
    await user.type(screen.getByLabelText(/^Total Volume/i), "1000 mL");

    await user.click(screen.getByRole("combobox", { name: "Autoclave" }));
    await user.click(await screen.findByRole("option", { name: /AC-01: Autoclave Alpha/ }));

    await waitFor(() => {
      expect(EquipmentConfigurationService.getAutoclavePrograms).toHaveBeenCalledWith(41, true);
    });
    await user.click(screen.getByRole("combobox", { name: "Autoclave program" }));
    await user.click(await screen.findByRole("option", { name: /P1: Liquid Standard/ }));

    await user.type(screen.getByLabelText(/^Cycle Number/i), "3");
    await user.type(screen.getByLabelText(/^pH/i), "7.3");
    changeDateField(/Expiry Date/i, "2027-06-30");

    await user.click(screen.getByRole("button", { name: "Save Prepared Lot" }));

    await waitFor(() => expect(MediaPreparationService.prepare).toHaveBeenCalledTimes(1));
    expect(vi.mocked(MediaPreparationService.prepare).mock.calls[0][0]).toEqual({
      materialId: 301,
      totalWeight: 40,
      totalVolume: "1000 mL",
      autoclaveEquipmentId: 41,
      autoclaveProgram: "P1: Liquid Standard",
      loadType: "Liquid",
      temperature: 121,
      cycleTime: 15,
      cycleNumber: 3,
      ph: 7.3,
      expiryDate: "2027-06-30",
      additives: []
    });
  });

  it('numeric fields (weight, pH, temperature, cycle time) use NumericEntryField kind="decimal"; cycle number kind="count"', async () => {
    renderDialog();
    const weight = await screen.findByLabelText(/^Total Weight/i);
    const ph = screen.getByLabelText(/^pH/i);
    const temp = screen.getByLabelText(/^Temperature/i);
    const cycleTime = screen.getByLabelText(/^Cycle Time/i);
    const cycleNumber = screen.getByLabelText(/^Cycle Number/i);

    for (const field of [weight, ph, temp, cycleTime]) {
      expect(field.hasAttribute("data-numeric-entry")).toBe(true);
      expect(field.getAttribute("inputmode")).toBe("decimal");
    }
    expect(cycleNumber.hasAttribute("data-numeric-entry")).toBe(true);
    expect(cycleNumber.getAttribute("inputmode")).toBe("numeric");
  });

  it("sections stack one per screen height in bench mode with a step indicator", async () => {
    renderDialog(true);
    const stepper = await screen.findByTestId("media-step-indicator");
    expect(stepper).toBeTruthy();
    const sections = document.querySelectorAll("[data-bench-section]");
    expect(sections.length).toBe(4);
  });

  it("media draft restores and keeps only allow-listed fields; network error keeps draft; 409 rejects without retry", async () => {
    const user = userEvent.setup();
    vi.mocked(EquipmentConfigurationService.getAutoclavePrograms).mockResolvedValue([sampleProgram]);
    await draftStore.put(
      makeDraft({
        userId: 7,
        formKey: "media-preparation",
        recordKey: "new",
        recordVersion: null,
        values: {
          materialId: 301,
          totalWeight: "42.5",
          totalVolume: "1000 mL",
          autoclaveEquipmentId: 41,
          autoclaveProgramId: 55,
          autoclaveProgram: "P1: Liquid Standard",
          loadType: "Liquid",
          temperature: 121,
          cycleTime: 15,
          cycleNumber: "4",
          ph: "7.2",
          expiryDate: "2027-06-30",
          additives: [],
          password: "should-not-be-stored"
        }
      })
    );

    renderDialog(true);
    const restoreBtn = await screen.findByRole("button", { name: "Restore entry" });
    await user.click(restoreBtn);

    expect((screen.getByLabelText(/^Total Weight/i) as HTMLInputElement).value).toBe("42.5");
    expect((screen.getByLabelText(/^Cycle Number/i) as HTMLInputElement).value).toBe("4");

    // Edit weight to trigger debounced draft write with allow-list
    await user.type(screen.getByLabelText(/^pH/i), "5");
    await new Promise((r) => setTimeout(r, 500));
    const stored = (await rawStoredDrafts()) as Array<{ values: Record<string, unknown>; state: string }>;
    expect(stored.length).toBe(1);
    expect("password" in stored[0].values).toBe(false);

    // Network error keeps draft and values
    vi.mocked(MediaPreparationService.prepare).mockRejectedValueOnce(new AxiosError("Network Error", "ERR_NETWORK"));
    await user.click(screen.getByRole("button", { name: "Save Prepared Lot" }));
    expect(await screen.findByRole("button", { name: "Try saving again" })).toBeTruthy();
    expect((screen.getByLabelText(/^Total Weight/i) as HTMLInputElement).value).toBe("42.5");
    expect(MediaPreparationService.prepare).toHaveBeenCalledTimes(1);

    // 409 conflict marks draft rejected and never auto-retries
    vi.mocked(MediaPreparationService.prepare).mockRejectedValueOnce(
      new AxiosError("Conflict", "ERR_BAD_REQUEST", undefined, undefined, {
        status: 409,
        statusText: "Conflict",
        data: { message: "Lot version conflict." },
        headers: {},
        config: {}
      } as AxiosResponse)
    );
    await user.click(screen.getByRole("button", { name: "Save Prepared Lot" }));
    await waitFor(() => expect(screen.getByText("Lot version conflict.")).toBeTruthy());
    expect(MediaPreparationService.prepare).toHaveBeenCalledTimes(2);
    const after409 = (await rawStoredDrafts()) as Array<{ state: string; rejectReason?: string }>;
    expect(after409[0]?.state).toBe("rejected");
  });

  it("server validation errors appear on the matching field", async () => {
    const user = userEvent.setup();
    vi.mocked(EquipmentConfigurationService.getAutoclavePrograms).mockResolvedValue([sampleProgram]);
    vi.mocked(MediaPreparationService.prepare).mockRejectedValueOnce(
      new AxiosError("Validation failed", "ERR_BAD_REQUEST", undefined, undefined, {
        status: 400,
        statusText: "Bad Request",
        data: {
          message: "Validation failed",
          errors: {
            totalWeight: ["Weight must be greater than zero."],
            ph: ["pH is outside allowed range."]
          }
        },
        headers: {},
        config: {}
      } as AxiosResponse)
    );

    renderDialog();
    await screen.findByRole("combobox", { name: "Dehydrated Media Stock (Inventory)" });
    await user.click(screen.getByRole("combobox", { name: "Dehydrated Media Stock (Inventory)" }));
    await user.click(await screen.findByRole("option", { name: /Tryptic Soy Agar Powder/ }));
    await user.type(screen.getByLabelText(/^Total Weight/i), "40");
    await user.type(screen.getByLabelText(/^Total Volume/i), "1000 mL");
    await user.click(screen.getByRole("combobox", { name: "Autoclave" }));
    await user.click(await screen.findByRole("option", { name: /AC-01: Autoclave Alpha/ }));
    await waitFor(() => expect(EquipmentConfigurationService.getAutoclavePrograms).toHaveBeenCalledWith(41, true));
    await user.click(screen.getByRole("combobox", { name: "Autoclave program" }));
    await user.click(await screen.findByRole("option", { name: /P1: Liquid Standard/ }));
    await user.type(screen.getByLabelText(/^Cycle Number/i), "1");
    await user.type(screen.getByLabelText(/^pH/i), "7.3");
    changeDateField(/Expiry Date/i, "2027-06-30");

    await user.click(screen.getByRole("button", { name: "Save Prepared Lot" }));

    expect(await screen.findByText("Weight must be greater than zero.")).toBeTruthy();
    expect(screen.getByText("pH is outside allowed range.")).toBeTruthy();
  });

  it("explicit Cancel in bench mode discards the draft from IndexedDB, while unmounting keeps it", async () => {
    const user = userEvent.setup();
    const onClose = vi.fn();
    const first = renderDialog(true, onClose);
    await screen.findByLabelText(/^Total Weight/i);

    await user.type(screen.getByLabelText(/^Total Weight/i), "42.5");
    // Unmount without cancelling (simulates navigation/reload): flush keeps the draft in IndexedDB.
    first.unmount();
    await waitFor(async () => expect(await rawStoredDrafts()).toHaveLength(1));

    const second = renderDialog(true, onClose);
    await user.click(await screen.findByRole("button", { name: "Restore entry" }));
    expect((screen.getByLabelText(/^Total Weight/i) as HTMLInputElement).value).toBe("42.5");

    // Explicit Cancel -> Discard removes the draft from IndexedDB.
    await user.click(screen.getByRole("button", { name: "Cancel" }));
    await user.click(await screen.findByRole("button", { name: "Discard" }));

    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
    await waitFor(async () => expect(await rawStoredDrafts()).toEqual([]));
    second.unmount();
  });

  it("shows the dialog title and field labels in Arabic when Arabic is active", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["labConfig"]);
    try {
      renderDialog();
      expect(await screen.findByText("تحضير تشغيلة أوساط زرعية جديدة")).toBeTruthy();
      expect(screen.getByLabelText(/^الوزن الإجمالي/)).toBeTruthy();
      expect(screen.getByRole("button", { name: "حفظ التشغيلة المحضّرة" })).toBeTruthy();
    } finally {
      await i18n.changeLanguage("en");
    }
  });
});
