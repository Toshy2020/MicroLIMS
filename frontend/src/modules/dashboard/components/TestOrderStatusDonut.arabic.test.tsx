import { afterEach, describe, expect, it } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";
import { TestOrderStatusDonut } from "./TestOrderStatusDonut";

describe("TestOrderStatusDonut in Arabic", () => {
  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("shows the Arabic title and empty message when there is no distribution yet", async () => {
    await renderInArabic(<TestOrderStatusDonut statusDist={null} />, ["common", "dashboard"]);

    expect(document.documentElement.dir).toBe("rtl");
    expect(screen.getByText("حالة طلبات الاختبار")).toBeTruthy();
    expect(screen.getByText("لا توجد بيانات بعد.")).toBeTruthy();
  });
});
