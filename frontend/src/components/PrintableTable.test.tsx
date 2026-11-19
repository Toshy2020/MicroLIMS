import { afterEach, describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import { renderInArabic, resetLanguage } from "../i18n/testUtils";
import { formatLabCalendarDate } from "../utils/formatDate";
import { PrintableTable } from "./PrintableTable";

afterEach(async () => {
  await resetLanguage();
});

interface Row {
  id: number;
  due: string;
}

describe("PrintableTable", () => {
  it("prints English and left to right while the interface is Arabic", async () => {
    const { container } = await renderInArabic(
      <PrintableTable<Row>
        title="Equipment Register"
        rows={[{ id: 1, due: "2026-12-05" }]}
        getRowId={(r) => r.id}
        columns={[{ label: "Calibration Due", render: (r) => formatLabCalendarDate(r.due, { english: true }) }]}
      />
    );

    // The interface itself stays Arabic and right to left.
    expect(document.documentElement.dir).toBe("rtl");

    const printed = container.querySelector(".print-only");
    expect(printed).toBeTruthy();
    expect(printed?.getAttribute("dir")).toBe("ltr");
    expect(printed?.getAttribute("lang")).toBe("en");
    expect(screen.getByText("05 Dec 2026")).toBeTruthy();
    const header = printed?.textContent ?? "";
    expect(header).toMatch(/Printed: \d{2} [A-Z][a-z]{2} \d{4}, /);
    expect(header).not.toMatch(/[؀-ۿ⁧]/);
  });
});
