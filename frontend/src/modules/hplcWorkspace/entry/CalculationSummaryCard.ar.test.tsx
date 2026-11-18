import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { i18n } from "../../../i18n";
import { CalculationSummaryCard } from "./CalculationSummaryCard";

describe("CalculationSummaryCard Arabic", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["common", "hplcWorkspace"]);
  });

  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage("en");
  });

  it("renders translated preview badge and empty notice in Arabic", () => {
    render(
      <PinnedLightTheme>
        <CalculationSummaryCard preview={[]} basis="AsIs" />
      </PinnedLightTheme>
    );

    expect(screen.getByText("معاينة، ليست النتيجة الرسمية")).toBeTruthy();
    expect(screen.getByText("الأساس: AsIs")).toBeTruthy();
  });
});
