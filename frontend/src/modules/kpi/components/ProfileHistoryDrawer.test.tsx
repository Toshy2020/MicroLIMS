import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { ProfileHistoryDrawer } from "./ProfileHistoryDrawer";
import { workflowProfileService } from "../services/workflowProfileService";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import type { WorkflowProfileVersion } from "../types/workflowProfileTypes";

const auth = vi.hoisted(() => ({ canConfigure: false }));

vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ fullName: "Sam Signer", username: "sam", role: "SectionHead", permissions: [] }),
  useHasPermission: () => auth.canConfigure
}));
vi.mock("../services/workflowProfileService", () => ({
  workflowProfileService: { list: vi.fn(), create: vi.fn(), signDraft: vi.fn() }
}));

const row = (overrides: Partial<WorkflowProfileVersion>): WorkflowProfileVersion => ({
  id: 1,
  workflowType: "CountTest",
  testCode: null,
  testName: null,
  workUnits: 1,
  targetAnalystDays: 7,
  effectiveFrom: "2026-01-01",
  status: "Effective",
  reasonForChange: "Baseline",
  electronicSignatureId: 5,
  createdByUserId: 2,
  createdByName: "Jane Head",
  createdAt: "2026-01-01T00:00:00Z",
  signedAt: "2026-01-01T00:00:00Z",
  version: 1,
  ...overrides
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  auth.canConfigure = false;
});

const renderDrawer = (testCode: string | null) =>
  render(
    <PinnedLightTheme>
      <ProfileHistoryDrawer open onClose={() => {}} workflowType="CountTest" testCode={testCode} />
    </PinnedLightTheme>
  );

describe("ProfileHistoryDrawer", () => {
  it("lists the type defaults with who, when and why", async () => {
    vi.mocked(workflowProfileService.list).mockResolvedValue([
      row({ id: 2, status: "Draft", reasonForChange: "Imported from legacy workload weight", createdByName: "System (legacy import)", signedAt: null }),
      row({ id: 1 })
    ]);

    renderDrawer(null);

    await waitFor(() => expect(screen.getByText("Baseline")).toBeTruthy());
    expect(workflowProfileService.list).toHaveBeenCalledWith({ workflowType: "CountTest", defaultOnly: true });
    expect(screen.getByText("Imported from legacy workload weight")).toBeTruthy();
    expect(screen.getByText("Jane Head")).toBeTruthy();
    expect(screen.getByText("Draft")).toBeTruthy();
  });

  it("asks for one test's rows by test code only", async () => {
    vi.mocked(workflowProfileService.list).mockResolvedValue([]);

    renderDrawer("TAMC");

    await waitFor(() => expect(workflowProfileService.list).toHaveBeenCalledWith({ testCode: "TAMC" }));
    expect(screen.getByText(/No versions yet/i)).toBeTruthy();
  });

  it("offers Review and sign for a Draft only to someone who may configure", async () => {
    vi.mocked(workflowProfileService.list).mockResolvedValue([row({ id: 2, status: "Draft", signedAt: null })]);

    renderDrawer(null);
    await waitFor(() => expect(screen.getByText("Draft")).toBeTruthy());
    expect(screen.queryByRole("button", { name: /Review and sign/i })).toBeNull();

    cleanup();
    auth.canConfigure = true;
    renderDrawer(null);
    await waitFor(() => expect(screen.getByRole("button", { name: /Review and sign/i })).toBeTruthy());
  });

  it("shows the server message when loading fails", async () => {
    vi.mocked(workflowProfileService.list).mockRejectedValue({ response: { data: { message: "Not allowed" } } });

    renderDrawer(null);

    await waitFor(() => expect(screen.getByText("Not allowed")).toBeTruthy());
  });
});
