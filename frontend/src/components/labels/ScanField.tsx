import { useRef, useState } from "react";
import QrCodeScannerOutlinedIcon from "@mui/icons-material/QrCodeScannerOutlined";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  InputAdornment,
  Stack,
  TextField
} from "@mui/material";
import axios from "axios";
import type { LabelEntityType, ScanResolveResponse } from "../../services/labelService";
import { monospaceFontFamily } from "../../theme/palette";
import type { StatusTone } from "../../theme/statusTokens";
import { getErrorMessage } from "../../utils/errorMessage";
import { ENTITY_TYPE_LABELS } from "./entityTypeLabels";
import { useScanResolve } from "./useScanResolve";

export interface ScanFieldProps {
  label: string;
  expectedType: LabelEntityType;
  offeredIds: readonly number[];
  onSelect: (id: number) => void;
  disabled?: boolean;
}

interface ScanFeedback {
  role: "status" | "alert";
  severity: "success" | "error";
  tone: StatusTone;
  message: string;
  hint: string | null;
  layoutNotice: string | null;
}

type EvaluatedScan =
  | {
      kind: "select";
      id: number;
      message: string;
      layoutNotice: string | null;
    }
  | {
      kind: "refusal";
      message: string;
      hint: string | null;
      layoutNotice: string | null;
    };

function evaluateScanResponse(
  response: ScanResolveResponse,
  expectedType: LabelEntityType,
  offeredIds: readonly number[]
): EvaluatedScan {
  const layoutNotice = response.layoutCorrected
    ? "Keyboard was in Arabic; the code was converted."
    : null;

  if (response.outcome === "Ambiguous") {
    return {
      kind: "refusal",
      message: "More than one record has this code. Pick it from the list.",
      hint: null,
      layoutNotice
    };
  }

  if (response.outcome === "NotFound" || !response.match) {
    return {
      kind: "refusal",
      message: "No record found for this code.",
      hint: response.hint?.trim() ? response.hint.trim() : null,
      layoutNotice
    };
  }

  const { match } = response;

  if (match.entityType !== expectedType) {
    const foundLabel = ENTITY_TYPE_LABELS[match.entityType] ?? match.entityType;
    const expectedLabel = ENTITY_TYPE_LABELS[expectedType] ?? expectedType;
    return {
      kind: "refusal",
      message: `This is a ${foundLabel}, not a ${expectedLabel}.`,
      hint: null,
      layoutNotice
    };
  }

  if (!match.usable) {
    return {
      kind: "refusal",
      message: match.blockedReason?.trim() || "This record cannot be used.",
      hint: null,
      layoutNotice
    };
  }

  if (expectedType === "EquipmentAsset" && match.linkedEquipmentId === null) {
    return {
      kind: "refusal",
      message: `${match.code} is not linked to a workflow instrument. Pick it from the list.`,
      hint: null,
      layoutNotice
    };
  }

  const candidateId =
    expectedType === "EquipmentAsset" ? match.linkedEquipmentId : match.id;

  if (candidateId === null || !offeredIds.includes(candidateId)) {
    return {
      kind: "refusal",
      message: `${match.code} is not available for this step.`,
      hint: null,
      layoutNotice
    };
  }

  return {
    kind: "select",
    id: candidateId,
    message: `Selected ${match.code}`,
    layoutNotice
  };
}

function resolveErrorMessage(err: unknown): string {
  const fallback = "Could not resolve scan.";
  if (axios.isAxiosError(err)) {
    return getErrorMessage(err, fallback);
  }
  if (err instanceof Error && err.message.trim()) {
    return err.message;
  }
  return fallback;
}

export function ScanField({
  label,
  expectedType,
  offeredIds,
  onSelect,
  disabled = false
}: ScanFieldProps) {
  const [value, setValue] = useState("");
  const [feedback, setFeedback] = useState<ScanFeedback | null>(null);
  const { resolve, pending } = useScanResolve();
  const inputRef = useRef<HTMLInputElement | null>(null);

  const offeredIdsRef = useRef(offeredIds);
  offeredIdsRef.current = offeredIds;
  const onSelectRef = useRef(onSelect);
  onSelectRef.current = onSelect;
  const expectedTypeRef = useRef(expectedType);
  expectedTypeRef.current = expectedType;

  const submitScan = async () => {
    const rawValue = value;
    if (disabled || !rawValue.trim()) {
      return;
    }

    setValue("");
    inputRef.current?.focus();

    try {
      const response = await resolve(rawValue);
      if (!response) {
        return;
      }

      const evaluated = evaluateScanResponse(
        response,
        expectedTypeRef.current,
        offeredIdsRef.current
      );

      if (evaluated.kind === "select") {
        onSelectRef.current(evaluated.id);
        setFeedback({
          role: "status",
          severity: "success",
          tone: "notDetected",
          message: evaluated.message,
          hint: null,
          layoutNotice: evaluated.layoutNotice
        });
      } else {
        setFeedback({
          role: "alert",
          severity: "error",
          tone: "detected",
          message: evaluated.message,
          hint: evaluated.hint,
          layoutNotice: evaluated.layoutNotice
        });
      }
    } catch (err: unknown) {
      setFeedback({
        role: "alert",
        severity: "error",
        tone: "detected",
        message: resolveErrorMessage(err),
        hint: null,
        layoutNotice: null
      });
    }
  };

  return (
    <Box sx={{ width: "100%" }}>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
        <TextField
          inputRef={inputRef}
          label={label}
          size="small"
          fullWidth
          value={value}
          disabled={disabled}
          onChange={(e) => setValue(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              e.preventDefault();
              void submitScan();
            }
          }}
          slotProps={{
            htmlInput: {
              "aria-busy": pending || undefined
            },
            input: {
              sx: { fontFamily: monospaceFontFamily },
              endAdornment: pending ? (
                <InputAdornment position="end">
                  <CircularProgress size={18} aria-label="Resolving scan" />
                </InputAdornment>
              ) : undefined
            }
          }}
        />
        <Button
          type="button"
          variant="outlined"
          size="small"
          disabled={disabled || pending || value.trim().length === 0}
          onClick={() => void submitScan()}
          startIcon={<QrCodeScannerOutlinedIcon fontSize="small" />}
          sx={{ whiteSpace: "nowrap", flexShrink: 0 }}
        >
          Scan
        </Button>
      </Stack>
      {feedback && (
        <Alert
          severity={feedback.severity}
          role={feedback.role}
          sx={(theme) => {
            const tokens = theme.custom.status[feedback.tone];
            return {
              mt: 1,
              bgcolor: tokens.bg,
              color: tokens.text,
              border: "1px solid",
              borderColor: tokens.border,
              "& .MuiAlert-icon": { color: tokens.text }
            };
          }}
        >
          <Box>{feedback.message}</Box>
          {feedback.hint && <Box>{feedback.hint}</Box>}
          {feedback.layoutNotice && <Box>{feedback.layoutNotice}</Box>}
        </Alert>
      )}
    </Box>
  );
}
