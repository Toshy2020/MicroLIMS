import { afterEach, describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { ExternalLabSubtitle } from "./TestResultCards";

afterEach(async () => {
  await resetLanguage();
});

describe("ExternalLabSubtitle in Arabic", () => {
  it("shows the external laboratory badge and report link in Arabic", async () => {
    await renderInArabic(<ExternalLabSubtitle lab="Lab-7" reportId={12} />, ["testingWorkspace"]);
    expect(screen.getByText("مختبر خارجي: Lab-7")).toBeTruthy();
    expect(screen.getByRole("link", { name: "التقرير الخارجي" })).toBeTruthy();
  });
});
