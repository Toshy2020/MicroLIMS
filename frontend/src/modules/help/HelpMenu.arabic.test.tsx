import { afterEach, describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { renderInArabic, resetLanguage } from "../../i18n/testUtils";
import { HelpMenu } from "./HelpMenu";

afterEach(async () => {
  await resetLanguage();
});

describe("help menu in Arabic", () => {
  it("shows the menu entries in Arabic", async () => {
    const user = userEvent.setup();
    await renderInArabic(
      <MemoryRouter>
        <HelpMenu />
      </MemoryRouter>,
      ["common", "help"]
    );
    await user.click(screen.getByRole("button", { name: "المساعدة" }));
    expect(screen.getByRole("menuitem", { name: "الدليل الكامل" })).toBeTruthy();
    expect(screen.getByRole("menuitem", { name: "حول" })).toBeTruthy();
  });
});
