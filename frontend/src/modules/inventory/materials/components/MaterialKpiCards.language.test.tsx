import { afterEach, describe, expect, it } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { renderInArabic, resetLanguage } from "../../../../i18n/testUtils";
import { MaterialKpiCards } from "./MaterialKpiCards";

describe("MaterialKpiCards in Arabic", () => {
  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("shows the stock tiles in Arabic and marks the selected one as filtered", async () => {
    await renderInArabic(<MaterialKpiCards items={[]} activeFilter="low_stock" onFilterSelect={() => {}} />, ["common", "inventory"]);
    expect(screen.getByText("إجمالي العناصر")).toBeTruthy();
    expect(screen.getByText("نفد من المخزون")).toBeTruthy();
    expect(screen.getByText("مخزون منخفض (مُصفّى)")).toBeTruthy();
  });
});
