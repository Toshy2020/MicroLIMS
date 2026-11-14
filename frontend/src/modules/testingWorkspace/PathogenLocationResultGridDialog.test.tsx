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
import { PathogenLocationResultGridDialog } from "./PathogenLocationResultGridDialog";

const service = vi.hoisted(() => ({
  getLocations: vi.fn(),
  recordBatchPathogenResults: vi.fn()
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
    id: 301 + idx,
    locationType: "Swab",
    locationName: `Swab ${idx + 1}`,
    gradeClassification: idx === 0 ? "Grade A" : "Grade B",
    status: null,
    enteredAt: null
  }));

const LOCATIONS = [
  { ...makeLocations(2)[0], locationName: "Isolator Port 1" },
  { ...makeLocations(2)[1], locationName: "Transfer Hatch" }
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
        <PathogenLocationResultGridDialog
          open
          testOrderId={77}
          testCode="EM-PATH"
          displayName="Pathogen Batch 01"
          minReadyAt={null}
          onClose={onClose}
          onSubmitted={onSubmitted}
        />
      </MemoryRouter>
    </AuthProvider>,
    { bench }
  );
}

async function storedPathogenDraft() {
  return draftStore.get<{ growth?: Record<number, "yes" | "no" | ""> }>(7, "pathogen-grid", "77");
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

describe("PathogenLocationResultGridDialog", () => {
  it("payload to recordBatchPathogenResults is unchanged", async () => {
    const user = userEvent.setup();
    const onSubmitted = vi.fn();
    service.getLocations.mockResolvedValue(LOCATIONS);
    service.recordBatchPathogenResults.mockResolvedValue({ ok: true });
    renderDialog(false, onSubmitted);

    const dialog = await screen.findByRole("dialog");
    // Locations load after the dialog opens; wait for their rows.
    const yesRadios = await within(dialog).findAllByRole("radio", { name: "Yes" });
    const noRadios = within(dialog).getAllByRole("radio", { name: "No" });
    await user.click(yesRadios[0]);
    await user.click(noRadios[1]);

    await user.click(within(dialog).getByRole("button", { name: "Submit Results" }));

    await waitFor(() => expect(service.recordBatchPathogenResults).toHaveBeenCalledTimes(1));
    expect(service.recordBatchPathogenResults).toHaveBeenCalledWith(77, [
      { sampleLocationId: 301, growthObserved: true },
      { sampleLocationId: 302, growthObserved: false }
    ]);
    expect(onSubmitted).toHaveBeenCalledTimes(1);
  });

  it("pathogen grid choices are large toggle buttons with text, not colour alone", async () => {
    const user = userEvent.setup();
    service.getLocations.mockResolvedValue(LOCATIONS);
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    const group1 = await within(dialog).findByRole("group", { name: "Growth observed for Isolator Port 1" });
    const yesBtn = within(group1).getByRole("button", { name: "Yes (Detected)" });
    const noBtn = within(group1).getByRole("button", { name: "No (Absent)" });

    expect(yesBtn.getAttribute("aria-pressed")).toBe("false");
    expect(noBtn.getAttribute("aria-pressed")).toBe("false");
    expect(parseFloat(getComputedStyle(yesBtn).minHeight)).toBeGreaterThanOrEqual(48);
    expect(parseFloat(getComputedStyle(noBtn).minHeight)).toBeGreaterThanOrEqual(48);

    await user.click(yesBtn);
    expect(yesBtn.getAttribute("aria-pressed")).toBe("true");
    expect(noBtn.getAttribute("aria-pressed")).toBe("false");
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
      const group = within(dialog).getByRole("group", { name: `Growth observed for Swab ${i}` });
      await user.click(within(group).getByRole("button", { name: "No (Absent)" }));
    }
    expect(progress.textContent).toBe("12 of 40 entered");
  });

  it("location name stays visible while scrolling (sticky)", async () => {
    service.getLocations.mockResolvedValue(LOCATIONS);
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    const headerCell = await within(dialog).findByRole("columnheader", { name: "Location" });
    const bodyCell = within(dialog).getByRole("cell", { name: /^Isolator Port 1/ });

    expect(getComputedStyle(headerCell).position).toBe("sticky");
    expect(getComputedStyle(headerCell).left).toBe("0px");
    expect(getComputedStyle(bodyCell).position).toBe("sticky");
    expect(getComputedStyle(bodyCell).left).toBe("0px");
  });

  it("draft restores all entered locations and stores only allow-listed fields", async () => {
    const user = userEvent.setup();
    service.getLocations.mockResolvedValue(LOCATIONS);
    const first = renderDialog(true);

    let dialog = await screen.findByRole("dialog");
    await user.click(within(within(dialog).getByRole("group", { name: "Growth observed for Isolator Port 1" })).getByRole("button", { name: "Yes (Detected)" }));
    await user.click(within(within(dialog).getByRole("group", { name: "Growth observed for Transfer Hatch" })).getByRole("button", { name: "No (Absent)" }));

    await waitFor(async () => {
      const saved = await storedPathogenDraft();
      expect(saved?.values.growth).toEqual({ 301: "yes", 302: "no" });
      expect(Object.keys(saved?.values ?? {})).toEqual(["growth"]);
    });

    first.unmount();
    renderDialog(true);
    dialog = await screen.findByRole("dialog");

    expect(await within(dialog).findByRole("region", { name: "Unsaved entry found" })).toBeTruthy();
    const g1 = within(dialog).getByRole("group", { name: "Growth observed for Isolator Port 1" });
    expect(within(g1).getByRole("button", { name: "Yes (Detected)" }).getAttribute("aria-pressed")).toBe("false");

    await user.click(within(dialog).getByRole("button", { name: "Restore entry" }));
    expect(within(g1).getByRole("button", { name: "Yes (Detected)" }).getAttribute("aria-pressed")).toBe("true");
    const g2 = within(dialog).getByRole("group", { name: "Growth observed for Transfer Hatch" });
    expect(within(g2).getByRole("button", { name: "No (Absent)" }).getAttribute("aria-pressed")).toBe("true");
    expect(service.recordBatchPathogenResults).not.toHaveBeenCalled();
  });

  it("network error keeps values with \"Keep draft\"", async () => {
    const user = userEvent.setup();
    service.getLocations.mockResolvedValue(LOCATIONS);
    service.recordBatchPathogenResults.mockRejectedValue(networkError());
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    const group1 = await within(dialog).findByRole("group", { name: "Growth observed for Isolator Port 1" });
    await user.click(within(group1).getByRole("button", { name: "Yes (Detected)" }));
    await user.click(within(within(dialog).getByRole("group", { name: "Growth observed for Transfer Hatch" })).getByRole("button", { name: "No (Absent)" }));
    await user.click(within(dialog).getByRole("button", { name: "Submit Results" }));

    expect(await within(dialog).findByRole("button", { name: "Keep draft" })).toBeTruthy();
    expect(within(within(dialog).getByRole("group", { name: "Growth observed for Isolator Port 1" })).getByRole("button", { name: "Yes (Detected)" }).getAttribute("aria-pressed")).toBe("true");
    expect(within(dialog).getByRole("alert").textContent).toContain("not saved to MicroLIMS");
    expect(toast.success).not.toHaveBeenCalled();
    await waitFor(async () => expect((await rawStoredDrafts()).length).toBe(1));

    await user.click(within(dialog).getByRole("button", { name: "Keep draft" }));
    expect(service.recordBatchPathogenResults).toHaveBeenCalledTimes(1);
  });

  it("409 keeps the draft as rejected, never auto-retried", async () => {
    const user = userEvent.setup();
    service.getLocations.mockResolvedValue(LOCATIONS);
    service.recordBatchPathogenResults.mockRejectedValue(conflict("Batch pathogen results already recorded."));
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    const group1 = await within(dialog).findByRole("group", { name: "Growth observed for Isolator Port 1" });
    await user.click(within(group1).getByRole("button", { name: "Yes (Detected)" }));
    await user.click(within(within(dialog).getByRole("group", { name: "Growth observed for Transfer Hatch" })).getByRole("button", { name: "No (Absent)" }));
    await waitFor(async () => expect(await storedPathogenDraft()).toBeDefined());

    await user.click(within(dialog).getByRole("button", { name: "Submit Results" }));

    await waitFor(async () => {
      const draft = await storedPathogenDraft();
      expect(draft?.state).toBe("rejected");
      expect(draft?.rejectReason).toBe("Batch pathogen results already recorded.");
    });
    expect(within(dialog).getByText("Batch pathogen results already recorded.")).toBeTruthy();
    await new Promise((r) => setTimeout(r, 300));
    expect(service.recordBatchPathogenResults).toHaveBeenCalledTimes(1);
    expect(toast.success).not.toHaveBeenCalled();
  });

  it("late save sends X-Draft-Started-At via submitHeaders()", async () => {
    const user = userEvent.setup();
    service.getLocations.mockResolvedValue(LOCATIONS);
    service.recordBatchPathogenResults.mockResolvedValue({ ok: true });
    renderDialog(true);

    const dialog = await screen.findByRole("dialog");
    const group1 = await within(dialog).findByRole("group", { name: "Growth observed for Isolator Port 1" });
    await user.click(within(group1).getByRole("button", { name: "Yes (Detected)" }));
    await user.click(within(within(dialog).getByRole("group", { name: "Growth observed for Transfer Hatch" })).getByRole("button", { name: "No (Absent)" }));

    vi.setSystemTime(new Date(NOW.getTime() + 6 * 60_000));
    await user.click(within(dialog).getByRole("button", { name: "Submit Results" }));

    await waitFor(() => expect(service.recordBatchPathogenResults).toHaveBeenCalledTimes(1));
    expect(service.recordBatchPathogenResults).toHaveBeenCalledWith(
      77,
      [
        { sampleLocationId: 301, growthObserved: true },
        { sampleLocationId: 302, growthObserved: false }
      ],
      { headers: { "X-Draft-Started-At": NOW.toISOString() } }
    );
  });
});
