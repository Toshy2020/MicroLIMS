import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { HplcSampleEntryPage } from "./HplcSampleEntryPage";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import type { HplcSampleEntryDto } from "../types";

const auth = vi.hoisted(() => ({
  current: {
    userId: 1,
    username: "admin",
    fullName: "System Admin",
    role: "SystemAdministrator" as string,
    permissions: [] as string[]
  }
}));

vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => auth.current
}));

vi.mock("../services/HplcWorkspaceService", () => ({
  HplcWorkspaceService: {
    getSampleEntry: vi.fn(),
    getRun: vi.fn().mockResolvedValue(null),
    saveReplicates: vi.fn()
  }
}));

vi.mock("../../laboratoryConfiguration/masterDataSimple/services/HplcMethodService", () => ({
  HplcMethodService: { getById: vi.fn() }
}));

vi.mock("../evidence/ReportUploadPanel", () => ({
  ReportUploadPanel: () => null
}));

vi.mock("./SendForReviewDialog", () => ({
  SendForReviewDialog: () => null
}));

vi.mock("react-router-dom", async (importOriginal) => {
  const actual = await importOriginal<typeof import("react-router-dom")>();
  return {
    ...actual,
    useParams: () => ({ instrumentId: "3", runId: "7", runSampleId: "101" }),
    useNavigate: () => vi.fn()
  };
});

// An editable, not-yet-submitted sample: the only thing that differs between
// the tests below is who is signed in.
const editableSample: HplcSampleEntryDto = {
  runSampleId: 101,
  hplcRunId: 7,
  runCode: "RUN-HPLC-2026-0001",
  sstCode: "SST-1",
  sstStatus: "Passed",
  testOrderId: 501,
  sampleNumber: "SMP-2026-0012",
  batchNumber: "B260901",
  productName: "Test Tablet",
  testCode: "HPLC-ASSAY",
  stageName: "Finished Product",
  status: "Assigned",
  basis: "Label",
  requiredReplicates: 1,
  methodWeights: [
    {
      hplcMethodAnalyteId: 10,
      analyteName: "Methanol",
      theoreticalWeightStdMg: 100,
      theoreticalWeightTestMg: 100
    }
  ],
  replicates: [
    {
      replicateNo: 1,
      actualWeightMg: 100,
      responses: [{ hplcMethodAnalyteId: 10, response: 5 }]
    }
  ],
  preview: [],
  official: [],
  evidence: [],
  editable: true,
  editableReason: null,
  submitted: false,
  canSubmit: true,
  canSubmitReason: null
} as unknown as HplcSampleEntryDto;

function renderPage() {
  return render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={["/hplc/3/run/7/sample/101"]}>
        <HplcSampleEntryPage />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

beforeEach(() => {
  vi.mocked(HplcWorkspaceService.getSampleEntry).mockResolvedValue(editableSample);
});

afterEach(cleanup);

const isDisabled = (el: HTMLElement) => (el as HTMLButtonElement | HTMLInputElement).disabled;

describe("HplcSampleEntryPage operator controls", () => {
  it("gives a SystemAdministrator without Hplc.Operate read-only entry controls", async () => {
    auth.current = { ...auth.current, role: "SystemAdministrator", permissions: [] };
    renderPage();

    const sendButton = await screen.findByRole("button", { name: "Send for Review" });
    expect(isDisabled(sendButton)).toBe(true);

    const inputs = screen.getAllByRole("spinbutton");
    expect(inputs.length).toBeGreaterThan(0);
    for (const input of inputs) {
      expect(isDisabled(input)).toBe(true);
    }
  });

  it("enables the same entry controls for a user who holds Hplc.Operate", async () => {
    auth.current = { ...auth.current, role: "Analyst", permissions: ["Hplc.Operate"] };
    renderPage();

    const sendButton = await screen.findByRole("button", { name: "Send for Review" });
    await waitFor(() => expect(isDisabled(sendButton)).toBe(false));

    for (const input of screen.getAllByRole("spinbutton")) {
      expect(isDisabled(input)).toBe(false);
    }
  });
});
