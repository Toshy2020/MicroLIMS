import {
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  TableContainer,
  Paper,
  Box,
  useTheme
} from "@mui/material";
import { tableHeadSx } from "../../../theme";
import { PreparationStatusBadge } from "../components/PreparationStatusBadge";
import { formatLabDateTime } from "../../../utils/formatDate";
import type { SolutionPreparationStatusHistoryResponse } from "../types";

interface Props {
  history: SolutionPreparationStatusHistoryResponse[];
}

export function PreparationHistoryTable({ history }: Props) {
  const theme = useTheme();

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table size="small">
        <TableHead sx={tableHeadSx(theme)}>
          <TableRow>
            <TableCell sx={{ fontWeight: 700 }}>Transition</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Changed By</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Changed At</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Reason / Note</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {history.map((h, i) => (
            <TableRow key={i}>
              <TableCell>
                <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                  {h.fromStatus && <PreparationStatusBadge status={h.fromStatus} />}
                  {h.fromStatus && <span>→</span>}
                  <PreparationStatusBadge status={h.toStatus} />
                </Box>
              </TableCell>
              <TableCell>{h.changedByUserName || "Automatic System"}</TableCell>
              <TableCell>{formatLabDateTime(h.changedAt)}</TableCell>
              <TableCell>{h.reason || "—"}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
