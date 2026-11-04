import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { MaterialReleaseDialog } from "./MaterialReleaseDialog";
import { MaterialService } from "../services/MaterialService";
import type { MaterialItem } from "../types/materialTypes";

vi.mock("../../../../contexts/AuthContext", () => ({
  useAuth: () => ({ fullName: "Head One", username: "head", role: "SectionHead" })
}));
vi.mock("../services/MaterialService", () => ({
  MaterialService: { release: vi.fn() }
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const lot = {
  id: 12, materialName: "Egg Yolk Tellurite Emulsion", batchNumber: "L-77", categoryName: "Supplement", approvalStatus: "PendingReview"
} as MaterialItem;

const renderDialog = (onSuccess = vi.fn(), onClose = vi.fn()) =>
  render(
    <PinnedLightTheme>
      <MaterialReleaseDialog open material={lot} onClose={onClose} onSuccess={onSuccess} />
    </PinnedLightTheme>
  );

const continueButton = () => screen.getByRole("button", { name: "Continue to sign" }) as HTMLButtonElement;

describe("MaterialReleaseDialog", () => {
  it("needs a comment to reject, but not to approve", async () => {
    renderDialog();
    expect(continueButton().disabled).toBe(false);

    await userEvent.click(screen.getByLabelText("Reject"));
    expect(continueButton().disabled).toBe(true);

    await userEvent.type(screen.getByLabelText(/^Comment/), "Seal was broken");
    expect(continueButton().disabled).toBe(false);
  });

  it("signs an approval with the password and reports success", async () => {
    vi.mocked(MaterialService.release).mockResolvedValue({});
    const onSuccess = vi.fn();
    const onClose = vi.fn();
    renderDialog(onSuccess, onClose);

    await userEvent.click(continueButton());
    expect(await screen.findByText("Electronic Signature")).toBeTruthy();
    expect(screen.getByText(/I am releasing Egg Yolk Tellurite Emulsion \(lot L-77\) for use/)).toBeTruthy();
    await userEvent.type(screen.getByLabelText(/Password/), "secret");
    await userEvent.click(screen.getByRole("button", { name: "Sign" }));

    await waitFor(() => expect(MaterialService.release).toHaveBeenCalledWith(12, { password: "secret", approved: true, comment: null }));
    expect(onSuccess).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it("shows the server's refusal and stays open", async () => {
    vi.mocked(MaterialService.release).mockRejectedValue({
      response: { data: { message: "The person who received this lot can't release it." } }
    });
    const onSuccess = vi.fn();
    renderDialog(onSuccess);

    await userEvent.click(screen.getByLabelText("Reject"));
    await userEvent.type(screen.getByLabelText(/^Comment/), "Seal was broken");
    await userEvent.click(continueButton());
    await userEvent.type(await screen.findByLabelText(/Password/), "secret");
    await userEvent.click(screen.getByRole("button", { name: "Sign" }));

    expect(await screen.findByText("The person who received this lot can't release it.")).toBeTruthy();
    expect(MaterialService.release).toHaveBeenCalledWith(12, { password: "secret", approved: false, comment: "Seal was broken" });
    expect(onSuccess).not.toHaveBeenCalled();
  });
});
