import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { UnmappedPreparationConfigurations } from "./UnmappedPreparationConfigurations";

const svc = vi.hoisted(() => ({ getUnmapped: vi.fn() }));
vi.mock("../../../testPreparation/services/ItemPreparationConfigurationService", () => ({
  ItemPreparationConfigurationService: svc
}));

afterEach(cleanup);

describe("UnmappedPreparationConfigurations", () => {
  it("renders nothing when every configuration is linked", async () => {
    svc.getUnmapped.mockResolvedValue([]);
    const { container } = render(<UnmappedPreparationConfigurations onOpenItem={vi.fn()} />);
    await waitFor(() => expect(svc.getUnmapped).toHaveBeenCalled());
    expect(container.textContent).toBe("");
  });

  it("lists unlinked configurations and opens the item", async () => {
    svc.getUnmapped.mockResolvedValue([
      { id: 1, itemId: 4, itemName: "Riboflavin", diluent: "Peptone", neutralizer: "Tween" }
    ]);
    const onOpen = vi.fn();
    render(<UnmappedPreparationConfigurations onOpenItem={onOpen} />);
    expect(await screen.findByText(/Not linked to master data/)).toBeTruthy();
    fireEvent.click(screen.getByText("Show"));
    fireEvent.click(screen.getByText("Riboflavin"));
    expect(onOpen).toHaveBeenCalledWith(4);
  });
});
