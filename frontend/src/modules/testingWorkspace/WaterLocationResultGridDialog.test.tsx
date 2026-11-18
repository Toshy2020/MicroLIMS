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
import { WaterLocationResultGridDialog } from "./WaterLocationResultGridDialog";

const service = vi.hoisted(() => ({
  getLocations: vi.fn(),
  recordWaterBatchReadings: vi.fn()
}));
vi.mock("./services/TestWorkflowService", () => ({ TestWorkflowService: service }));
vi.mock("../../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));
vi.mock("sonner", async (importOriginal) => ({
  ...(await importOriginal<typeof import("sonner")>()),
  toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() })
}));

const NOW = new Date("2026-10-11T09:00:00Z");

const makeLocations = (count: number) =>
  Array.from({ length: count }, (_, idx) => ({
    id: 201 + idx,
    locationType: "WaterPoint",
    locationName: `Water Point ${idx + 1}`,
    alertLimit: "10",
    actionLimit: "25",
    specLimit: "50",
    rawReadings: null,
    calculatedResult: null,
    status: null
  }));

const LOCATIONS = [
  { ...makeLocations(2)[0], locationName: "WFI Loop Point 1" },
  { ...makeLocations(2)[1], locationName: "PW Tank Outlet" }
];

const networkError = () => new AxiosError("Network Error", "ERR_NETWORK");
const conflict = (message: string) =>
  new AxiosError("Request failed", "ERR_BAD_REQUEST", undefined, undefined, {
    status: 409,
    statusText: "Conflict",
    data: { message },
    headers: {},
    config: {}
  } as AxiosResponse);

function renderDialog(bench: boolean, onSubmitted = vi.fn(), onClose = vi.fn()) {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter>
        <WaterLocationResultGridDialog
          open
          testOrderId={66}
          testCode="WFI-TAMC"
          displayName="Water Batch 01"
          minReadyAt={null}
          onClose={onClose}
          onSubmitted={onSubmitted}
        />
      </MemoryRouter>
    </AuthProvider>,
    { bench }
  );
}

type User = ReturnType<typeof userEvent.setup>;
async function tap(user: User, digits: string) {
  for (const d of digits) await user.click(screen.getByRole("button", { name: d }));
}

async function storedWaterDraft() {
  return draftStore.get<{ readingsByLocation?: Record<number, string[]> }>(7, "water-grid", "66");
}

beforeEach(async () => {
  localStorage.clear();
  await resetDraftDb();
  signInForTest(7);
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(NOW);
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.clearAllMocks();
  localStorage.clear();
});

describe("WaterLocationResultGridDialog", () => {
  it("payload to recordWaterBatchReadings is unchanged", async () => {
    const user = userEvent.setup();
    const onSubmitted = vi.fn();
    service.getLocations.mockResolvedValue(LOCATIONS);
    service.recordWaterBatchReadings.mockResolvedValue({ ok: true });
    renderDialog(false, onSubmitted);

    const dialog = await screen.findByRole("dialog");
    await user.type(await within(dialog).findByLabelText("WFI Loop Point 1 reading 1"), "3");
    await user.click(within(dialog).getByRole("button", { name: "Add plate reading for WFI Loop Point 1" }));
    await user.type(within(dialog).getByLabelText("WFI Loop Point 1 reading 2"), "5");
    await user.type(within(dialog).getByLabelText("PW Tank Outlet reading 1"), "12");

    await user.click(within(dialog).getByRole("button", { name: "Submit Results" }));

    await waitFor(() => expect(service.recordWaterBatchReadings).toHaveBeenCalledTimes(1));
    expect(service.recordWaterBatchReadings).toHaveBeenCalledWith(66, [
      { sampleLocationId: 201, readings: [3, 5] },
      { sampleLocationId: 202, readings: [12] }
    ]);
    expect(onSubmitted).toHaveBeenCalledTimes(1);
  });

  it("one location per row with a labelled field; no fixed 90 px width (field fills its cell)", async () => {
    service.getLocations.mockResolvedValue(LOCATIONS);
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    const field1 = await within(dialog).findByLabelText("WFI Loop Point 1 reading 1");
    const rows = within(dialog).getAllByRole("row");
    expect(rows).toHaveLength(3);
    expect(field1.hasAttribute("data-numeric-entry")).toBe(true);
    expect(field1.getAttribute("inputmode")).toBe("numeric");

    const root1 = field1.closest(".MuiTextField-root");
    expect(root1).not.toBeNull();
    expect(getComputedStyle(root1!).width).not.toBe("90px");
    expect(root1!.className).toContain("MuiFormControl-fullWidth");
  });

  it("Next advances to the next location and scrolls it into view", async () => {
    const user = userEvent.setup();
    const scrollIntoView = vi.fn();
    Element.prototype.scrollIntoView = scrollIntoView;
    service.getLocations.mockResolvedValue(LOCATIONS);
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    const field1 = await within(dialog).findByLabelText("WFI Loop Point 1 reading 1");
    const field2 = within(dialog).getByLabelText("PW Tank Outlet reading 1");

    await user.click(field1);
    await tap(user, "3");
    scrollIntoView.mockClear();

    await user.click(within(dialog).getByRole("button", { name: "Next" }));
    expect(document.activeElement).toBe(field2);
    expect(scrollIntoView).toHaveBeenCalled();
  });

  it("progress reads \"12 of 40 entered\" and is announced politely", async () => {
    const user = userEvent.setup();
    service.getLocations.mockResolvedValue(makeLocations(40));
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    const progress = await within(dialog).findByRole("status");
    expect(progress.getAttribute("aria-live")).toBe("polite");
    expect(progress.textContent).toBe("0 of 40 entered");

    for (let i = 1; i <= 12; i++) {
      await user.type(within(dialog).getByLabelText(`Water Point ${i} reading 1`), String(i));
    }
    expect(progress.textContent).toBe("12 of 40 entered");
  });

  it("location name stays visible while scrolling (sticky)", async () => {
    service.getLocations.mockResolvedValue(LOCATIONS);
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    const headerCell = await within(dialog).findByRole("columnheader", { name: "Sampling Point" });
    const bodyCell = within(dialog).getByRole("cell", { name: "WFI Loop Point 1" });

    expect(getComputedStyle(headerCell).position).toBe("sticky");
    expect(getComputedStyle(headerCell).getPropertyValue("inset-inline-start")).toBe("0px");
    expect(getComputedStyle(bodyCell).position).toBe("sticky");
    expect(getComputedStyle(bodyCell).getPropertyValue("inset-inline-start")).toBe("0px");
  });

  it("add/remove reading buttons have record-specific aria-labels and >= 48 px targets in bench mode", async () => {
    service.getLocations.mockResolvedValue(LOCATIONS);
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    const addBtn = await within(dialog).findByRole("button", { name: "Add plate reading for WFI Loop Point 1" });
    const removeBtn = within(dialog).getByRole("button", { name: "Remove reading 1 for WFI Loop Point 1" });

    expect(parseFloat(getComputedStyle(addBtn).minHeight)).toBeGreaterThanOrEqual(48);
    expect(parseFloat(getComputedStyle(removeBtn).minHeight)).toBeGreaterThanOrEqual(48);
    expect(parseFloat(getComputedStyle(removeBtn).minWidth)).toBeGreaterThanOrEqual(48);
  });

  it("draft restores all entered locations and stores only allow-listed fields", async () => {
    const user = userEvent.setup();
    service.getLocations.mockResolvedValue(LOCATIONS);
    const first = renderDialog(true);

    let dialog = await screen.findByRole("dialog");
    await user.click(await within(dialog).findByLabelText("WFI Loop Point 1 reading 1"));
    await tap(user, "7");
    await user.click(within(dialog).getByRole("button", { name: "Next" }));
    await tap(user, "11");

    await waitFor(async () => {
      const saved = await storedWaterDraft();
      expect(saved?.values.readingsByLocation).toEqual({ 201: ["7"], 202: ["11"] });
      expect(Object.keys(saved?.values ?? {})).toEqual(["readingsByLocation"]);
    });

    first.unmount();
    renderDialog(true);
    dialog = await screen.findByRole("dialog");

    expect(await within(dialog).findByRole("region", { name: "Unsaved entry found" })).toBeTruthy();
    expect((await within(dialog).findByLabelText("WFI Loop Point 1 reading 1") as HTMLInputElement).value).toBe("");
    await user.click(within(dialog).getByRole("button", { name: "Restore entry" }));
    expect((within(dialog).getByLabelText("WFI Loop Point 1 reading 1") as HTMLInputElement).value).toBe("7");
    expect((within(dialog).getByLabelText("PW Tank Outlet reading 1") as HTMLInputElement).value).toBe("11");
    expect(service.recordWaterBatchReadings).not.toHaveBeenCalled();
  });

  it("network error keeps values with \"Keep draft\"", async () => {
    const user = userEvent.setup();
    service.getLocations.mockResolvedValue(LOCATIONS);
    service.recordWaterBatchReadings.mockRejectedValue(networkError());
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    await user.click(await within(dialog).findByLabelText("WFI Loop Point 1 reading 1"));
    await tap(user, "3");
    await user.click(within(dialog).getByRole("button", { name: "Next" }));
    await tap(user, "12");
    await user.click(within(dialog).getByRole("button", { name: "Submit Results" }));

    expect(await within(dialog).findByRole("button", { name: "Keep draft" })).toBeTruthy();
    expect((within(dialog).getByLabelText("WFI Loop Point 1 reading 1") as HTMLInputElement).value).toBe("3");
    expect(within(dialog).getByRole("alert").textContent).toContain("not saved to MicroLIMS");
    expect(toast.success).not.toHaveBeenCalled();
    await waitFor(async () => expect((await rawStoredDrafts()).length).toBe(1));

    await user.click(within(dialog).getByRole("button", { name: "Keep draft" }));
    expect(service.recordWaterBatchReadings).toHaveBeenCalledTimes(1);
  });

  it("409 keeps the draft as rejected, never auto-retried", async () => {
    const user = userEvent.setup();
    service.getLocations.mockResolvedValue(LOCATIONS);
    service.recordWaterBatchReadings.mockRejectedValue(conflict("Readings already recorded."));
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    await user.click(await within(dialog).findByLabelText("WFI Loop Point 1 reading 1"));
    await tap(user, "3");
    await user.click(within(dialog).getByRole("button", { name: "Next" }));
    await tap(user, "12");
    await waitFor(async () => expect(await storedWaterDraft()).toBeDefined());

    await user.click(within(dialog).getByRole("button", { name: "Submit Results" }));

    await waitFor(async () => {
      const draft = await storedWaterDraft();
      expect(draft?.state).toBe("rejected");
      expect(draft?.rejectReason).toBe("Readings already recorded.");
    });
    expect(within(dialog).getByText("Readings already recorded.")).toBeTruthy();
    await new Promise((r) => setTimeout(r, 300));
    expect(service.recordWaterBatchReadings).toHaveBeenCalledTimes(1);
    expect(toast.success).not.toHaveBeenCalled();
  });

  it("late save sends X-Draft-Started-At via submitHeaders()", async () => {
    const user = userEvent.setup();
    service.getLocations.mockResolvedValue(LOCATIONS);
    service.recordWaterBatchReadings.mockResolvedValue({ ok: true });
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    await user.click(await within(dialog).findByLabelText("WFI Loop Point 1 reading 1"));
    await tap(user, "3");
    await user.click(within(dialog).getByRole("button", { name: "Next" }));
    await tap(user, "12");

    vi.setSystemTime(new Date(NOW.getTime() + 6 * 60_000));
    await user.click(within(dialog).getByRole("button", { name: "Submit Results" }));

    await waitFor(() => expect(service.recordWaterBatchReadings).toHaveBeenCalledTimes(1));
    expect(service.recordWaterBatchReadings).toHaveBeenCalledWith(
      66,
      [
        { sampleLocationId: 201, readings: [3] },
        { sampleLocationId: 202, readings: [12] }
      ],
      { headers: { "X-Draft-Started-At": NOW.toISOString() } }
    );
  });
});
