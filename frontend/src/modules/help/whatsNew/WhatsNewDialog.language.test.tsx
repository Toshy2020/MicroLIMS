import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { i18n } from "../../../i18n";
import { HelpPanelProvider } from "../HelpPanelContext";
import { WhatsNewDialog } from "./WhatsNewDialog";
import { getTourSteps } from "../tour/tourSteps";

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage("en");
});

describe("G3 help tour & release notes in Arabic", () => {
  it("renders Arabic release notes and tour steps when language is Arabic", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["help"]);
    const t = i18n.getFixedT("ar", "help");

    render(
      <PinnedLightTheme>
        <MemoryRouter>
          <HelpPanelProvider>
            <WhatsNewDialog open onClose={() => {}} role="Analyst" />
          </HelpPanelProvider>
        </MemoryRouter>
      </PinnedLightTheme>
    );

    expect(screen.getByText("الجديد")).toBeTruthy();
    expect(
      screen.getByText("يجمع «عملي» اختباراتك المُسندة ومهام القراءة وحالات الاعتماد في قائمة واحدة مرتبة حسب الأولوية.")
    ).toBeTruthy();
    expect(getTourSteps(t).Analyst[0]?.title).toBe("قائمة عملك");
  });
});
