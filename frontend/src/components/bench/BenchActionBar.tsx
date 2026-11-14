import { useId } from "react";
import type { ReactNode } from "react";
import { Box, Button, CircularProgress, Typography } from "@mui/material";
import { BENCH_TARGET_GAP } from "../../theme/benchTheme";

export interface BenchActionBarProps {
  onCancel(): void;
  onSave(): void;
  onSaveAndNext?(): void;
  saving: boolean;
  saveLabel?: string;
  saveDisabledReason?: string;
  keypad?: ReactNode;
}

export function BenchActionBar({ onCancel, onSave, onSaveAndNext, saving, saveLabel = "Save", saveDisabledReason, keypad }: BenchActionBarProps) {
  const reasonId = useId();
  const blocked = saving || Boolean(saveDisabledReason);
  return (
    <Box sx={{ display: "flex", flexDirection: "column", gap: `${BENCH_TARGET_GAP}px`, width: "100%" }}>
      {keypad}
      {saveDisabledReason && (
        <Typography id={reasonId} variant="body2" color="text.secondary">
          {saveDisabledReason}
        </Typography>
      )}
      <Box sx={{ display: "flex", flexWrap: "wrap", justifyContent: "flex-end", gap: `${BENCH_TARGET_GAP}px` }}>
        <Button onClick={onCancel} disabled={saving}>
          Cancel
        </Button>
        {onSaveAndNext && (
          <Button variant="outlined" onClick={() => { if (!blocked) onSaveAndNext(); }} disabled={blocked}>
            Save and next
          </Button>
        )}
        <Button
          variant="contained"
          onClick={() => { if (!blocked) onSave(); }}
          disabled={blocked}
          aria-describedby={saveDisabledReason ? reasonId : undefined}
          aria-busy={saving}
          startIcon={saving ? <CircularProgress size={16} color="inherit" /> : undefined}
        >
          {saveLabel}
        </Button>
      </Box>
    </Box>
  );
}
