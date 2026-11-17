import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { globalSearchApi } from "../../api/globalSearchApi";
import { GlobalSearchDialog } from "./GlobalSearchDialog";

vi.mock("../../api/globalSearchApi", () => ({ globalSearchApi: { search: vi.fn() } }));

beforeEach(() => {
  localStorage.clear();
  vi.mocked(globalSearchApi.search).mockResolvedValue({
    query: "FP01",
    totalMatches: 1,
    executionTimeMs: 1,
    categories: [
      {
        categoryName: "Samples",
        count: 1,
        items: [
          { id: 1, entityType: "Sample", title: "FP0107026", subtitle: "Sample", status: "Received", matchedField: "Code", matchedValue: "FP0107026", badgeColor: "info", navigationTarget: "/samples/1" }
        ]
      }
    ]
  });
});

afterEach(async () => {
  localStorage.clear();
  await resetLanguage();
});

describe("GlobalSearchDialog in Arabic", () => {
  it("shows its controls in Arabic, mirrors the result arrow and keeps the code readable", async () => {
    const user = userEvent.setup();
    await renderInArabic(
      <MemoryRouter>
        <GlobalSearchDialog open onClose={() => {}} />
      </MemoryRouter>
    );

    const input = screen.getByRole("combobox", { name: "ابحث في MicroLIMS عن عينات وطلبات ومواد ومعدات ومستندات" });
    expect(screen.getByRole("group", { name: "تصفية نتائج البحث حسب الفئة" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "عينات" })).toBeTruthy();

    await user.type(input, "FP01");
    const dialog = screen.getByRole("dialog");
    const item = await within(dialog).findByRole("button", { name: /FP0107026/ });
    // The "next" arrow beside the result is a direction icon: mirrored in Arabic.
    const arrow = item.querySelector("svg") as SVGElement;
    expect(arrow.parentElement?.style.transform).toBe("scaleX(-1)");

    await user.clear(input);
    await user.type(input, "FP01");
    await waitFor(() => expect(screen.getByText("تم العثور على 1 نتيجة لـ FP01")).toBeTruthy());
  });
});
