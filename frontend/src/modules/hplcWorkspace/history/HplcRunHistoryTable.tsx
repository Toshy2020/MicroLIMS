import {
  Box,
  Typography,
  Button,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  useTheme
} from "@mui/material";
import VisibilityIcon from "@mui/icons-material/Visibility";
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { tableHeadSx } from "../../../theme";
import type { HplcRunListItem } from "../types";

export interface HplcRunHistoryTableProps {
  historyRuns: HplcRunListItem[];
  equipmentId: number;
  onSelectRun: (runId: number) => void;
}

export function HplcRunHistoryTable({
  historyRuns,
  onSelectRun
}: HplcRunHistoryTableProps) {
  const theme = useTheme();

  return (
    <Paper
      elevation={0}
      sx={{
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        overflow: "hidden"
      }}
    >
      <Box sx={{ p: 2, borderBottom: `1px solid ${theme.palette.divider}` }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
          Instrument Run History ({historyRuns.length})
        </Typography>
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Historical record of completed, abandoned, and past HPLC runs on this instrument.
        </Typography>
      </Box>
      <TableContainer>
        <Table size="small">
          <TableHead sx={tableHeadSx(theme)}>
            <TableRow>
              <TableCell>Run Code</TableCell>
              <TableCell>Method</TableCell>
              <TableCell>Analyst</TableCell>
              <TableCell>Started At</TableCell>
              <TableCell>Closed At</TableCell>
              <TableCell>Run Status</TableCell>
              <TableCell>SST Status</TableCell>
              <TableCell align="right">Action</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {historyRuns.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8} sx={{ textAlign: "center", py: 4, color: "text.secondary" }}>
                  No past runs recorded for this instrument.
                </TableCell>
              </TableRow>
            ) : (
              historyRuns.map((hr) => (
                <TableRow key={hr.id} hover>
                  <TableCell sx={{ fontWeight: 600 }}>{hr.code}</TableCell>
                  <TableCell>{hr.methodAbbreviation}</TableCell>
                  <TableCell>{hr.analystUserName}</TableCell>
                  <TableCell>{new Date(hr.startedAt).toLocaleString()}</TableCell>
                  <TableCell>{hr.closedAt ? new Date(hr.closedAt).toLocaleString() : "—"}</TableCell>
                  <TableCell>
                    <HplcStatusBadge status={hr.status} />
                  </TableCell>
                  <TableCell>
                    <HplcStatusBadge status={hr.sstStatus} />
                  </TableCell>
                  <TableCell align="right">
                    <Button
                      size="small"
                      variant="outlined"
                      startIcon={<VisibilityIcon fontSize="small" />}
                      onClick={() => onSelectRun(hr.id)}
                      sx={{ textTransform: "none" }}
                    >
                      View
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Paper>
  );
}
