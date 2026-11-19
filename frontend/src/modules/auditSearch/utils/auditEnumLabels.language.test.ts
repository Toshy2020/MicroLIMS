import { afterEach, describe, expect, it } from "vitest";
import { i18n } from "../../../i18n";
import { lookupEnumLabel } from "./auditEnumLabels";
import { getReceivingCategories } from "../../receiving/constants/receivingConstants";
import { describeWorkflowAnalystEnters } from "../../testingWorkspace/utils/workflowPanelMapping";
import { pathogenObservationLabel } from "../../testingWorkspace/utils/pathogenObservationLabel";

afterEach(async () => {
  await i18n.changeLanguage("en");
});

describe("G2 labels in Arabic", () => {
  it("resolves Arabic labels across auditSearch, receiving, and testingWorkspace", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["auditSearch", "receiving", "testingWorkspace"]);

    expect(lookupEnumLabel("Sample", "Status", 4, i18n.getFixedT("ar", "auditSearch"))).toBe("معتمدة");
    expect(getReceivingCategories(i18n.getFixedT("ar", "receiving"))[1]?.label).toBe("مادة خام");
    expect(
      describeWorkflowAnalystEnters({ workflowType: "CountTest" }, i18n.getFixedT("ar", "testingWorkspace"))[0]
    ).toBe("تشغيلة أوساط زرعية");
    expect(pathogenObservationLabel("NoGrowth", i18n.getFixedT("ar", "testingWorkspace"))).toBe("لا يوجد نمو");
  });
});
