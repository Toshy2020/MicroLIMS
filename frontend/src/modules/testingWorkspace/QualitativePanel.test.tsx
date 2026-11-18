import { describe, expect, it, vi, afterEach, beforeEach } from "vitest";
import { screen, waitFor, cleanup, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, type AxiosResponse } from "axios";
import { MemoryRouter } from "react-router-dom";
import { toast } from "sonner";
import { AuthProvider } from "../../contexts/AuthContext";
import { renderWithTheme } from "../../components/bench/benchTestUtils";
import { QualitativePanel } from "./QualitativePanel";
import { SpecificationService } from "../laboratoryConfiguration/specifications/services/SpecificationService";
import { ItemService } from "../laboratoryConfiguration/items/services/ItemService";
import { EquipmentConfigurationService } from "../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService";
import * as laboratorySectionService from "../../services/laboratorySectionService";
import { TestWorkflowService } from "./services/TestWorkflowService";
import { draftStore } from "../../services/drafts/draftStore";
import { rawStoredDrafts, resetDraftDb, signInForTest } from "../../services/drafts/draftTestUtils";

vi.mock("sonner", async (importOriginal) => ({
  ...(await importOriginal<typeof import("sonner")>()),
  toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() })
}));

vi.mock("../../services/apiClient", () => ({
  apiClient: { get: vi.fn(() => new Promise(() => {})), post: vi.fn() }
}));

vi.mock("../laboratoryConfiguration/specifications/services/SpecificationService", () => ({
  SpecificationService: {
    getForTestOrder: vi.fn()
  }
}));

vi.mock("../laboratoryConfiguration/items/services/ItemService", () => ({
  ItemService: {
    getAll: vi.fn()
  }
}));

vi.mock("../laboratoryConfiguration/masterDataSimple/services/EquipmentConfigurationService", () => ({
  EquipmentConfigurationService: {
    getConfiguredSummary: vi.fn()
  }
}));

vi.mock("../../services/laboratorySectionService", () => ({
  getSections: vi.fn(),
  getMySections: vi.fn()
}));

vi.mock("./services/TestWorkflowService", () => ({
  TestWorkflowService: {
    recordQualitativeResult: vi.fn()
  }
}));

beforeEach(() => {
  localStorage.clear();
});
afterEach(() => {
  cleanup();
  localStorage.clear();
});

function renderPanel(perContainer: boolean, onRecorded = vi.fn(), bench = false) {
  return renderWithTheme(
    <AuthProvider>
      <MemoryRouter>
        <QualitativePanel
          testOrderId={42}
          displayName="Description"
          testCode="DESC"
          itemId={1}
          current={{ workflowType: "Qualitative", perContainer, testName: "DESC" }}
          onRecorded={onRecorded}
        />
      </MemoryRouter>
    </AuthProvider>,
    { bench }
  );
}

async function signWith(user: ReturnType<typeof userEvent.setup>, password: string) {
  await user.click(screen.getByRole("button", { name: /Sign and record result/i }));
  await user.type(await screen.findByLabelText(/Password/), password);
  await user.click(screen.getByRole("button", { name: "Sign" }));
}

describe("QualitativePanel per-container interaction", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(SpecificationService.getForTestOrder).mockResolvedValue([
      {
        id: 101,
        testCode: "DESC",
        parameterName: "Description",
        expectedResultText: "White to off-white crystalline powder",
        displayOrder: 1
      }
    ] as never);
    vi.mocked(ItemService.getAll).mockResolvedValue([
      { id: 1, name: "Sample A", assignedTests: [{ testCode: "DESC" }] }
    ] as never);
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([
      { sectionId: 1, sectionCode: "FP", sectionName: "Finished Product" }
    ] as never);
    vi.mocked(EquipmentConfigurationService.getConfiguredSummary).mockResolvedValue([]);
    vi.mocked(TestWorkflowService.recordQualitativeResult).mockResolvedValue({
      outcomeSummary: "Pass",
      status: "Completed"
    } as never);
  });

  it("renders per-container field and handles count blur, row updates, toggling, and a11y attributes", async () => {
    const user = userEvent.setup();
    renderPanel(true);

    // Wait for specifications to load
    await waitFor(() => {
      expect(screen.getByText(/Qualitative Observations/i)).toBeTruthy();
    });

    // 1. Initial info prompt when count is empty
    expect(
      screen.getByText(/Enter the number of containers received \(1 to 200\) to record results\./i)
    ).toBeTruthy();

    const countInput = screen.getByLabelText(/Containers received/i);
    expect(countInput).toBeTruthy();

    // 2. Typing container count does not immediately create rows until blur / Enter
    await user.type(countInput, "3");
    // Rows should not exist yet before commit
    expect(screen.queryByLabelText("Container 1 result")).toBeNull();

    // Commit via blur
    await user.tab();

    // Now 3 container rows appear with accessible labels and no pre-selected choice
    const toggle1 = screen.getByLabelText("Container 1 result");
    const toggle2 = screen.getByLabelText("Container 2 result");
    const toggle3 = screen.getByLabelText("Container 3 result");
    expect(toggle1).toBeTruthy();
    expect(toggle2).toBeTruthy();
    expect(toggle3).toBeTruthy();

    const compliesBtns = screen.getAllByRole("button", { name: /Complies/i });
    const nonComplyBtns = screen.getAllByRole("button", { name: /Does not comply/i });
    for (const btn of [...compliesBtns, ...nonComplyBtns]) {
      expect(btn.getAttribute("aria-pressed")).toBe("false");
    }
    // No conclusion shown and Sign stays disabled with reason until every container is chosen
    expect(screen.queryByRole("status")).toBeNull();
    const signBtn = screen.getByRole("button", { name: /Sign and record result/i }) as HTMLButtonElement;
    expect(signBtn.disabled).toBe(true);
    expect(screen.getByText("Choose Complies or Does not comply for every row.")).toBeTruthy();

    // 3. Choose Complies for containers 1 and 3, and Does not comply for container 2
    await user.click(compliesBtns[0]);
    await user.click(compliesBtns[2]);
    await user.click(nonComplyBtns[1]); // container 2

    // Conclusion updates to single failing container wording once all containers have a choice
    const statusBox = screen.getByRole("status");
    expect(statusBox).toBeTruthy();
    expect(statusBox.textContent).toContain("Container 2 of 3 does not comply");

    // Observation for container 2 is now required
    const obsInput2 = screen.getByPlaceholderText("Describe what container 2 shows.");
    expect(obsInput2).toBeTruthy();
    await user.type(obsInput2, "Foreign blue speck on surface");
    expect((obsInput2 as HTMLInputElement).value).toBe("Foreign blue speck on surface");

    // 4. Toggle container 2 to "Complies" -> observation is cleared and conclusion updates
    await user.click(compliesBtns[1]); // container 2 complies

    expect(statusBox.textContent).toContain("All 3 containers comply");
    expect(signBtn.disabled).toBe(false);

    // If container 2 is failed again, previous observation was cleared
    await user.click(nonComplyBtns[1]);
    const recheckedObs = screen.getByPlaceholderText("Describe what container 2 shows.");
    expect((recheckedObs as HTMLInputElement).value).toBe("");
  });

  it("typing '12' over '10' preserves existing container states", async () => {
    const user = userEvent.setup();
    renderPanel(true);

    await waitFor(() => {
      expect(screen.getByText(/Qualitative Observations/i)).toBeTruthy();
    });

    const countInput = screen.getByLabelText(/Containers received/i);

    // Commit 10 containers
    await user.type(countInput, "10");
    await user.tab();

    expect(screen.getByLabelText("Container 10 result")).toBeTruthy();

    // Fail container 5 with custom observation
    const nonComplyBtns = screen.getAllByRole("button", { name: /Does not comply/i });
    await user.click(nonComplyBtns[4]); // index 5 (0-indexed 4)
    const obs5 = screen.getByPlaceholderText("Describe what container 5 shows.");
    await user.type(obs5, "Discolored powder");

    // User types "1" then "12" without wiping container 5
    await user.clear(countInput);
    await user.type(countInput, "12");
    await user.tab();

    // Verify container 12 exists and container 5 is still intact with its observation
    expect(screen.getByLabelText("Container 12 result")).toBeTruthy();
    const obs5After = screen.getByPlaceholderText("Describe what container 5 shows.");
    expect((obs5After as HTMLInputElement).value).toBe("Discolored powder");
  });

  it("submits qualitative per-container payload with observation in each container", async () => {
    const user = userEvent.setup();
    const onRecorded = vi.fn();
    renderPanel(true, onRecorded);

    await waitFor(() => {
      expect(screen.getByText(/Qualitative Observations/i)).toBeTruthy();
    });

    const countInput = screen.getByLabelText(/Containers received/i);
    await user.type(countInput, "2");
    await user.tab();

    // Fail container 1 and choose Complies for container 2
    const nonComplyBtns = screen.getAllByRole("button", { name: /Does not comply/i });
    const compliesBtns = screen.getAllByRole("button", { name: /Complies/i });
    await user.click(nonComplyBtns[0]);
    const obs1 = screen.getByPlaceholderText("Describe what container 1 shows.");
    await user.type(obs1, "Particle observed");

    const signBtn = screen.getByRole("button", { name: /Sign and record result/i }) as HTMLButtonElement;
    // Container 2 is still unchosen, so Sign stays disabled with the reason
    expect(signBtn.disabled).toBe(true);
    expect(screen.getByText("Choose Complies or Does not comply for every row.")).toBeTruthy();

    await user.click(compliesBtns[1]);
    expect(signBtn.disabled).toBe(false);
    await signWith(user, "secret123");

    await waitFor(() => {
      expect(TestWorkflowService.recordQualitativeResult).toHaveBeenCalledTimes(1);
    });

    expect(TestWorkflowService.recordQualitativeResult).toHaveBeenCalledWith(
      42,
      expect.objectContaining({
        containerCount: 2,
        password: "secret123",
        parameters: [
          expect.objectContaining({
            specificationId: 101,
            conforms: false,
            containers: [
              { index: 1, conforms: false, observation: "Particle observed" },
              { index: 2, conforms: true, observation: null }
            ]
          })
        ]
      })
    );
  });
});

// Payload pins: written against the code before the bench work and unchanged since.
describe("payload to TestWorkflowService.recordQualitativeResult", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date(2026, 9, 11, 9, 0, 30));
    vi.mocked(SpecificationService.getForTestOrder).mockResolvedValue([
      { id: 101, testCode: "DESC", parameterName: "Description", expectedResultText: "White powder", displayOrder: 1 },
      { id: 102, testCode: "DESC", parameterName: "Odour", expectedResultText: "Odourless", displayOrder: 2 }
    ] as never);
    vi.mocked(ItemService.getAll).mockResolvedValue([]);
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([]);
    vi.mocked(EquipmentConfigurationService.getConfiguredSummary).mockResolvedValue([]);
    vi.mocked(TestWorkflowService.recordQualitativeResult).mockResolvedValue({ outcomeSummary: "Pass", status: "Completed" } as never);
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("qualitative: payload to recordQualitativeResult unchanged", async () => {
    const user = userEvent.setup();
    renderPanel(false);
    await screen.findByText(/Qualitative Observations/i);

    const signBtn = screen.getByRole("button", { name: /Sign and record result/i }) as HTMLButtonElement;
    expect(signBtn.disabled).toBe(true);
    expect(screen.getByText("Choose Complies or Does not comply for every row.")).toBeTruthy();

    await user.click(screen.getAllByRole("button", { name: /^Complies$/i })[0]);
    await user.type(screen.getAllByLabelText(/Observation \(optional\)/)[0], "  Fine white powder  ");
    // Row 2 is still unchosen -> Sign still disabled
    expect(signBtn.disabled).toBe(true);

    await user.click(screen.getAllByRole("button", { name: /Does not comply/i })[1]);
    await user.type(screen.getByLabelText(/Observation \(required\)/), "Faint odour");
    await user.type(screen.getByLabelText(/Comment \(optional\)/), "  checked twice  ");
    expect(signBtn.disabled).toBe(false);
    await signWith(user, "secret123");

    await waitFor(() => expect(TestWorkflowService.recordQualitativeResult).toHaveBeenCalledTimes(1));
    const call = vi.mocked(TestWorkflowService.recordQualitativeResult).mock.calls[0];
    expect(call).toHaveLength(2);
    expect(call[0]).toBe(42);
    expect(call[1]).toStrictEqual({
      analysedAt: new Date(2026, 9, 11, 9, 0).toISOString(),
      equipmentId: null,
      parameters: [
        { specificationId: 101, conforms: true, observation: "Fine white powder" },
        { specificationId: 102, conforms: false, observation: "Faint odour" }
      ],
      password: "secret123",
      comment: "checked twice"
    });
  });
});

// Bench mode (a shared touch tablet): drafts, and the signature fields that must never be in one.
describe("QualitativePanel in bench mode", () => {
  const NOW = new Date("2026-10-11T09:00:00Z");
  const PASSWORD = "Sup3rSecret!";
  const COMMENT = "reviewer-only remark 7731";

  beforeEach(async () => {
    vi.clearAllMocks();
    await resetDraftDb();
    signInForTest(7);
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(NOW);
    vi.mocked(SpecificationService.getForTestOrder).mockResolvedValue([
      { id: 101, testCode: "DESC", parameterName: "Description", expectedResultText: "White powder", displayOrder: 1 },
      { id: 102, testCode: "DESC", parameterName: "Odour", expectedResultText: "Odourless", displayOrder: 2 }
    ] as never);
    vi.mocked(ItemService.getAll).mockResolvedValue([]);
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([]);
    vi.mocked(EquipmentConfigurationService.getConfiguredSummary).mockResolvedValue([]);
    vi.mocked(TestWorkflowService.recordQualitativeResult).mockResolvedValue({ outcomeSummary: "Pass", status: "Completed" } as never);
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  const networkError = () => new AxiosError("Network Error", "ERR_NETWORK");
  const conflict = (message: string) =>
    new AxiosError("Request failed", "ERR_BAD_REQUEST", undefined, undefined, { status: 409, statusText: "Conflict", data: { message }, headers: {}, config: {} } as AxiosResponse);

  async function failOdourAndComment(user: ReturnType<typeof userEvent.setup>) {
    await screen.findByText(/Qualitative Observations/i);
    await user.click(screen.getAllByRole("button", { name: /^Complies$/i })[0]);
    await user.click(screen.getAllByRole("button", { name: /Does not comply/i })[1]);
    await user.type(screen.getByLabelText(/Observation \(required\)/), "Faint odour");
    await user.type(screen.getByLabelText(/Comment \(optional\)/), COMMENT);
  }

  async function sign(user: ReturnType<typeof userEvent.setup>) {
    await user.click(screen.getByRole("button", { name: "Sign and record result" }));
    await user.type(await screen.findByLabelText(/Password/), PASSWORD);
    await user.click(screen.getByRole("button", { name: "Sign" }));
  }

  const storedDraft = () => draftStore.get<Record<string, unknown>>(7, "qualitative", "42:DESC");

  it("qualitative: stored draft has no password and no signature comment; after restore the password field is empty", async () => {
    const user = userEvent.setup();
    vi.mocked(TestWorkflowService.recordQualitativeResult).mockRejectedValue(networkError());
    const first = renderPanel(false, vi.fn(), true);
    await failOdourAndComment(user);
    await sign(user);

    // The signing attempt reached the (unreachable) server with the password and comment.
    await waitFor(() => expect(TestWorkflowService.recordQualitativeResult).toHaveBeenCalledTimes(1));
    await waitFor(async () => expect((await storedDraft())?.values).toBeDefined());
    // Let any pending write settle, then read what is really on the tablet.
    await new Promise((r) => setTimeout(r, 700));
    const raw = JSON.stringify(await rawStoredDrafts());
    expect(raw).toContain("Faint odour");
    expect(raw).not.toContain(PASSWORD);
    expect(raw).not.toContain(COMMENT);
    expect(raw.toLowerCase()).not.toContain("password");
    expect(raw).not.toContain('"comment"');
    expect(Object.keys((await storedDraft())?.values as object).sort()).toEqual(["analysedAt", "containerCountInput", "equipmentId", "rows"]);

    first.unmount();
    renderPanel(false, vi.fn(), true);
    await screen.findByRole("region", { name: "Unsaved entry found" });
    await user.click(screen.getByRole("button", { name: "Restore entry" }));
    expect((screen.getByLabelText(/Observation \(required\)/) as HTMLInputElement).value).toBe("Faint odour");
    // The comment and the password were never kept.
    expect((screen.getByLabelText(/Comment \(optional\)/) as HTMLTextAreaElement).value).toBe("");
    await user.click(screen.getByRole("button", { name: "Sign and record result" }));
    expect(((await screen.findByLabelText(/Password/)) as HTMLInputElement).value).toBe("");
    expect(TestWorkflowService.recordQualitativeResult).toHaveBeenCalledTimes(1);
  });

  it("qualitative: save with a network error keeps the values, shows \"Keep draft\", no success toast", async () => {
    const user = userEvent.setup();
    vi.mocked(TestWorkflowService.recordQualitativeResult).mockRejectedValue(networkError());
    const onRecorded = vi.fn();
    renderPanel(false, onRecorded, true);
    await failOdourAndComment(user);
    await sign(user);

    expect(await screen.findByRole("button", { name: "Keep draft" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Sign and record result" })).toBeNull();
    expect((screen.getByLabelText(/Observation \(required\)/) as HTMLInputElement).value).toBe("Faint odour");
    expect(toast.success).not.toHaveBeenCalled();
    expect(onRecorded).not.toHaveBeenCalled();
  });

  it("qualitative: 409 keeps the draft as rejected with the server message and is not retried", async () => {
    const user = userEvent.setup();
    vi.mocked(TestWorkflowService.recordQualitativeResult).mockRejectedValue(conflict("This test was already recorded."));
    renderPanel(false, vi.fn(), true);
    await failOdourAndComment(user);
    await waitFor(async () => expect(await storedDraft()).toBeDefined());
    await sign(user);

    await waitFor(async () => {
      const draft = await draftStore.get<unknown>(7, "qualitative", "42:DESC");
      expect(draft?.state).toBe("rejected");
      expect(draft?.rejectReason).toBe("This test was already recorded.");
    });
    expect(screen.getByText("This test was already recorded.")).toBeTruthy();
    await new Promise((r) => setTimeout(r, 300));
    expect(TestWorkflowService.recordQualitativeResult).toHaveBeenCalledTimes(1);
  });

  it("late save sends X-Draft-Started-At; a prompt save does not", async () => {
    const user = userEvent.setup();
    const late = renderPanel(false, vi.fn(), true);
    await failOdourAndComment(user);
    vi.setSystemTime(new Date(NOW.getTime() + 6 * 60_000));
    await sign(user);
    await waitFor(() => expect(TestWorkflowService.recordQualitativeResult).toHaveBeenCalledTimes(1));
    const lateCall = vi.mocked(TestWorkflowService.recordQualitativeResult).mock.calls[0];
    expect(lateCall[0]).toBe(42);
    expect(lateCall[2]).toEqual({ headers: { "X-Draft-Started-At": NOW.toISOString() } });
    late.unmount();

    vi.mocked(TestWorkflowService.recordQualitativeResult).mockClear();
    vi.setSystemTime(new Date(NOW.getTime() + 60 * 60_000));
    renderPanel(false, vi.fn(), true);
    await failOdourAndComment(user);
    vi.setSystemTime(new Date(NOW.getTime() + 61 * 60_000));
    await sign(user);
    await waitFor(() => expect(TestWorkflowService.recordQualitativeResult).toHaveBeenCalledTimes(1));
    expect(vi.mocked(TestWorkflowService.recordQualitativeResult).mock.calls[0]).toHaveLength(2);
  });

  it("qualitative: container count is a NumericEntryField with the keypad in bench mode", async () => {
    const user = userEvent.setup();
    renderPanel(true, vi.fn(), true);
    await screen.findByText(/Qualitative Observations/i);
    const count = screen.getByLabelText(/Containers received/i);
    expect(count.hasAttribute("data-numeric-entry")).toBe(true);
    await user.click(count);
    await user.click(screen.getByRole("button", { name: "3" }));
    await user.click(screen.getByRole("button", { name: "Next" }));
    // One group per parameter (two parameters), three containers each.
    expect(screen.getAllByLabelText("Container 3 result")).toHaveLength(2);
    expect(screen.queryByLabelText("Container 4 result")).toBeNull();
  });

  it("qualitative: the gated Sign button gives its reason as text in bench mode", async () => {
    renderPanel(true, vi.fn(), true);
    await screen.findByText(/Qualitative Observations/i);
    const sign = screen.getByRole("button", { name: "Sign and record result" }) as HTMLButtonElement;
    expect(sign.disabled).toBe(true);
    expect(screen.getAllByText("Enter the number of containers received (1 to 200).").length).toBeGreaterThan(0);
  });

  it("qualitative: outside bench mode nothing is kept as a draft", async () => {
    const user = userEvent.setup();
    renderPanel(false, vi.fn(), false);
    await failOdourAndComment(user);
    await new Promise((r) => setTimeout(r, 700));
    expect(await rawStoredDrafts()).toEqual([]);
  });

  it("restored draft keeps unchosen parameter rows unchosen", async () => {
    const user = userEvent.setup();
    const first = renderPanel(false, vi.fn(), true);
    await screen.findByText(/Qualitative Observations/i);

    // Choose Complies for Description only; leave Odour unchosen.
    const descGroup = screen.getByLabelText("Conformance for Description");
    await user.click(within(descGroup).getByRole("button", { name: /^Complies$/i }));

    await new Promise((r) => setTimeout(r, 600));
    await waitFor(async () => expect((await storedDraft())?.values).toBeDefined());

    first.unmount();
    renderPanel(false, vi.fn(), true);
    await screen.findByRole("region", { name: "Unsaved entry found" });
    await user.click(screen.getByRole("button", { name: "Restore entry" }));

    const restoredDesc = screen.getByLabelText("Conformance for Description");
    const restoredOdour = screen.getByLabelText("Conformance for Odour");
    expect(within(restoredDesc).getByRole("button", { name: /^Complies$/i }).getAttribute("aria-pressed")).toBe("true");
    expect(within(restoredOdour).getByRole("button", { name: /^Complies$/i }).getAttribute("aria-pressed")).toBe("false");
    expect(within(restoredOdour).getByRole("button", { name: /Does not comply/i }).getAttribute("aria-pressed")).toBe("false");

    const signBtn = screen.getByRole("button", { name: "Sign and record result" }) as HTMLButtonElement;
    expect(signBtn.disabled).toBe(true);
    expect(screen.getByText("Choose Complies or Does not comply for every row.")).toBeTruthy();
  });

  it("restored draft keeps unchosen container rows unchosen", async () => {
    const user = userEvent.setup();
    vi.mocked(SpecificationService.getForTestOrder).mockResolvedValue([
      { id: 101, testCode: "DESC", parameterName: "Description", expectedResultText: "White powder", displayOrder: 1 }
    ] as never);

    const first = renderPanel(true, vi.fn(), true);
    await screen.findByText(/Qualitative Observations/i);

    await user.click(screen.getByLabelText(/Containers received/i));
    await user.click(screen.getByRole("button", { name: "2" }));
    await user.click(screen.getByRole("button", { name: "Next" }));

    // Choose Complies for container 1 only; leave container 2 unchosen.
    const c1 = screen.getByLabelText("Container 1 result");
    await user.click(within(c1).getByRole("button", { name: /^Complies$/i }));

    await new Promise((r) => setTimeout(r, 600));
    await waitFor(async () => expect((await storedDraft())?.values).toBeDefined());

    first.unmount();
    renderPanel(true, vi.fn(), true);
    await screen.findByRole("region", { name: "Unsaved entry found" });
    await user.click(screen.getByRole("button", { name: "Restore entry" }));

    const restoredC1 = screen.getByLabelText("Container 1 result");
    const restoredC2 = screen.getByLabelText("Container 2 result");
    expect(within(restoredC1).getByRole("button", { name: /^Complies$/i }).getAttribute("aria-pressed")).toBe("true");
    expect(within(restoredC2).getByRole("button", { name: /^Complies$/i }).getAttribute("aria-pressed")).toBe("false");
    expect(within(restoredC2).getByRole("button", { name: /Does not comply/i }).getAttribute("aria-pressed")).toBe("false");

    const signBtn = screen.getByRole("button", { name: "Sign and record result" }) as HTMLButtonElement;
    expect(signBtn.disabled).toBe(true);
    expect(screen.getByText("Choose Complies or Does not comply for every row.")).toBeTruthy();
  });
});

describe("explicit qualitative choice (no pre-selected default)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date(2026, 9, 11, 9, 0, 30));
    vi.mocked(SpecificationService.getForTestOrder).mockResolvedValue([
      { id: 101, testCode: "DESC", parameterName: "Description", expectedResultText: "White powder", displayOrder: 1 },
      { id: 102, testCode: "DESC", parameterName: "Odour", expectedResultText: "Odourless", displayOrder: 2 }
    ] as never);
    vi.mocked(ItemService.getAll).mockResolvedValue([]);
    vi.mocked(laboratorySectionService.getSections).mockResolvedValue([]);
    vi.mocked(EquipmentConfigurationService.getConfiguredSummary).mockResolvedValue([]);
    vi.mocked(TestWorkflowService.recordQualitativeResult).mockResolvedValue({ outcomeSummary: "Pass", status: "Completed" } as never);
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("rows start unchosen, Sign is disabled with the reason tied via aria-describedby, and groups are keyboard operable", async () => {
    const user = userEvent.setup();
    renderPanel(false);
    await screen.findByText(/Qualitative Observations/i);

    const descGroup = screen.getByLabelText("Conformance for Description");
    const odourGroup = screen.getByLabelText("Conformance for Odour");
    const descComplies = within(descGroup).getByRole("button", { name: /^Complies$/i });
    const descNonComply = within(descGroup).getByRole("button", { name: /Does not comply/i });
    const odourComplies = within(odourGroup).getByRole("button", { name: /^Complies$/i });
    const odourNonComply = within(odourGroup).getByRole("button", { name: /Does not comply/i });

    for (const btn of [descComplies, descNonComply, odourComplies, odourNonComply]) {
      expect(btn.getAttribute("aria-pressed")).toBe("false");
    }

    const signBtn = screen.getByRole("button", { name: "Sign and record result" }) as HTMLButtonElement;
    expect(signBtn.disabled).toBe(true);
    const reason = screen.getByText("Choose Complies or Does not comply for every row.");
    expect(reason).toBeTruthy();
    expect(signBtn.getAttribute("aria-describedby")).toBe(reason.getAttribute("id"));

    // Keyboard operability: focus and press Space on Description Complies -> Odour still unchosen so Sign stays disabled
    descComplies.focus();
    await user.keyboard(" ");
    expect(descComplies.getAttribute("aria-pressed")).toBe("true");
    expect(signBtn.disabled).toBe(true);
    expect(screen.getByText("Choose Complies or Does not comply for every row.")).toBeTruthy();

    // Keyboard operability: focus and press Enter on Odour Complies -> all rows chosen, Sign enabled, reason gone
    odourComplies.focus();
    await user.keyboard("{Enter}");
    expect(odourComplies.getAttribute("aria-pressed")).toBe("true");
    expect(signBtn.disabled).toBe(false);
    expect(screen.queryByText("Choose Complies or Does not comply for every row.")).toBeNull();

    await signWith(user, "secret123");
    await waitFor(() => expect(TestWorkflowService.recordQualitativeResult).toHaveBeenCalledTimes(1));
    expect(TestWorkflowService.recordQualitativeResult).toHaveBeenCalledWith(42, {
      analysedAt: new Date(2026, 9, 11, 9, 0).toISOString(),
      equipmentId: null,
      parameters: [
        { specificationId: 101, conforms: true, observation: null },
        { specificationId: 102, conforms: true, observation: null }
      ],
      password: "secret123",
      comment: null
    });
  });
});
