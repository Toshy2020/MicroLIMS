import { Fragment } from "react";
import {
  Box, Chip, IconButton, Paper, Stack, Table, TableBody, TableCell,
  TableContainer, TableHead, TablePagination, TableRow, Typography
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowRightIcon from "@mui/icons-material/KeyboardArrowRight";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { tableHeadSx } from "../../../theme";
import { useTranslation } from "../../../i18n";
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
  const { t } = useTranslation("errorMonitoring");
  return (
    <Paper>
      <TableContainer sx={{ maxHeight: { md: "calc(100vh - 240px)" }, overflowX: "auto" }}>
        <Table stickyHeader size="small">
          <TableHead sx={tableHeadSx}>
            <TableRow>
              <TableCell sx={{ width: 48 }} />
              <TableCell>{t("table.severity")}</TableCell>
              <TableCell>{t("table.summary")}</TableCell>
              <TableCell>{t("table.sources")}</TableCell>
              <TableCell align="right">{t("table.occurrences")}</TableCell>
              <TableCell>{t("table.firstSeen")}</TableCell>
              <TableCell>{t("table.lastSeen")}</TableCell>
              <TableCell>{t("table.status")}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.length === 0 && (
              <TableRow>
                <TableCell colSpan={8}>
                  <Typography
                    sx={{
                      color: "text.secondary",
                      py: 3,
                      textAlign: "center"
                    }}>
                    {t("table.empty")}
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
                      <IconButton size="small" aria-label={expanded ? t("table.collapse") : t("table.expand")}>
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
                    <TableCell>{formatDateTimeWithTimeZone(incident.firstSeenUtc)}</TableCell>
                    <TableCell>{formatDateTimeWithTimeZone(incident.lastSeenUtc)}</TableCell>
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
