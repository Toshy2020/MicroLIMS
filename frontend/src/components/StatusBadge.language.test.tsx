import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { i18n } from "../i18n";
import { StatusBadge } from "./StatusBadge";
import { workflowEventLabel } from "../utils/workflowEventLabel";
import { formatRoleName } from "../utils/userDisplay";
import { getMfaStateDisplay } from "../utils/mfaStateDisplay";

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage("en");
});

describe("G1 common labels in Arabic", () => {
  it("renders translated StatusBadge and helper labels when language is Arabic", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["common"]);
    const t = i18n.getFixedT("ar", "common");

    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <StatusBadge status="InProgress" />
          <StatusBadge status="Completed" />
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(screen.getByText("قيد التنفيذ")).toBeTruthy();
    expect(screen.getByText("مكتمل")).toBeTruthy();
    expect(workflowEventLabel("ReviewCompleted", null, t)).toBe("اكتملت المراجعة");
    expect(formatRoleName("SectionHead", null, t)).toBe("رئيس القسم");
    expect(getMfaStateDisplay("Overdue", t)?.label).toBe("متأخر");
  });
});
