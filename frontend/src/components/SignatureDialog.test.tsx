import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { SignatureDialog } from "./SignatureDialog";

vi.mock("../contexts/AuthContext", () => ({
  useAuth: () => ({ fullName: "Head One", username: "head", role: "SectionHead" })
}));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

describe("SignatureDialog", () => {
  it("asks for one password by default and signs with it", async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(<SignatureDialog open meaningStatement="I am approving this." onCancel={() => {}} onConfirm={onConfirm} />);

    expect(screen.getByText("Head One")).toBeTruthy();
    expect(screen.getByText("I am approving this.")).toBeTruthy();
    expect(screen.queryByText("Second signature")).toBeNull();
    const sign = screen.getByRole("button", { name: "Sign" }) as HTMLButtonElement;
    expect(sign.disabled).toBe(true);

    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "pw-1" } });
    fireEvent.click(sign);

    await waitFor(() => expect(onConfirm).toHaveBeenCalledWith("pw-1", undefined));
  });

  it("with a second signer, waits for both and passes both", async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    render(<SignatureDialog open meaningStatement="I am skipping it." requireSecondSigner onCancel={() => {}} onConfirm={onConfirm} />);

    expect(screen.getByText("Second signature")).toBeTruthy();
    const sign = screen.getByRole("button", { name: "Sign" }) as HTMLButtonElement;
    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "pw-1" } });
    expect(sign.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText(/^Second signer username/), { target: { value: " approver " } });
    expect(sign.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText(/^Second signer password/), { target: { value: "pw-2" } });
    expect(sign.disabled).toBe(false);
    fireEvent.click(sign);

    await waitFor(() => expect(onConfirm).toHaveBeenCalledWith("pw-1", { username: "approver", password: "pw-2" }));
  });

  it("shows the server's message, clears the passwords and stays open when signing fails", async () => {
    const onConfirm = vi.fn().mockRejectedValue({ response: { data: { message: "The second signature could not be verified." } } });
    render(<SignatureDialog open meaningStatement="I am skipping it." requireSecondSigner onCancel={() => {}} onConfirm={onConfirm} />);

    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "pw-1" } });
    fireEvent.change(screen.getByLabelText(/^Second signer username/), { target: { value: "approver" } });
    fireEvent.change(screen.getByLabelText(/^Second signer password/), { target: { value: "bad" } });
    fireEvent.click(screen.getByRole("button", { name: "Sign" }));

    expect(await screen.findByText("The second signature could not be verified.")).toBeTruthy();
    expect((screen.getByLabelText(/^Password/) as HTMLInputElement).value).toBe("");
    expect((screen.getByLabelText(/^Second signer password/) as HTMLInputElement).value).toBe("");
    expect((screen.getByLabelText(/^Second signer username/) as HTMLInputElement).value).toBe("approver");
  });
});
