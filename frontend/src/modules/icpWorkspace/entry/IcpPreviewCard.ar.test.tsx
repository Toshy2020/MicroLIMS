import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { i18n } from "../../../i18n";
import { IcpPreviewCard } from "./IcpPreviewCard";

describe("IcpPreviewCard Arabic", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["common", "icpWorkspace"]);
  });

  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage("en");
  });

  it("renders translated official badge and empty state in Arabic", () => {
    render(
      <PinnedLightTheme>
        <IcpPreviewCard submitted official={[]} />
      </PinnedLightTheme>
    );

    expect(screen.getByText("نتائج رسمية (مُرسَلة)")).toBeTruthy();
    expect(screen.getByText("لم تُسجَّل نتائج رسمية بعد لإدخال العينة هذا.")).toBeTruthy();
  });

  it("renders translated preview table headers and element result in Arabic", () => {
    render(
      <PinnedLightTheme>
        <IcpPreviewCard
          submitted={false}
          preview={[
            {
              icpTestElementId: 1,
              parameterName: "Pb 220.353",
              quantity: "Concentration",
              display: "0.12 ppm",
              unit: "ppm",
              specLimit: "< 0.5 ppm",
              status: null,
              problem: null
            }
          ]}
        />
      </PinnedLightTheme>
    );

    expect(screen.getByText("المعامل")).toBeTruthy();
    expect(screen.getByText("الكمية")).toBeTruthy();
    expect(screen.getByText("الوحدة")).toBeTruthy();
    expect(screen.getByText("Pb 220.353")).toBeTruthy();
    expect(screen.getByText("0.12 ppm")).toBeTruthy();
  });
});
