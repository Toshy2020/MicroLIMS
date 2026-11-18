import { useEffect, useRef, useState } from "react";
import { Alert, Box, Button, Stack, Typography } from "@mui/material";
import type { TFunction } from "i18next";
import { useTranslation } from "../../i18n";
import { FloatingDialog } from "../FloatingDialog";

export interface CameraScanDialogProps {
  open: boolean;
  onClose: () => void;
  onDetected: (value: string) => void;
}

type RequestedFormat = "data_matrix" | "code_128";

const REQUESTED_FORMATS: RequestedFormat[] = ["data_matrix", "code_128"];

interface BarcodeDetectionResult {
  rawValue?: string;
}

interface BarcodeDetectorLike {
  detect: (image: HTMLVideoElement) => Promise<readonly BarcodeDetectionResult[]>;
}

interface NativeBarcodeDetectorConstructor {
  new (options: { formats: RequestedFormat[] }): BarcodeDetectorLike;
  getSupportedFormats?: () => Promise<readonly string[]>;
}

async function createDetector(): Promise<BarcodeDetectorLike> {
  const nativeCtor = (window as Window & { BarcodeDetector?: NativeBarcodeDetectorConstructor }).BarcodeDetector;

  if (nativeCtor && typeof nativeCtor.getSupportedFormats === "function") {
    try {
      const supported = await nativeCtor.getSupportedFormats();
      if (REQUESTED_FORMATS.every((fmt) => supported.includes(fmt))) {
        return new nativeCtor({ formats: [...REQUESTED_FORMATS] });
      }
    } catch {
      // Fall back to the WebAssembly ponyfill if native capability probing fails.
    }
  }

  const [
    { BarcodeDetector: PonyfillDetector, prepareZXingModule },
    { default: zxingReaderWasmUrl }
  ] = await Promise.all([
    import("barcode-detector/ponyfill"),
    import("zxing-wasm/reader/zxing_reader.wasm?url")
  ]);

  prepareZXingModule({
    overrides: {
      locateFile: (path: string, prefix: string) => {
        if (path.endsWith(".wasm")) {
          return zxingReaderWasmUrl;
        }
        return prefix + path;
      }
    }
  });

  return new PonyfillDetector({ formats: [...REQUESTED_FORMATS] });
}

function describeCameraError(err: unknown, t: TFunction): string {
  if (err instanceof DOMException || err instanceof Error) {
    if (err.name === "NotAllowedError" || err.name === "PermissionDeniedError") {
      return t("components.cameraScanDialog.permissionDenied");
    }
    if (err.name === "NotFoundError" || err.name === "OverconstrainedError" || err.name === "DevicesNotFoundError") {
      return t("components.cameraScanDialog.notFound");
    }
    if (err.name === "SecurityError") {
      return t("components.cameraScanDialog.httpsRequired");
    }
    if (err.message.trim()) {
      return err.message;
    }
  }
  return t("components.cameraScanDialog.startFailed");
}

function stopMediaStream(stream: MediaStream | null): void {
  if (!stream) return;
  for (const track of stream.getTracks()) {
    track.stop();
  }
}

export function CameraScanDialog({ open, onClose, onDetected }: CameraScanDialogProps) {
  const { t } = useTranslation();
  const tRef = useRef(t);
  tRef.current = t;
  const [error, setError] = useState<string | null>(null);
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const handledRef = useRef(false);
  const onCloseRef = useRef(onClose);
  onCloseRef.current = onClose;
  const onDetectedRef = useRef(onDetected);
  onDetectedRef.current = onDetected;

  useEffect(() => {
    if (!open) {
      stopMediaStream(streamRef.current);
      streamRef.current = null;
      return;
    }

    handledRef.current = false;
    setError(null);

    if (!window.isSecureContext) {
      setError(tRef.current("components.cameraScanDialog.httpsRequired"));
      return;
    }

    const getUserMedia = navigator.mediaDevices?.getUserMedia?.bind(navigator.mediaDevices);
    if (!getUserMedia) {
      setError(tRef.current("components.cameraScanDialog.browserUnsupported"));
      return;
    }

    let cancelled = false;
    let rafId: number | null = null;
    let detecting = false;

    const cleanup = () => {
      cancelled = true;
      if (rafId !== null) {
        cancelAnimationFrame(rafId);
        rafId = null;
      }
      stopMediaStream(streamRef.current);
      streamRef.current = null;
      if (videoRef.current) {
        videoRef.current.srcObject = null;
      }
    };

    const start = async () => {
      try {
        const [stream, detector] = await Promise.all([
          getUserMedia({ video: { facingMode: "environment" }, audio: false }),
          createDetector()
        ]);

        if (cancelled) {
          stopMediaStream(stream);
          return;
        }

        streamRef.current = stream;
        const video = videoRef.current;
        if (video) {
          video.srcObject = stream;
          await video.play?.().catch(() => undefined);
        }

        const scanFrame = () => {
          if (cancelled || handledRef.current) return;

          const currentVideo = videoRef.current;
          if (!currentVideo || detecting) {
            rafId = requestAnimationFrame(scanFrame);
            return;
          }

          detecting = true;
          void detector
            .detect(currentVideo)
            .then((results) => {
              detecting = false;
              if (cancelled || handledRef.current) return;

              const match = results.find(
                (item) => typeof item.rawValue === "string" && item.rawValue.trim().length > 0
              );
              if (match?.rawValue) {
                handledRef.current = true;
                cleanup();
                onDetectedRef.current(match.rawValue);
                onCloseRef.current();
                return;
              }

              rafId = requestAnimationFrame(scanFrame);
            })
            .catch(() => {
              detecting = false;
              if (!cancelled && !handledRef.current) {
                rafId = requestAnimationFrame(scanFrame);
              }
            });
        };

        rafId = requestAnimationFrame(scanFrame);
      } catch (err: unknown) {
        if (cancelled) return;
        stopMediaStream(streamRef.current);
        streamRef.current = null;
        setError(describeCameraError(err, tRef.current));
      }
    };

    void start();

    return cleanup;
  }, [open]);

  return (
    <FloatingDialog
      open={open}
      onClose={onClose}
      title={t("components.cameraScanDialog.title")}
      maxWidth="sm"
      actions={<Button onClick={onClose}>{t("actions.close")}</Button>}
    >
      <Stack spacing={1.5}>
        {error ? (
          <Alert severity="error" role="alert">
            {error}
          </Alert>
        ) : (
          <>
            <Box
              sx={{
                borderRadius: 1,
                overflow: "hidden",
                border: 1,
                borderColor: "divider",
                bgcolor: "background.default"
              }}
            >
              <Box
                component="video"
                ref={videoRef}
                playsInline
                muted
                autoPlay
                aria-label={t("components.cameraScanDialog.previewAria")}
                sx={{ width: "100%", display: "block", maxHeight: 360, objectFit: "cover" }}
              />
            </Box>
            <Typography variant="body2" color="text.secondary">
              {t("components.cameraScanDialog.hint")}
            </Typography>
          </>
        )}
      </Stack>
    </FloatingDialog>
  );
}
