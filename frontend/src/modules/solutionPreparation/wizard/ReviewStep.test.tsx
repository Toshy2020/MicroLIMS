import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";
import type { SolutionPreparationResponse } from "../types";
import { ReviewStep } from "./ReviewStep";

const recipe = {
  id: 3, name: "Buffer", type: "Reagent", shelfLifeValue: 1, shelfLifeUnit: "Month", storageCondition: "RT",
  finalVolumeMl: 1000, instructions: "", sterilizationRequired: false, components: []
};

const preparation = {
  id: 1, version: 1, sectionId: 1, code: null, solutionMasterId: 3, solutionMasterName: "Buffer", type: "Reagent",
  hplcMethodId: null, hplcMethodAbbreviation: null, status: "InProgress", effectiveStatus: "InProgress",
  startedByUserId: 7, startedByUserName: "Alice", startedAt: "2026-11-14T08:00:00Z",
  preparedByUserId: null, preparedByUserName: null, preparedAt: null, expiresAt: null,
  finalVolumeMl: null, measuredPh: null, recipeSnapshotJson: JSON.stringify(recipe), components: [], statusHistory: []
} as unknown as SolutionPreparationResponse;

afterEach(async () => {
  cleanup();
  await resetLanguage();
});

describe("ReviewStep in Arabic", () => {
  it("shows the Arabic heading and the Arabic validation message", async () => {
    await renderInArabic(
      <ReviewStep preparation={preparation} onBack={vi.fn()} onSign={vi.fn()} />,
      ["common", "solutionPreparation"]
    );

    expect(screen.getByText("الخطوة 3: مراجعة التحضير")).toBeTruthy();
    expect(screen.getByText("لا يمكن إكمال التحضير. يرجى تصحيح ما يلي:")).toBeTruthy();
    expect(screen.getByText("الحجم النهائي مطلوب ويجب أن يكون أكبر من 0.")).toBeTruthy();
    expect(screen.getByRole("button", { name: "العودة إلى التعديل" })).toBeTruthy();
  });
});
