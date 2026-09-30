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
import { HplcStatusBadge } from "../components/HplcStatusBadge";
import { tableHeadSx } from "../../../theme";
import type { HplcRunListItem } from "../types";

export interface WorkspaceHistoryTabProps {
  historyRuns: HplcRunListItem[];
  equipmentId: number;
  onSelectRun: (runId: number) => void;
}

export function WorkspaceHistoryTab({
  historyRuns,
  onSelectRun
}: WorkspaceHistoryTabProps) {
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
          Instrument Run History
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
              <TableCell>Status</TableCell>
              <TableCell>SST Status</TableCell>
              <TableCell align="right">Action</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {historyRuns.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} sx={{ textAlign: "center", py: 3, color: "text.secondary" }}>
                  No past runs recorded for this instrument.
                </TableCell>
              </TableRow>
            ) : (
              historyRuns.map((hr) => (
                <TableRow key={hr.id}>
                  <TableCell sx={{ fontWeight: 600 }}>{hr.code}</TableCell>
                  <TableCell>{hr.methodAbbreviation}</TableCell>
                  <TableCell>{hr.analystUserName}</TableCell>
                  <TableCell>{new Date(hr.startedAt).toLocaleString()}</TableCell>
                  <TableCell><HplcStatusBadge status={hr.status} /></TableCell>
                  <TableCell><HplcStatusBadge status={hr.sstStatus} /></TableCell>
                  <TableCell align="right">
                    <Button
                      size="small"
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
