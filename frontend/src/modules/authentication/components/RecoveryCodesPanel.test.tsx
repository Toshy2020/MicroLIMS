import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { RecoveryCodesPanel } from "./RecoveryCodesPanel";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

describe("RecoveryCodesPanel", () => {
  it("Continue is disabled until \"I have saved these codes\" is ticked", async () => {
    const user = userEvent.setup();
    const onDone = vi.fn();
    render(
      <PinnedLightTheme>
        <RecoveryCodesPanel codes={["AAAA-1111", "BBBB-2222"]} onDone={onDone} />
      </PinnedLightTheme>
    );

    expect(screen.getByText("AAAA-1111")).toBeTruthy();
    expect(screen.getByText("BBBB-2222")).toBeTruthy();

    const continueButton = screen.getByRole("button", { name: "Continue" }) as HTMLButtonElement;
    expect(continueButton.disabled).toBe(true);

    await user.click(screen.getByRole("checkbox", { name: "I have saved these codes" }));
    expect(continueButton.disabled).toBe(false);

    await user.click(continueButton);
    expect(onDone).toHaveBeenCalledTimes(1);
  });

  it("supports copying and printing recovery codes", async () => {
    const user = userEvent.setup();
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText },
      configurable: true
    });
    const printSpy = vi.spyOn(window, "print").mockImplementation(() => {});

    render(
      <PinnedLightTheme>
        <RecoveryCodesPanel codes={["AAAA-1111", "BBBB-2222"]} onDone={vi.fn()} />
      </PinnedLightTheme>
    );

    await user.click(screen.getByRole("button", { name: "Copy codes" }));
    expect(writeText).toHaveBeenCalledWith("AAAA-1111\nBBBB-2222");

    await user.click(screen.getByRole("button", { name: "Print" }));
    expect(printSpy).toHaveBeenCalledTimes(1);
  });
});
