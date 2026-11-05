import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";
import { PreparationWhereUsedPanel } from "./PreparationWhereUsedPanel";

const svc = vi.hoisted(() => ({ getWhereUsed: vi.fn() }));
vi.mock("../services/SamplePreparationService", () => ({ SamplePreparationService: svc }));

const renderPanel = () =>
  render(
    <PinnedLightTheme>
      <MemoryRouter>
        <PreparationWhereUsedPanel lot={{ mediaId: 9 }} />
      </MemoryRouter>
    </PinnedLightTheme>
  );

beforeEach(() => svc.getWhereUsed.mockReset());
afterEach(cleanup);

describe("PreparationWhereUsedPanel", () => {
  it("shows the empty state when the lot was never used", async () => {
    svc.getWhereUsed.mockResolvedValue([]);
    renderPanel();
    expect(await screen.findByText("Not used in any test preparation yet.")).toBeTruthy();
    expect(svc.getWhereUsed).toHaveBeenCalledWith({ mediaId: 9 });
  });

  it("lists each sample with item, role and who prepared it", async () => {
    svc.getWhereUsed.mockResolvedValue([
      { sampleId: 7, sampleNumber: "S-0007", itemName: "Riboflavin", role: "Diluent", preparedAt: "2026-10-01T08:00:00Z", preparedByName: "Ana Analyst" },
      { sampleId: 8, sampleNumber: "S-0008", itemName: "Lactose", role: "Neutralizer", preparedAt: "2026-10-02T08:00:00Z", preparedByName: null }
    ]);
    renderPanel();
    await waitFor(() => expect(screen.getByText("S-0007")).toBeTruthy());
    expect(screen.getByText("Riboflavin")).toBeTruthy();
    expect(screen.getByText("Diluent")).toBeTruthy();
    expect(screen.getByText("Ana Analyst")).toBeTruthy();
    expect(screen.getByText("S-0008").closest("a")?.getAttribute("href")).toBe("/samples/8/report");
    expect(screen.queryByText("Not used in any test preparation yet.")).toBeNull();
  });
});
