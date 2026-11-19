import { afterEach, describe, expect, it } from "vitest";
import { i18n } from "../../../i18n";
import { formatKpiValue } from "./kpiFormat";
import { createEmptySpecFormModel, validateStep as validateSpecStep } from "../../laboratoryConfiguration/items/specWizard/specFormModel";
import { createEmptyMaterialFormModel, validateStep as validateMaterialStep } from "../../inventory/materials/materialWizard/materialFormModel";

afterEach(async () => {
  await i18n.changeLanguage("en");
});

describe("G4 KPI & form-model labels in Arabic", () => {
  it("formats KPI values and form validation messages in Arabic", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["kpi", "labConfig", "inventory"]);

    expect(formatKpiValue("days", 3.5, i18n.getFixedT("ar", "kpi"))).toBe("3.5 أيام");
    expect(validateSpecStep("test", createEmptySpecFormModel(), i18n.getFixedT("ar", "labConfig")).testCode).toBe(
      "الاختبار المُسند مطلوب."
    );
    expect(
      validateMaterialStep(
        "material",
        createEmptyMaterialFormModel("2026-01-01"),
        { isEditing: false, sectionCount: 1, category: null },
        i18n.getFixedT("ar", "inventory")
      ).materialName
    ).toBe("اسم المادة ورقم الدفعة/التشغيلة وتاريخ الاستلام وموقع التخزين والكمية المستلمة مطلوبة.");
  });
});
