import { describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { apiClient } from "../../services/apiClient";
import { ConnectionBanner } from "./ConnectionBanner";
import { renderWithTheme } from "./benchTestUtils";

vi.mock("../../services/apiClient", () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), patch: vi.fn(), delete: vi.fn() }
}));

describe("ConnectionBanner", () => {
  it("banner text is exactly \"No connection. Your entries are not saved to MicroLIMS. They are kept on this tablet until you can save.\" with role=\"alert\"", () => {
    renderWithTheme(<ConnectionBanner online={false} unsavedCount={0} />, { bench: true });

    const alert = screen.getByRole("alert");
    expect(alert.textContent).toBe(
      "No connection. Your entries are not saved to MicroLIMS. They are kept on this tablet until you can save."
    );
  });

  it("shows nothing while online and nothing has happened", () => {
    renderWithTheme(<ConnectionBanner online unsavedCount={2} />, { bench: false });

    expect(screen.queryByRole("alert")).toBeNull();
    expect(screen.queryByRole("status")).toBeNull();
  });

  it("on reconnect announces \"Connection restored. You have 2 unsaved entries.\" and sends nothing", async () => {
    const user = userEvent.setup();
    const { rerender } = renderWithTheme(<ConnectionBanner online={false} unsavedCount={2} />, { bench: true });
    expect(screen.getByRole("alert")).toBeTruthy();

    rerender(<ConnectionBanner online unsavedCount={2} />);

    expect(screen.queryByRole("alert")).toBeNull();
    expect(screen.getByRole("status").textContent).toContain("Connection restored. You have 2 unsaved entries.");
    for (const method of ["get", "post", "put", "patch", "delete"] as const) {
      expect(apiClient[method]).not.toHaveBeenCalled();
    }

    await user.click(screen.getByRole("button", { name: "Dismiss" }));
    expect(screen.queryByRole("status")).toBeNull();
  });

  it("says only that the connection is restored when nothing is unsaved, and uses the singular for one entry", () => {
    const first = renderWithTheme(<ConnectionBanner online={false} unsavedCount={0} />, { bench: false });
    first.rerender(<ConnectionBanner online unsavedCount={0} />);
    expect(screen.getByRole("status").textContent).toContain("Connection restored.");
    expect(screen.getByRole("status").textContent).not.toContain("unsaved");
    first.unmount();

    const second = renderWithTheme(<ConnectionBanner online={false} unsavedCount={1} />, { bench: false });
    second.rerender(<ConnectionBanner online unsavedCount={1} />);
    expect(screen.getByRole("status").textContent).toContain("You have 1 unsaved entry.");
  });
});
