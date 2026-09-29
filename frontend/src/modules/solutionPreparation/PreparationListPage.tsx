import { useState, useEffect, useMemo, useCallback } from "react";
import { useNavigate } from "react-router-dom";
import {
  Box,
  Paper,
  Typography,
  Table,
  TableHead,
  TableBody,
  TableRow,
  TableCell,
  TableContainer,
  Button,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Stack,
  IconButton,
  Tooltip,
  CircularProgress,
  Alert,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import RefreshIcon from "@mui/icons-material/Refresh";
import { PageHeader } from "../../components/PageHeader";
import { tableHeadSx } from "../../theme";
import { PreparationStatusBadge } from "./components/PreparationStatusBadge";
import { SolutionPreparationService } from "./services/SolutionPreparationService";
import { formatLabDate, formatLabDateTime } from "../../utils/formatDate";
import type {
  SolutionType,
  SolutionPreparationStatus,
  SolutionPreparationListItem
} from "./types";

export function PreparationListPage() {
  const navigate = useNavigate();
  const theme = useTheme();

  const [items, setItems] = useState<SolutionPreparationListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<"ALL" | SolutionPreparationStatus>("ALL");
  const [typeFilter, setTypeFilter] = useState<"ALL" | SolutionType>("ALL");

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await SolutionPreparationService.getAll(
        statusFilter === "ALL" ? undefined : statusFilter,
        typeFilter === "ALL" ? undefined : typeFilter
      );
      setItems(data);
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setError(e.response?.data?.message ?? e.message ?? "Could not load solution preparations.");
    } finally {
      setLoading(false);
    }
  }, [statusFilter, typeFilter]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Client-side search filtering
  const filteredItems = useMemo(() => {
    if (!searchQuery.trim()) return items;
    const q = searchQuery.toLowerCase().trim();
    return items.filter((item) => {
      const codeMatch = item.code?.toLowerCase().includes(q) ?? false;
      const nameMatch = item.solutionMasterName.toLowerCase().includes(q);
      const methodMatch = item.hplcMethodAbbreviation?.toLowerCase().includes(q) ?? false;
      const preparerMatch = item.preparedByUserName?.toLowerCase().includes(q) ?? false;
      return codeMatch || nameMatch || methodMatch || preparerMatch;
    });
  }, [items, searchQuery]);

  return (
    <Box sx={{ p: 3 }}>
      <PageHeader
        title="Solution Preparation"
        subtitle="Manage and execute preparations for mobile phases, diluents, and volumetric titrants"
      >
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => navigate("/preparation/new")}
          sx={{ textTransform: "none", fontWeight: 700 }}
        >
          Start Preparation
        </Button>
      </PageHeader>

      {/* Filter Toolbar */}
      <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
        <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: "center" }}>
          <TextField
            size="small"
            placeholder="Search code, recipe, method, preparer..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            sx={{ flex: 1, minWidth: 200 }}
          />

          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="status-filter-label">Status</InputLabel>
            <Select
              labelId="status-filter-label"
              value={statusFilter}
              label="Status"
              onChange={(e) => setStatusFilter(e.target.value as "ALL" | SolutionPreparationStatus)}
            >
              <MenuItem value="ALL">All Statuses</MenuItem>
              <MenuItem value="InProgress">In Progress</MenuItem>
              <MenuItem value="Prepared">Prepared</MenuItem>
              <MenuItem value="Expired">Expired</MenuItem>
              <MenuItem value="Discarded">Discarded</MenuItem>
              <MenuItem value="Cancelled">Cancelled</MenuItem>
            </Select>
          </FormControl>

          <FormControl size="small" sx={{ minWidth: 160 }}>
            <InputLabel id="type-filter-label">Type</InputLabel>
            <Select
              labelId="type-filter-label"
              value={typeFilter}
              label="Type"
              onChange={(e) => setTypeFilter(e.target.value as "ALL" | SolutionType)}
            >
              <MenuItem value="ALL">All Types</MenuItem>
              <MenuItem value="MobilePhase">Mobile Phase</MenuItem>
              <MenuItem value="Diluent">Diluent</MenuItem>
              <MenuItem value="Titrant">Titrant</MenuItem>
            </Select>
          </FormControl>

          <Tooltip title="Refresh">
            <IconButton onClick={loadData} disabled={loading}>
              <RefreshIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Stack>
      </Paper>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {/* Preparations Table */}
      <TableContainer component={Paper} variant="outlined">
        <Table size="small">
          <TableHead sx={tableHeadSx(theme)}>
            <TableRow>
              <TableCell sx={{ fontWeight: 700 }}>Code</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Type</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Solution Recipe</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>HPLC Method</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Prepared At</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Expires At</TableCell>
              <TableCell sx={{ fontWeight: 700 }}>Prepared By</TableCell>
              <TableCell sx={{ fontWeight: 700, width: 90, textAlign: "right" }}>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={9} align="center" sx={{ py: 4 }}>
                  <CircularProgress size={24} />
                </TableCell>
              </TableRow>
            ) : filteredItems.length === 0 ? (
              <TableRow>
                <TableCell colSpan={9} align="center" sx={{ py: 4, color: "text.secondary" }}>
                  No solution preparations found.
                </TableCell>
              </TableRow>
            ) : (
              filteredItems.map((item) => {
                const isInProgress = item.effectiveStatus === "InProgress";
                return (
                  <TableRow
                    key={item.id}
                    hover
                    sx={{ cursor: "pointer" }}
                    onClick={() => navigate(isInProgress ? `/preparation/${item.id}/edit` : `/preparation/${item.id}`)}
                  >
                    <TableCell>
                      {item.code ? (
                        <Typography variant="body2" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
                          {item.code}
                        </Typography>
                      ) : (
                        <Typography variant="caption" sx={{ color: "text.secondary", fontStyle: "italic" }}>
                          (In Progress)
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2">{item.type}</Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ fontWeight: 600 }}>
                        {item.solutionMasterName}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2">
                        {item.hplcMethodAbbreviation || "—"}
                      </Typography>
                    </TableCell>
                    <TableCell>
                      <PreparationStatusBadge status={item.effectiveStatus} />
                    </TableCell>
                    <TableCell>
                      {item.preparedAt ? formatLabDateTime(item.preparedAt) : "—"}
                    </TableCell>
                    <TableCell>
                      {item.expiresAt ? formatLabDate(item.expiresAt) : "—"}
                    </TableCell>
                    <TableCell>
                      {item.preparedByUserName || "—"}
                    </TableCell>
                    <TableCell align="right" onClick={(e) => e.stopPropagation()}>
                      <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                        {isInProgress ? (
                          <Tooltip title="Resume Preparation">
                            <IconButton
                              size="small"
                              onClick={() => navigate(`/preparation/${item.id}/edit`)}
                            >
                              <EditOutlinedIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        ) : (
                          <Tooltip title="View Record">
                            <IconButton
                              size="small"
                              onClick={() => navigate(`/preparation/${item.id}`)}
                            >
                              <VisibilityOutlinedIcon fontSize="small" />
                            </IconButton>
                          </Tooltip>
                        )}
                      </Stack>
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
}
