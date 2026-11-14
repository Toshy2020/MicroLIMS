import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { cleanup, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter } from "react-router-dom";
import { toast } from "sonner";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderWithTheme } from "../../components/bench/benchTestUtils";
import { draftStore } from "../../services/drafts/draftStore";
import { rawStoredDrafts, resetDraftDb, signInForTest } from "../../services/drafts/draftTestUtils";
import { TestWorkflowDialog } from "./TestWorkflowDialog";

const service = vi.hoisted(() => ({ getCurrentStep: vi.fn(), recordCountResult: vi.fn(), previewCountResult: vi.fn() }));
vi.mock("./services/TestWorkflowService", () => ({ TestWorkflowService: service }));
vi.mock("../../services/lookupCache", () => ({
  lookupCache: { getReleasedMedia: vi.fn().mockResolvedValue([]), getIncubators: vi.fn().mockResolvedValue([]) }
}));
vi.mock("../../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));
vi.mock("sonner", async (importOriginal) => ({
  ...(await importOriginal<typeof import("sonner")>()),
  toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() })
}));

const NOW = new Date("2026-10-11T09:00:00Z");

beforeEach(async () => {
  localStorage.clear();
  await resetDraftDb();
  signInForTest(7);
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(NOW);
  service.previewCountResult.mockResolvedValue({
    reported: "120 CFU/mL",
    average: 12,
    calculated: 120,
    status: "WithinLimits",
    messages: []
  });
});
afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.clearAllMocks();
  localStorage.clear();
});

const makeStep = (isFinalStep: boolean) => ({
  id: 1, stepOrder: 1, stepName: "TAMC", stepType: "PlateCount", targetOrganismId: null,
  stepMedia: [{ mediaProductId: 5, mediaProductName: "Tryptic soy agar", tempMin: 30, tempMax: 35, incubationMinHours: 72, incubationMaxHours: 120, isRequired: true }],
  incubationMinHours: 72, incubationMaxHours: 120, temperatureMin: 30, temperatureMax: 35, isFinalStep
});

const incubating = (remainingMinimumSeconds: number, isFinalStep = true) => ({
  testOrderId: 9, step: makeStep(isFinalStep), stepNumber: 1, totalSteps: 1, testName: "TAMC", workflowType: "CountTest",
  sampleContext: { sampleName: "Tablet", batchNumber: null, controlNumber: null, reason: null, systemReferenceNumber: null, sampleType: "FinishedProduct", configuredDilutionFactor: 10 },
  incubationLock: {
    isLocked: true, incubationEndUtc: "2026-10-16T09:00:00Z", remainingSeconds: 120 * 3600,
    minReadyAt: new Date(Date.now() + remainingMinimumSeconds * 1000).toISOString(), remainingMinimumSeconds
  },
  previousSteps: [{ stepNumber: 1, stepName: "TAMC", status: "Incubating", mediaName: "Tryptic soy agar", lotNumber: "TSA-01", incubatorName: "INC-02", incubatorSetTemp: 32.5, incubationStartUtc: "2026-10-11T07:00:00Z", incubationEndUtc: null, observation: null }],
  allStepsComplete: false, finalResult: null,
  allSteps: [{ stepOrder: 1, stepName: "TAMC" }], completedSteps: []
});

function renderDialog(bench: boolean) {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter>
        <TestWorkflowDialog testOrderId={9} testCode="TAMC" category="FinishedProduct" displayName="Tablet" />
      </MemoryRouter>
    </AuthProvider>,
    { bench }
  );
}

const networkError = () => new AxiosError("Network Error", "ERR_NETWORK");
const conflict = (message: string) =>
  new AxiosError("Request failed", "ERR_BAD_REQUEST", undefined, undefined, { status: 409, statusText: "Conflict", data: { message }, headers: {}, config: {} } as AxiosResponse);

type User = ReturnType<typeof userEvent.setup>;

async function openEntry(user: User) {
  await user.click(await screen.findByRole("button", { name: "Record Result" }));
  return screen.findByRole("dialog");
}

async function tap(user: User, digits: string) {
  for (const d of digits) await user.click(screen.getByRole("button", { name: d }));
}

async function storedCountDraft() {
  return draftStore.get<{ readings?: string[] }>(7, "count-reading", "9:TAMC");
}

describe("TestWorkflowDialog count reading in bench mode", () => {
  it("count: plates render as NumericEntryField rows in bench mode; Next walks plates in order; \"Save and next\" is offered when another reading is waiting", async () => {
    const user = userEvent.setup();
    service.getCurrentStep.mockResolvedValue(incubating(0, false));
    renderDialog(true);
    const dialog = await openEntry(user);

    const plate1 = within(dialog).getByLabelText("Plate 1");
    const plate2 = within(dialog).getByLabelText("Plate 2");
    for (const plate of [plate1, plate2]) {
      expect(plate.hasAttribute("data-numeric-entry")).toBe(true);
      expect(plate.getAttribute("inputmode")).toBe("numeric");
    }

    await user.click(plate1);
    await tap(user, "12");
    await user.click(within(dialog).getByRole("button", { name: "Next" }));
    expect(document.activeElement).toBe(plate2);
    await tap(user, "7");
    expect((plate1 as HTMLInputElement).value).toBe("12");
    expect((plate2 as HTMLInputElement).value).toBe("7");

    expect(within(dialog).getByRole("button", { name: "Save and next" })).toBeTruthy();
  });

  it("count: Save and next is not offered when this is the last reading", async () => {
    const user = userEvent.setup();
    service.getCurrentStep.mockResolvedValue(incubating(0, true));
    renderDialog(true);
    const dialog = await openEntry(user);
    expect(within(dialog).getByRole("button", { name: "Save" })).toBeTruthy();
    expect(within(dialog).queryByRole("button", { name: "Save and next" })).toBeNull();
  });

  it("count: Save and next sends the same payload and goes straight on to the next step", async () => {
    const user = userEvent.setup();
    service.getCurrentStep.mockResolvedValue(incubating(0, false));
    service.recordCountResult.mockResolvedValue({ allStepsComplete: false, isDefinitive: true, outcomeSummary: "Step 1 recorded" });
    renderDialog(true);
    const dialog = await openEntry(user);

    await user.click(within(dialog).getByLabelText("Plate 1"));
    await tap(user, "12");
    await user.click(within(dialog).getByRole("button", { name: "Save and next" }));

    await waitFor(() => expect(service.recordCountResult).toHaveBeenCalledTimes(1));
    expect(service.recordCountResult.mock.calls[0]).toEqual([
      9,
      { stepName: "TAMC", rawPlateReadings: ["12"], dilutionFactor: 10, dilutionFactorOverrideNote: undefined }
    ]);
    await waitFor(() => expect(service.getCurrentStep).toHaveBeenCalledTimes(2));
    expect(screen.queryByRole("button", { name: "Proceed to Next Step" })).toBeNull();
    await waitFor(async () => expect(await storedCountDraft()).toBeUndefined());
  });

  it("count: a plate can be marked TNTC or uncountable and is sent as that text", async () => {
    const user = userEvent.setup();
    service.getCurrentStep.mockResolvedValue(incubating(0));
    service.recordCountResult.mockResolvedValue({ allStepsComplete: true, isDefinitive: true, outcomeSummary: "ok" });
    renderDialog(true);
    const dialog = await openEntry(user);

    await user.click(within(dialog).getByLabelText("Plate 1"));
    await tap(user, "9");
    await user.click(within(dialog).getByRole("button", { name: "Mark plate 2 as TNTC" }));
    expect((within(dialog).getByLabelText("Plate 2") as HTMLInputElement).value).toBe("TNTC");
    expect(within(dialog).getByText("Non-numeric: flagged for reviewer decision")).toBeTruthy();
    await user.click(within(dialog).getByRole("button", { name: "Save" }));

    await waitFor(() => expect(service.recordCountResult).toHaveBeenCalledTimes(1));
    expect(service.recordCountResult.mock.calls[0][1].rawPlateReadings).toEqual(["9", "TNTC"]);
  });

  it("count: marking one plate TNTC and another uncountable shows the refusal message before save", async () => {
    const user = userEvent.setup();
    service.getCurrentStep.mockResolvedValue(incubating(0));
    renderDialog(true);
    const dialog = await openEntry(user);

    await user.click(within(dialog).getByRole("button", { name: "Mark plate 1 as TNTC" }));
    await user.click(within(dialog).getByRole("button", { name: "Mark plate 2 as uncountable" }));

    expect(await within(dialog).findByText("A reading set cannot contain both TNTC and Uncountable. Choose one.")).toBeTruthy();
    expect(service.previewCountResult).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole("button", { name: "Save" }));
    expect(service.recordCountResult).not.toHaveBeenCalled();
  });

  it("count: typed values survive closing and reopening the dialog (draft offered and accepted)", async () => {
    const user = userEvent.setup();
    service.getCurrentStep.mockResolvedValue(incubating(0));
    const first = renderDialog(true);
    let dialog = await openEntry(user);
    await user.click(within(dialog).getByLabelText("Plate 1"));
    await tap(user, "12");
    await user.click(within(dialog).getByRole("button", { name: "Next" }));
    await tap(user, "14");
    await waitFor(async () => expect((await storedCountDraft())?.values.readings).toEqual(["12", "14"]));

    first.unmount();
    renderDialog(true);
    dialog = await openEntry(user);

    // Offered, not applied.
    expect(within(dialog).getByRole("region", { name: "Unsaved entry found" })).toBeTruthy();
    expect((within(dialog).getByLabelText("Plate 1") as HTMLInputElement).value).toBe("");
    await user.click(within(dialog).getByRole("button", { name: "Restore entry" }));
    expect((within(dialog).getByLabelText("Plate 1") as HTMLInputElement).value).toBe("12");
    expect((within(dialog).getByLabelText("Plate 2") as HTMLInputElement).value).toBe("14");
    // Restoring never saves anything.
    expect(service.recordCountResult).not.toHaveBeenCalled();
  });

  it("count: save with a network error keeps the dialog values, shows \"Keep draft\", no success toast", async () => {
    const user = userEvent.setup();
    service.getCurrentStep.mockResolvedValue(incubating(0));
    service.recordCountResult.mockRejectedValue(networkError());
    renderDialog(true);
    const dialog = await openEntry(user);
    await user.click(within(dialog).getByLabelText("Plate 1"));
    await tap(user, "12");
    await user.click(within(dialog).getByRole("button", { name: "Save" }));

    expect(await within(dialog).findByRole("button", { name: "Keep draft" })).toBeTruthy();
    expect(within(dialog).queryByRole("button", { name: "Save" })).toBeNull();
    expect((within(dialog).getByLabelText("Plate 1") as HTMLInputElement).value).toBe("12");
    expect(within(dialog).getByRole("alert").textContent).toContain("not saved to MicroLIMS");
    expect(toast.success).not.toHaveBeenCalled();
    await waitFor(async () => expect((await storedCountDraft())?.values.readings).toEqual(["12", ""]));
    expect((await rawStoredDrafts()).length).toBe(1);

    // Keep draft does not send anything.
    await user.click(within(dialog).getByRole("button", { name: "Keep draft" }));
    expect(service.recordCountResult).toHaveBeenCalledTimes(1);
  });

  it("count: 409 keeps the draft as rejected with the server message", async () => {
    const user = userEvent.setup();
    service.getCurrentStep.mockResolvedValue(incubating(0));
    service.recordCountResult.mockRejectedValue(conflict("This step has already been recorded."));
    renderDialog(true);
    const dialog = await openEntry(user);
    await user.click(within(dialog).getByLabelText("Plate 1"));
    await tap(user, "12");
    await waitFor(async () => expect(await storedCountDraft()).toBeDefined());
    await user.click(within(dialog).getByRole("button", { name: "Save" }));

    await waitFor(async () => {
      const draft = await draftStore.get<unknown>(7, "count-reading", "9:TAMC");
      expect(draft?.state).toBe("rejected");
      expect(draft?.rejectReason).toBe("This step has already been recorded.");
    });
    expect(within(dialog).getByText("This step has already been recorded.")).toBeTruthy();
    expect((within(dialog).getByLabelText("Plate 1") as HTMLInputElement).value).toBe("12");
    // Never retried on its own.
    await new Promise((r) => setTimeout(r, 300));
    expect(service.recordCountResult).toHaveBeenCalledTimes(1);
    expect(toast.success).not.toHaveBeenCalled();
  });

  it("late save sends X-Draft-Started-At; a prompt save does not", async () => {
    const user = userEvent.setup();
    service.getCurrentStep.mockResolvedValue(incubating(0));
    service.recordCountResult.mockResolvedValue({ allStepsComplete: true, isDefinitive: true, outcomeSummary: "ok" });

    // Late: more than five minutes after the first entry.
    const late = renderDialog(true);
    let dialog = await openEntry(user);
    await user.click(within(dialog).getByLabelText("Plate 1"));
    await tap(user, "12");
    vi.setSystemTime(new Date(NOW.getTime() + 6 * 60_000));
    await user.click(within(dialog).getByRole("button", { name: "Save" }));
    await waitFor(() => expect(service.recordCountResult).toHaveBeenCalledTimes(1));
    expect(service.recordCountResult.mock.calls[0]).toEqual([
      9,
      { stepName: "TAMC", rawPlateReadings: ["12"], dilutionFactor: 10, dilutionFactorOverrideNote: undefined },
      { headers: { "X-Draft-Started-At": NOW.toISOString() } }
    ]);
    late.unmount();

    // Prompt: saved a minute after the first entry.
    service.recordCountResult.mockClear();
    vi.setSystemTime(new Date(NOW.getTime() + 60 * 60_000));
    const prompt = renderDialog(true);
    dialog = await openEntry(user);
    await user.click(within(dialog).getByLabelText("Plate 1"));
    await tap(user, "8");
    vi.setSystemTime(new Date(NOW.getTime() + 61 * 60_000));
    await user.click(within(dialog).getByRole("button", { name: "Save" }));
    await waitFor(() => expect(service.recordCountResult).toHaveBeenCalledTimes(1));
    expect(service.recordCountResult.mock.calls[0]).toHaveLength(2);
    prompt.unmount();
  });

  it("count: confirming a discard removes the draft and reopening the form does not save a blank one", async () => {
    const user = userEvent.setup();
    service.getCurrentStep.mockResolvedValue(incubating(0));
    renderDialog(true);
    let dialog = await openEntry(user);
    await user.click(within(dialog).getByLabelText("Plate 1"));
    await tap(user, "12");
    await waitFor(async () => expect(await storedCountDraft()).toBeDefined());

    await user.click(within(dialog).getByRole("button", { name: "Cancel" }));
    await user.click(await screen.findByRole("button", { name: "Discard" }));
    await waitFor(async () => expect(await storedCountDraft()).toBeUndefined());

    dialog = await openEntry(user);
    expect((within(dialog).getByLabelText("Plate 1") as HTMLInputElement).value).toBe("");
    await new Promise((r) => setTimeout(r, 700));
    expect(await rawStoredDrafts()).toEqual([]);
  });

  it("count: outside bench mode nothing is kept as a draft and the plates are plain fields", async () => {
    const user = userEvent.setup();
    service.getCurrentStep.mockResolvedValue(incubating(0));
    renderDialog(false);
    await user.click(await screen.findByRole("button", { name: "Record Result" }));
    const plate1 = screen.getByLabelText("Plate 1");
    expect(plate1.hasAttribute("data-numeric-entry")).toBe(false);
    expect(screen.queryByRole("group", { name: "Number keypad" })).toBeNull();
    await user.type(plate1, "12");
    await new Promise((r) => setTimeout(r, 700));
    expect(await rawStoredDrafts()).toEqual([]);
  });

  it("countdown text is ≥ 24 px in bench mode and has tabular numerals", async () => {
    service.getCurrentStep.mockResolvedValue(incubating(3600));
    renderDialog(true);
    const timer = await screen.findByRole("timer");
    const time = within(timer).getByText("01:00:00");
    const style = getComputedStyle(time);
    expect(parseFloat(style.fontSize)).toBeGreaterThanOrEqual(24);
    expect(style.fontVariantNumeric).toBe("tabular-nums");
  });

  it("countdown 'Ready' text is ≥ 24 px in bench mode", async () => {
    service.getCurrentStep.mockResolvedValue(incubating(0));
    renderDialog(true);
    const timer = await screen.findByRole("timer");
    const ready = within(timer).getByText("Ready");
    expect(parseFloat(getComputedStyle(ready).fontSize)).toBeGreaterThanOrEqual(24);
  });
});
