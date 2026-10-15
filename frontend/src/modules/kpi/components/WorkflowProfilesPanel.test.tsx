import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { WorkflowProfilesPanel } from "./WorkflowProfilesPanel";
import { workflowProfileService } from "../services/workflowProfileService";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { WorkflowProfileCoverage, WorkflowProfileOverview } from "../types/workflowProfileTypes";

const auth = vi.hoisted(() => ({ canConfigure: false }));

vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ fullName: "Sam Signer", username: "sam", role: "SectionHead", permissions: [] }),
  useHasPermission: () => auth.canConfigure
}));
vi.mock("../services/workflowProfileService", () => ({
  workflowProfileService: { overview: vi.fn(), coverage: vi.fn(), list: vi.fn(), create: vi.fn(), signDraft: vi.fn() }
}));

const overview: WorkflowProfileOverview = {
  types: [
    {
      workflowType: "CountTest",
      effectiveWorkUnits: 1,
      effectiveTargetAnalystDays: 7,
      lastEffectiveFrom: "2026-01-01",
      lastChangeAt: "2026-01-01T09:00:00Z",
      lastChangeByName: "Jane Head",
      coverage: "Configured",
      draftCount: 1,
      tests: [
        {
          testCode: "TAMC",
          testName: "Total Aerobic Microbial Count",
          effectiveWorkUnits: 4,
          effectiveTargetAnalystDays: 7,
          hasOverride: true,
          coverage: "Configured"
        }
      ]
    },
    {
      workflowType: "Measurement",
      effectiveWorkUnits: null,
      effectiveTargetAnalystDays: null,
      lastEffectiveFrom: null,
      lastChangeAt: null,
      lastChangeByName: null,
      coverage: "NeedsConfiguration",
      draftCount: 0,
      tests: [
        {
          testCode: "PH",
          testName: "pH",
          effectiveWorkUnits: null,
          effectiveTargetAnalystDays: null,
          hasOverride: false,
          coverage: "NeedsConfiguration"
        }
      ]
    }
  ]
};

const coverage: WorkflowProfileCoverage = {
  typesWithoutUnits: ["Measurement"],
  typesWithoutTarget: ["Measurement"],
  testsNeedingConfiguration: [overview.types[1].tests[0]]
};

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  auth.canConfigure = false;
});

function renderPanel() {
  vi.mocked(workflowProfileService.overview).mockResolvedValue(overview);
  vi.mocked(workflowProfileService.coverage).mockResolvedValue(coverage);
  render(
    <PinnedLightTheme>
      <WorkflowProfilesPanel />
    </PinnedLightTheme>
  );
}

describe("WorkflowProfilesPanel", () => {
  it("shows the effective values per workflow type and marks what is not set", async () => {
    renderPanel();

    await waitFor(() => expect(screen.getByText("Count Test")).toBeTruthy());
    expect(screen.getByText("Measurement")).toBeTruthy();
    expect(screen.getAllByText("Not set").length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText("Jane Head")).toBeTruthy();
    expect(screen.getByText("1 draft")).toBeTruthy();
    expect(screen.getByText(/1 workflow type without work units/i)).toBeTruthy();
  });

  it("hides Add version from someone without Kpi.ConfigureProfiles", async () => {
    renderPanel();
    await waitFor(() => expect(screen.getByText("Count Test")).toBeTruthy());

    expect(screen.queryAllByRole("button", { name: /Add version/i })).toHaveLength(0);
    expect(screen.getAllByRole("button", { name: /History/i }).length).toBeGreaterThan(0);
  });

  it("offers Add version to someone with Kpi.ConfigureProfiles", async () => {
    auth.canConfigure = true;
    renderPanel();
    await waitFor(() => expect(screen.getByText("Count Test")).toBeTruthy());

    expect(screen.getAllByRole("button", { name: /Add version/i }).length).toBeGreaterThan(0);
  });

  it("needs-configuration filter hides fully configured types", async () => {
    renderPanel();
    await waitFor(() => expect(screen.getByText("Count Test")).toBeTruthy());

    await userEvent.click(screen.getByLabelText(/Needs configuration only/i));

    expect(screen.queryByText("Count Test")).toBeNull();
    expect(screen.getByText("Measurement")).toBeTruthy();
  });

  it("expands a type to show its tests and flags overrides", async () => {
    renderPanel();
    await waitFor(() => expect(screen.getByText("Count Test")).toBeTruthy());

    await userEvent.click(screen.getByRole("button", { name: /Show tests for Count Test/i }));

    expect(screen.getByText("TAMC")).toBeTruthy();
    expect(screen.getByText("Override")).toBeTruthy();
  });

  it("shows the load error instead of an empty table", async () => {
    vi.mocked(workflowProfileService.overview).mockRejectedValue({ response: { data: { message: "No access" } } });
    vi.mocked(workflowProfileService.coverage).mockResolvedValue(coverage);
    render(
      <PinnedLightTheme>
        <WorkflowProfilesPanel />
      </PinnedLightTheme>
    );

    await waitFor(() => expect(screen.getByText("No access")).toBeTruthy());
    expect(screen.queryByText("Count Test")).toBeNull();
  });
});
