import { Box, Button, IconButton, Paper, Stack, Tooltip, Typography } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import { LimitPills, ToneChip } from "../../../../components/configHierarchy";
import { EmRoom, RoomTestConfig, emTestTypeLabel } from "../emConfigTypes";
import { GradeChip } from "./GradeChip";

interface Props {
  room: EmRoom;
  configs: RoomTestConfig[];
  onEditRoom: () => void;
  onDeleteRoom: () => void;
  onAddConfig: () => void;
  onEditConfig: (c: RoomTestConfig) => void;
  onDeleteConfig: (c: RoomTestConfig) => void;
}

// One room: its grade, and every test configured for it with limits.
// A room with no tests is highlighted, since samples from it would have
// nothing to run.
export function EmRoomCard({ room, configs, onEditRoom, onDeleteRoom, onAddConfig, onEditConfig, onDeleteConfig }: Props) {
  const empty = configs.length === 0;
  return (
    <Paper variant="outlined" sx={{ borderRadius: 2, overflow: "hidden", display: "flex", flexDirection: "column", borderColor: empty ? "warning.light" : "divider" }}>
      <Stack direction="row" sx={{ p: 2, alignItems: "flex-start", gap: 1 }}>
        <Box sx={{ flexGrow: 1, minWidth: 0 }}>
          <Typography component="h3" sx={{ fontSize: 16, fontWeight: 600, overflowWrap: "anywhere" }}>{room.name}</Typography>
          <Stack direction="row" spacing={0.75} sx={{ mt: 0.75, alignItems: "center", flexWrap: "wrap" }}>
            <GradeChip grade={room.gradeClassification} />
            {empty ? (
              <ToneChip label="No tests configured" tone="inconclusive" />
            ) : (
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                {configs.length} {configs.length === 1 ? "test" : "tests"}
              </Typography>
            )}
          </Stack>
        </Box>
        <Tooltip title="Edit room">
          <IconButton aria-label={`Edit ${room.name}`} onClick={onEditRoom}>
            <EditIcon fontSize="small" />
          </IconButton>
        </Tooltip>
        <Tooltip title="Delete room">
          <IconButton aria-label={`Delete ${room.name}`} color="error" onClick={onDeleteRoom}>
            <DeleteIcon fontSize="small" />
          </IconButton>
        </Tooltip>
      </Stack>

      <Box sx={{ px: 2, py: 1.5, borderTop: "1px solid", borderColor: "divider", bgcolor: "background.default", flexGrow: 1, display: "flex", flexDirection: "column", gap: 1 }}>
        {empty ? (
          <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
            Samples received for this room will have no tests to run until one is added.
          </Typography>
        ) : (
          configs.map((c) => (
            <Stack key={c.id} spacing={0.75} sx={{ p: 1.25, borderRadius: 1, bgcolor: "background.paper", border: "1px solid", borderColor: "divider" }}>
              <Stack direction="row" sx={{ alignItems: "center", gap: 1 }}>
                <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                  <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{emTestTypeLabel(c.testType)}</Typography>
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{c.testCode}</Typography>
                </Box>
                <Tooltip title="Edit test">
                  <IconButton size="small" aria-label={`Edit ${emTestTypeLabel(c.testType)} ${c.testCode}`} onClick={() => onEditConfig(c)}>
                    <EditIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
                <Tooltip title="Delete test">
                  <IconButton size="small" color="error" aria-label={`Delete ${emTestTypeLabel(c.testType)} ${c.testCode}`} onClick={() => onDeleteConfig(c)}>
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Tooltip>
              </Stack>
              <LimitPills {...c} />
            </Stack>
          ))
        )}
        <Button
          size="small"
          variant={empty ? "contained" : "outlined"}
          startIcon={<AddIcon />}
          onClick={onAddConfig}
          aria-label={`Add test to ${room.name}`}
          sx={{ alignSelf: "flex-start", textTransform: "none", fontWeight: 600, borderStyle: empty ? undefined : "dashed" }}
        >
          {empty ? "Add first test" : "Add test"}
        </Button>
      </Box>
    </Paper>
  );
}
