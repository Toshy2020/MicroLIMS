import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { ConfirmPreparationForm } from "./ConfirmPreparationForm";
import type { ItemPreparationConfiguration } from "./services/ItemPreparationConfigurationService";

const auth = vi.hoisted(() => ({
  current: { userId: 1, username: "head", fullName: "Section Head", role: "SectionHead" as string, permissions: [] as string[] }
}));

vi.mock("../../contexts/AuthContext", () => ({
  useAuth: () => auth.current
}));

const svc = vi.hoisted(() => ({ confirm: vi.fn(), getLotOptions: vi.fn() }));
vi.mock("./services/SamplePreparationService", () => ({ SamplePreparationService: svc }));
vi.mock("../../components/SignatureDialog", () => ({
  SignatureDialog: ({ open, onConfirm }: any) =>
    open ? <button onClick={() => onConfirm("pw")}>sign</button> : null
}));

const noLots = {
  configurationId: 1, needsMaterialMapping: false,
  diluent: { source: "ContainedInMedium", name: "", lotRequired: false, options: [] },
  neutralizer: { source: "ContainedInMedium", name: "", lotRequired: false, options: [] }
};
const withLots = {
  configurationId: 1, needsMaterialMapping: false,
  diluent: {
    source: "SolutionRecipe", name: "Saline", lotRequired: true,
    options: [
      { id: 101, code: "SP-101", expiresAt: "2027-01-31T00:00:00Z", usable: true, reason: null },
      { id: 102, code: "SP-102", expiresAt: "2026-01-01T00:00:00Z", usable: false, reason: "Expired" }
    ]
  },
  neutralizer: {
    source: "MaterialEntry", name: "Polysorbate 80", lotRequired: true,
    options: [{ id: 201, code: "RM-201", expiresAt: null, usable: true, reason: null }]
  }
};

beforeEach(() => {
  svc.confirm.mockReset().mockResolvedValue({});
  svc.getLotOptions.mockReset().mockResolvedValue(noLots);
});

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
  it("locks confirmation for a SectionHead who is not an assigned analyst", async () => {
    auth.current = { ...auth.current, userId: 1, role: "SectionHead" };
    renderForm();

    expect(screen.queryByText("Sample Assignment Rule:")).not.toBeNull();
    expect(isDisabled(screen.getByRole("button", { name: "Confirm & Start Testing" }))).toBe(true);
  });

  it("does not lock confirmation for the assigned analyst", async () => {
    auth.current = { ...auth.current, userId: 2, role: "Analyst" };
    renderForm();
    await waitFor(() => expect(isDisabled(screen.getByRole("button", { name: "Confirm & Start Testing" }))).toBe(false));

    expect(screen.queryByText("Sample Assignment Rule:")).toBeNull();
    expect(isDisabled(screen.getByRole("button", { name: "Confirm & Start Testing" }))).toBe(false);
  });
});

describe("ConfirmPreparationForm lots", () => {
  const pickLot = async (label: RegExp, option: RegExp) => {
    fireEvent.mouseDown(screen.getByRole("combobox", { name: label }));
    fireEvent.click(await screen.findByRole("option", { name: option }));
  };

  it("needs a lot per role, disables unusable lots with the reason, and sends both lot ids", async () => {
    auth.current = { ...auth.current, userId: 2, role: "Analyst" };
    svc.getLotOptions.mockResolvedValue(withLots);
    renderForm();
    expect(svc.getLotOptions).toHaveBeenCalledWith({ sampleId: 42 });

    const confirmBtn = await screen.findByRole("button", { name: "Confirm & Start Testing" });
    await screen.findByRole("combobox", { name: /Diluent lot/ });
    expect(isDisabled(confirmBtn)).toBe(true);

    fireEvent.mouseDown(screen.getByRole("combobox", { name: /Diluent lot/ }));
    const expired = await screen.findByRole("option", { name: /SP-102.*Expired/ });
    expect(expired.getAttribute("aria-disabled")).toBe("true");
    fireEvent.click(screen.getByRole("option", { name: /SP-101/ }));
    expect(isDisabled(confirmBtn)).toBe(true);

    await pickLot(/Neutralizer lot/, /RM-201/);
    expect(isDisabled(confirmBtn)).toBe(false);

    fireEvent.click(confirmBtn);
    fireEvent.click(await screen.findByText("sign"));
    await waitFor(() => expect(svc.confirm).toHaveBeenCalledWith({
      sampleId: 42, password: "pw", diluentLotId: 101, neutralizerLotId: 201
    }));
  });

  it("shows a warning and no picker for a legacy configuration, and sends no lots", async () => {
    auth.current = { ...auth.current, userId: 2, role: "Analyst" };
    svc.getLotOptions.mockResolvedValue({
      ...noLots, needsMaterialMapping: true,
      diluent: { source: null, name: "Peptone water", lotRequired: false, options: [] },
      neutralizer: { source: null, name: "Tween 80", lotRequired: false, options: [] }
    });
    renderForm();
    expect(await screen.findByText(/not linked to master data/)).toBeTruthy();
    expect(screen.queryByRole("combobox", { name: /lot/i })).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: "Confirm & Start Testing" }));
    fireEvent.click(await screen.findByText("sign"));
    await waitFor(() => expect(svc.confirm).toHaveBeenCalledWith({
      sampleId: 42, password: "pw", diluentLotId: null, neutralizerLotId: null
    }));
  });
});
