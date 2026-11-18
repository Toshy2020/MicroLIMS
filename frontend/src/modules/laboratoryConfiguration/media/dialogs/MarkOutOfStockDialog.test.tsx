import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { i18n } from "../../../../i18n";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { MarkOutOfStockDialog } from "./MarkOutOfStockDialog";

afterEach(cleanup);

const lot = { id: 1, lotNumber: "TSA-001", mediaClass: "Prepared", materialName: "Tryptic Soy Agar" };

describe("MarkOutOfStockDialog language", () => {
  it("shows the English title by default", () => {
    render(
      <PinnedLightTheme>
        <MarkOutOfStockDialog open lot={lot} onClose={vi.fn()} onSuccess={vi.fn()} />
      </PinnedLightTheme>
    );
    expect(screen.getByText("Mark Media Lot Out of Stock")).toBeTruthy();
  });

  it("shows the Arabic title and buttons when Arabic is active", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["labConfig"]);
    try {
      render(
        <PinnedLightTheme>
          <MarkOutOfStockDialog open lot={lot} onClose={vi.fn()} onSuccess={vi.fn()} />
        </PinnedLightTheme>
      );
      expect(screen.getByText("تحديد تشغيلة أوساط زرعية كمنتهية من المخزون")).toBeTruthy();
      expect(screen.getByRole("button", { name: "تأكيد نفاد المخزون" })).toBeTruthy();
    } finally {
      await i18n.changeLanguage("en");
    }
  });
});
