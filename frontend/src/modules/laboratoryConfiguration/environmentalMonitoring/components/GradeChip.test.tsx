import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { i18n } from "../../../../i18n";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { GradeChip } from "./GradeChip";

afterEach(cleanup);

describe("GradeChip language", () => {
  it("shows the grade in English by default", () => {
    render(<PinnedLightTheme><GradeChip grade="B" /></PinnedLightTheme>);
    expect(screen.getByText("Grade B")).toBeTruthy();
  });

  it("shows the grade in Arabic when Arabic is active", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["labConfig"]);
    try {
      render(<PinnedLightTheme><GradeChip grade="B" /></PinnedLightTheme>);
      expect(screen.getByText("الدرجة B")).toBeTruthy();
    } finally {
      await i18n.changeLanguage("en");
    }
  });
});
