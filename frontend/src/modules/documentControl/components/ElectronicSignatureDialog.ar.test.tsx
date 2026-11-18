import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { i18n } from "../../../i18n";
import { ElectronicSignatureDialog } from "./ElectronicSignatureDialog";

describe("ElectronicSignatureDialog Arabic", () => {
  afterEach(async () => {
    cleanup();
    await i18n.changeLanguage("en");
  });

  it("renders signature dialog in Arabic while keeping English meaning", async () => {
    await i18n.changeLanguage("ar");
    await i18n.loadNamespaces(["documentControl"]);

    render(
      <PinnedLightTheme>
        <ElectronicSignatureDialog
          open
          onClose={vi.fn()}
          onConfirm={vi.fn()}
          documentCode="SOP-001"
          documentTitle="Sampling"
          revisionNumber="01"
          signerFullName="Ada Admin"
          signerUsername="ada"
          meaning="Approved"
          meaningTranslation="معتمد"
        />
      </PinnedLightTheme>
    );

    expect(await screen.findByText("التوقيع الإلكتروني 21 CFR Part 11")).toBeTruthy();
    expect(screen.getByText("Approved (معتمد)")).toBeTruthy();
  });
});
