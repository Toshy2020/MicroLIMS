import type { ReactNode } from "react";
import { ButtonBase, Chip, Paper, Stack, Tooltip, Typography, useTheme } from "@mui/material";
import type { SxProps, Theme } from "@mui/material";
import type { KpiDefinition, KpiEntry, KpiResult } from "../types/kpiTypes";
import { FLAG_LABELS, entryView, formatExtras, formatPrior } from "../utils/kpiFormat";

// Gap messages and flags from the server, in the one non-failure warning tone.
export function KpiChips({ result }: { result: KpiResult }) {
  const theme = useTheme();
  if (result.gaps.length === 0 && result.flags.length === 0) return null;
  const tone = theme.custom.status.inconclusive;
  const sx = { bgcolor: tone.bg, color: tone.text, border: `1px solid ${tone.border}`, fontSize: 12 };
  return (
    <Stack direction="row" useFlexGap sx={{ flexWrap: "wrap", gap: 0.5 }}>
      {result.gaps.map((g) => <Chip key={g.code} size="small" label={g.message} sx={sx} />)}
      {result.flags.map((f) => <Chip key={f} size="small" label={FLAG_LABELS[f] ?? f} sx={sx} />)}
    </Stack>
  );
}

// Typography as a block-level span: the text sits inside a <button>, where <p> is not allowed.
function Text({ children, sx, ...rest }: { children: ReactNode; sx?: SxProps<Theme>; "data-muted"?: string }) {
  return (
    <Typography component="span" {...rest} sx={[{ display: "block" }, ...(Array.isArray(sx) ? sx : [sx])]}>
      {children}
    </Typography>
  );
}

interface KpiTileProps {
  entry: KpiEntry;
  definition?: KpiDefinition;
  onOpenRecords(key: string): void;
}

export function KpiTile({ entry, definition, onOpenRecords }: KpiTileProps) {
  const theme = useTheme();
  const view = entryView(entry);

  if (view.kind === "error") {
    const tone = theme.custom.status.detected;
    return (
      <Paper sx={{ p: 2, height: "100%", border: `1px solid ${tone.border}` }}>
        <Typography sx={{ fontSize: 12.5, fontWeight: 600, color: "text.secondary" }}>{entry.name}</Typography>
        <Typography role="alert" sx={{ fontSize: 16, fontWeight: 700, color: tone.text, my: 0.5 }}>
          Could not be calculated
        </Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{view.message}</Typography>
      </Paper>
    );
  }

  const { result } = view;
  const label = view.kind === "noData" ? "No data" : view.text;
  const extras = formatExtras(entry);

  const button = (
    <ButtonBase
      onClick={() => onOpenRecords(entry.key)}
      aria-label={`${entry.name}: ${label}. Show records`}
      sx={{
        display: "flex", flexDirection: "column", alignItems: "flex-start", gap: 0.5,
        width: "100%", height: "100%", p: 2, textAlign: "left", borderRadius: "inherit"
      }}
    >
      <Text sx={{ fontSize: 12.5, fontWeight: 600, color: "text.secondary" }}>{entry.name}</Text>
      {view.kind === "noData" ? (
        <Text sx={{ fontSize: 20, fontWeight: 700, color: "text.secondary" }}>No data</Text>
      ) : (
        <Text
          data-muted={view.muted ? "true" : "false"}
          sx={{ fontSize: 24, fontWeight: 800, color: view.muted ? "text.disabled" : theme.palette.primary.main }}
        >
          {view.text}
        </Text>
      )}
      {view.kind === "value" && view.muted && (
        <Text sx={{ fontSize: 12, color: "text.secondary" }}>too few records to judge</Text>
      )}
      {extras.map((x) => (
        <Text key={x.label} sx={{ fontSize: 12, color: "text.secondary" }}>{x.label} {x.text}</Text>
      ))}
      <Text sx={{ fontSize: 12, color: "text.secondary" }}>n = {result.n}</Text>
      {result.prior != null && (
        <Text sx={{ fontSize: 12, color: "text.secondary" }}>{formatPrior(entry.unit, result.prior)}</Text>
      )}
      <KpiChips result={result} />
    </ButtonBase>
  );

  return (
    <Paper sx={{ height: "100%" }}>
      {definition ? <Tooltip title={definition.definition} arrow describeChild>{button}</Tooltip> : button}
    </Paper>
  );
}
