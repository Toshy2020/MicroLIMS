import {
  Box,
  Paper,
  Typography,
  Button,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Alert,
  Tooltip,
  useTheme
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { IcpStatusBadge } from "../components/IcpStatusBadge";
import { tableHeadSx } from "../../../theme";
import { monospaceFontFamily } from "../../../theme/palette";
import type { IcpRunDto } from "../types";

export interface IcpSampleAssignmentPanelProps {
  run: IcpRunDto;
  canOperate: boolean;
  onRunUpdated?: () => void;
}

export function IcpSampleAssignmentPanel({
  run,
  canOperate
}: IcpSampleAssignmentPanelProps) {
  const theme = useTheme();
  const samples = run.samples || [];

  return (
    <Paper
      elevation={0}
      sx={{
        p: 2.5,
        borderRadius: 2,
        border: `1px solid ${theme.palette.divider}`,
        backgroundColor: theme.palette.background.paper
      }}
    >
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2, flexWrap: "wrap", gap: 1.5 }}>
        <Box>
          <Typography variant="subtitle1" sx={{ fontWeight: 700 }}>
            Assigned Samples ({samples.length})
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            Test orders assigned to this ICP run for analysis and result recording.
          </Typography>
        </Box>

        {canOperate && run.status === "Open" && (
          <Tooltip title={!run.canAssignSamples ? (run.canAssignSamplesReason || "Sample assignment locked") : ""}>
            <span>
              <Button
                variant="contained"
                startIcon={<AddIcon />}
                disabled={!run.canAssignSamples}
                sx={{ textTransform: "none" }}
              >
                Assign Samples
              </Button>
            </span>
          </Tooltip>
        )}
      </Box>

      {!run.canAssignSamples && run.canAssignSamplesReason && run.status === "Open" && (
        <Alert severity="info" sx={{ mb: 2 }}>
          {run.canAssignSamplesReason}
        </Alert>
      )}

      {samples.length === 0 ? (
        <Box sx={{ py: 6, textAlign: "center" }}>
          <ScienceOutlinedIcon sx={{ fontSize: 48, color: "text.disabled", mb: 1 }} />
          <Typography variant="body1" sx={{ fontWeight: 600, color: "text.secondary" }}>
            No samples assigned to this run yet
          </Typography>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            {run.canAssignSamples
              ? "Confirm calibration to enable assigning eligible test orders to this run."
              : run.canAssignSamplesReason || "Sample assignment is locked."}
          </Typography>
        </Box>
      ) : (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1.5, overflowX: "auto" }}>
          <Table size="small">
            <TableHead sx={tableHeadSx}>
              <TableRow>
                <TableCell>Sample Number</TableCell>
                <TableCell>Batch Number</TableCell>
                <TableCell>Product / Item</TableCell>
                <TableCell>Test Code</TableCell>
                <TableCell>Status</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {samples.map((s) => (
                <TableRow key={s.id}>
                  <TableCell sx={{ fontFamily: monospaceFontFamily, fontWeight: 600 }}>
                    {s.sampleNumber}
                  </TableCell>
                  <TableCell>{s.batchNumber || "—"}</TableCell>
                  <TableCell>{s.productName || "—"}</TableCell>
                  <TableCell sx={{ fontFamily: monospaceFontFamily }}>{s.testCode}</TableCell>
                  <TableCell>
                    <IcpStatusBadge status={s.status} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Paper>
  );
}
