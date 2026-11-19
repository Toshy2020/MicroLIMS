import { useState } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { BenchDialog } from "../components/bench/BenchDialog";
import { renderWithTheme } from "../components/bench/benchTestUtils";
import { UpdatePrompt } from "./UpdatePrompt";
import {
  registerServiceWorker,
  resetServiceWorkerStateForTest,
  setPendingUpdateForTest,
} from "./registerServiceWorker";

afterEach(() => {
  cleanup();
  localStorage.clear();
  resetServiceWorkerStateForTest();
});

function ShellWithBenchDialog() {
  const [open, setOpen] = useState(true);
  return (
    <>
      <BenchDialog
        open={open}
        title="Record counts"
        onClose={() => setOpen(false)}
        onSave={() => {}}
        saving={false}
        dirty={false}
      >
        <div>Plate entry</div>
      </BenchDialog>
      <UpdatePrompt />
    </>
  );
}

describe("UpdatePrompt", () => {
  it("UpdatePrompt shows \"A new version is available.\" with Reload; does not appear while a bench dialog is open; appears after it closes", async () => {
    const user = userEvent.setup();
    renderWithTheme(<ShellWithBenchDialog />, { bench: true });

    expect(screen.getByRole("dialog")).toBeTruthy();

    act(() => {
      setPendingUpdateForTest(vi.fn());
    });

    expect(screen.queryByText("A new version is available.")).toBeNull();

    await user.click(screen.getByRole("button", { name: "Cancel" }));

    const status = await screen.findByRole("status");
    expect(status.getAttribute("aria-live")).toBe("polite");
    expect(status.textContent).toContain("A new version is available.");
    expect(screen.getByRole("button", { name: "Reload" })).toBeTruthy();
  });

  it("Reload is never called without a click", async () => {
    const user = userEvent.setup();
    const reload = vi.fn();
    renderWithTheme(<UpdatePrompt />, { bench: true });

    act(() => {
      setPendingUpdateForTest(reload);
    });

    expect(screen.getByText("A new version is available.")).toBeTruthy();
    expect(reload).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Reload" }));
    expect(reload).toHaveBeenCalledTimes(1);
    expect(reload).toHaveBeenCalledWith(true);
  });

  it("registerServiceWorker is a no-op in tests", () => {
    const register = vi.fn();
    Object.defineProperty(navigator, "serviceWorker", {
      configurable: true,
      value: { register },
    });
    try {
      registerServiceWorker();
      expect(register).not.toHaveBeenCalled();
    } finally {
      Reflect.deleteProperty(navigator, "serviceWorker");
    }
  });
});
