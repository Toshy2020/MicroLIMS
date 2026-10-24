import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { PinnedLightTheme } from "../theme/PinnedLightTheme";
import { sectionCountersService } from "../services/sectionCountersService";
import { SectionPage } from "./Section";

vi.mock("../hooks/useMenuGroups", () => ({
  useMenuGroups: () => [{
    groupName: "LABORATORIES",
    items: [
      {
        label: "Microbiology Laboratory", path: "/sections/microbiology-laboratory", counterLab: "MICRO",
        children: [
          { label: "Workspace", path: "/microbiology/workspace", featured: true, counterKey: "workspace", icon: ScienceOutlinedIcon },
          { label: "Biological Waste Destruction", path: "/laboratory-configuration/biological-waste", icon: ScienceOutlinedIcon }
        ]
      },
      { label: "Microbiology Configuration", path: "/sections/microbiology-configuration", children: [
        { label: "Organisms", path: "/laboratory-configuration/organisms", icon: ScienceOutlinedIcon }
      ] }
    ]
  }]
}));
vi.mock("../services/sectionCountersService", () => ({ sectionCountersService: { get: vi.fn() } }));

afterEach(() => { cleanup(); vi.mocked(sectionCountersService.get).mockReset(); });

function renderAt(path: string) {
  render(
    <PinnedLightTheme>
      <MemoryRouter initialEntries={[path]}><SectionPage /></MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("SectionPage live counters", () => {
  it("shows a laboratory's figures on the card they belong to", async () => {
    vi.mocked(sectionCountersService.get).mockResolvedValue([
      { card: "workspace", counters: [{ label: "overdue", value: 4, tone: "detected" }, { label: "awaiting review", value: 0, tone: "pending" }] }
    ]);
    renderAt("/sections/microbiology-laboratory");

    const workspace = (await screen.findByText("overdue")).closest("a")!;
    expect(sectionCountersService.get).toHaveBeenCalledWith("MICRO");
    expect(workspace.textContent).toContain("4 overdue");
    expect(workspace.textContent).toContain("0 awaiting review");
    const waste = screen.getByText("Biological Waste Destruction").closest("a")!;
    expect(waste.textContent).not.toMatch(/overdue/);
  });

  it("still lists the pages when the figures cannot be loaded", async () => {
    vi.mocked(sectionCountersService.get).mockRejectedValue(new Error("down"));
    renderAt("/sections/microbiology-laboratory");

    await waitFor(() => expect(sectionCountersService.get).toHaveBeenCalled());
    expect(screen.getByText("Workspace")).toBeTruthy();
    expect(screen.queryByText("overdue")).toBeNull();
  });

  it("asks for no figures on a section that is not a laboratory", () => {
    renderAt("/sections/microbiology-configuration");

    expect(screen.getByText("Organisms")).toBeTruthy();
    expect(sectionCountersService.get).not.toHaveBeenCalled();
  });
});
