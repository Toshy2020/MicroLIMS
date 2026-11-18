import {
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TableContainer,
  TablePagination,
  Box,
  Typography,
  Chip,
  Button,
  Tooltip
} from "@mui/material";
import TimelineIcon from "@mui/icons-material/Timeline";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import type { AuditLogItem } from "../types/auditTypes";
import { ENTITY_DISPLAY_NAMES } from "../types/auditTypes";
import { AuditDiffViewer } from "./AuditDiffViewer";
import { tableHeadSx } from "../../../theme";
import { useTranslation } from "../../../i18n";

interface Props {
  items: AuditLogItem[];
  // Server-paged (keyset): `items` is exactly the current page; there is no total, only "has more".
  hasMore: boolean;
  page: number;
  rowsPerPage: number;
  onPageChange: (newPage: number) => void;
  onRowsPerPageChange: (newRowsPerPage: number) => void;
  onSelectEvent: (item: AuditLogItem) => void;
}

const ACTION_COLORS: Record<string, "success" | "warning" | "error" | "default"> = {
  Create: "success",
  Update: "warning",
  Delete: "error"
};

export function AuditResultsTable({
  items,
  hasMore,
  page,
  rowsPerPage,
  onPageChange,
  onRowsPerPageChange,
  onSelectEvent
}: Props) {
  const { t } = useTranslation("auditSearch");
  if (items.length === 0) {
    return (
      <Paper sx={{ p: 5, textAlign: "center", border: "1px solid", borderColor: "divider", borderRadius: 2 }}>
        <Typography sx={{ fontSize: 15, fontWeight: 600, color: "text.primary", mb: 0.5 }}>
          {t("results.noEvents")}
        </Typography>
        <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
          {t("results.adjust")}
        </Typography>
      </Paper>
    );
  }

  return (
    <Paper sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
      <TableContainer sx={{ maxHeight: { md: "calc(100vh - 240px)" } }}>
        <Table stickyHeader size="small">
          <TableHead sx={tableHeadSx}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 140 }}>{t("results.dateTime")}</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 180 }}>{t("results.user")}</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 150 }}>{t("results.entity")}</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "center", width: 90 }}>{t("results.action")}</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 260 }}>{t("results.changedFields")}</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 140 }}>{t("results.recordRef")}</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "center", width: 100 }}>{t("results.details")}</TableCell>
            </TableRow>
          </TableHead>

          <TableBody>
            {items.map((item) => {
              const friendlyEntity = ENTITY_DISPLAY_NAMES[item.entityName] ?? item.entityName;
              const hasRecordRef = item.sampleReferenceNumber || item.batchNumber || item.controlNumber || item.mediaLotNumber || item.cryovialCode;

              return (
                <TableRow
                  key={item.id}
                  hover
                  onClick={() => onSelectEvent(item)}
                  sx={{
                    cursor: "pointer",
                    "&:last-child td, &:last-child th": { border: 0 }
                  }}
                >
                  {/* Timestamp */}
                  <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>
                    <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                      {formatDateTimeWithTimeZone(item.timestamp)}
                    </Typography>
                    <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
                      UTC
                    </Typography>
                  </TableCell>

                  {/* User */}
                  <TableCell sx={{ fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                      {item.userName}
                    </Typography>
                    <Typography sx={{ fontSize: 12, color: "primary.main", fontWeight: 600 }}>
                      {item.userCurrentRole ? t("results.currentRole", { role: item.userCurrentRole }) : t("results.userFallback")}
                    </Typography>
                    <Typography sx={{ fontSize: 12, color: "text.secondary", fontFamily: "monospace" }}>
                      {t("results.userIdLine", { id: item.userId })}
                    </Typography>
                  </TableCell>

                  {/* Entity */}
                  <TableCell sx={{ fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                      {friendlyEntity}
                    </Typography>
                    <Typography sx={{ fontSize: 12, fontFamily: "monospace", color: "text.secondary" }}>
                      {t("results.entityIdLine", { id: item.entityId })}
                    </Typography>
                    {item.entityIdNegative && (
                      <Tooltip
                        title={
                          item.correctedByAuditLogId
                            ? t("results.correctedTip", { id: item.correctedByAuditLogId })
                            : t("results.needsCorrectionTip")
                        }
                      >
                        <Chip
                          label={item.correctedByAuditLogId ? t("results.corrected", { id: item.correctedByAuditLogId }) : t("results.needsCorrection")}
                          size="small"
                          color={item.correctedByAuditLogId ? "info" : "warning"}
                          sx={{ fontSize: 11, fontWeight: 700, height: 18, mt: 0.5 }}
                        />
                      </Tooltip>
                    )}
                  </TableCell>

                  {/* Action */}
                  <TableCell sx={{ textAlign: "center" }}>
                    <Chip
                      label={item.action.toUpperCase()}
                      size="small"
                      color={ACTION_COLORS[item.action] ?? "default"}
                      sx={{ fontSize: 12, fontWeight: 700, height: 20 }}
                    />
                  </TableCell>

                  {/* Inline Diff */}
                  <TableCell sx={{ fontSize: 12 }}>
                    <AuditDiffViewer
                      action={item.action}
                      previousValue={item.previousValue}
                      newValue={item.newValue}
                      entityName={item.entityName}
                      compact={true}
                      onViewAll={() => onSelectEvent(item)}
                    />
                  </TableCell>

                  {/* Record Ref */}
                  <TableCell sx={{ fontSize: 12 }}>
                    {item.sampleReferenceNumber && (
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("results.sample")}</Typography>
                        <Typography sx={{ fontSize: 12, fontWeight: 600, fontFamily: "monospace" }}>
                          {item.sampleReferenceNumber}
                        </Typography>
                      </Box>
                    )}
                    {item.batchNumber && (
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("results.batch")}</Typography>
                        <Typography sx={{ fontSize: 12, fontWeight: 600, fontFamily: "monospace" }}>
                          {item.batchNumber}
                        </Typography>
                      </Box>
                    )}
                    {item.mediaLotNumber && (
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("results.mediaLot")}</Typography>
                        <Typography sx={{ fontSize: 12, fontWeight: 600, fontFamily: "monospace" }}>
                          {item.mediaLotNumber}
                        </Typography>
                      </Box>
                    )}
                    {item.cryovialCode && (
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("results.cryovial")}</Typography>
                        <Typography sx={{ fontSize: 12, fontWeight: 600, fontFamily: "monospace" }}>
                          {item.cryovialCode}
                        </Typography>
                      </Box>
                    )}
                    {!hasRecordRef && (
                      <Typography sx={{ fontSize: 12, color: "text.secondary" }}>—</Typography>
                    )}
                  </TableCell>

                  {/* Actions / Trace */}
                  <TableCell sx={{ textAlign: "center" }} onClick={(e) => e.stopPropagation()}>
                    <Tooltip title={t("results.inspectTip")}>
                      <Button
                        size="small"
                        variant="outlined"
                        startIcon={<TimelineIcon sx={{ fontSize: 14 }} />}
                        onClick={() => onSelectEvent(item)}
                        sx={{
                          textTransform: "none",
                          fontSize: 12,
                          py: 0.3,
                          px: 1,
                          fontWeight: 600
                        }}
                      >
                        {t("results.trace")}
                      </Button>
                    </Tooltip>
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </TableContainer>

      <TablePagination
        rowsPerPageOptions={[10, 25, 50, 100]}
        component="div"
        count={-1}
        labelDisplayedRows={({ from, to }) => `${from}-${to}`}
        slotProps={{ actions: { nextButton: { disabled: !hasMore } } }}
        rowsPerPage={rowsPerPage}
        page={page}
        onPageChange={(_, newPage) => onPageChange(newPage)}
        onRowsPerPageChange={(e) => {
          onRowsPerPageChange(parseInt(e.target.value, 10));
        }}
        sx={{ borderTop: "1px solid", borderColor: "divider" }}
      />
    </Paper>
  );
}
