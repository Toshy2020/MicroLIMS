import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { ConfirmPreparationForm } from "./ConfirmPreparationForm";
import type { ItemPreparationConfiguration } from "./services/ItemPreparationConfigurationService";

const auth = vi.hoisted(() => ({
  current: { userId: 1, username: "head", fullName: "Section Head", role: "SectionHead" as string, permissions: [] as string[] }
}));

vi.mock("../../contexts/AuthContext", () => ({
  useAuth: () => auth.current
}));

vi.mock("./services/SamplePreparationService", () => ({
  SamplePreparationService: { confirm: vi.fn() }
}));

const config = {
  id: 1,
  itemId: 1,
  amount: 1,
  technique: "PourPlate",
  filtrationVolume: null,
  washingVolume: null,
  diluent: "Peptone water",
  neutralizer: "Tween 80",
  approvalStatus: "Approved",
  createdByUserId: 9,
  createdByName: "Config Author",
  createdAt: "2026-10-01T00:00:00Z",
  approvedByUserId: 9,
  approvedByName: "Config Author",
  approvedAt: "2026-10-02T00:00:00Z"
} as ItemPreparationConfiguration;

// Assigned to analyst #2 only.
const sample = {
  sampleId: 42,
  assignedAnalystIds: [2],
  assignedAnalystNames: ["Analyst Two"]
};

function renderForm() {
  return render(
    <PinnedLightTheme>
      <ConfirmPreparationForm sample={sample} config={config} onSaved={vi.fn()} />
    </PinnedLightTheme>
  );
}

afterEach(cleanup);

const isDisabled = (el: HTMLElement) => (el as HTMLButtonElement).disabled;

describe("ConfirmPreparationForm assignment lock", () => {
  it("locks confirmation for a SectionHead who is not an assigned analyst", () => {
    auth.current = { ...auth.current, userId: 1, role: "SectionHead" };
    renderForm();

    expect(screen.queryByText("Sample Assignment Rule:")).not.toBeNull();
    expect(isDisabled(screen.getByRole("button", { name: "Confirm & Start Testing" }))).toBe(true);
  });

  it("does not lock confirmation for the assigned analyst", () => {
    auth.current = { ...auth.current, userId: 2, role: "Analyst" };
    renderForm();

    expect(screen.queryByText("Sample Assignment Rule:")).toBeNull();
    expect(isDisabled(screen.getByRole("button", { name: "Confirm & Start Testing" }))).toBe(false);
  });
});
