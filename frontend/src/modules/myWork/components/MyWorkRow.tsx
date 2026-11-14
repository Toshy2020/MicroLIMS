import { Box, Button, Stack, Typography, useTheme } from "@mui/material";
import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import { Link } from "react-router-dom";
import { monospaceFontFamily } from "../../../theme/palette";
import type { MyWorkItem } from "../myWorkTypes";
import { routeForTarget } from "../targetRoutes";
import { UrgencyLabel } from "./UrgencyLabel";

function formatDueAt(dueAtUtc: string, timeZoneId: string): string {
  const date = new Date(dueAtUtc);
  if (Number.isNaN(date.getTime())) return dueAtUtc;
  try {
    return new Intl.DateTimeFormat("en-GB", {
      day: "2-digit",
      month: "short",
      hour: "2-digit",
      minute: "2-digit",
      timeZone: timeZoneId || undefined,
      timeZoneName: "short"
    }).format(date);
  } catch {
    return new Intl.DateTimeFormat("en-GB", {
      day: "2-digit",
      month: "short",
      hour: "2-digit",
      minute: "2-digit",
      timeZoneName: "short"
    }).format(date);
  }
}

export interface MyWorkRowProps {
  item: MyWorkItem;
  timeZoneId: string;
  onTake: (item: MyWorkItem) => void;
  onRelease: (item: MyWorkItem) => void;
  busyKey?: string | null;
}

export function MyWorkRow({ item, timeZoneId, onTake, onRelease, busyKey }: MyWorkRowProps) {
  const theme = useTheme();
  const route = item.locked ? null : routeForTarget(item.target, item.lab);
  const canTake = !item.locked && item.actions.includes("take") && item.target.testOrderId != null;
  const canRelease = !item.locked && item.actions.includes("release") && item.target.testOrderId != null;
  const isBusy = busyKey === item.key;

  const titleContent = (
    <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap" }}>
      <Typography
        component="span"
        sx={{
          fontFamily: monospaceFontFamily,
          fontVariantNumeric: "tabular-nums",
          fontSize: 13,
          fontWeight: 700,
          color: item.locked ? "text.secondary" : "primary.main"
        }}
      >
        {item.identifier}
      </Typography>
      <Typography
        component="span"
        sx={{
          fontSize: 13.5,
          fontWeight: 600,
          color: item.locked ? "text.secondary" : "text.primary"
        }}
      >
        {item.title}
      </Typography>
    </Box>
  );

  return (
    <Box
      data-row-key={item.key}
      data-testid={`my-work-row-${item.key}`}
      aria-disabled={item.locked ? "true" : undefined}
      sx={{
        display: "flex",
        justifyContent: "space-between",
        alignItems: "center",
        gap: 2,
        px: 2,
        py: 1.5,
        borderRadius: 1.5,
        border: "1px solid",
        borderColor: item.locked ? theme.custom.status.pending.border : theme.palette.divider,
        bgcolor: item.locked ? theme.custom.status.pending.bg : theme.palette.background.paper,
        opacity: item.locked ? 0.85 : 1,
        "&:hover": item.locked
          ? undefined
          : {
              bgcolor: theme.palette.action.hover
            }
      }}
    >
      <Box sx={{ minWidth: 0, flex: 1 }}>
        {route ? (
          <Box
            component={Link}
            to={route}
            sx={{
              textDecoration: "none",
              color: "inherit",
              display: "inline-block",
              "&:hover": { textDecoration: "underline" },
              "&:focus-visible": {
                outline: `2px solid ${theme.palette.primary.main}`,
                outlineOffset: 2,
                borderRadius: 0.5
              }
            }}
          >
            {titleContent}
          </Box>
        ) : (
          titleContent
        )}

        <Stack direction="row" spacing={1.5} sx={{ mt: 0.5, alignItems: "center", flexWrap: "wrap" }}>
          {item.context && (
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {item.context}
            </Typography>
          )}
          {item.sectionName && (
            <Typography variant="caption" sx={{ color: "text.secondary" }}>
              {item.sectionName}
            </Typography>
          )}
          {item.locked && (
            <Stack direction="row" spacing={0.5} sx={{ alignItems: "center", color: theme.custom.status.pending.text }}>
              <LockOutlinedIcon sx={{ fontSize: 14 }} aria-hidden />
              <Typography variant="caption" sx={{ fontWeight: 600 }}>
                {item.lockReason || "Locked"}
              </Typography>
            </Stack>
          )}
          {!item.locked && !route && (
            <Typography variant="caption" sx={{ color: "text.secondary", fontStyle: "italic" }}>
              Open from the workspace
            </Typography>
          )}
        </Stack>
      </Box>

      <Stack direction="row" spacing={1.25} sx={{ alignItems: "center", flexShrink: 0, flexWrap: "wrap" }}>
        <UrgencyLabel urgency={item.urgency} />
        {item.dueAtUtc && (
          <Typography
            data-testid="my-work-due-time"
            variant="caption"
            sx={{
              color: "text.secondary",
              fontVariantNumeric: "tabular-nums",
              whiteSpace: "nowrap"
            }}
          >
            {formatDueAt(item.dueAtUtc, timeZoneId)}
          </Typography>
        )}
        {canTake && (
          <Button
            size="small"
            variant="outlined"
            disabled={isBusy}
            onClick={() => onTake(item)}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            Take
          </Button>
        )}
        {canRelease && (
          <Button
            size="small"
            variant="outlined"
            color="warning"
            disabled={isBusy}
            onClick={() => onRelease(item)}
            sx={{ textTransform: "none", fontWeight: 600 }}
          >
            Release
          </Button>
        )}
      </Stack>
    </Box>
  );
}
