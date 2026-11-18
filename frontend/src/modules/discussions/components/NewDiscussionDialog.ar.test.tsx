import { afterEach, describe, expect, it } from "vitest";
import { cleanup, screen } from "@testing-library/react";
import { renderInArabic, resetLanguage } from "../../../i18n/testUtils";
import { NewDiscussionDialog } from "./NewDiscussionDialog";

describe("NewDiscussionDialog Arabic", () => {
  afterEach(async () => {
    cleanup();
    await resetLanguage();
  });

  it("shows the title, field labels and buttons in Arabic", async () => {
    await renderInArabic(
      <NewDiscussionDialog open onClose={() => {}} onCreated={() => {}} />,
      ["discussions"]
    );

    expect(await screen.findByText("مناقشة جديدة")).toBeTruthy();
    expect(screen.getByLabelText(/^العنوان/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "إلغاء" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "نشر المناقشة" })).toBeTruthy();
  });
});
