import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { ChangeReasonDialog } from "./ChangeReasonDialog";
import { LimitPills } from "../configHierarchy/LimitPills";
import { limitOrderWarning } from "../configHierarchy/LimitFields";
import { i18n } from "../../i18n";

afterEach(async () => {
  await resetLanguage();
});

describe("shared configuration components in Arabic", () => {
  it("shows the change-reason dialog texts and the live character counter in Arabic", async () => {
    const user = userEvent.setup();
    await renderInArabic(<ChangeReasonDialog open onConfirm={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.getByText("سبب التغيير", { selector: "span" })).toBeTruthy();
    await user.type(screen.getByRole("textbox", { name: /سبب التغيير/ }), "قصير");
    expect(screen.getByText(/4 \/ 10 حرفًا كحد أدنى/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "إلغاء" })).toBeTruthy();
  });

  it("shows limit pills and the limit-order warning in Arabic", async () => {
    await renderInArabic(<LimitPills alertLimit="10" actionLimit="" specLimit="100" />);
    expect(screen.getByText("تنبيه 10")).toBeTruthy();
    expect(screen.getByText("إجراء غير محدد")).toBeTruthy();
    const warning = limitOrderWarning(
      { alertLimit: "5", actionLimit: "1", specLimit: "", unit: "" },
      i18n.getFixedT("ar", "common")
    );
    expect(warning).toBe("حد التنبيه أعلى من حد الإجراء.");
  });
});
