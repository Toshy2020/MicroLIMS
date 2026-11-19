import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { i18n } from "../../../../i18n";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { WaterLocationPendingList } from "./WaterLocationPendingList";
import type { PendingWaterPoint } from "../waterSamplingPointPaste";

afterEach(cleanup);

const row = (code: string): PendingWaterPoint => ({
  code,
  waterDepartmentId: 1,
  waterDepartmentName: "PW",
  location: "Loop A",
  frequency: {} as PendingWaterPoint["frequency"],
  testingFrequency: "Weekly",
  assignedTestCodes: ["TAMC"]
});

const renderList = () =>
  render(
    <PinnedLightTheme>
      <WaterLocationPendingList
        rows={[row("PW-01"), row("PW-02")]}
        saving={false}
        error={null}
        onSave={vi.fn()}
        onDiscard={vi.fn()}
        onRemoveRow={vi.fn()}
      />
    </PinnedLightTheme>
  );

describe("WaterLocationPendingList language", () => {
  it("shows the English title with the count", () => {
    renderList();
    expect(screen.getByText("Pending sample locations (2)")).toBeTruthy();
  });

  it("shows the Arabic title and column header when Arabic is active", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["labConfig"]);
    try {
      renderList();
      expect(screen.getByText("مواقع أخذ العينات بانتظار الحفظ (2)")).toBeTruthy();
      expect(screen.getByText("رمز النقطة")).toBeTruthy();
    } finally {
      await i18n.changeLanguage("en");
    }
  });
});
