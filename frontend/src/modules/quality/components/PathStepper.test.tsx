import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { PathStepper } from "./PathStepper";
import { pathStep } from "../testFixtures";

const steps = [
  pathStep({ stepKey: "raised", label: "Raised", isCompleted: true, completedAtUtc: "2026-11-20T08:00:00Z", completedByName: "Ann Analyst" }),
  pathStep({ stepKey: "investigation", label: "Investigation", isCurrent: true }),
  pathStep({ stepKey: "approval", label: "QA approval" }),
  pathStep({ stepKey: "closed", label: "Closed" })
];

function renderStepper(value = steps) {
  return render(
    <PinnedLightTheme>
      <PathStepper steps={value} />
    </PinnedLightTheme>
  );
}

describe("PathStepper", () => {
  afterEach(() => {
    cleanup();
  });

  it("shows every step of the path with a text label", () => {
    renderStepper();

    const path = screen.getByRole("group", { name: "Record path" });
    for (const label of ["Raised", "Investigation", "QA approval", "Closed"]) {
      expect(within(path).getByText(label)).toBeTruthy();
    }
  });

  it("marks the current step for assistive technology and says so in words", () => {
    renderStepper();

    const current = document.querySelector("[aria-current='step']");
    expect(current?.textContent).toContain("Investigation");
    expect(document.querySelectorAll("[aria-current='step']")).toHaveLength(1);

    const status = screen.getByRole("status");
    expect(status.textContent).toBe("Current step: Investigation (step 2 of 4)");
  });

  it("tells completed, current and not-started steps apart without relying on colour", () => {
    renderStepper();

    const path = screen.getByRole("group", { name: "Record path" });
    expect(within(path).getByText("completed")).toBeTruthy();
    expect(within(path).getByText("current step")).toBeTruthy();
    expect(within(path).getAllByText("not started")).toHaveLength(2);
  });

  it("shows who finished a step and when, with the time zone", () => {
    renderStepper();

    const line = screen.getByText(/Ann Analyst/);
    expect(line.textContent).toMatch(/Ann Analyst · .*20 Nov 2026.*\d{2}:\d{2}\s+\S+/);
  });

  it("says the path is complete when no step is current", () => {
    renderStepper(steps.map((s) => ({ ...s, isCompleted: true, isCurrent: false })));

    expect(screen.getByRole("status").textContent).toBe("All steps of the path are completed.");
    expect(document.querySelector("[aria-current='step']")).toBeNull();
  });

  it("renders nothing when the record has no path", () => {
    renderStepper([]);

    expect(screen.queryByRole("group", { name: "Record path" })).toBeNull();
  });
});
