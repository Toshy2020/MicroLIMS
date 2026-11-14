import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { WhereUsedPanel } from "./WhereUsedPanel";
import {
  MasterRecordType,
  UsageSummary,
  masterDataUsageService,
} from "../../services/masterDataUsageService";

vi.mock("../../services/masterDataUsageService", () => ({
  masterDataUsageService: {
    getUsage: vi.fn(),
  },
}));

function renderPanel(type: MasterRecordType = "TestDefinition", id = 10) {
  return render(
    <PinnedLightTheme>
      <MemoryRouter>
        <WhereUsedPanel type={type} id={id} />
      </MemoryRouter>
    </PinnedLightTheme>
  );
}

describe("WhereUsedPanel", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it("shows loading state while fetching and passes an AbortSignal that aborts on unmount", () => {
    let capturedSignal: AbortSignal | undefined;
    vi.mocked(masterDataUsageService.getUsage).mockImplementation(
      (_type, _id, signal) => {
        capturedSignal = signal;
        return new Promise(() => {});
      }
    );

    const { unmount } = renderPanel("Item", 42);

    expect(screen.getByRole("status")).toBeTruthy();
    expect(screen.getByText("Checking where this record is used...")).toBeTruthy();
    expect(capturedSignal).toBeDefined();
    expect(capturedSignal?.aborted).toBe(false);

    unmount();
    expect(capturedSignal?.aborted).toBe(true);
  });

  it("lists lines with counts as text, marks open-work lines with 'Open', and links to ListRoute", async () => {
    const summary: UsageSummary = {
      type: "TestDefinition",
      id: 10,
      openWorkCount: 3,
      historicalCount: 12,
      lines: [
        {
          key: "open-test-orders",
          label: "Open test orders",
          count: 3,
          isOpenWork: true,
          listRoute: "/receiving-testing",
        },
        {
          key: "specifications",
          label: "Specifications",
          count: 5,
          isOpenWork: false,
          listRoute: "/laboratory-configuration/specifications",
        },
        {
          key: "sample-tests",
          label: "Auto-assigned item tests",
          count: 0,
          isOpenWork: false,
          listRoute: "/laboratory-configuration/items",
        },
        {
          key: "other-sections",
          label: "In other sections",
          count: 2,
          isOpenWork: false,
          listRoute: null,
        },
      ],
    };
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue(summary);

    renderPanel("TestDefinition", 10);

    const openLink = await screen.findByRole("link", { name: "Open test orders" });
    expect(openLink.getAttribute("href")).toBe("/receiving-testing");
    expect(screen.getByText("3")).toBeTruthy();
    expect(screen.getByText("Open")).toBeTruthy();

    const specLink = screen.getByRole("link", { name: "Specifications" });
    expect(specLink.getAttribute("href")).toBe("/laboratory-configuration/specifications");
    expect(screen.getByText("5")).toBeTruthy();

    expect(screen.getByText("In other sections")).toBeTruthy();
    expect(screen.getByText("2")).toBeTruthy();
    expect(screen.queryByRole("link", { name: "In other sections" })).toBeNull();
    expect(screen.queryByText("Auto-assigned item tests")).toBeNull();
  });

  it("shows 'Not used anywhere' when all usage counts are zero", async () => {
    vi.mocked(masterDataUsageService.getUsage).mockResolvedValue({
      type: "Specification",
      id: 7,
      openWorkCount: 0,
      historicalCount: 0,
      lines: [
        {
          key: "open-test-orders",
          label: "Open test orders",
          count: 0,
          isOpenWork: true,
          listRoute: "/receiving-testing",
        },
      ],
    });

    renderPanel("Specification", 7);

    expect(await screen.findByText("Not used anywhere")).toBeTruthy();
  });

  it("shows error alert and retries fetch when Retry is clicked", async () => {
    const user = userEvent.setup();
    vi.mocked(masterDataUsageService.getUsage)
      .mockRejectedValueOnce(new Error("Network down"))
      .mockResolvedValueOnce({
        type: "Equipment",
        id: 5,
        openWorkCount: 1,
        historicalCount: 1,
        lines: [
          {
            key: "open-incubations",
            label: "Open incubations",
            count: 1,
            isOpenWork: true,
            listRoute: "/receiving-testing",
          },
        ],
      });

    renderPanel("Equipment", 5);

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Network down");

    await user.click(screen.getByRole("button", { name: "Retry" }));

    await waitFor(() => {
      expect(masterDataUsageService.getUsage).toHaveBeenCalledTimes(2);
    });
    expect(await screen.findByRole("link", { name: "Open incubations" })).toBeTruthy();
    expect(screen.getByText("1")).toBeTruthy();
  });
});
