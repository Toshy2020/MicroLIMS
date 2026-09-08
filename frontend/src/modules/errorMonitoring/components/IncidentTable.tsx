import { Fragment } from "react";
import {
  Box, Chip, IconButton, Paper, Stack, Table, TableBody, TableCell,
  TableContainer, TableHead, TablePagination, TableRow, Typography
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowRightIcon from "@mui/icons-material/KeyboardArrowRight";
import { formatLabDateTime } from "../../../utils/formatDate";
import { tableHeadSx } from "../../../theme";
import type { IncidentListItem } from "../types/errorMonitoringTypes";
import { SEVERITY_COLORS } from "../types/errorMonitoringTypes";
import { IncidentRowDetail } from "./IncidentRowDetail";

interface Props {
  items: IncidentListItem[];
  totalCount: number;
  page: number;
  rowsPerPage: number;
  expandedId: string | null;
  onToggleExpand: (id: string) => void;
  onPageChange: (page: number) => void;
  onRowsPerPageChange: (rowsPerPage: number) => void;
  onChanged: () => void;
}

export function IncidentTable({
  items, totalCount, page, rowsPerPage, expandedId,
  onToggleExpand, onPageChange, onRowsPerPageChange, onChanged
}: Props) {
  return (
    <Paper>
      <TableContainer sx={{ overflowX: "auto" }}>
        <Table size="small">
          <TableHead sx={tableHeadSx}>
            <TableRow>
              <TableCell sx={{ width: 48 }} />
              <TableCell>Severity</TableCell>
              <TableCell>Summary</TableCell>
              <TableCell>Sources</TableCell>
              <TableCell align="right">Occurrences</TableCell>
              <TableCell>First seen</TableCell>
              <TableCell>Last seen</TableCell>
              <TableCell>Status</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.length === 0 && (
              <TableRow>
                <TableCell colSpan={8}>
                  <Typography sx={{ py: 3, textAlign: "center" }} color="text.secondary">
                    No incidents match these filters.
                  </Typography>
                </TableCell>
              </TableRow>
            )}

            {items.map((incident) => {
              const expanded = expandedId === incident.id;
              return (
                <Fragment key={incident.id}>
                  <TableRow hover sx={{ cursor: "pointer" }} onClick={() => onToggleExpand(incident.id)}>
                    <TableCell>
                      <IconButton size="small" aria-label={expanded ? "Collapse" : "Expand"}>
                        {expanded ? <KeyboardArrowDownIcon fontSize="small" /> : <KeyboardArrowRightIcon fontSize="small" />}
                      </IconButton>
                    </TableCell>
                    <TableCell>
                      <Chip size="small" color={SEVERITY_COLORS[incident.severity]} label={incident.severity} />
                    </TableCell>
                    <TableCell sx={{ maxWidth: 460 }}>
                      <Typography variant="body2" noWrap title={incident.summary}>
                        {incident.summary}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={0.5}>
                        {incident.sources.map((s) => <Chip key={s} size="small" variant="outlined" label={s} />)}
                      </Stack>
                    </TableCell>
                    <TableCell align="right">{incident.occurrenceCount}</TableCell>
                    <TableCell>{formatLabDateTime(incident.firstSeenUtc)}</TableCell>
                    <TableCell>{formatLabDateTime(incident.lastSeenUtc)}</TableCell>
                    <TableCell>
                      <Chip
                        size="small"
                        variant={incident.status === "Open" ? "filled" : "outlined"}
                        color={incident.status === "Open" ? "warning" : "success"}
                        label={incident.status}
                      />
                    </TableCell>
                  </TableRow>

                  {expanded && (
                    <TableRow>
                      <TableCell colSpan={8} sx={{ p: 0, borderBottom: 0 }}>
                        <Box><IncidentRowDetail incident={incident} onChanged={onChanged} /></Box>
                      </TableCell>
                    </TableRow>
                  )}
                </Fragment>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>

      <TablePagination
        component="div"
        count={totalCount}
        page={page}
        rowsPerPage={rowsPerPage}
        rowsPerPageOptions={[25, 50, 100]}
        onPageChange={(_, newPage) => onPageChange(newPage)}
        onRowsPerPageChange={(e) => onRowsPerPageChange(parseInt(e.target.value, 10))}
      />
    </Paper>
  );
}
