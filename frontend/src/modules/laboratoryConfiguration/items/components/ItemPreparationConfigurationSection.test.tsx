import { afterEach, describe, it, expect, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { ItemPreparationConfigurationSection } from "./ItemPreparationConfigurationSection";

const auth = vi.hoisted(() => ({ permissions: [] as string[] }));
vi.mock("../../../../contexts/AuthContext", () => ({ useAuth: () => auth }));
vi.mock("../../../testPreparation/PreparationStepsSummary", () => ({ PreparationStepsSummary: () => null }));
vi.mock("../../../testPreparation/services/ItemPreparationConfigurationService", () => ({
  ItemPreparationConfigurationService: {
    get: vi.fn().mockResolvedValue({
      itemId: 1, amount: 10, technique: "PourPlate", diluent: "Peptone", neutralizer: "Tween", approvalStatus: "PendingReview",
    }),
  },
}));

afterEach(cleanup);

async function renderSection(permissions: string[]) {
  auth.permissions = permissions;
  render(<ItemPreparationConfigurationSection itemId={1} itemName="Item A" />);
  await waitFor(() => expect(screen.queryByRole("progressbar")).toBeNull());
}

describe("ItemPreparationConfigurationSection", () => {
  it("hides Edit and Approve from a user who cannot manage items (the QC Manager)", async () => {
    await renderSection(["tests.not-applicable.final-approve"]);
    expect(screen.queryByText("Edit")).toBeNull();
    expect(screen.queryByText("Approve")).toBeNull();
  });

  it("shows Edit and Approve to a user holding Items.Manage", async () => {
    await renderSection(["Items.Manage"]);
    expect(screen.getByText("Edit")).toBeTruthy();
    expect(screen.getByText("Approve")).toBeTruthy();
  });
});
