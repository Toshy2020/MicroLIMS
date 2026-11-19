import { afterEach, describe, expect, it, vi } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { renderInArabic, resetLanguage } from "../i18n/testUtils";
import { BreadcrumbLabel, BreadcrumbProvider } from "./breadcrumbContext";
import { AppBreadcrumbs } from "./AppBreadcrumbs";
import { PartialOutageBanner } from "./lab/PartialOutageBanner";
import { ConfirmationDialog } from "./ConfirmationDialog";
import { FormDialog } from "./lab/FormDialog";
import { RegisterTable, type RegisterColumn } from "./lab/RegisterTable";
import { Ltr } from "./Ltr";
import { DirectionalIcon } from "./DirectionalIcon";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";

interface Row {
  id: number;
  code: string;
  name: string;
  count: number;
}

const COLUMNS: RegisterColumn<Row>[] = [
  { key: "code", label: "Code", sortable: true, nowrap: true },
  { key: "name", label: "Name", sortable: true },
  { key: "count", label: "Count", sortable: true, numeric: true }
];

const ROWS: Row[] = [
  { id: 1, code: "FP0107026", name: "Alpha", count: 20 },
  { id: 2, code: "FP0107027", name: "Beta", count: 10 }
];

afterEach(async () => {
  await resetLanguage();
});

describe("shared components in Arabic", () => {
  it("Identifiers_stay_ltr_inside_arabic_sentence", async () => {
    await renderInArabic(
      <MemoryRouter>
        <BreadcrumbProvider>
          <BreadcrumbLabel label="FP0107026" />
          <AppBreadcrumbs trail={null} groups={[]} />
          <RegisterTable columns={COLUMNS} rows={ROWS} getRowId={(r) => r.id} empty={{ title: "none" }} />
          <p>
            عينة <Ltr>FP0107026</Ltr> مستلمة
          </p>
        </BreadcrumbProvider>
      </MemoryRouter>
    );

    // The breadcrumb names the record: the sample number is isolated, left to right.
    const crumb = screen.getByRole("navigation", { name: "مسار التنقل" });
    const crumbId = within(crumb).getByText("FP0107026");
    expect(crumbId.tagName).toBe("BDI");
    expect(crumbId.getAttribute("dir")).toBe("ltr");

    // A table's identifier and number cells are isolated too.
    const table = screen.getByRole("table");
    for (const text of ["FP0107026", "20"]) {
      const cell = within(table).getByText(text);
      expect(cell.tagName).toBe("BDI");
      expect(cell.getAttribute("dir")).toBe("ltr");
    }
    // A plain text cell is left to the browser.
    expect(within(table).getByText("Alpha").tagName).toBe("TD");

    const sentence = screen.getByText(/مستلمة/);
    expect(within(sentence).getByText("FP0107026").getAttribute("dir")).toBe("ltr");
  });

  it("shows the partial outage banner in Arabic and retries", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    await renderInArabic(<PartialOutageBanner sources={["a", "b"]} onRetry={onRetry} />);
    expect(screen.getByText("تعذّر تحميل بعض البيانات: a، b.")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "إعادة المحاولة" }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it("puts a destructive confirmation's focus on Cancel, in Arabic", async () => {
    await renderInArabic(<ConfirmationDialog open destructive message="x" onConfirm={() => {}} onCancel={() => {}} />);
    const dialog = screen.getByRole("alertdialog", { name: "تأكيد" });
    expect(within(dialog).getByRole("button", { name: "إلغاء" })).toBe(document.activeElement);
  });

  it("FormDialog shows Cancel and the close button in Arabic", async () => {
    await renderInArabic(
      <FormDialog open title="t" onClose={() => {}} onSubmit={() => {}} submitLabel="حفظ">
        <span>body</span>
      </FormDialog>
    );
    expect(screen.getByRole("button", { name: "إلغاء" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "إغلاق النافذة" })).toBeTruthy();
  });

  it("mirrors a direction icon only in Arabic", async () => {
    await renderInArabic(
      <DirectionalIcon>
        <ChevronRightIcon data-testid="chevron" />
      </DirectionalIcon>
    );
    expect(screen.getByTestId("chevron").parentElement?.style.transform).toBe("scaleX(-1)");
  });
});

describe("RegisterTable in Arabic", () => {
  it("reverses the numeric alignment, keeps the sort icon on the right side and translates the pager", async () => {
    const many = Array.from({ length: 30 }, (_, i) => ({ id: i + 1, code: `C-${i}`, name: `N${i}`, count: i }));
    await renderInArabic(
      <MemoryRouter>
        <RegisterTable columns={COLUMNS} rows={many} getRowId={(r) => r.id} empty={{ title: "none" }} defaultSort={{ key: "code", direction: "asc" }} />
      </MemoryRouter>
    );
    const table = screen.getByRole("table");
    const headers = within(table).getAllByRole("columnheader");
    // Same logical order in the document; the page direction (rtl) lays the first column out on the right.
    expect(headers.map((h) => h.textContent)).toEqual(["Code", "Name", "Count"]);
    expect(document.documentElement.dir).toBe("rtl");

    // A numeric column sits at the end of its row: right in English, left in Arabic.
    expect(getComputedStyle(headers[2]).textAlign).toBe("left");
    expect(getComputedStyle(headers[1]).textAlign).toBe("right");

    // The sort arrow is vertical: it points up/down the same way in both directions and is not mirrored.
    const sortButton = within(headers[0]).getByRole("button", { name: "Code" });
    const icon = sortButton.querySelector("svg") as SVGElement;
    expect(getComputedStyle(icon).transform).toBe("rotate(180deg)"); // ascending
    await userEvent.setup().click(sortButton);
    expect(headers[0].getAttribute("aria-sort")).toBe("descending");
    expect(getComputedStyle(icon).transform).toBe("rotate(0deg)");

    expect(screen.getByText("عدد الصفوف في الصفحة:")).toBeTruthy();
    expect(screen.getByText("1–25 من 30")).toBeTruthy();
    expect(screen.getByRole("button", { name: "الانتقال إلى الصفحة التالية" })).toBeTruthy();
  });
});
