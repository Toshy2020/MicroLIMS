import React from "react";
import {
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TableContainer,
  TablePagination,
  Paper,
  Box,
  Button,
  Typography,
  Tooltip,
  useTheme
} from "@mui/material";
import AcUnitIcon from "@mui/icons-material/AcUnit";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import CheckCircleOutlineIcon from "@mui/icons-material/CheckCircleOutline";
import HighlightOffIcon from "@mui/icons-material/HighlightOff";
import { Link } from "react-router-dom";
import { StatusBadge } from "../../../../components/StatusBadge";
import { formatLabDate } from "../../../../utils/formatDate";
import { CryovialItem } from "../types/cryovialTypes";
import { isMaterialExpiringSoon } from "../../../inventory/materials/components/MaterialKpiCards";
import { brandColors } from "../../../../theme";

interface CryovialReviewTableProps {
  items: CryovialItem[];
  page: number;
  rowsPerPage: number;
  onPageChange: (newPage: number) => void;
  onRowsPerPageChange: (newRowsPerPage: number) => void;
  onApproveClick: (cryovial: CryovialItem, approved: boolean) => void;
  onThawClick: (cryovial: CryovialItem) => void;
  onDestroyClick: (cryovial: CryovialItem) => void;
}

export function CryovialReviewTable({
  items,
  page,
  rowsPerPage,
  onPageChange,
  onRowsPerPageChange,
  onApproveClick,
  onThawClick,
  onDestroyClick
}: CryovialReviewTableProps) {
  const theme = useTheme();
  const { detected, action, purple } = theme.custom.status;

  const isExpired = (expiryDateStr: string) => {
    if (!expiryDateStr) return false;
    return new Date(expiryDateStr) <= new Date();
  };

  const paginatedItems = items.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);

  return (
    <Paper sx={{ width: "100%", overflow: "hidden", border: "1px solid", borderColor: "divider", borderRadius: 1.5 }}>
      <TableContainer sx={{ maxHeight: "calc(100vh - 360px)", minHeight: 300 }}>
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, bgcolor: "background.default", color: "text.secondary" }}>
                Code
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, bgcolor: "background.default", color: "text.secondary" }}>
                Organism &amp; Source
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, bgcolor: "background.default", color: "text.secondary" }}>
                Status
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, bgcolor: "background.default", color: "text.secondary" }}>
                Vials Stock
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, bgcolor: "background.default", color: "text.secondary" }}>
                Prepared
              </TableCell>
              <TableCell sx={{ fontWeight: 700, fontSize: 12, bgcolor: "background.default", color: "text.secondary" }}>
                Expiry
              </TableCell>
              <TableCell align="right" sx={{ fontWeight: 700, fontSize: 12, bgcolor: "background.default", color: "text.secondary" }}>
                Actions
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {paginatedItems.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 6 }}>
                  <Typography sx={{ color: "text.secondary", fontSize: 14 }}>
                    No cryovials matching the filter criteria.
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              paginatedItems.map((c) => {
                const expired = isExpired(c.expiryDate);
                const expiringSoon = !expired && isMaterialExpiringSoon(c.expiryDate, 30);
                const organismName = c.organism?.scientificName ?? c.organismNameSnapshot;
                const atcc = c.organism?.atccNumber ? ` (ATCC ${c.organism.atccNumber})` : "";
                const depleted = c.vialsRemaining === 0;

                return (
                  <TableRow
                    key={c.id}
                    hover
                    sx={{
                      "&:nth-of-type(even)": { bgcolor: "background.default" },
                      opacity: c.isDestroyed ? 0.65 : 1
                    }}
                  >
                    {/* Code */}
                    <TableCell sx={{ py: 1.25 }}>
                      <Typography
                        sx={{
                          fontSize: 13,
                          fontWeight: 700,
                          fontFamily: "monospace",
                          color: theme.palette.primary.main
                        }}
                      >
                        {c.code}
                      </Typography>
                      {c.storageCondition && (
                        <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap>
                          {c.storageCondition}
                        </Typography>
                      )}
                    </TableCell>

                    {/* Organism & Source */}
                    <TableCell sx={{ py: 1.25 }}>
                      <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
                        {organismName}
                        {atcc && (
                          <Typography component="span" sx={{ fontSize: 11, color: "text.secondary", ml: 0.5 }}>
                            {atcc}
                          </Typography>
                        )}
                      </Typography>
                      {c.material && (
                        <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap>
                          {c.material.materialName} · Batch {c.material.batchNumber}
                        </Typography>
                      )}
                    </TableCell>

                    {/* Status */}
                    <TableCell sx={{ py: 1.25 }}>
                      {c.isDestroyed ? (
                        <StatusBadge status="Destroyed" label="Destroyed" />
                      ) : (
                        <StatusBadge status={c.approvalStatus} />
                      )}
                    </TableCell>

                    {/* Vials Stock */}
                    <TableCell sx={{ py: 1.25 }}>
                      <Typography
                        sx={{
                          fontSize: 13,
                          fontWeight: 700,
                          color: depleted ? detected.text : "text.primary"
                        }}
                      >
                        {c.vialsRemaining} of {c.numberOfVialsPrepared} vials
                      </Typography>
                      {depleted && !c.isDestroyed && (
                        <Box sx={{ mt: 0.25 }}>
                          <StatusBadge status="Depleted" label="Depleted" />
                        </Box>
                      )}
                    </TableCell>

                    {/* Prepared */}
                    <TableCell sx={{ py: 1.25 }}>
                      <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>
                        {formatLabDate(c.preparedAt)}
                      </Typography>
                      {c.preparedByName && (
                        <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap>
                          {c.preparedByName}
                        </Typography>
                      )}
                    </TableCell>

                    {/* Expiry */}
                    <TableCell sx={{ py: 1.25 }}>
                      <Typography
                        sx={{
                          fontSize: 12,
                          fontWeight: expired || expiringSoon ? 700 : 500,
                          color: expired ? detected.text : expiringSoon ? action.text : "text.primary"
                        }}
                      >
                        {formatLabDate(c.expiryDate)}
                      </Typography>
                      {expired && (
                        <Typography sx={{ fontSize: 10, color: detected.text, fontWeight: 700 }}>
                          Expired
                        </Typography>
                      )}
                      {expiringSoon && (
                        <Typography sx={{ fontSize: 10, color: action.text, fontWeight: 600 }}>
                          Expiring soon
                        </Typography>
                      )}
                    </TableCell>

                    {/* Actions */}
                    <TableCell align="right" sx={{ py: 1.25 }}>
                      <Box sx={{ display: "flex", justifyContent: "flex-end", alignItems: "center", gap: 0.75 }}>
                        {c.approvalStatus === "PendingReview" && !c.isDestroyed ? (
                          <>
                            <Button
                              size="small"
                              variant="outlined"
                              color="success"
                              onClick={() => onApproveClick(c, true)}
                              startIcon={<CheckCircleOutlineIcon fontSize="small" />}
                              sx={{ px: 1, py: 0.25, fontSize: 11, fontWeight: 700 }}
                            >
                              Approve
                            </Button>
                            <Button
                              size="small"
                              variant="outlined"
                              color="error"
                              onClick={() => onApproveClick(c, false)}
                              startIcon={<HighlightOffIcon fontSize="small" />}
                              sx={{ px: 1, py: 0.25, fontSize: 11, fontWeight: 700 }}
                            >
                              Reject
                            </Button>
                          </>
                        ) : (
                          <>
                            {c.approvalStatus === "Approved" && !c.isDestroyed && !expired && c.vialsRemaining > 0 && (
                              <Tooltip title="Thaw a single vial from this approved batch">
                                <Button
                                  size="small"
                                  variant="outlined"
                                  onClick={() => onThawClick(c)}
                                  startIcon={<AcUnitIcon fontSize="small" />}
                                  sx={{
                                    px: 1,
                                    py: 0.25,
                                    fontSize: 11,
                                    fontWeight: 700,
                                    borderColor: theme.palette.primary.main,
                                    color: theme.palette.primary.main,
                                    "&:hover": {
                                      borderColor: theme.palette.primary.dark,
                                      bgcolor: purple.bg
                                    }
                                  }}
                                >
                                  Thaw Vial
                                </Button>
                              </Tooltip>
                            )}

                            {!c.isDestroyed && (
                              <Tooltip title="Decommission batch">
                                <Button
                                  size="small"
                                  color="error"
                                  onClick={() => onDestroyClick(c)}
                                  startIcon={<DeleteOutlineIcon fontSize="small" />}
                                  sx={{ px: 1, py: 0.25, fontSize: 11 }}
                                >
                                  Destroy
                                </Button>
                              </Tooltip>
                            )}
                          </>
                        )}

                        <Tooltip title="View laboratory report record">
                          <Button
                            component={Link}
                            to={`/cryovials/${c.id}/report`}
                            target="_blank"
                            rel="noopener"
                            size="small"
                            variant="outlined"
                            startIcon={<DescriptionOutlinedIcon fontSize="small" />}
                            sx={{
                              px: 1,
                              py: 0.25,
                              fontSize: 11,
                              borderColor: "divider",
                              color: "text.secondary",
                              "&:hover": { bgcolor: "background.default" }
                            }}
                          >
                            Record
                          </Button>
                        </Tooltip>
                      </Box>
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <TablePagination
        rowsPerPageOptions={[10, 25, 50, 100]}
        component="div"
        count={items.length}
        rowsPerPage={rowsPerPage}
        page={page}
        onPageChange={(_, newPage) => onPageChange(newPage)}
        onRowsPerPageChange={(e) => {
          onRowsPerPageChange(parseInt(e.target.value, 10));
          onPageChange(0);
        }}
      />
    </Paper>
  );
}
