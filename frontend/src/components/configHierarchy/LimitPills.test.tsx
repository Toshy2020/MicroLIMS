import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { LimitPills } from "./LimitPills";

describe("LimitPills", () => {
  it("renders micro Alert, Action, Spec pills when limitType is null/omitted", () => {
    render(
      <PinnedLightTheme>
        <LimitPills alertLimit="10" actionLimit="100" specLimit="500" unit="CFU/mL" />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Alert 10")).toBeDefined();
    expect(screen.getByText("Action 100")).toBeDefined();
    expect(screen.getByText("Spec 500")).toBeDefined();
    expect(screen.getByText("CFU/mL")).toBeDefined();
  });

  it("renders Range chemistry limit with unit ('6.5 – 8.5 pH')", () => {
    render(
      <PinnedLightTheme>
        <LimitPills limitType="Range" lowerLimit={6.5} upperLimit={8.5} unit="pH" />
      </PinnedLightTheme>
    );

    expect(screen.getByText("6.5 – 8.5 pH")).toBeDefined();
  });

  it("renders NotMoreThan chemistry limit ('NMT 1.3 µS/cm')", () => {
    render(
      <PinnedLightTheme>
        <LimitPills limitType="NotMoreThan" upperLimit={1.3} unit="µS/cm" />
      </PinnedLightTheme>
    );

    expect(screen.getByText("NMT 1.3 µS/cm")).toBeDefined();
  });

  it("renders NotLessThan chemistry limit ('NLT 5')", () => {
    render(
      <PinnedLightTheme>
        <LimitPills limitType="NotLessThan" lowerLimit={5} />
      </PinnedLightTheme>
    );

    expect(screen.getByText("NLT 5")).toBeDefined();
  });

  it("renders Qualitative chemistry limit (expected text)", () => {
    render(
      <PinnedLightTheme>
        <LimitPills limitType="Qualitative" expectedResultText="Clear colourless liquid" />
      </PinnedLightTheme>
    );

    expect(screen.getByText("Clear colourless liquid")).toBeDefined();
  });
});
