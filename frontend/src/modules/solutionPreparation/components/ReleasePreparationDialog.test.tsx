import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { ReleasePreparationDialog } from "./ReleasePreparationDialog";
import { SolutionPreparationService } from "../services/SolutionPreparationService";

vi.mock("../../../contexts/AuthContext", () => ({
  useAuth: () => ({ fullName: "Head One", username: "head", role: "SectionHead" })
}));
vi.mock("../services/SolutionPreparationService", () => ({
  SolutionPreparationService: { release: vi.fn() }
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const renderDialog = (onSuccess = vi.fn(), onClose = vi.fn()) =>
  render(
    <PinnedLightTheme>
      <ReleasePreparationDialog
        open
        preparationId={31}
        code="DL-PB01/11/2026"
        solutionName="Phosphate buffer"
        onClose={onClose}
        onSuccess={onSuccess}
      />
    </PinnedLightTheme>
  );

const signIn = async () => {
  await userEvent.click(screen.getByRole("button", { name: "Continue to sign" }));
  await userEvent.type(await screen.findByLabelText(/Password/), "secret");
  await userEvent.click(screen.getByRole("button", { name: "Sign" }));
};

describe("ReleasePreparationDialog", () => {
  it("signs an approval and posts the decision", async () => {
    vi.mocked(SolutionPreparationService.release).mockResolvedValue({} as never);
    const onSuccess = vi.fn();
    const onClose = vi.fn();
    renderDialog(onSuccess, onClose);

    await signIn();

    await waitFor(() =>
      expect(SolutionPreparationService.release).toHaveBeenCalledWith(31, { approved: true, comment: null, password: "secret" })
    );
    expect(onSuccess).toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it("shows the server's message when a rejection has no comment", async () => {
    vi.mocked(SolutionPreparationService.release).mockRejectedValue({
      response: { data: { message: "A comment is required to reject a preparation." } }
    });
    const onSuccess = vi.fn();
    renderDialog(onSuccess);

    await userEvent.click(screen.getByLabelText("Reject"));
    await signIn();

    expect(await screen.findByText("A comment is required to reject a preparation.")).toBeTruthy();
    expect(SolutionPreparationService.release).toHaveBeenCalledWith(31, { approved: false, comment: null, password: "secret" });
    expect(onSuccess).not.toHaveBeenCalled();
  });

  it("sends the trimmed comment with a rejection", async () => {
    vi.mocked(SolutionPreparationService.release).mockResolvedValue({} as never);
    renderDialog();

    await userEvent.click(screen.getByLabelText("Reject"));
    await userEvent.type(screen.getByLabelText(/^Comment/), "  Turbid  ");
    await signIn();

    await waitFor(() =>
      expect(SolutionPreparationService.release).toHaveBeenCalledWith(31, { approved: false, comment: "Turbid", password: "secret" })
    );
  });
});
