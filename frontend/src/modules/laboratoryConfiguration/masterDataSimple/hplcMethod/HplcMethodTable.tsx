import {
  TableContainer,
  Paper,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  Chip,
  Stack,
  IconButton,
  Tooltip,
  CircularProgress,
  Typography,
  useTheme
} from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import BlockIcon from "@mui/icons-material/Block";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import HistoryIcon from "@mui/icons-material/History";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { tableHeadSx } from "../../../../theme";
import { HplcMethodListItem } from "../services/HplcMethodService";

export interface HplcMethodTableProps {
  methods: HplcMethodListItem[];
  loading: boolean;
  searchQuery: string;
  statusFilter: string;
  sectionFilter: string;
  resolveSectionDisplay: (sectionName: string) => string;
  onViewHistory: (method: HplcMethodListItem) => void;
  onEdit: (method: HplcMethodListItem) => void;
  onToggleActive: (method: HplcMethodListItem) => void;
}

export function HplcMethodTable({
  methods,
  loading,
  searchQuery,
  statusFilter,
  sectionFilter,
  resolveSectionDisplay,
  onViewHistory,
  onEdit,
  onToggleActive
}: HplcMethodTableProps) {
  const theme = useTheme();

  return (
    <TableContainer component={Paper} elevation={0} sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2 }}>
      <Table size="small">
        <TableHead>
          <TableRow sx={tableHeadSx(theme)}>
            <TableCell sx={{ fontWeight: 600 }}>Name</TableCell>
            <TableCell sx={{ fontWeight: 600 }}>Abbreviation</TableCell>
            <TableCell sx={{ fontWeight: 600 }}>Analytes</TableCell>
            <TableCell sx={{ fontWeight: 600 }}>Section</TableCell>
            <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
            <TableCell sx={{ fontWeight: 600 }}>Last Modified</TableCell>
            <TableCell align="right" sx={{ fontWeight: 600 }}>Actions</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {loading ? (
            <TableRow>
              <TableCell colSpan={7} align="center" sx={{ py: 6 }}>
                <CircularProgress size={32} />
                <Typography variant="body2" sx={{ mt: 1, color: "text.secondary" }}>
                  Loading HPLC methods...
                </Typography>
              </TableCell>
            </TableRow>
          ) : methods.length === 0 ? (
            <TableRow>
              <TableCell colSpan={7} align="center" sx={{ py: 6 }}>
                <ScienceOutlinedIcon sx={{ fontSize: 40, color: "text.disabled", mb: 1 }} />
                <Typography variant="body1" sx={{ color: "text.secondary", fontWeight: 500 }}>
                  No HPLC methods found
                </Typography>
                <Typography variant="body2" sx={{ color: "text.disabled", mt: 0.5 }}>
                  {searchQuery || statusFilter !== "ALL" || sectionFilter !== "ALL"
                    ? "Try adjusting your search or filters."
                    : "Click 'Add Method' to register your first HPLC analytical method master."}
                </Typography>
              </TableCell>
            </TableRow>
          ) : (
            methods.map((m) => (
              <TableRow key={m.id} hover>
                <TableCell sx={{ fontWeight: 600 }}>{m.name}</TableCell>
                <TableCell>
                  <Chip
                    label={m.abbreviation}
                    size="small"
                    color="primary"
                    variant="outlined"
                    sx={{ fontSize: 11, fontWeight: 700 }}
                  />
                </TableCell>
                <TableCell>
                  <Chip
                    label={`${m.analyteCount} ${m.analyteCount === 1 ? "analyte" : "analytes"}`}
                    size="small"
                    variant="outlined"
                    sx={{ fontSize: 11 }}
                  />
                </TableCell>
                <TableCell>
                  <Chip
                    label={resolveSectionDisplay(m.sectionName)}
                    size="small"
                    variant="outlined"
                    sx={{ fontSize: 12 }}
                  />
                </TableCell>
                <TableCell>
                  <Chip
                    icon={m.isActive ? <CheckCircleOutlinedIcon fontSize="small" /> : <BlockIcon fontSize="small" />}
                    label={m.isActive ? "Active" : "Inactive"}
                    size="small"
                    color={m.isActive ? "success" : "default"}
                    sx={{ fontSize: 11, fontWeight: 600 }}
                  />
                </TableCell>
                <TableCell sx={{ fontSize: 12, color: "text.secondary" }}>
                  {new Date(m.lastModifiedAt).toLocaleDateString()}
                </TableCell>
                <TableCell align="right">
                  <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                    <Tooltip title="View Audit History">
                      <IconButton size="small" onClick={() => onViewHistory(m)}>
                        <HistoryIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title="Edit Method">
                      <IconButton size="small" onClick={() => onEdit(m)}>
                        <EditIcon fontSize="small" />
                      </IconButton>
                    </Tooltip>
                    <Tooltip title={m.isActive ? "Deactivate Method" : "Activate Method"}>
                      <IconButton
                        size="small"
                        color={m.isActive ? "error" : "success"}
                        onClick={() => onToggleActive(m)}
                      >
                        {m.isActive ? <BlockIcon fontSize="small" /> : <CheckCircleOutlinedIcon fontSize="small" />}
                      </IconButton>
                    </Tooltip>
                  </Stack>
                </TableCell>
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </TableContainer>
  );
}
