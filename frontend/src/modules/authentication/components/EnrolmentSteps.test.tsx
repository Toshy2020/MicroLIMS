import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { EnrolmentSteps } from "./EnrolmentSteps";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

const sampleStart = {
  manualKey: "JBSWY3DPEHPK3PXP",
  otpAuthUri: "otpauth://totp/MicroLIMS:sam?secret=JBSWY3DPEHPK3PXP",
  qrSvg: '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="10" height="10"/></svg>'
};

describe("EnrolmentSteps", () => {
  it("shows the QR image with alt text, and the key as selectable text", async () => {
    const start = vi.fn().mockResolvedValue(sampleStart);
    const { container } = render(
      <PinnedLightTheme>
        <EnrolmentSteps start={start} confirm={vi.fn()} onDone={vi.fn()} />
      </PinnedLightTheme>
    );

    const img = (await screen.findByAltText(
      "QR code to add MicroLIMS to your authenticator app"
    )) as HTMLImageElement;
    expect(img.src.startsWith("data:image/svg+xml")).toBe(true);
    expect(container.querySelector("svg")).toBeNull();

    const keyEl = screen.getByText("JBSW Y3DP EHPK 3PXP");
    expect(keyEl).toBeTruthy();
  });

  it("wrong code shows an inline error and stays on the code step", async () => {
    const user = userEvent.setup();
    const start = vi.fn().mockResolvedValue(sampleStart);
    const confirm = vi.fn().mockRejectedValue({
      response: { status: 400, data: { message: "The code is not correct." } }
    });
    render(
      <PinnedLightTheme>
        <EnrolmentSteps start={start} confirm={confirm} onDone={vi.fn()} />
      </PinnedLightTheme>
    );

    const codeInput = await screen.findByLabelText("6-digit code");
    await user.type(codeInput, "111111");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("The code is not correct.");
    expect(screen.getByLabelText("6-digit code")).toBeTruthy();
    await waitFor(() => expect(document.activeElement).toBe(screen.getByLabelText("6-digit code")));
  });

  it("Continue is disabled until \"I have saved these codes\" is ticked", async () => {
    const user = userEvent.setup();
    const start = vi.fn().mockResolvedValue(sampleStart);
    const confirm = vi.fn().mockResolvedValue({ recoveryCodes: ["CODE-1111", "CODE-2222"] });
    const onDone = vi.fn();
    render(
      <PinnedLightTheme>
        <EnrolmentSteps start={start} confirm={confirm} onDone={onDone} />
      </PinnedLightTheme>
    );

    await user.type(await screen.findByLabelText("6-digit code"), "123456");
    await user.click(screen.getByRole("button", { name: "Confirm" }));

    await waitFor(() => expect(screen.getByText("CODE-1111")).toBeTruthy());
    const continueBtn = screen.getByRole("button", { name: "Continue" }) as HTMLButtonElement;
    expect(continueBtn.disabled).toBe(true);

    await user.click(screen.getByRole("checkbox", { name: "I have saved these codes" }));
    expect(continueBtn.disabled).toBe(false);
    await user.click(continueBtn);
    expect(onDone).toHaveBeenCalledTimes(1);
  });
});
