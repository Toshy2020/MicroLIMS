import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import axios, { AxiosHeaders } from "axios";
import { lightTheme } from "../../theme";
import {
  labelService,
  type LabelEntityType,
  type ScanMatch,
  type ScanResolveResponse
} from "../../services/labelService";
import { ScanField } from "./ScanField";

vi.mock("../../services/labelService", () => ({
  labelService: {
    getStatus: vi.fn(),
    print: vi.fn(),
    getFormats: vi.fn(),
    updateFormat: vi.fn(),
    resolveScan: vi.fn()
  }
}));

function makeMatch(overrides: Partial<ScanMatch> = {}): ScanMatch {
  return {
    entityType: "MediaLot",
    id: 10,
    code: "ML-2026-001",
    displayName: "Tryptic Soy Agar",
    status: "Released",
    usable: true,
    blockedReason: null,
    navigationTarget: "/media-lots?lotId=10",
    linkedEquipmentId: null,
    ...overrides
  };
}

function makeResolveResponse(overrides: Partial<ScanResolveResponse> = {}): ScanResolveResponse {
  return {
    outcome: "Found",
    layoutCorrected: false,
    normalizedValue: "MED:ML-2026-001",
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

function makeAxiosError(status: number, message: string) {
  const headers = new AxiosHeaders();
  return new axios.AxiosError(
    `Request failed with status code ${status}`,
    axios.AxiosError.ERR_BAD_REQUEST,
    undefined,
    undefined,
    {
      status,
      statusText: "Error",
      headers,
      config: { headers },
      data: { success: false, message, data: null, errors: null }
    }
  );
}

interface RenderOptions {
  label?: string;
  expectedType?: LabelEntityType;
  offeredIds?: readonly number[];
  onSelect?: (id: number) => void;
  disabled?: boolean;
}

function renderScanField({
  label = "Scan media lot",
  expectedType = "MediaLot",
  offeredIds = [10, 20],
  onSelect = vi.fn(),
  disabled = false
}: RenderOptions = {}) {
  const result = render(
    <ThemeProvider theme={lightTheme}>
      <ScanField
        label={label}
        expectedType={expectedType}
        offeredIds={offeredIds}
        onSelect={onSelect}
        disabled={disabled}
      />
    </ThemeProvider>
  );
  return { onSelect, ...result };
}

describe("ScanField", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it("selects an offered match and shows Selected {code} in status region", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        match: makeMatch({ id: 10, code: "ML-2026-001" })
      })
    );

    const { onSelect } = renderScanField({ offeredIds: [10, 20] });
    const input = screen.getByLabelText("Scan media lot");

    await user.type(input, "MED:ML-2026-001{Enter}");

    await waitFor(() => {
      expect(onSelect).toHaveBeenCalledTimes(1);
      expect(onSelect).toHaveBeenCalledWith(10);
    });
    expect(screen.getByRole("status").textContent).toContain("Selected ML-2026-001");
  });

  it("uses linkedEquipmentId for EquipmentAsset when comparing against offeredIds and selecting", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        normalizedValue: "EQ:INC-01",
        match: makeMatch({
          entityType: "EquipmentAsset",
          id: 999,
          linkedEquipmentId: 55,
          code: "INC-01",
          displayName: "Incubator 1"
        })
      })
    );

    const { onSelect } = renderScanField({
      label: "Scan incubator",
      expectedType: "EquipmentAsset",
      offeredIds: [55, 66]
    });

    await user.type(screen.getByLabelText("Scan incubator"), "EQ:INC-01{Enter}");

    await waitFor(() => {
      expect(onSelect).toHaveBeenCalledWith(55);
    });
    expect(screen.getByRole("status").textContent).toContain("Selected INC-01");
  });

  it("shows server blockedReason in alert and never selects when match is not usable", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        match: makeMatch({
          id: 10,
          code: "ML-2026-EXP",
          usable: false,
          blockedReason: "Media lot ML-2026-EXP is expired."
        })
      })
    );

    const { onSelect } = renderScanField({ offeredIds: [10] });
    await user.type(screen.getByLabelText("Scan media lot"), "MED:ML-2026-EXP{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Media lot ML-2026-EXP is expired.");
    expect(onSelect).not.toHaveBeenCalled();
  });

  it("shows wrong type message with human-readable labels when entityType differs", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        match: makeMatch({
          entityType: "Sample",
          id: 10,
          code: "S-2026-001"
        })
      })
    );

    const { onSelect } = renderScanField({
      expectedType: "MediaLot",
      offeredIds: [10]
    });

    await user.type(screen.getByLabelText("Scan media lot"), "S:S-2026-001{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("This is a Sample, not a Media lot.");
    expect(onSelect).not.toHaveBeenCalled();
  });

  it("shows not offered message when match id is outside offeredIds", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        match: makeMatch({
          id: 99,
          code: "ML-2026-099"
        })
      })
    );

    const { onSelect } = renderScanField({ offeredIds: [10, 20] });
    await user.type(screen.getByLabelText("Scan media lot"), "MED:ML-2026-099{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("ML-2026-099 is not available for this step.");
    expect(onSelect).not.toHaveBeenCalled();
  });

  it("never selects an id that was not offered", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        normalizedValue: "EQ:EQ-999",
        match: makeMatch({
          entityType: "EquipmentAsset",
          id: 10,
          linkedEquipmentId: 888,
          code: "EQ-999",
          usable: true
        })
      })
    );

    const { onSelect } = renderScanField({
      label: "Scan incubator",
      expectedType: "EquipmentAsset",
      offeredIds: [10, 20]
    });

    await user.type(screen.getByLabelText("Scan incubator"), "EQ:EQ-999{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("EQ-999 is not available for this step.");
    expect(onSelect).not.toHaveBeenCalled();
  });

  it("shows equipment with no link message when EquipmentAsset has linkedEquipmentId null", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        normalizedValue: "EQ:BAL-01",
        match: makeMatch({
          entityType: "EquipmentAsset",
          id: 77,
          linkedEquipmentId: null,
          code: "BAL-01",
          usable: true
        })
      })
    );

    const { onSelect } = renderScanField({
      label: "Scan autoclave",
      expectedType: "EquipmentAsset",
      offeredIds: [77]
    });

    await user.type(screen.getByLabelText("Scan autoclave"), "EQ:BAL-01{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain(
      "BAL-01 is not linked to a workflow instrument. Pick it from the list."
    );
    expect(onSelect).not.toHaveBeenCalled();
  });

  it("shows not found message plus hint when present", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        outcome: "NotFound",
        match: null,
        hint: "Switch keyboard to English and scan again."
      })
    );

    const { onSelect } = renderScanField();
    await user.type(screen.getByLabelText("Scan media lot"), "UNKNOWN{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("No record found for this code.");
    expect(alert.textContent).toContain("Switch keyboard to English and scan again.");
    expect(onSelect).not.toHaveBeenCalled();
  });

  it("shows not found message without extra hint when hint is null", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        outcome: "NotFound",
        match: null,
        hint: null
      })
    );

    const { onSelect } = renderScanField();
    await user.type(screen.getByLabelText("Scan media lot"), "MISSING{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toBe("No record found for this code.");
    expect(onSelect).not.toHaveBeenCalled();
  });

  it("shows ambiguous message when more than one record matches", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        outcome: "Ambiguous",
        match: null,
        candidates: [
          makeMatch({ entityType: "MediaLot", id: 10, code: "2026-001" }),
          makeMatch({ entityType: "MaterialStockLot", id: 30, code: "2026-001" })
        ]
      })
    );

    const { onSelect } = renderScanField();
    await user.type(screen.getByLabelText("Scan media lot"), "2026-001{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("More than one record has this code. Pick it from the list.");
    expect(onSelect).not.toHaveBeenCalled();
  });

  it("adds Arabic keyboard notice when layoutCorrected is true", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        layoutCorrected: true,
        match: makeMatch({ id: 10, code: "ML-2026-001" })
      })
    );

    const { onSelect } = renderScanField({ offeredIds: [10] });
    await user.type(screen.getByLabelText("Scan media lot"), "ةثيث:ةم-2026-001{Enter}");

    await waitFor(() => {
      expect(onSelect).toHaveBeenCalledWith(10);
    });
    const status = screen.getByRole("status");
    expect(status.textContent).toContain("Selected ML-2026-001");
    expect(status.textContent).toContain("Keyboard was in Arabic; the code was converted.");
  });

  it("shows the error message in an alert when resolve fails", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockRejectedValueOnce(
      makeAxiosError(503, "Label resolution service is unavailable.")
    );

    const { onSelect } = renderScanField();
    await user.type(screen.getByLabelText("Scan media lot"), "MED:ML-2026-001{Enter}");

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Label resolution service is unavailable.");
    expect(onSelect).not.toHaveBeenCalled();
  });

  it("a second scan before the first returns selects only the second", async () => {
    const user = userEvent.setup();
    const first = createDeferred<ScanResolveResponse>();
    const second = createDeferred<ScanResolveResponse>();
    const signals: (AbortSignal | undefined)[] = [];

    vi.mocked(labelService.resolveScan).mockImplementation((_value, signal) => {
      signals.push(signal);
      return signals.length === 1 ? first.promise : second.promise;
    });

    const { onSelect } = renderScanField({ offeredIds: [10, 20] });
    const input = screen.getByLabelText("Scan media lot");

    await user.type(input, "MED:FIRST{Enter}");
    await waitFor(() => {
      expect(labelService.resolveScan).toHaveBeenCalledTimes(1);
    });
    expect(screen.getByRole("progressbar")).toBeTruthy();

    await user.type(input, "MED:SECOND{Enter}");
    await waitFor(() => {
      expect(labelService.resolveScan).toHaveBeenCalledTimes(2);
    });
    expect(signals[0]?.aborted).toBe(true);

    first.resolve(
      makeResolveResponse({
        match: makeMatch({ id: 10, code: "FIRST" })
      })
    );
    second.resolve(
      makeResolveResponse({
        match: makeMatch({ id: 20, code: "SECOND" })
      })
    );

    await waitFor(() => {
      expect(onSelect).toHaveBeenCalledTimes(1);
      expect(onSelect).toHaveBeenCalledWith(20);
    });
    expect(screen.getByRole("status").textContent).toContain("Selected SECOND");
  });

  it("clears the input and keeps focus after a scan", async () => {
    const user = userEvent.setup();
    vi.mocked(labelService.resolveScan).mockResolvedValueOnce(
      makeResolveResponse({
        match: makeMatch({ id: 10, code: "ML-2026-001" })
      })
    );

    renderScanField({ offeredIds: [10] });
    const input = screen.getByLabelText("Scan media lot") as HTMLInputElement;

    await user.type(input, "MED:ML-2026-001{Enter}");

    await waitFor(() => {
      expect(screen.getByRole("status").textContent).toContain("Selected ML-2026-001");
    });
    expect(input.value).toBe("");
    expect(document.activeElement).toBe(input);
  });

  it("disables the submit button while pending and respects disabled prop", async () => {
    const user = userEvent.setup();
    const pendingResolve = createDeferred<ScanResolveResponse>();
    vi.mocked(labelService.resolveScan).mockReturnValueOnce(pendingResolve.promise);

    renderScanField({ offeredIds: [10] });
    const input = screen.getByLabelText("Scan media lot");
    const scanButton = screen.getByRole("button", { name: "Scan" }) as HTMLButtonElement;

    expect(scanButton.disabled).toBe(true);
    await user.type(input, "MED:ML-2026-001");
    expect(scanButton.disabled).toBe(false);

    await user.click(scanButton);
    await waitFor(() => {
      expect(screen.getByRole("progressbar")).toBeTruthy();
    });
    expect(scanButton.disabled).toBe(true);

    pendingResolve.resolve(
      makeResolveResponse({
        match: makeMatch({ id: 10, code: "ML-2026-001" })
      })
    );

    await waitFor(() => {
      expect(screen.getByRole("status").textContent).toContain("Selected ML-2026-001");
    });

    cleanup();
    renderScanField({ disabled: true });
    expect((screen.getByLabelText("Scan media lot") as HTMLInputElement).disabled).toBe(true);
    expect((screen.getByRole("button", { name: "Scan" }) as HTMLButtonElement).disabled).toBe(true);
  });
});
