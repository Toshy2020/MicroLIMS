import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { ThemeProvider } from "@mui/material";
import { lightTheme } from "../../theme";
import { AuthProvider } from "../../contexts/AuthContext";
import { entryDefaultsService, type EntryDefault } from "../../services/entryDefaultsService";
import { lookupCache } from "../../services/lookupCache";
import { LAST_CHOICE_CAPTION, UNCONFIRMED_LOT_SAVE_REASON } from "../../components/entry/LastChoiceField";
import { TestWorkflowDialog } from "./TestWorkflowDialog";

const workflow = vi.hoisted(() => ({ getCurrentStep: vi.fn(), selectMedia: vi.fn() }));
vi.mock("./services/TestWorkflowService", () => ({ TestWorkflowService: workflow }));
vi.mock("../../services/entryDefaultsService", () => ({ entryDefaultsService: { get: vi.fn() } }));
vi.mock("../../services/lookupCache", () => ({
  lookupCache: { getReleasedMedia: vi.fn(), getIncubators: vi.fn() }
}));
vi.mock("../../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));

const step = {
  id: 1, stepOrder: 1, stepName: "TAMC", stepType: "PlateCount", targetOrganismId: null,
  stepMedia: [{ id: 11, mediaProductId: 5, mediaProductName: "Tryptic soy agar", tempMin: 30, tempMax: 35, incubationMinHours: 72, incubationMaxHours: 120, isRequired: true }],
  incubationMinHours: 72, incubationMaxHours: 120, temperatureMin: 30, temperatureMax: 35, isFinalStep: true
};
const selecting = {
  testOrderId: 9, step, stepNumber: 1, totalSteps: 1, testName: "TAMC", workflowType: "CountTest",
  sampleContext: { sampleName: "Tablet", batchNumber: null, controlNumber: null, reason: null, systemReferenceNumber: null, sampleType: "FinishedProduct", configuredDilutionFactor: 10 },
  incubationLock: null, previousSteps: [], allStepsComplete: false, finalResult: null,
  allSteps: [{ stepOrder: 1, stepName: "TAMC" }], completedSteps: []
};
const lot = (id: number, lotNumber: string) => ({
  id, lotNumber, materialId: 1, material: { mediaProductId: 5 }, expiryDate: "2027-01-01T00:00:00Z", isReleasedForUse: true, status: "Active"
});
const incubator = (id: number, code: string, setPointTemperature: number) => ({
  id, code, name: `Incubator ${code}`, type: "Incubator", setPointTemperature
});
const offered = (field: string, id: number, label: string): EntryDefault => ({
  field, id, text: null, label, lastUsedAtUtc: "2026-11-13T08:00:00Z"
});

function renderDialog() {
  return render(
    <ThemeProvider theme={lightTheme}>
      <AuthProvider>
        <MemoryRouter>
          <TestWorkflowDialog testOrderId={9} testCode="TAMC" category="FinishedProduct" displayName="Tablet" />
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}

const startButton = () => screen.getByRole("button", { name: "Start Incubation" }) as HTMLButtonElement;

beforeEach(() => {
  vi.mocked(workflow.getCurrentStep).mockResolvedValue(selecting);
  vi.mocked(lookupCache.getReleasedMedia).mockResolvedValue([lot(101, "TSA-01"), lot(102, "TSA-02")] as never);
  vi.mocked(lookupCache.getIncubators).mockResolvedValue([incubator(201, "INC-01", 32.5), incubator(202, "INC-02", 32.5), incubator(203, "INC-HOT", 50)] as never);
  vi.mocked(entryDefaultsService.get).mockResolvedValue({ defaults: [] });
});
afterEach(() => { cleanup(); vi.clearAllMocks(); });

describe("TestWorkflowDialog last-choice defaults", () => {
  it("shows the offered incubator with the caption and needs no confirmation", async () => {
    vi.mocked(entryDefaultsService.get).mockResolvedValue({ defaults: [offered("incubatorEquipmentId", 201, "INC-01")] });
    renderDialog();

    expect(await screen.findByText(LAST_CHOICE_CAPTION)).toBeTruthy();
    expect(screen.getByRole("combobox", { name: /Incubator/ }).textContent).toContain("INC-01");
    expect(screen.queryByRole("button", { name: "Confirm" })).toBeNull();
    expect(entryDefaultsService.get).toHaveBeenCalledWith(
      expect.objectContaining({ context: "incubation", testOrderId: 9, stepMediaId: 11, mediaProductId: 5 }),
      expect.anything()
    );
  });

  it("disables Start Incubation with the reason until a pre-filled lot is confirmed", async () => {
    const user = userEvent.setup();
    vi.mocked(entryDefaultsService.get).mockResolvedValue({
      defaults: [offered("mediaLotId", 101, "TSA-01"), offered("incubatorEquipmentId", 201, "INC-01")]
    });
    renderDialog();

    expect(await screen.findByText(UNCONFIRMED_LOT_SAVE_REASON)).toBeTruthy();
    expect(startButton().disabled).toBe(true);

    await user.click(screen.getByRole("button", { name: "Confirm" }));
    expect(screen.queryByText(UNCONFIRMED_LOT_SAVE_REASON)).toBeNull();
    expect(startButton().disabled).toBe(false);
  });

  it("changing the pre-filled lot removes its caption and unblocks Start Incubation", async () => {
    const user = userEvent.setup();
    vi.mocked(entryDefaultsService.get).mockResolvedValue({
      defaults: [offered("mediaLotId", 101, "TSA-01"), offered("incubatorEquipmentId", 201, "INC-01")]
    });
    renderDialog();
    await screen.findByText(UNCONFIRMED_LOT_SAVE_REASON);
    // The incubator default is dropped when the lot changes, so pick it again by hand.
    await user.click(screen.getByRole("combobox", { name: /Media lot/ }));
    await user.click(await screen.findByRole("option", { name: /TSA-02/ }));
    await user.click(screen.getByRole("combobox", { name: /Incubator/ }));
    await user.click(await screen.findByRole("option", { name: /INC-02/ }));

    expect(screen.queryByText(LAST_CHOICE_CAPTION)).toBeNull();
    expect(screen.queryByText(UNCONFIRMED_LOT_SAVE_REASON)).toBeNull();
    expect(startButton().disabled).toBe(false);
  });

  it("leaves the fields empty and shows no error when the defaults request fails", async () => {
    vi.mocked(entryDefaultsService.get).mockRejectedValue(new Error("Server error"));
    renderDialog();

    await waitFor(() => expect(entryDefaultsService.get).toHaveBeenCalled());
    expect(await screen.findByRole("combobox", { name: /Media lot/ })).toBeTruthy();
    expect(screen.queryByText(LAST_CHOICE_CAPTION)).toBeNull();
    expect(screen.queryByRole("alert")).toBeNull();
    expect(startButton().disabled).toBe(true);
  });

  it("applies nothing when the server offers nothing (setting off or no history)", async () => {
    renderDialog();
    await waitFor(() => expect(entryDefaultsService.get).toHaveBeenCalled());
    await screen.findByRole("combobox", { name: /Media lot/ });
    expect(screen.queryByText(LAST_CHOICE_CAPTION)).toBeNull();
    expect(screen.getByRole("combobox", { name: /Incubator/ }).textContent).not.toContain("INC-");
  });

  it("does not pre-select an offered incubator that is not selectable for this step", async () => {
    vi.mocked(entryDefaultsService.get).mockResolvedValue({ defaults: [offered("incubatorEquipmentId", 203, "INC-HOT")] });
    renderDialog();
    await waitFor(() => expect(entryDefaultsService.get).toHaveBeenCalled());
    await screen.findByRole("combobox", { name: /Media lot/ });
    expect(screen.queryByText(LAST_CHOICE_CAPTION)).toBeNull();
  });
});
