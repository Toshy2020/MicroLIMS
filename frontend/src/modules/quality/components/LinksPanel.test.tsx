import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../../theme/PinnedLightTheme";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn()
}));
vi.mock("../../../services/apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../services/apiClient")>();
  return { ...actual, apiClient: http };
});

import { LinksPanel } from "./LinksPanel";
import { link, networkFailure, refusal } from "../testFixtures";
import type { QualityLinkDto } from "../../../services/qualityService";

const searchResult = {
  data: {
    data: {
      query: "S-2026",
      totalMatches: 3,
      executionTimeMs: 4,
      categories: [
        {
          categoryName: "Samples",
          count: 1,
          items: [
            {
              id: 10,
              entityType: "Sample",
              title: "Sample S-2026-0010",
              subtitle: "Amoxicillin 500mg",
              status: "InTesting",
              matchedField: "ReferenceNumber",
              matchedValue: "S-2026-0010",
              badgeColor: "info",
              navigationTarget: "/samples/10"
            }
          ]
        },
        {
          categoryName: "Media",
          count: 1,
          items: [
            {
              id: 66,
              entityType: "Media",
              title: "Media: TSA (Lot: L-66)",
              subtitle: "Status: Released",
              status: "Released",
              matchedField: "LotNumber",
              matchedValue: "L-66",
              badgeColor: "success",
              navigationTarget: "/media/66"
            }
          ]
        },
        {
          categoryName: "Solutions",
          count: 1,
          items: [
            {
              id: 5,
              entityType: "SolutionPreparation",
              title: "Solution: Buffer (SP-5)",
              subtitle: "Prepared",
              status: "Prepared",
              matchedField: "Code",
              matchedValue: "SP-5",
              badgeColor: "info",
              navigationTarget: "/solutions/5"
            }
          ]
        }
      ]
    }
  }
};

function renderPanel(props: { links?: QualityLinkDto[] | null; canEdit?: boolean; onChanged?: () => Promise<void> } = {}) {
  const onChanged = props.onChanged ?? vi.fn().mockResolvedValue(undefined);
  render(
    <PinnedLightTheme>
      <MemoryRouter>
        <LinksPanel
          recordId={5}
          recordVersion={4}
          links={props.links === undefined ? [] : props.links}
          canEdit={props.canEdit ?? true}
          onChanged={onChanged}
        />
      </MemoryRouter>
    </PinnedLightTheme>
  );
  return { onChanged };
}

describe("LinksPanel", () => {
  beforeEach(() => {
    http.get.mockResolvedValue(searchResult);
    http.post.mockResolvedValue({ data: { data: link() } });
    http.delete.mockResolvedValue({});
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("lists a readable link as its number and label, opening the linked record", () => {
    renderPanel({ links: [link({ number: "S-2026-0010", label: "Amoxicillin 500mg", route: "/samples/10" })] });

    const anchor = screen.getByRole("link", { name: /S-2026-0010/ });
    expect(anchor.getAttribute("href")).toBe("/samples/10");
    expect(screen.getByText("Amoxicillin 500mg")).toBeTruthy();
  });

  it("shows a restricted link as its number only, with the reason in words", () => {
    renderPanel({ links: [link({ id: 32, number: "EQ-0042", label: null, route: null, isRestricted: true })] });

    const item = screen.getByRole("listitem");
    expect(within(item).getByText("EQ-0042")).toBeTruthy();
    expect(within(item).getByText("Restricted")).toBeTruthy();
    expect(within(item).getByText("You may not open this record, so only its number is shown.")).toBeTruthy();
    expect(within(item).queryByRole("link")).toBeNull();
  });

  it("searches with the global search and offers only records that can be linked", async () => {
    const user = userEvent.setup();
    renderPanel();

    await user.click(screen.getByRole("button", { name: "Add link" }));
    await user.type(screen.getByLabelText("Search for a record to link"), "S-2026");

    await waitFor(() => {
      expect(http.get).toHaveBeenCalledWith("/search/global", {
        params: { q: "S-2026", category: undefined, limit: 10 },
        signal: expect.any(AbortSignal)
      });
    });
    expect(await screen.findByRole("button", { name: "Link Sample S-2026-0010" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Link Media: TSA (Lot: L-66)" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Buffer/ })).toBeNull();
  });

  it("does not search before two characters are typed", async () => {
    const user = userEvent.setup();
    renderPanel();

    await user.click(screen.getByRole("button", { name: "Add link" }));
    await user.type(screen.getByLabelText("Search for a record to link"), "S");

    expect(http.get).not.toHaveBeenCalled();
  });

  it("links the chosen record with its target type and the record version", async () => {
    const user = userEvent.setup();
    const { onChanged } = renderPanel();

    await user.click(screen.getByRole("button", { name: "Add link" }));
    await user.type(screen.getByLabelText("Search for a record to link"), "S-2026");
    await user.click(await screen.findByRole("button", { name: "Link Media: TSA (Lot: L-66)" }));

    await waitFor(() => {
      expect(http.post).toHaveBeenCalledWith(
        "/quality/5/links",
        { targetType: "MediaLot", targetId: 66 },
        { headers: { "If-Match": '"4"' } }
      );
    });
    await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
  });

  it("shows the server's refusal beside the action and does not reload", async () => {
    const user = userEvent.setup();
    http.post.mockRejectedValue(refusal("This target is already linked."));
    const { onChanged } = renderPanel();

    await user.click(screen.getByRole("button", { name: "Add link" }));
    await user.type(screen.getByLabelText("Search for a record to link"), "S-2026");
    await user.click(await screen.findByRole("button", { name: "Link Sample S-2026-0010" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("This target is already linked.");
    expect(onChanged).not.toHaveBeenCalled();
  });

  it("says when the search itself fails", async () => {
    const user = userEvent.setup();
    http.get.mockRejectedValue(networkFailure());
    renderPanel();

    await user.click(screen.getByRole("button", { name: "Add link" }));
    await user.type(screen.getByLabelText("Search for a record to link"), "S-2026");

    expect((await screen.findByRole("alert")).textContent).toContain("The search failed. Try again.");
  });

  it("removes a link with the record version", async () => {
    const user = userEvent.setup();
    const { onChanged } = renderPanel({ links: [link({ id: 31, number: "S-2026-0010" })] });

    await user.click(screen.getByRole("button", { name: "Remove link S-2026-0010" }));

    await waitFor(() => {
      expect(http.delete).toHaveBeenCalledWith("/quality/5/links/31", { headers: { "If-Match": '"4"' } });
    });
    await waitFor(() => expect(onChanged).toHaveBeenCalledTimes(1));
  });

  it("is read-only without edit permission: no add or remove controls", () => {
    renderPanel({ links: [link()], canEdit: false });

    expect(screen.queryByRole("button", { name: "Add link" })).toBeNull();
    expect(screen.queryByRole("button", { name: /Remove link/ })).toBeNull();
    expect(screen.getByText("Amoxicillin 500mg")).toBeTruthy();
  });

  it("shows the empty state and the unavailable state", () => {
    renderPanel({ links: [] });
    expect(screen.getByText("No records are linked yet.")).toBeTruthy();
    cleanup();

    renderPanel({ links: null });
    expect(screen.getByText("Links could not be loaded.")).toBeTruthy();
  });
});
