import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { render, screen, waitFor, cleanup } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router-dom";
import { toast } from "sonner";
import { PinnedLightTheme } from "../../theme/PinnedLightTheme";
import { GlobalSearchDialog } from "./GlobalSearchDialog";
import { globalSearchApi } from "../../api/globalSearchApi";
import { labelService, type ScanMatch, type ScanResolveResponse } from "../../services/labelService";

vi.mock("../../api/globalSearchApi", () => ({
  globalSearchApi: {
    search: vi.fn()
  }
}));

vi.mock("../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location-probe">{`${location.pathname}${location.search}`}</div>;
}

function renderDialog(onClose = vi.fn()) {
  const result = render(
    <MemoryRouter initialEntries={["/"]}>
      <PinnedLightTheme>
        <GlobalSearchDialog open={true} onClose={onClose} />
        <LocationProbe />
      </PinnedLightTheme>
    </MemoryRouter>
  );
  return { onClose, ...result };
}

function makeMatch(overrides: Partial<ScanMatch> = {}): ScanMatch {
  return {
    entityType: "Sample",
    id: 101,
    code: "S-2026-001",
    displayName: "Purified Water",
    status: "InTesting",
    usable: true,
    blockedReason: null,
    navigationTarget: "/receiving-testing?sampleId=101",
    linkedEquipmentId: null,
    ...overrides
  };
}

function makeResolveResponse(overrides: Partial<ScanResolveResponse> = {}): ScanResolveResponse {
  return {
    outcome: "Found",
    layoutCorrected: false,
    normalizedValue: "S-2026-001",
    match: makeMatch(),
    candidates: [],
    hint: null,
    ...overrides
  };
}

function createDeferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

describe("GlobalSearchDialog", () => {
  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
  });

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.spyOn(toast, "info").mockImplementation(() => "");
    vi.spyOn(toast, "error").mockImplementation(() => "");
  });

  it("renders search input when open", () => {
    renderDialog();
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

    const user = userEvent.setup();
    renderDialog();

    const input = screen.getByPlaceholderText(/search microlims/i);
    await user.type(input, "SMP");

    await waitFor(() => {
      expect(screen.getByText("SMP-2026-0001")).toBeTruthy();
      expect(screen.getByText("Raw Material • Testing")).toBeTruthy();
    });
  });

  it("stores and displays recent searches from localStorage", async () => {
    localStorage.setItem("microlims_recent_searches", JSON.stringify(["PARACETAMOL", "BATCH-99"]));

    renderDialog();

    expect(screen.getByText("PARACETAMOL")).toBeTruthy();
    expect(screen.getByText("BATCH-99")).toBeTruthy();
  });

  it("renders category filter chips and scopes search when a category is selected", async () => {
    vi.mocked(globalSearchApi.search).mockResolvedValue({
      query: "WATER",
      totalMatches: 0,
      executionTimeMs: 5,
      categories: []
    });

    const user = userEvent.setup();
    renderDialog();

    await user.click(screen.getByRole("button", { name: "Samples" }));
    await user.type(screen.getByPlaceholderText(/search microlims/i), "WATER");

    await waitFor(() => {
      expect(globalSearchApi.search).toHaveBeenCalledWith("WATER", "Samples", 15, expect.any(AbortSignal));
    });
  });

  it("typing a code and pressing Enter at once opens the resolved record", async () => {
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        outcome: "Found",
        match: makeMatch({ navigationTarget: "/receiving-testing?sampleId=101" })
      })
    );

    const user = userEvent.setup();
    const { onClose } = renderDialog();

    const input = screen.getByPlaceholderText(/search microlims/i);
    await user.type(input, "S:S-2026-001{Enter}");

    await waitFor(() => {
      expect(labelService.resolveScan).toHaveBeenCalledWith("S:S-2026-001", expect.any(AbortSignal));
      expect(onClose).toHaveBeenCalledTimes(1);
      expect(screen.getByTestId("location-probe").textContent).toBe("/receiving-testing?sampleId=101");
    });
    expect(globalSearchApi.search).not.toHaveBeenCalled();
    expect(toast.info).not.toHaveBeenCalled();
  });

  it("a second scan while the first resolve is pending opens the second record only", async () => {
    const first = createDeferred<ScanResolveResponse>();
    const second = createDeferred<ScanResolveResponse>();
    const signals: (AbortSignal | undefined)[] = [];

    vi.mocked(labelService.resolveScan).mockImplementation((_value, signal) => {
      signals.push(signal);
      return signals.length === 1 ? first.promise : second.promise;
    });

    const user = userEvent.setup();
    const { onClose } = renderDialog();

    const input = screen.getByPlaceholderText(/search microlims/i);
    await user.type(input, "S:FIRST{Enter}");

    await waitFor(() => {
      expect(labelService.resolveScan).toHaveBeenCalledTimes(1);
    });
    expect(signals[0]?.aborted).toBe(false);

    await user.clear(input);
    await user.type(input, "S:SECOND{Enter}");

    await waitFor(() => {
      expect(labelService.resolveScan).toHaveBeenCalledTimes(2);
    });
    expect(signals[0]?.aborted).toBe(true);
    expect(signals[1]?.aborted).toBe(false);

    // Even if the superseded first promise still resolves after abort, it must be discarded.
    first.resolve(
      makeResolveResponse({
        match: makeMatch({ id: 1, code: "FIRST", navigationTarget: "/samples/1" })
      })
    );
    second.resolve(
      makeResolveResponse({
        match: makeMatch({ id: 2, code: "SECOND", navigationTarget: "/samples/2" })
      })
    );

    await waitFor(() => {
      expect(onClose).toHaveBeenCalledTimes(1);
      expect(screen.getByTestId("location-probe").textContent).toBe("/samples/2");
    });
  });

  it("an ambiguous scan lists the candidates", async () => {
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        outcome: "Ambiguous",
        match: null,
        candidates: [
          makeMatch({
            entityType: "Sample",
            id: 10,
            code: "2026-001",
            displayName: "Purified Water",
            status: "InTesting",
            navigationTarget: "/receiving-testing?sampleId=10"
          }),
          makeMatch({
            entityType: "MediaLot",
            id: 20,
            code: "2026-001",
            displayName: "Tryptic Soy Agar",
            status: "Released",
            navigationTarget: "/media-lots?lotId=20"
          })
        ]
      })
    );

    const user = userEvent.setup();
    const { onClose } = renderDialog();

    const input = screen.getByPlaceholderText(/search microlims/i);
    await user.type(input, "2026-001{Enter}");

    expect(await screen.findByText(/Sample • Purified Water/)).toBeTruthy();
    expect(screen.getByText(/Media lot • Tryptic Soy Agar/)).toBeTruthy();
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByTestId("location-probe").textContent).toBe("/");

    await user.click(screen.getByText(/Media lot • Tryptic Soy Agar/));
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId("location-probe").textContent).toBe("/media-lots?lotId=20");
  });

  it("an unknown value falls back to search results", async () => {
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        outcome: "NotFound",
        match: null,
        candidates: [],
        hint: "Switch keyboard to English and scan again."
      })
    );
    vi.mocked(globalSearchApi.search).mockResolvedValueOnce({
      query: "WATER",
      totalMatches: 1,
      executionTimeMs: 4,
      categories: [
        {
          categoryName: "Samples",
          count: 1,
          items: [
            {
              id: 55,
              entityType: "Sample",
              title: "SMP-WATER-55",
              subtitle: "Water • InTesting",
              status: "InTesting",
              navigationTarget: "/receiving-testing?sampleId=55",
              matchedField: "ProductName",
              matchedValue: "Water",
              badgeColor: "info"
            }
          ]
        }
      ]
    });

    const user = userEvent.setup();
    const { onClose } = renderDialog();

    const input = screen.getByPlaceholderText(/search microlims/i);
    await user.type(input, "WATER{Enter}");

    await waitFor(() => {
      expect(globalSearchApi.search).toHaveBeenCalledWith("WATER", undefined, 5, expect.any(AbortSignal));
      expect(screen.getByText("SMP-WATER-55")).toBeTruthy();
    });
    expect(screen.getByRole("status").textContent).toContain("Switch keyboard to English and scan again.");
    expect(onClose).not.toHaveBeenCalled();
  });

  it("arrow-key selection followed by Enter opens the selected result, not the resolve result", async () => {
    vi.mocked(globalSearchApi.search).mockResolvedValueOnce({
      query: "SMP",
      totalMatches: 2,
      executionTimeMs: 8,
      categories: [
        {
          categoryName: "Samples",
          count: 2,
          items: [
            {
              id: 1,
              entityType: "Sample",
              title: "SMP-001",
              subtitle: "First sample",
              status: "InTesting",
              navigationTarget: "/samples/1",
              matchedField: "ReferenceNumber",
              matchedValue: "SMP-001",
              badgeColor: "info"
            },
            {
              id: 2,
              entityType: "Sample",
              title: "SMP-002",
              subtitle: "Second sample",
              status: "InTesting",
              navigationTarget: "/samples/2",
              matchedField: "ReferenceNumber",
              matchedValue: "SMP-002",
              badgeColor: "info"
            }
          ]
        }
      ]
    });

    const user = userEvent.setup();
    const { onClose } = renderDialog();

    const input = screen.getByPlaceholderText(/search microlims/i);
    await user.type(input, "SMP");
    await screen.findByText("SMP-002");

    await user.keyboard("{ArrowDown}{Enter}");

    expect(labelService.resolveScan).not.toHaveBeenCalled();
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(screen.getByTestId("location-probe").textContent).toBe("/samples/2");
  });

  it("shows the Arabic keyboard notice when the layout was corrected", async () => {
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        outcome: "Found",
        layoutCorrected: true,
        normalizedValue: "S:S-2026-055",
        match: makeMatch({ id: 55, code: "S-2026-055", navigationTarget: "/samples/55" })
      })
    );

    const user = userEvent.setup();
    const { onClose } = renderDialog();

    const input = screen.getByPlaceholderText(/search microlims/i);
    await user.type(input, "س:س-2026-055{Enter}");

    await waitFor(() => {
      expect(toast.info).toHaveBeenCalledWith("Keyboard was in Arabic; the code was converted.");
      expect(onClose).toHaveBeenCalledTimes(1);
      expect(screen.getByTestId("location-probe").textContent).toBe("/samples/55");
    });
  });

  it("falls back to normal search and toasts error when resolve fails", async () => {
    vi.mocked(labelService.resolveScan).mockRejectedValueOnce(new Error("Network failure"));
    vi.mocked(globalSearchApi.search).mockResolvedValueOnce({
      query: "SMP",
      totalMatches: 1,
      executionTimeMs: 4,
      categories: [
        {
          categoryName: "Samples",
          count: 1,
          items: [
            {
              id: 77,
              entityType: "Sample",
              title: "SMP-FALLBACK-77",
              subtitle: "Fallback",
              status: "InTesting",
              navigationTarget: "/samples/77",
              matchedField: "ReferenceNumber",
              matchedValue: "SMP",
              badgeColor: "info"
            }
          ]
        }
      ]
    });

    const user = userEvent.setup();
    const { onClose } = renderDialog();

    await user.type(screen.getByPlaceholderText(/search microlims/i), "SMP{Enter}");

    await waitFor(() => {
      expect(toast.error).toHaveBeenCalled();
      expect(screen.getByText("SMP-FALLBACK-77")).toBeTruthy();
    });
    expect(onClose).not.toHaveBeenCalled();
  });

  it("camera-detected code opens the record", async () => {
    const originalMediaDevices = Object.getOwnPropertyDescriptor(navigator, "mediaDevices");
    const originalBarcodeDetector = Object.getOwnPropertyDescriptor(window, "BarcodeDetector");
    const originalIsSecureContext = Object.getOwnPropertyDescriptor(window, "isSecureContext");

    const stop = vi.fn();
    // jsdom has no camera or navigator.mediaDevices implementation.
    Object.defineProperty(navigator, "mediaDevices", {
      configurable: true,
      value: {
        getUserMedia: vi.fn().mockResolvedValue({ getTracks: () => [{ stop }] })
      }
    });
    Object.defineProperty(window, "isSecureContext", { configurable: true, value: true });
    // jsdom has no BarcodeDetector API.
    Object.defineProperty(window, "BarcodeDetector", {
      configurable: true,
      writable: true,
      value: class {
        static getSupportedFormats = vi.fn().mockResolvedValue(["data_matrix", "code_128"]);
        detect = vi.fn().mockResolvedValue([{ rawValue: "S:S-2026-001" }]);
      }
    });
    vi.spyOn(HTMLMediaElement.prototype, "play").mockImplementation(() => Promise.resolve());

    try {
      vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
        makeResolveResponse({
          outcome: "Found",
          match: makeMatch({ id: 101, code: "S-2026-001", navigationTarget: "/receiving-testing?sampleId=101" })
        })
      );

      const user = userEvent.setup();
      const { onClose } = renderDialog();

      await user.click(screen.getByRole("button", { name: "Scan with camera" }));

      await waitFor(() => {
        expect(labelService.resolveScan).toHaveBeenCalledWith("S:S-2026-001", expect.any(AbortSignal));
        expect(onClose).toHaveBeenCalledTimes(1);
        expect(screen.getByTestId("location-probe").textContent).toBe("/receiving-testing?sampleId=101");
      });
    } finally {
      if (originalMediaDevices) {
        Object.defineProperty(navigator, "mediaDevices", originalMediaDevices);
      } else {
        delete (navigator as unknown as Record<string, unknown>).mediaDevices;
      }
      if (originalBarcodeDetector) {
        Object.defineProperty(window, "BarcodeDetector", originalBarcodeDetector);
      } else {
        delete (window as unknown as Record<string, unknown>).BarcodeDetector;
      }
      if (originalIsSecureContext) {
        Object.defineProperty(window, "isSecureContext", originalIsSecureContext);
      }
    }
  });
});
