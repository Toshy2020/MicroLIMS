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
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import { formatLabDateTime } from "../../../utils/formatDate";
import type { AuditLogItem } from "../types/auditTypes";
import { ENTITY_DISPLAY_NAMES } from "../types/auditTypes";
import { AuditDiffViewer } from "./AuditDiffViewer";

interface Props {
  items: AuditLogItem[];
  totalCount: number;
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
  totalCount,
  page,
  rowsPerPage,
  onPageChange,
  onRowsPerPageChange,
  onSelectEvent
}: Props) {
  const paginatedItems = items.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);

  if (items.length === 0) {
    return (
      <Paper sx={{ p: 5, textAlign: "center", border: "1px solid", borderColor: "divider", borderRadius: 2 }}>
        <Typography sx={{ fontSize: 15, fontWeight: 600, color: "text.primary", mb: 0.5 }}>
          No audit events found
        </Typography>
        <Typography sx={{ fontSize: 13, color: "text.secondary" }}>
          Try adjusting your search criteria or date filters.
        </Typography>
      </Paper>
    );
  }

  return (
    <Paper sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2, overflow: "hidden" }}>
      <TableContainer>
        <Table size="small">
          <TableHead sx={{ bgcolor: "background.default" }}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 140 }}>Date / Time</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 180 }}>User</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 150 }}>Entity</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "center", width: 90 }}>Action</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 260 }}>Changed Fields (Diff)</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 140 }}>Record Ref</TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "center", width: 100 }}>Details</TableCell>
            </TableRow>
          </TableHead>

          <TableBody>
            {paginatedItems.map((item) => {
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
                  <TableCell sx={{ fontSize: 11, whiteSpace: "nowrap" }}>
                    <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                      {formatLabDateTime(item.timestamp)}
                    </Typography>
                    <Typography sx={{ fontSize: 10, color: "text.secondary" }}>
                      UTC
                    </Typography>
                  </TableCell>

                  {/* User */}
                  <TableCell sx={{ fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                      {item.userName}
                    </Typography>
                    <Typography sx={{ fontSize: 10, color: "primary.main", fontWeight: 600 }}>
                      {item.userRole ?? "User"}
                    </Typography>
                    <Typography sx={{ fontSize: 10, color: "text.secondary", fontFamily: "monospace" }}>
                      ID: #{item.userId}
                    </Typography>
                  </TableCell>

                  {/* Entity */}
                  <TableCell sx={{ fontSize: 12 }}>
                    <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                      {friendlyEntity}
                    </Typography>
                    <Typography sx={{ fontSize: 11, fontFamily: "monospace", color: "text.secondary" }}>
                      ID: {item.entityId}
                    </Typography>
                  </TableCell>

                  {/* Action */}
                  <TableCell sx={{ textAlign: "center" }}>
                    <Chip
                      label={item.action.toUpperCase()}
                      size="small"
                      color={ACTION_COLORS[item.action] ?? "default"}
                      sx={{ fontSize: 10, fontWeight: 700, height: 20 }}
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
                  <TableCell sx={{ fontSize: 11 }}>
                    {item.sampleReferenceNumber && (
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Sample:</Typography>
                        <Typography sx={{ fontSize: 11, fontWeight: 600, fontFamily: "monospace" }}>
                          {item.sampleReferenceNumber}
                        </Typography>
                      </Box>
                    )}
                    {item.batchNumber && (
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Batch:</Typography>
                        <Typography sx={{ fontSize: 11, fontWeight: 600, fontFamily: "monospace" }}>
                          {item.batchNumber}
                        </Typography>
                      </Box>
                    )}
                    {item.mediaLotNumber && (
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Media Lot:</Typography>
                        <Typography sx={{ fontSize: 11, fontWeight: 600, fontFamily: "monospace" }}>
                          {item.mediaLotNumber}
                        </Typography>
                      </Box>
                    )}
                    {item.cryovialCode && (
                      <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                        <Typography sx={{ fontSize: 10, color: "text.secondary" }}>Cryovial:</Typography>
                        <Typography sx={{ fontSize: 11, fontWeight: 600, fontFamily: "monospace" }}>
                          {item.cryovialCode}
                        </Typography>
                      </Box>
                    )}
                    {!hasRecordRef && (
                      <Typography sx={{ fontSize: 11, color: "text.secondary" }}>—</Typography>
                    )}
                  </TableCell>

                  {/* Actions / Trace */}
                  <TableCell sx={{ textAlign: "center" }} onClick={(e) => e.stopPropagation()}>
                    <Tooltip title="Inspect Event & Traceability Chain">
                      <Button
                        size="small"
                        variant="outlined"
                        startIcon={<TimelineIcon sx={{ fontSize: 14 }} />}
                        onClick={() => onSelectEvent(item)}
                        sx={{
                          textTransform: "none",
                          fontSize: 11,
                          py: 0.3,
                          px: 1,
                          fontWeight: 600
                        }}
                      >
                        Trace
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
        count={totalCount}
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
