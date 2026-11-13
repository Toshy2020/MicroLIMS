import { useState } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThemeProvider } from "@mui/material/styles";
import { Button } from "@mui/material";
import { lightTheme } from "../../theme";
import { CameraScanDialog } from "./CameraScanDialog";

const ponyfillPrepareMock = vi.fn();
const ponyfillDetectMock = vi.fn();

// WebAssembly decoding cannot execute inside jsdom, so mock the fallback package boundary.
vi.mock("barcode-detector/ponyfill", () => ({
  prepareZXingModule: ponyfillPrepareMock,
  BarcodeDetector: class MockPonyfillBarcodeDetector {
    detect = ponyfillDetectMock;
  }
}));

interface FakeDetectorInstance {
  detect: ReturnType<typeof vi.fn>;
}

const originalMediaDevices = Object.getOwnPropertyDescriptor(navigator, "mediaDevices");
const originalBarcodeDetector = Object.getOwnPropertyDescriptor(window, "BarcodeDetector");
const originalIsSecureContext = Object.getOwnPropertyDescriptor(window, "isSecureContext");

function installCameraMocks({
  detectedSequence = [[]],
  supportedFormats = ["data_matrix", "code_128"],
  getUserMediaError
}: {
  detectedSequence?: Array<Array<{ rawValue: string }>>;
  supportedFormats?: string[];
  getUserMediaError?: Error;
} = {}) {
  const stop = vi.fn();
  const track = { stop } as unknown as MediaStreamTrack;
  const stream = {
    getTracks: () => [track]
  } as unknown as MediaStream;

  const getUserMedia = getUserMediaError
    ? vi.fn().mockRejectedValue(getUserMediaError)
    : vi.fn().mockResolvedValue(stream);

  // jsdom has no camera or navigator.mediaDevices implementation.
  Object.defineProperty(navigator, "mediaDevices", {
    configurable: true,
    value: { getUserMedia }
  });

  Object.defineProperty(window, "isSecureContext", {
    configurable: true,
    value: true
  });

  let callCount = 0;
  const detectorInstances: FakeDetectorInstance[] = [];
  const detectFn = vi.fn().mockImplementation(async () => {
    const idx = Math.min(callCount, detectedSequence.length - 1);
    callCount += 1;
    return detectedSequence[idx] ?? [];
  });

  // jsdom has no BarcodeDetector API.
  class FakeBarcodeDetector {
    detect = detectFn;
    constructor(_options?: { formats?: string[] }) {
      detectorInstances.push(this);
    }
    static getSupportedFormats = vi.fn().mockResolvedValue(supportedFormats);
  }

  Object.defineProperty(window, "BarcodeDetector", {
    configurable: true,
    writable: true,
    value: FakeBarcodeDetector
  });

  return { stop, getUserMedia, detectFn, FakeBarcodeDetector };
}

function renderCameraDialog(props: Partial<React.ComponentProps<typeof CameraScanDialog>> = {}) {
  const onClose = props.onClose ?? vi.fn();
  const onDetected = props.onDetected ?? vi.fn();
  const result = render(
    <ThemeProvider theme={lightTheme}>
      <CameraScanDialog open={true} onClose={onClose} onDetected={onDetected} {...props} />
    </ThemeProvider>
  );
  return { onClose, onDetected, ...result };
}

describe("CameraScanDialog", () => {
  beforeEach(() => {
    vi.spyOn(HTMLMediaElement.prototype, "play").mockImplementation(() => Promise.resolve());
  });

  afterEach(() => {
    cleanup();
    vi.restoreAllMocks();
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
  });

  it("reports the first detected value and stops the camera", async () => {
    const { stop, getUserMedia } = installCameraMocks({
      detectedSequence: [[], [{ rawValue: "MED:ML-2026-001" }]]
    });

    const onClose = vi.fn();
    const onDetected = vi.fn();
    renderCameraDialog({ onClose, onDetected });

    await waitFor(() => {
      expect(onDetected).toHaveBeenCalledTimes(1);
      expect(onDetected).toHaveBeenCalledWith("MED:ML-2026-001");
      expect(onClose).toHaveBeenCalledTimes(1);
      expect(stop).toHaveBeenCalled();
    });
    expect(getUserMedia).toHaveBeenCalledWith({
      video: { facingMode: "environment" },
      audio: false
    });
  });

  it("shows the permission-refused message", async () => {
    const denied = new DOMException("Permission denied", "NotAllowedError");
    installCameraMocks({ getUserMediaError: denied });

    renderCameraDialog();

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toMatch(/permission/i);
  });

  it("shows the no-camera message", async () => {
    const missing = new DOMException("Requested device not found", "NotFoundError");
    installCameraMocks({ getUserMediaError: missing });

    renderCameraDialog();

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toMatch(/no camera/i);
  });

  it("Escape closes and stops the camera", async () => {
    const { stop, getUserMedia } = installCameraMocks({
      detectedSequence: [[]]
    });

    function TriggerHost() {
      const [open, setOpen] = useState(false);
      return (
        <ThemeProvider theme={lightTheme}>
          <Button type="button" onClick={() => setOpen(true)}>
            Open camera
          </Button>
          <CameraScanDialog open={open} onClose={() => setOpen(false)} onDetected={vi.fn()} />
        </ThemeProvider>
      );
    }

    const user = userEvent.setup();
    render(<TriggerHost />);

    const trigger = screen.getByRole("button", { name: "Open camera" });
    await user.click(trigger);

    await waitFor(() => {
      expect(getUserMedia).toHaveBeenCalledTimes(1);
      expect(screen.getByRole("dialog")).toBeTruthy();
    });

    await user.keyboard("{Escape}");

    await waitFor(() => {
      expect(screen.queryByRole("dialog")).toBeNull();
      expect(stop).toHaveBeenCalled();
      expect(document.activeElement).toBe(trigger);
    });
  });

  it("reports only once when the detector fires repeatedly", async () => {
    const { stop } = installCameraMocks({
      detectedSequence: [
        [{ rawValue: "S:FIRST-001" }],
        [{ rawValue: "S:SECOND-002" }]
      ]
    });

    const onClose = vi.fn();
    const onDetected = vi.fn();
    renderCameraDialog({ onClose, onDetected });

    await waitFor(() => {
      expect(onDetected).toHaveBeenCalledTimes(1);
    });
    expect(onDetected).toHaveBeenCalledWith("S:FIRST-001");
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(stop).toHaveBeenCalled();
  });

  it("falls back to the bundled decoder when the browser has no BarcodeDetector", async () => {
    ponyfillPrepareMock.mockReset();
    ponyfillDetectMock.mockReset().mockResolvedValue([{ rawValue: "MED:PONYFILL-01" }]);

    const { stop } = installCameraMocks({ detectedSequence: [[]] });
    // Remove native BarcodeDetector so the component takes the lazy ponyfill path.
    delete (window as unknown as Record<string, unknown>).BarcodeDetector;

    const onClose = vi.fn();
    const onDetected = vi.fn();
    renderCameraDialog({ onClose, onDetected });

    await waitFor(() => {
      expect(ponyfillPrepareMock).toHaveBeenCalledTimes(1);
      expect(onDetected).toHaveBeenCalledWith("MED:PONYFILL-01");
      expect(onClose).toHaveBeenCalledTimes(1);
      expect(stop).toHaveBeenCalled();
    });

    const prepareArg = ponyfillPrepareMock.mock.calls[0]?.[0] as {
      overrides?: { locateFile?: (path: string, prefix: string) => string };
    };
    const locateFile = prepareArg?.overrides?.locateFile;
    expect(typeof locateFile).toBe("function");

    const wasmLocation = locateFile!("zxing_reader.wasm", "/assets/");
    expect(wasmLocation).toMatch(/zxing_reader.*\.wasm/i);
    expect(wasmLocation).not.toMatch(/^https?:\/\//i);
    expect(locateFile!("other.bin", "/assets/")).toBe("/assets/other.bin");
  });
});
