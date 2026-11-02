import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PinnedLightTheme } from "../../../../theme/PinnedLightTheme";
import { MediaProductList } from "./MediaProductList";
import type { MediaProductOption } from "../types/mediaConfigurationTypes";

const product = (over: Partial<MediaProductOption>): MediaProductOption => ({
  id: 1, name: "Tryptic Soy Agar", code: "TSA", isActive: true, hasStock: false,
  configurationCount: 1, batchCount: 2, incubationConditionCount: 1, ...over
});

afterEach(cleanup);

const renderList = (products: MediaProductOption[], onSetActive = vi.fn(), isManager = true) =>
  render(
    <PinnedLightTheme>
      <MediaProductList
        products={products}
        selectedProductId={null}
        onSelectProduct={vi.fn()}
        onRename={vi.fn()}
        onDelete={vi.fn()}
        onSetActive={onSetActive}
        isManager={isManager}
        emptyMessage="none"
      />
    </PinnedLightTheme>
  );

describe("MediaProductList activation", () => {
  it("offers Deactivate for an active product", async () => {
    const onSetActive = vi.fn();
    renderList([product({})], onSetActive);
    expect(screen.queryByText("Inactive")).toBeNull();
    await userEvent.click(screen.getByRole("button", { name: "Deactivate media product" }));
    expect(onSetActive).toHaveBeenCalledWith(expect.objectContaining({ code: "TSA" }), false);
  });

  it("marks an inactive product and offers Activate", async () => {
    const onSetActive = vi.fn();
    renderList([product({ isActive: false })], onSetActive);
    expect(screen.getByText("Inactive")).toBeTruthy();
    await userEvent.click(screen.getByRole("button", { name: "Activate media product" }));
    expect(onSetActive).toHaveBeenCalledWith(expect.objectContaining({ code: "TSA" }), true);
  });

  it("hides the action without MasterData.Manage", () => {
    renderList([product({})], vi.fn(), false);
    expect(screen.queryByRole("button", { name: /activate media product/i })).toBeNull();
  });
});
