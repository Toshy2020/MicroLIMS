import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { i18n } from "../../../i18n";
import { SstStatusCard } from "./SstStatusCard";

describe("SstStatusCard Arabic", () => {
  beforeEach(async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["common", "hplcWorkspace"]);
  });

  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage("en");
  });

  it("renders translated record heading and status badge in Arabic", () => {
    render(
      <PinnedLightTheme>
        <SstStatusCard code="SST-2026-001" status="Passed" />
      </PinnedLightTheme>
    );

    expect(screen.getByText("سجل ملاءمة النظام")).toBeTruthy();
    expect(screen.getByText("ناجح")).toBeTruthy();
  });
});
