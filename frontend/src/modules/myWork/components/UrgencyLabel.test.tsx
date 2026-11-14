import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { UrgencyLabel } from "./UrgencyLabel";

afterEach(cleanup);

describe("UrgencyLabel", () => {
  it("renders urgency as visible text, not colour alone", () => {
    render(
      <PinnedLightTheme>
        <>
          <UrgencyLabel urgency="Overdue" />
          <UrgencyLabel urgency="Now" />
          <UrgencyLabel urgency="Today" />
          <UrgencyLabel urgency="Later" />
        </>
      </PinnedLightTheme>
    );

    expect(screen.getByText("Overdue")).toBeTruthy();
    expect(screen.getByText("Now")).toBeTruthy();
    expect(screen.getByText("Today")).toBeTruthy();
    expect(screen.getByText("Later")).toBeTruthy();
  });

  it("renders nothing for None urgency", () => {
    const { container } = render(
      <PinnedLightTheme>
        <UrgencyLabel urgency="None" />
      </PinnedLightTheme>
    );
    expect(container.textContent).toBe("");
  });
});
