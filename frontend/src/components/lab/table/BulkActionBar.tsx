import { useMemo, useState } from "react";
import { Box, Button, Stack, Typography } from "@mui/material";
import { useOptionalAuth } from "../../../contexts/AuthContext";
import type { RowSelection } from "./useRowSelection";

export interface BulkAction<Id> {
  key: string;
  label: string;
  permission?: string;
  run(ids: Id[]): Promise<void> | void;
  eligible?(id: Id): boolean;
  ineligibleReason?: string;
}

export interface BulkActionBarProps<Id> {
  selection: RowSelection<Id>;
  actions: BulkAction<Id>[];
  totalMatching?: number;
  onSelectAllMatching?: () => void;
}

function actionVerb(label: string): string {
  const firstWord = label.trim().split(/\s+/)[0]?.toLowerCase() ?? "processed";
  if (firstWord === "assign") return "assigned";
  if (firstWord === "export") return "exported";
  if (firstWord === "print") return "printed";
  return `${firstWord}ed`;
}

export function BulkActionBar<Id>({
  selection,
  actions,
  totalMatching,
  onSelectAllMatching,
}: BulkActionBarProps<Id>) {
  const auth = useOptionalAuth();
  const [runningKey, setRunningKey] = useState<string | null>(null);

  const permittedActions = useMemo(
    () =>
      actions.filter(
        (a) => !a.permission || (auth?.permissions.includes(a.permission) ?? false)
      ),
    [actions, auth]
  );

  if (selection.count === 0 || permittedActions.length === 0) {
    return null;
  }

  const selectedIds = [...selection.selected];

  const handleRun = async (action: BulkAction<Id>, ids: Id[]) => {
    setRunningKey(action.key);
    try {
      await action.run(ids);
    } finally {
      setRunningKey(null);
    }
  };

  const showSelectAllMatching =
    typeof totalMatching === "number" &&
    totalMatching > selection.count &&
    !selection.allMatching;

  return (
    <Box
      sx={{
        px: 1.5,
        py: 1,
        borderBottom: "1px solid",
        borderColor: "divider",
        bgcolor: "action.selected",
        display: "flex",
        flexWrap: "wrap",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 1.5,
      }}
    >
      <Stack direction="row" spacing={1.5} sx={{ alignItems: "center", flexWrap: "wrap" }}>
        <Typography
          variant="body2"
          sx={{ fontWeight: 600 }}
          role="status"
          aria-live="polite"
        >
          {selection.count.toLocaleString()} selected
        </Typography>
        {showSelectAllMatching && (
          <Button
            size="small"
            variant="text"
            onClick={() => {
              if (onSelectAllMatching) {
                onSelectAllMatching();
              } else {
                selection.selectAllMatching(totalMatching);
              }
            }}
          >
            Select all {totalMatching.toLocaleString()} that match
          </Button>
        )}
        {permittedActions.map((action) => {
          if (!action.eligible || selection.allMatching) return null;
          const eligibleCount = selectedIds.filter((id) =>
            action.eligible ? action.eligible(id) : true
          ).length;
          if (eligibleCount === selectedIds.length) return null;
          return (
            <Typography
              key={`${action.key}-eligibility`}
              variant="caption"
              color="text.secondary"
            >
              {eligibleCount} of {selectedIds.length} can be {actionVerb(action.label)}.
              {action.ineligibleReason ? ` ${action.ineligibleReason}` : ""}
            </Typography>
          );
        })}
      </Stack>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
        {permittedActions.map((action) => {
          const targetIds = action.eligible && !selection.allMatching
            ? selectedIds.filter((id) => (action.eligible ? action.eligible(id) : true))
            : selectedIds;
          const disabled =
            runningKey !== null || (!selection.allMatching && targetIds.length === 0);
          return (
            <Button
              key={action.key}
              size="small"
              variant="outlined"
              disabled={disabled}
              onClick={() => void handleRun(action, targetIds)}
            >
              {action.label}
            </Button>
          );
        })}
        <Button
          size="small"
          variant="text"
          disabled={runningKey !== null}
          onClick={selection.clear}
        >
          Clear
        </Button>
      </Stack>
    </Box>
  );
}
