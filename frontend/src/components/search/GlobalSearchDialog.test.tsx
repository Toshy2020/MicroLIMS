import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent, waitFor, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { GlobalSearchDialog } from "./GlobalSearchDialog";
import { globalSearchApi } from "../../api/globalSearchApi";

vi.mock("../../api/globalSearchApi", () => ({
  globalSearchApi: {
    search: vi.fn()
  }
}));

describe("GlobalSearchDialog", () => {
  afterEach(cleanup);
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
  });

  it("renders search input when open", () => {
    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <GlobalSearchDialog open={true} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    expect(screen.getByPlaceholderText(/search microlims/i)).toBeTruthy();
  });

  it("executes search when input changes and renders results", async () => {
    vi.mocked(globalSearchApi.search).mockResolvedValueOnce({
      query: "SMP",
      totalMatches: 1,
      executionTimeMs: 10,
      categories: [
        {
          categoryName: "Samples",
          count: 1,
          items: [
            {
              id: 101,
                  entityType: "Sample",
              title: "SMP-2026-0001",
              subtitle: "Raw Material • Testing",
                            status: "InTesting",
              navigationTarget: "/receiving-testing?sampleId=101",
              matchedField: "ReferenceNumber",
                  matchedValue: "x",
                  badgeColor: "info"
            }
          ]
        }
      ]
    });

    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <GlobalSearchDialog open={true} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    const input = screen.getByPlaceholderText(/search microlims/i);
    fireEvent.change(input, { target: { value: "SMP" } });

    await waitFor(() => {
      expect(screen.getByText("SMP-2026-0001")).toBeTruthy();
      expect(screen.getByText("Raw Material • Testing")).toBeTruthy();
    });
  });

  it("stores and displays recent searches from localStorage", async () => {
    localStorage.setItem("microlims_recent_searches", JSON.stringify(["PARACETAMOL", "BATCH-99"]));

    render(
      <MemoryRouter>
        <PinnedLightTheme>
          <GlobalSearchDialog open={true} onClose={vi.fn()} />
        </PinnedLightTheme>
      </MemoryRouter>
    );

    expect(screen.getByText("PARACETAMOL")).toBeTruthy();
    expect(screen.getByText("BATCH-99")).toBeTruthy();
  });
});
