import { afterEach, describe, expect, it } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { FloatingDialog } from "./FloatingDialog";
import { renderWithTheme } from "./bench/benchTestUtils";

afterEach(() => {
  cleanup();
  localStorage.clear();
});

describe("FloatingDialog benchFullScreen", () => {
  it("is full-screen only in bench mode with the prop", () => {
    renderWithTheme(<FloatingDialog open title="T" onClose={() => {}} benchFullScreen>x</FloatingDialog>, { bench: true });
    expect(screen.getByRole("dialog").className).toContain("MuiDialog-paperFullScreen");
    cleanup();
    renderWithTheme(<FloatingDialog open title="T" onClose={() => {}}>x</FloatingDialog>, { bench: true });
    expect(screen.getByRole("dialog").className).not.toContain("MuiDialog-paperFullScreen");
    cleanup();
    renderWithTheme(<FloatingDialog open title="T" onClose={() => {}} benchFullScreen>x</FloatingDialog>, { bench: false });
    expect(screen.getByRole("dialog").className).not.toContain("MuiDialog-paperFullScreen");
  });
});
