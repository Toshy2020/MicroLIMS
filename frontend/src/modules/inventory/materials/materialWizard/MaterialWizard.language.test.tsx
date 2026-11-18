import { afterEach, describe, expect, it } from "vitest";
import { cleanup } from "@testing-library/react";
import { renderInArabic, resetLanguage } from "../../../../i18n/testUtils";
import { createDatesStep } from "./DatesStep";
import { createLotStep } from "./LotStep";


describe("material wizard steps in Arabic", () => {
  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("titles the steps and labels the dates step in Arabic", async () => {
    await renderInArabic(<div />, ["common", "inventory"]);
    const noop = () => null;
    const getContext = () => ({}) as never;
    const dates = createDatesStep(() => null, noop as never, getContext);
    const lot = createLotStep(() => null, noop as never, getContext);
    expect(dates.title).toBe("التواريخ");
    expect(lot.title).toBe("الدفعة والتخزين");
    const rows = dates.summary?.({ receivingDate: "", expiryDate: "" } as never) ?? [];
    expect(rows.map((r) => r.label)).toEqual(["تاريخ الاستلام", "تاريخ انتهاء الصلاحية"]);
  });
});
