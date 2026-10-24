import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ApplyCandidate, ApplyLimitsDialog } from "./ApplyLimitsDialog";

vi.mock("../../../../contexts/AuthContext", () => ({
  useAuth: () => ({ fullName: "Head One", username: "head", role: "SectionHead" })
}));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

const candidate: ApplyCandidate = {
  subjectName: "Filling Room",
  proposedAlert: 6,
  proposedAction: 9,
  count: 40,
  belowMinimumPoints: false,
  target: {
    kind: "RoomTestConfiguration", configurationId: 7, version: 3, location: "Filling Room", detail: "Production · Grade C",
    testCode: "EM_TAMC", testType: "Settle Plate", alertLimit: "10", actionLimit: "20", specLimit: "50", unit: "CFU/Plate"
  }
};

function renderDialog(onSubmit = vi.fn().mockResolvedValue([])) {
  const onApplied = vi.fn();
  render(
    <ApplyLimitsDialog open candidates={[candidate]} basisSummary="mean + 2 / 3 σ"
      onCancel={() => {}} onSubmit={onSubmit} onApplied={onApplied} />
  );
  return { onSubmit, onApplied };
}

describe("ApplyLimitsDialog", () => {
  it("prefills the calculated limits and needs a reason before signing", () => {
    renderDialog();

    expect((screen.getByLabelText("New alert limit for Filling Room") as HTMLInputElement).value).toBe("6");
    expect((screen.getByLabelText("New action limit for Filling Room") as HTMLInputElement).value).toBe("9");
    const apply = screen.getByRole("button", { name: /Sign & apply/ }) as HTMLButtonElement;
    expect(apply.disabled).toBe(true);

    fireEvent.change(screen.getByLabelText(/Reason for change/), { target: { value: "Annual review" } });
    expect(apply.disabled).toBe(false);
  });

  it("marks an edited value as an override and submits it with the signer's password", async () => {
    const { onSubmit, onApplied } = renderDialog();

    fireEvent.change(screen.getByLabelText("New action limit for Filling Room"), { target: { value: "12" } });
    expect(screen.getByText("Override")).toBeTruthy();
    fireEvent.change(screen.getByLabelText(/Reason for change/), { target: { value: "Annual review" } });
    fireEvent.click(screen.getByRole("button", { name: /Sign & apply/ }));

    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "pw-1" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => expect(onSubmit).toHaveBeenCalledWith(
      [{ kind: "RoomTestConfiguration", configurationId: 7, version: 3, subjectName: "Filling Room", alertLimit: 6, actionLimit: 12 }],
      "Annual review", "pw-1"));
    await waitFor(() => expect(onApplied).toHaveBeenCalled());
  });
});
