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
        label: "Microbiology Laboratory", path: "/sections/microbiology-laboratory", counterSource: "MICRO",
        children: [
          { label: "Workspace", path: "/microbiology/workspace", featured: true, counterKey: "workspace", icon: ScienceOutlinedIcon },
          { label: "Biological Waste Destruction", path: "/laboratory-configuration/biological-waste", icon: ScienceOutlinedIcon }
        ]
      },
      { label: "Document Control", path: "/sections/document-control", counterSource: "document-control", children: [
        { label: "My Reading List", path: "/document-control/my-reading-list", counterKey: "reading-list", icon: ScienceOutlinedIcon }
      ] },
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

  it("shows the reading list figures on the Document Control page", async () => {
    vi.mocked(sectionCountersService.get).mockResolvedValue([
      { card: "reading-list", counters: [{ label: "to read", value: 3, tone: "info" }, { label: "overdue", value: 1, tone: "detected" }] }
    ]);
    renderAt("/sections/document-control");

    const card = (await screen.findByText("to read")).closest("a")!;
    expect(sectionCountersService.get).toHaveBeenCalledWith("document-control");
    expect(card.textContent).toContain("My Reading List");
    expect(card.textContent).toContain("3 to read");
    expect(card.textContent).toContain("1 overdue");
  });

  it("asks for no figures on a section without a counter source", () => {
    renderAt("/sections/microbiology-configuration");

    expect(screen.getByText("Organisms")).toBeTruthy();
    expect(sectionCountersService.get).not.toHaveBeenCalled();
  });
});
