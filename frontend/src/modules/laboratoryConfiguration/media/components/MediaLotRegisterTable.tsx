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
  Tooltip
} from "@mui/material";
import HistoryOutlinedIcon from "@mui/icons-material/HistoryOutlined";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import { StatusBadge } from "../../../../components/StatusBadge";
import { mediaClassLabel } from "../../../../services/masterDataOptions";
import { lifecycleOf } from "./MediaLotKpiCards";
import { brandColors } from "../../../../theme";
import { useAuth } from "../../../../contexts/AuthContext";

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
            <TableRow sx={{ "& th": { bgcolor: "#f9fafb", fontWeight: 700, fontSize: 11, py: 1 } }}>
              <TableCell>Lot Number</TableCell>
              <TableCell>Type</TableCell>
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
                    bgcolor: isSelected ? "#faf5ff" : "inherit",
                    borderLeft: isSelected
                      ? `4px solid ${brandColors.sectionTitle}`
                      : "4px solid transparent",
                    "&:hover": { bgcolor: isSelected ? "#faf5ff" : "#fdfbfe" }
                  }}
                >
                  <TableCell sx={{ py: 1.25 }}>
                    <Typography sx={{ fontWeight: isSelected ? 700 : 600, fontSize: 13, color: isSelected ? brandColors.pageTitle : "#111827" }}>
                      {lot.lotNumber}
                    </Typography>
                    <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                      Prep: {new Date(lot.preparedAt).toLocaleDateString()}
                    </Typography>
                  </TableCell>

                  <TableCell sx={{ py: 1.25, fontSize: 12 }}>
                    {mediaClassLabel(lot.mediaType?.class)}
                  </TableCell>

                  <TableCell sx={{ py: 1.25 }}>
                    <StatusBadge status={lifecycle} />
                  </TableCell>
                </TableRow>
              );
            })}

            {lots.length === 0 && (
              <TableRow>
                <TableCell colSpan={3} align="center" sx={{ py: 3, color: "#9ca3af", fontSize: 12 }}>
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
          sx={{ borderTop: "1px solid #f0f0f0", ".MuiTablePagination-toolbar": { minHeight: 40 } }}
        />
      </Box>
    );
  }

  return (
    <Box>
      <Table size="small">
        <TableHead>
          <TableRow sx={{ bgcolor: "#fafafa" }}>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Lot Number</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Media Type</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Dehydrated Material</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Prepared On</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Expiry Date</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12 }}>Status</TableCell>
            <TableCell sx={{ fontWeight: 700, fontSize: 12, textAlign: "right" }}>Actions</TableCell>
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
                  bgcolor: isSelected ? "#faf5ff" : "inherit",
                  borderLeft: isSelected
                    ? `4px solid ${brandColors.sectionTitle}`
                    : "4px solid transparent",
                  "&:hover": { bgcolor: isSelected ? "#faf5ff" : "#fdfbfe" }
                }}
              >
                <TableCell sx={{ py: 1.5 }}>
                  <Typography sx={{ fontWeight: 700, fontSize: 13, color: brandColors.pageTitle }}>
                    {lot.lotNumber}
                  </Typography>
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                    ID #{lot.id}
                  </Typography>
                </TableCell>

                <TableCell sx={{ fontSize: 12, fontWeight: 500 }}>
                  {mediaClassLabel(lot.mediaType?.class)}
                </TableCell>

                <TableCell sx={{ fontSize: 12 }}>
                  {lot.material ? (
                    <>
                      <Typography sx={{ fontSize: 12, fontWeight: 600 }}>{lot.material.materialName}</Typography>
                      <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                        Batch: {lot.material.batchNumber}
                      </Typography>
                    </>
                  ) : (
                    "—"
                  )}
                </TableCell>

                <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>
                  {new Date(lot.preparedAt).toLocaleDateString()}
                </TableCell>

                <TableCell sx={{ fontSize: 12, whiteSpace: "nowrap" }}>
                  {new Date(lot.expiryDate).toLocaleDateString()}
                </TableCell>

                <TableCell>
                  <StatusBadge status={lifecycle} />
                </TableCell>

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
                      <IconButton size="small" onClick={() => onViewRecord(lot.id)}>
                        <DescriptionOutlinedIcon fontSize="small" sx={{ color: "#4b5563" }} />
                      </IconButton>
                    </Tooltip>

                    <Tooltip title="Audit Trail">
                      <IconButton size="small" onClick={() => onViewAuditHistory(lot.id)}>
                        <HistoryOutlinedIcon fontSize="small" sx={{ color: "#4b5563" }} />
                      </IconButton>
                    </Tooltip>
                  </Box>
                </TableCell>
              </TableRow>
            );
          })}

          {lots.length === 0 && (
            <TableRow>
              <TableCell colSpan={7} align="center" sx={{ py: 4, color: "#9ca3af", fontSize: 13 }}>
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
