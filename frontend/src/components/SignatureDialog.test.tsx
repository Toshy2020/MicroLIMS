import { useState } from "react";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { AuthProvider, type SignInMethod } from "../contexts/AuthContext";
import { SignatureDialog } from "./SignatureDialog";

function seedAuth(signInMethod: SignInMethod = "Local") {
  const payload = btoa(
    JSON.stringify({
      "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "SectionHead",
      permission: "Samples.View"
    })
  );
  localStorage.setItem("microlims_token", `h.${payload}.s`);
  localStorage.setItem("microlims_username", "head");
  localStorage.setItem("microlims_full_name", "Head One");
  localStorage.setItem("microlims_sign_in_method", signInMethod);
}

function renderDialog(ui: React.ReactElement, signInMethod: SignInMethod = "Local") {
  seedAuth(signInMethod);
  return render(<AuthProvider>{ui}</AuthProvider>);
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  vi.clearAllMocks();
});

describe("SignatureDialog", () => {
  it("asks for one password by default and signs with it", async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    renderDialog(<SignatureDialog open meaningStatement="I am approving this." onCancel={() => {}} onConfirm={onConfirm} />);

    expect(screen.getByText("Head One")).toBeTruthy();
    expect(screen.getByText("Section Head")).toBeTruthy();
    expect(screen.getByText("I am approving this.")).toBeTruthy();
    expect(screen.queryByText("Second signature")).toBeNull();
    const sign = screen.getByRole("button", { name: "Sign" }) as HTMLButtonElement;
    expect(sign.disabled).toBe(true);

    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "pw-1" } });
    fireEvent.click(sign);

    await waitFor(() => expect(onConfirm).toHaveBeenCalledWith("pw-1", undefined));
  });

  it("ignores a second Enter while the first confirm is pending", async () => {
    let finish: () => void = () => {};
    const onConfirm = vi.fn().mockReturnValue(new Promise<void>((resolve) => { finish = resolve; }));
    renderDialog(<SignatureDialog open meaningStatement="I am approving this." onCancel={() => {}} onConfirm={onConfirm} />);

    const password = screen.getByLabelText(/^Password/);
    fireEvent.change(password, { target: { value: "pw-1" } });
    fireEvent.keyDown(password, { key: "Enter" });
    fireEvent.keyDown(password, { key: "Enter" });

    expect(onConfirm).toHaveBeenCalledTimes(1);
    finish();
    await waitFor(() => expect(screen.getByRole("button", { name: "Sign" })).toBeTruthy());
  });

  it("with a second signer, waits for both and passes both", async () => {
    const onConfirm = vi.fn().mockResolvedValue(undefined);
    renderDialog(<SignatureDialog open meaningStatement="I am skipping it." requireSecondSigner onCancel={() => {}} onConfirm={onConfirm} />);

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
    renderDialog(<SignatureDialog open meaningStatement="I am skipping it." requireSecondSigner onCancel={() => {}} onConfirm={onConfirm} />);

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

describe("SignatureDialog required comment", () => {
  it("labels the comment as required and keeps Sign disabled until it is filled", () => {
    const Harness = () => {
      const [c, setC] = useState("");
      return <SignatureDialog open meaningStatement="I am withdrawing." showComment commentLabel="Reason (required)" commentRequired
        comment={c} onCommentChange={setC} onCancel={() => {}} onConfirm={async () => {}} />;
    };
    renderDialog(<Harness />);
    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: "pw" } });
    const sign = screen.getByRole("button", { name: "Sign" }) as HTMLButtonElement;
    expect(sign.disabled).toBe(true);
    fireEvent.change(screen.getByLabelText(/Reason \(required\)/), { target: { value: "Wrong item" } });
    expect(sign.disabled).toBe(false);
  });
});

describe("SignatureDialog directory vs local signing password label", () => {
  it("shows Signing password and hint for a directory user", () => {
    renderDialog(
      <SignatureDialog open meaningStatement="I am approving this." onCancel={() => {}} onConfirm={async () => {}} />,
      "Directory"
    );

    expect(screen.getByLabelText(/^Signing password/)).toBeTruthy();
    expect(screen.getByText("This is your MicroLIMS signing password, not your company password.")).toBeTruthy();
  });

  it("shows Password without hint for a local user", () => {
    renderDialog(
      <SignatureDialog open meaningStatement="I am approving this." onCancel={() => {}} onConfirm={async () => {}} />,
      "Local"
    );

    expect(screen.getByLabelText(/^Password/)).toBeTruthy();
    expect(screen.queryByText("This is your MicroLIMS signing password, not your company password.")).toBeNull();
  });
});
