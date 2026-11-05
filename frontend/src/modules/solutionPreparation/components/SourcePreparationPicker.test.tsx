import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { SourcePreparationPicker } from "./SourcePreparationPicker";
import { releaseStateText } from "../releaseState";
import type { SolutionPreparationComponentResponse, SourcePreparationOption } from "../types";

afterEach(cleanup);

const component: SolutionPreparationComponentResponse = {
  id: 5, order: 1, kind: "PreparedSolution", materialMasterEntryId: null, entryCode: "", entryName: "Phosphate buffer stock",
  recipeQuantity: 10, recipeUnit: "Milliliter", materialId: null, lotBatchNumber: null, lotUnit: null, quantityUsed: null,
  sourceSolutionMasterId: 4
};

const options: SourcePreparationOption[] = [
  { preparationId: 1, code: "RE-PBS01/11/2026", preparedAt: "2026-11-01T08:00:00Z", expiresAt: "2026-12-01T08:00:00Z", approvalStatus: "Approved", usable: true, reason: null },
  { preparationId: 2, code: "RE-PBS02/11/2026", preparedAt: "2026-11-02T08:00:00Z", expiresAt: "2026-12-02T08:00:00Z", approvalStatus: "PendingReview", usable: false, reason: "Awaiting release." }
];

describe("SourcePreparationPicker", () => {
  it("lists unusable preparations disabled with the server's reason", async () => {
    const onSelect = vi.fn();
    render(
      <PinnedLightTheme>
        <SourcePreparationPicker
          component={component}
          options={options}
          selectedPreparationId={null}
          quantityUsed={null}
          onSelect={onSelect}
          onChangeQuantity={vi.fn()}
        />
      </PinnedLightTheme>
    );

    await userEvent.click(screen.getByRole("combobox"));
    const listbox = await screen.findByRole("listbox");
    const usable = within(listbox).getByRole("option", { name: /RE-PBS01/ });
    const blocked = within(listbox).getByRole("option", { name: /RE-PBS02/ });
    expect(usable.getAttribute("aria-disabled")).not.toBe("true");
    expect(blocked.getAttribute("aria-disabled")).toBe("true");
    expect(blocked.textContent).toContain("Blocked: Awaiting release.");

    await userEvent.click(usable);
    expect(onSelect).toHaveBeenCalledWith(1);
  });
});

describe("releaseStateText", () => {
  it("describes each release state, and nothing when no release is needed", () => {
    expect(releaseStateText({ approvalStatus: "PendingReview" })).toBe("Awaiting release");
    expect(releaseStateText({ approvalStatus: "Approved", releasedByUserName: "Head One", releasedAt: "2026-11-03T10:00:00Z" }))
      .toMatch(/^Released by Head One on /);
    expect(releaseStateText({ approvalStatus: "Rejected", releasedByUserName: "Head One", releasedAt: null, releaseComment: "Turbid" }))
      .toBe("Rejected by Head One: Turbid");
    expect(releaseStateText({ approvalStatus: null })).toBeNull();
  });
});
