import {
  Alert,
  Box,
  Button,
  CircularProgress,
  IconButton,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
  useTheme
} from "@mui/material";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { tableHeadSx } from "../../../../theme";
import { monospaceFontFamily } from "../../../../theme/palette";
import type { PendingWaterPoint } from "../waterSamplingPointPaste";
import { plural } from "../waterConfigTypes";

interface Props {
  rows: PendingWaterPoint[];
  saving: boolean;
  error: string | null;
  onSave: () => void;
  onDiscard: () => void;
  onRemoveRow: (index: number) => void;
}

/**
 * Preview list for accepted pasted water sampling points prior to batch save.
 * Nothing is created until Save; server row errors are displayed against each row.
 */
export function WaterLocationPendingList({ rows, saving, error, onSave, onDiscard, onRemoveRow }: Props) {
  const theme = useTheme();

  if (rows.length === 0) return null;

  return (
    <Paper
      variant="outlined"
      role="region"
      aria-label="Pending sample locations"
      sx={{ mb: 2.5, borderRadius: 2, overflow: "hidden", borderColor: error ? "error.main" : "primary.main" }}
    >
      <Stack
        direction={{ xs: "column", sm: "row" }}
        sx={{
          px: 2.5,
          py: 2,
          borderBottom: "1px solid",
          borderColor: "divider",
          justifyContent: "space-between",
          alignItems: { sm: "center" },
          gap: 1.5
        }}
      >
        <Box>
          <Typography component="h2" sx={{ fontSize: 18, fontWeight: 700 }}>
            Pending sample locations ({rows.length})
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Review the {plural(rows.length, "location")} below. Nothing is saved until you click Save.
          </Typography>
        </Box>
        <Stack direction="row" spacing={1}>
          <Button variant="outlined" onClick={onDiscard} disabled={saving} sx={{ textTransform: "none" }}>
            Discard
          </Button>
          <Button
            variant="contained"
            onClick={onSave}
            disabled={saving}
            startIcon={saving ? <CircularProgress size={16} color="inherit" /> : undefined}
            sx={{ textTransform: "none", fontWeight: 700 }}
          >
            {saving ? "Saving..." : "Save"}
          </Button>
        </Stack>
      </Stack>

      {error && (
        <Alert severity="error" sx={{ mx: 2.5, mt: 2 }}>
          {error}
        </Alert>
      )}

      <TableContainer sx={{ maxHeight: "40vh" }}>
        <Table size="small" stickyHeader aria-label="Pending sample locations">
          <TableHead sx={tableHeadSx}>
            <TableRow>
              <TableCell>Row</TableCell>
              <TableCell>Point code</TableCell>
              <TableCell>Water system</TableCell>
              <TableCell>Point name</TableCell>
              <TableCell>Frequency</TableCell>
              <TableCell>Assigned tests</TableCell>
              <TableCell>Problem</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rows.map((r, i) => {
              const rowProblems = r.errors ? Object.values(r.errors) : [];
              const hasProblem = rowProblems.length > 0;
              return (
                <TableRow
                  key={`${r.code}-${i}`}
                  sx={hasProblem ? { bgcolor: theme.custom.status.detected.bg } : undefined}
                >
                  <TableCell component="th" scope="row" sx={{ fontVariantNumeric: "tabular-nums" }}>
                    {i + 1}
                  </TableCell>
                  <TableCell sx={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>{r.code}</TableCell>
                  <TableCell>{r.waterDepartmentName}</TableCell>
                  <TableCell>{r.location || "—"}</TableCell>
                  <TableCell>{r.testingFrequency || "—"}</TableCell>
                  <TableCell>{r.assignedTestCodes.length > 0 ? r.assignedTestCodes.join(", ") : "—"}</TableCell>
                  <TableCell sx={hasProblem ? { color: theme.custom.status.detected.text, fontWeight: 600 } : undefined}>
                    {hasProblem ? rowProblems.join(" ") : "None"}
                  </TableCell>
                  <TableCell align="right">
                    <Tooltip title="Remove row">
                      <span>
                        <IconButton
                          size="small"
                          aria-label={`Remove row ${i + 1} (${r.code})`}
                          onClick={() => onRemoveRow(i)}
                          disabled={saving}
                        >
                          <DeleteOutlinedIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>
    </Paper>
  );
}
