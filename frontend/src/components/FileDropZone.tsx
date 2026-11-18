import { useRef, useState, useId, DragEvent } from "react";
import { Box, Button, Typography } from "@mui/material";
import CloudUploadOutlinedIcon from "@mui/icons-material/CloudUploadOutlined";
import InsertDriveFileOutlinedIcon from "@mui/icons-material/InsertDriveFileOutlined";
import { useTranslation } from "../i18n";
import { Ltr } from "./Ltr";

interface FileDropZoneProps {
  file: File | null;
  onFile: (file: File | null) => void;
  /** Passed to the file input, e.g. ".pdf,.png". */
  accept: string;
  /** What the server accepts, in words - shown under the prompt. */
  formatsHint: string;
  /** Trailing note, e.g. that the upload is recorded in the audit trail. */
  note?: string;
  disabled?: boolean;
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

// A file target that says what it takes: drop a file or browse, with the
// accepted formats and size spelled out. The server still validates type,
// signature and size; this only tells the user the rules up front.
export function FileDropZone({ file, onFile, accept, formatsHint, note, disabled }: FileDropZoneProps) {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);
  const inputId = useId();
  const [dragOver, setDragOver] = useState(false);

  const handleDrop = (e: DragEvent<HTMLElement>) => {
    e.preventDefault();
    setDragOver(false);
    if (disabled) return;
    const dropped = e.dataTransfer.files?.[0];
    if (dropped) onFile(dropped);
  };

  return (
    <Box
      onDragOver={(e) => { e.preventDefault(); if (!disabled) setDragOver(true); }}
      onDragLeave={() => setDragOver(false)}
      onDrop={handleDrop}
      sx={{
        border: "1.5px dashed",
        borderColor: dragOver ? "primary.main" : "divider",
        bgcolor: dragOver ? "action.hover" : "background.default",
        borderRadius: 2,
        px: 2,
        py: 2.5,
        textAlign: "center",
        transition: "border-color 0.15s ease, background-color 0.15s ease"
      }}
    >
      <input
        ref={inputRef}
        id={inputId}
        type="file"
        accept={accept}
        aria-label={t("components.fileDropZone.uploadAria", { formats: formatsHint })}
        style={{ display: "none" }}
        onChange={(e) => { onFile(e.target.files?.[0] ?? null); e.target.value = ""; }}
      />
      {file ? (
        <Box sx={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 1, flexWrap: "wrap" }}>
          <InsertDriveFileOutlinedIcon sx={{ color: "text.secondary" }} />
          <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{file.name}</Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>(<Ltr>{formatBytes(file.size)}</Ltr>)</Typography>
          <Button size="small" onClick={() => inputRef.current?.click()} disabled={disabled}>
            {t("components.fileDropZone.changeFile")}
          </Button>
        </Box>
      ) : (
        <>
          <CloudUploadOutlinedIcon sx={{ fontSize: 32, color: "text.secondary" }} />
          <Typography sx={{ fontSize: 14, fontWeight: 600, mt: 0.5 }}>
            {t("components.fileDropZone.dragDropPrefix")}{" "}
            <Button
              variant="text"
              size="small"
              onClick={() => inputRef.current?.click()}
              disabled={disabled}
              sx={{ p: 0, minWidth: 0, fontSize: 14, verticalAlign: "baseline" }}
            >
              {t("components.fileDropZone.browse")}
            </Button>
          </Typography>
        </>
      )}
      <Typography sx={{ fontSize: 12, color: "text.secondary", mt: 0.75 }}>
        {formatsHint}{note ? ` · ${note}` : ""}
      </Typography>
    </Box>
  );
}
