import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { screen, cleanup, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter } from "react-router-dom";
import { AuthProvider } from "../../../contexts/AuthContext";
import { renderWithTheme } from "../../../components/bench/benchTestUtils";
import { draftStore } from "../../../services/drafts/draftStore";
import { makeDraft, rawStoredDrafts, resetDraftDb, signInForTest } from "../../../services/drafts/draftTestUtils";
import { BiochemicalTestPanel } from "./BiochemicalTestPanel";
import type { TestWorkflowStepDto } from "../types/testWorkflowTypes";

const service = vi.hoisted(() => ({
  recordAnalystDecision: vi.fn(),
  submitBiochemical: vi.fn()
}));
vi.mock("../services/TestWorkflowService", () => ({ TestWorkflowService: service }));
vi.mock("../../../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));

const networkError = () => new AxiosError("Network Error", "ERR_NETWORK");
const conflict = (message: string) =>
  new AxiosError("Request failed", "ERR_BAD_REQUEST", undefined, undefined, {
    status: 409,
    statusText: "Conflict",
    data: { message },
    headers: {},
    config: {}
  } as AxiosResponse);

beforeEach(async () => {
  localStorage.clear();
  await resetDraftDb();
  signInForTest(7);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const step = {
  id: 5,
  stepOrder: 5,
  stepName: "Biochemical Identification",
  stepType: "BiochemicalTest",
  targetOrganismId: 1,
  phenotypicTestTypes: ["Oxidase", "Catalase"],
  stepMedia: [],
  incubationMinHours: 0,
  incubationMaxHours: 0,
  temperatureMin: 0,
  temperatureMax: 0,
  isFinalStep: true
} as unknown as TestWorkflowStepDto;

function renderPanel(confirmatoryOutcome: string | null, bench = false, onSubmitted = vi.fn()) {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter>
        <BiochemicalTestPanel
          testOrderId={7}
          step={step}
          confirmatoryOutcome={confirmatoryOutcome}
          onSubmitted={onSubmitted}
        />
      </MemoryRouter>
    </AuthProvider>,
    { bench }
  );
}

describe("BiochemicalTestPanel", () => {
  it("payload to recordAnalystDecision is unchanged", async () => {
    const user = userEvent.setup();
    service.recordAnalystDecision.mockResolvedValue({ outcomeSummary: "Detected" });
    const onSubmitted = vi.fn();

    renderPanel("AllConforming", false, onSubmitted);

    const submitDetected = await screen.findByRole("button", { name: /Submit as Detected/ });
    await user.click(submitDetected);

    await waitFor(() => expect(service.recordAnalystDecision).toHaveBeenCalledTimes(1));
    expect(service.recordAnalystDecision).toHaveBeenCalledWith(7, "SubmitAsDetected");
    expect(onSubmitted).toHaveBeenCalled();
  });

  it("payload to submitBiochemical is unchanged", async () => {
    const user = userEvent.setup();
    service.submitBiochemical.mockResolvedValue({ outcomeSummary: "Not Detected" });
    const onSubmitted = vi.fn();

    renderPanel("Inconclusive", false, onSubmitted);

    const input = await screen.findByLabelText(/Biochemical result/);
    await user.type(input, "Oxidase negative, Catalase positive");
    await user.click(screen.getByRole("radio", { name: /^Not Detected/ }));
    await user.click(screen.getByRole("button", { name: "Submit Result" }));

    await waitFor(() => expect(service.submitBiochemical).toHaveBeenCalledTimes(1));
    expect(service.submitBiochemical).toHaveBeenCalledWith(
      7,
      "Biochemical Identification",
      "Oxidase negative, Catalase positive",
      false
    );
    expect(onSubmitted).toHaveBeenCalled();
  });

  it("observation choices render as one column of large labelled options in bench mode and gated action shows reason", async () => {
    renderPanel("Inconclusive", true);

    const group = await screen.findByRole("radiogroup");
    expect(within(group).getByRole("radio", { name: /^Not Detected/ })).toBeTruthy();
    expect(within(group).getByRole("radio", { name: /^Detected/ })).toBeTruthy();

    expect((screen.getByRole("button", { name: "Submit Result" }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText("Enter the result and choose Detected or Not Detected to continue.")).toBeTruthy();
  });

  it("drafts restore observations but never an analyst decision, keep on network error, and mark 409 as rejected", async () => {
    const user = userEvent.setup();
    await draftStore.put(
      makeDraft({
        userId: 7,
        formKey: "pathogen-biochemical",
        recordKey: "7:Biochemical Identification",
        startedAtDevice: "2020-01-01T00:00:00.000Z",
        values: { text: "IMViC ++--", organismDetected: true }
      })
    );
    service.recordAnalystDecision.mockResolvedValueOnce({ outcomeSummary: "Proceeding" });
    service.submitBiochemical.mockRejectedValueOnce(networkError());

    // In decision phase, draft restore notice is NOT offered and decision is never drafted
    renderPanel("AllConforming", true);
    expect(await screen.findByRole("button", { name: "Proceed to Biochemical Test" })).toBeTruthy();
    expect(screen.queryByRole("region", { name: "Unsaved entry found" })).toBeNull();

    await user.click(screen.getByRole("button", { name: "Proceed to Biochemical Test" }));

    // Now in form phase, draft restore notice IS offered and restores text + organismDetected
    await user.click(await screen.findByRole("button", { name: "Restore entry" }));
    expect((screen.getByLabelText(/Biochemical result/) as HTMLInputElement).value).toBe("IMViC ++--");

    await user.click(screen.getByRole("button", { name: "Submit Result" }));
    expect(await screen.findByText(/kept on this tablet/i)).toBeTruthy();
    expect(service.submitBiochemical).toHaveBeenCalledWith(
      7,
      "Biochemical Identification",
      "IMViC ++--",
      true,
      { headers: { "X-Draft-Started-At": "2020-01-01T00:00:00.000Z" } }
    );

    const storedAfterNet = (await rawStoredDrafts()) as { state: string; values: Record<string, unknown> }[];
    expect(storedAfterNet).toHaveLength(1);
    expect("decision" in storedAfterNet[0].values).toBe(false);
    expect("phase" in storedAfterNet[0].values).toBe(false);

    service.submitBiochemical.mockRejectedValueOnce(conflict("Stale biochemical step"));
    await user.click(screen.getByRole("button", { name: "Try saving again" }));
    expect(await screen.findByText("Stale biochemical step")).toBeTruthy();
    expect(service.submitBiochemical).toHaveBeenCalledTimes(2);
    const storedAfter409 = (await rawStoredDrafts()) as { state: string }[];
    expect(storedAfter409[0].state).toBe("rejected");
  });

  it("writes only allow-listed observation fields to IndexedDB when editing in bench mode", async () => {
    const user = userEvent.setup();
    const { unmount } = renderPanel("Inconclusive", true);

    const input = await screen.findByLabelText(/Biochemical result/);
    await user.type(input, "Catalase positive");
    await user.click(screen.getByRole("radio", { name: /^Detected/ }));
    unmount();

    await waitFor(async () => {
      const stored = (await rawStoredDrafts()) as { values: Record<string, unknown> }[];
      expect(stored).toHaveLength(1);
      expect(stored[0].values).toEqual({ text: "Catalase positive", organismDetected: true });
      expect("decision" in stored[0].values).toBe(false);
      expect("phase" in stored[0].values).toBe(false);
    });
  });
});
