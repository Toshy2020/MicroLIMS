import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { i18n } from "../../../i18n";
import { RevisionLifecycleStrip } from "./RevisionLifecycleStrip";

describe("RevisionLifecycleStrip Arabic", () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage("en");
  });

  it("renders lifecycle steps in Arabic when Arabic is active", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["documentControl"]);

    render(
      <PinnedLightTheme>
        <RevisionLifecycleStrip status="Effective" revisionNumber="01" />
      </PinnedLightTheme>
    );

    expect(await screen.findByText("ساري المفعول")).toBeTruthy();
    expect(screen.getByText("مسودة")).toBeTruthy();
    expect(screen.getByText("الإصدار 01 هنا")).toBeTruthy();
  });
});
