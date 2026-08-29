import React, { useState } from "react";
import {
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Typography,
  TablePagination,
  Box,
  Button,
  IconButton,
  Tooltip,
  useTheme
} from "@mui/material";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import { Link } from "react-router-dom";
import { StatusBadge } from "../../../../components/StatusBadge";
import { lifecycleOf } from "./MediaLotKpiCards";
import { brandColors } from "../../../../theme";
import { useAuth } from "../../../../contexts/AuthContext";

function formatDateDDMMYY(value: string | number | Date | null | undefined): string {
  if (!value) return "—";
  const d = new Date(value);
  if (isNaN(d.getTime())) return "—";
  const day = String(d.getDate()).padStart(2, "0");
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const year = String(d.getFullYear()).slice(-2);
  return `${day}/${month}/${year}`;
}

interface Props {
  lots: any[];
  awaitingApprovalIds: Set<number>;
  selectedLotId?: number | null;
  onSelectLot: (lot: any) => void;
  isCompact?: boolean;
  onViewRecord: (lotId: number) => void;
  onViewAuditHistory: (lotId: number) => void;
  onRequestReleaseDecision?: (lot: any, approved: boolean) => void;
}

export function MediaLotRegisterTable({
  lots,
  awaitingApprovalIds,
  selectedLotId,
  onSelectLot,
  isCompact,
  onViewRecord,
  onViewAuditHistory,
  onRequestReleaseDecision
}: Props) {
  const { role } = useAuth();
  const theme = useTheme();
  const canRelease = role === "SectionHead" || role === "SystemAdministrator";

  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(isCompact ? 10 : 25);

  const handleChangePage = (_: unknown, newPage: number) => {
    setPage(newPage);
  };

  const handleChangeRowsPerPage = (event: React.ChangeEvent<HTMLInputElement>) => {
    setRowsPerPage(parseInt(event.target.value, 10));
    setPage(0);
  };

  const paginatedLots = lots.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);

  if (isCompact) {
    return (
      <Box>
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow sx={{ "& th": { bgcolor: "background.default", fontWeight: 700, fontSize: 11, py: 1 } }}>
              <TableCell sx={{ width: 95 }}>Prepared On</TableCell>
              <TableCell>Material / Lot</TableCell>
              <TableCell sx={{ width: 95 }}>Status</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {paginatedLots.map((lot) => {
              const isSelected = selectedLotId === lot.id;
              const lifecycle = lifecycleOf(lot, awaitingApprovalIds);

              return (
                <TableRow
                  key={lot.id}
                  hover
                  onClick={() => onSelectLot(lot)}
                  sx={{
                    cursor: "pointer",
                    bgcolor: isSelected ? theme.custom.status.purple.bg : "inherit",
                    borderLeft: isSelected
                      ? `4px solid ${brandColors.sectionTitle}`
                      : "4px solid transparent",
                    "&:hover": { bgcolor: isSelected ? theme.custom.status.purple.bg : "background.default" }
                  }}
                >
                  <TableCell sx={{ py: 1.25, fontSize: 11.5, color: "text.secondary", whiteSpace: "nowrap" }}>
                    {formatDateDDMMYY(lot.preparedAt)}
                  </TableCell>

                  <TableCell sx={{ py: 1.25 }}>
                    <Typography sx={{ fontWeight: isSelected ? 700 : 600, fontSize: 12.5, color: isSelected ? brandColors.pageTitle : "text.primary" }}>
                      {lot.material?.materialName || "Dehydrated Material"}
                    </Typography>
                    <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                      Lot: {lot.lotNumber}
                    </Typography>
                  </TableCell>

                  <TableCell sx={{ py: 1.25 }}>
                    <StatusBadge status={lifecycle} />
                  </TableCell>
                </TableRow>
              );
            })}

            {lots.length === 0 && (
              <TableRow>
                <TableCell colSpan={3} align="center" sx={{ py: 3, color: "text.secondary", fontSize: 12 }}>
                  No media lots found.
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>

        <TablePagination
          component="div"
          count={lots.length}
          page={page}
          onPageChange={handleChangePage}
          rowsPerPage={rowsPerPage}
          onRowsPerPageChange={handleChangeRowsPerPage}
          rowsPerPageOptions={[10, 25, 50]}
          sx={{ borderTop: "1px solid", borderColor: "divider", ".MuiTablePagination-toolbar": { minHeight: 40 } }}
        />
      </Box>
    );
  }

  return (
    <Box>
      <Table size="small">
        <TableHead>
          <TableRow sx={{ bgcolor: "background.default" }}>
            <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 120 }}>Prepared On</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 180 }}>Dehydrated Material</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 150 }}>Lot Number</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12, minWidth: 120 }}>Expiry Date</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12, width: 140 }}>Status</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right", width: 130 }}>Actions</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {paginatedLots.map((lot) => {
            const isSelected = selectedLotId === lot.id;
            const lifecycle = lifecycleOf(lot, awaitingApprovalIds);

            return (
              <TableRow
                key={lot.id}
                hover
                onClick={() => onSelectLot(lot)}
                sx={{
                  cursor: "pointer",
                  bgcolor: isSelected ? theme.custom.status.purple.bg : "inherit",
                  borderLeft: isSelected
                    ? `4px solid ${brandColors.sectionTitle}`
                    : "4px solid transparent",
                  "&:hover": { bgcolor: isSelected ? theme.custom.status.purple.bg : "background.default" }
                }}
              >
                {/* 1. Prepared On */}
                <TableCell sx={{ fontSize: 12, color: "text.secondary", whiteSpace: "nowrap" }}>
                  {formatDateDDMMYY(lot.preparedAt)}
                </TableCell>

                {/* 2. Dehydrated Material */}
                <TableCell sx={{ fontSize: 12 }}>
                  {lot.material ? (
                    <>
                      <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                        {lot.material.materialName}
                      </Typography>
                      {lot.material.batchNumber && (
                        <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                          Batch: {lot.material.batchNumber}
                        </Typography>
                      )}
                    </>
                  ) : (
                    "—"
                  )}
                </TableCell>

                {/* 3. Lot Number */}
                <TableCell sx={{ py: 1.5 }}>
                  <Typography sx={{ fontWeight: 700, fontSize: 13, color: isSelected ? brandColors.pageTitle : "text.primary" }}>
                    {lot.lotNumber}
                  </Typography>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                    ID #{lot.id}
                  </Typography>
                </TableCell>

                {/* 4. Expiry Date */}
                <TableCell sx={{ fontSize: 12, color: "text.secondary", whiteSpace: "nowrap" }}>
                  {formatDateDDMMYY(lot.expiryDate)}
                </TableCell>

                {/* 5. Status */}
                <TableCell>
                  <StatusBadge status={lifecycle} />
                </TableCell>

                {/* 6. Actions */}
                <TableCell sx={{ textAlign: "right" }} onClick={(e) => e.stopPropagation()}>
                  <Box sx={{ display: "flex", justifyContent: "flex-end", alignItems: "center", gap: 0.5 }}>
                    {lifecycle === "Awaiting Approval" && canRelease && onRequestReleaseDecision && (
                      <>
                        <Button
                          size="small"
                          color="success"
                          variant="outlined"
                          onClick={() => onRequestReleaseDecision(lot, true)}
                          sx={{ fontSize: 11, py: 0.25, px: 1, minWidth: "auto", fontWeight: 700 }}
                        >
                          Release
                        </Button>
                        <Button
                          size="small"
                          color="error"
                          variant="outlined"
                          onClick={() => onRequestReleaseDecision(lot, false)}
                          sx={{ fontSize: 11, py: 0.25, px: 1, minWidth: "auto", fontWeight: 700 }}
                        >
                          Reject
                        </Button>
                      </>
                    )}

                    <Tooltip title="View Lot Record (Printable)">
                      <IconButton
                        component={Link}
                        to={`/media/${lot.id}/report`}
                        target="_blank"
                        rel="noopener"
                        size="small"
                      >
                        <DescriptionOutlinedIcon fontSize="small" sx={{ color: "text.secondary" }} />
                      </IconButton>
                    </Tooltip>

                    <Tooltip title="Audit Trail">
                      <IconButton size="small" onClick={() => onViewAuditHistory(lot.id)}>
                        <HistoryOutlinedIcon fontSize="small" sx={{ color: "text.secondary" }} />
                      </IconButton>
                    </Tooltip>
                  </Box>
                </TableCell>
              </TableRow>
            );
          })}

          {lots.length === 0 && (
            <TableRow>
              <TableCell colSpan={6} align="center" sx={{ py: 4, color: "text.secondary", fontSize: 13 }}>
                No media lots match this filter.
              </TableCell>
            </TableRow>
          )}
        </TableBody>
      </Table>

      <TablePagination
        component="div"
        count={lots.length}
        page={page}
        onPageChange={handleChangePage}
        rowsPerPage={rowsPerPage}
        onRowsPerPageChange={handleChangeRowsPerPage}
        rowsPerPageOptions={[10, 25, 50]}
      />
    </Box>
  );
}
