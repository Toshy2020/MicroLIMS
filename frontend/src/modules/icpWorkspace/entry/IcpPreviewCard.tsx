import {
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Alert,
  useTheme
} from "@mui/material";
import ErrorOutlinedIcon from "@mui/icons-material/ErrorOutlined";
import { StatusBadge } from "../../../components/StatusBadge";
import { IcpStatusBadge } from "../components/IcpStatusBadge";
import { tableHeadSx } from "../../../theme";
import { formatQuantityLabel } from "./replicateMapping";
import type { IcpPreviewResultDto, IcpOfficialResultDto } from "../types";

export interface IcpPreviewCardProps {
  preview?: IcpPreviewResultDto[];
  official?: IcpOfficialResultDto[];
  submitted?: boolean;
}

export function IcpPreviewCard({
  preview = [],
  official = [],
  submitted = false
}: IcpPreviewCardProps) {
  const theme = useTheme();

  if (submitted) {
    return (
      <Box>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 1.5 }}>
          <StatusBadge status="Completed" label="Official Results (Submitted)" />
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            These official results were recorded and sent for peer review.
          </Typography>
        </Box>

        {official.length === 0 ? (
          <Alert severity="info">
            No official results recorded yet for this sample entry.
          </Alert>
        ) : (
          <TableContainer
            sx={{
              border: `1px solid ${theme.palette.divider}`,
              borderRadius: 1.5,
              overflowX: "auto"
            }}
          >
            <Table size="small">
              <TableHead sx={tableHeadSx(theme)}>
                <TableRow>
                  <TableCell>Parameter</TableCell>
                  <TableCell>Quantity</TableCell>
                  <TableCell align="right">Result</TableCell>
                  <TableCell>Spec Limit</TableCell>
                  <TableCell>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {official.map((o, idx) => (
                  <TableRow key={`${o.parameterName}-${o.quantity}-${idx}`} hover>
                    <TableCell sx={{ fontWeight: 600 }}>{o.parameterName}</TableCell>
                    <TableCell>{formatQuantityLabel(o.quantity)}</TableCell>
                    <TableCell
                      align="right"
                      sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}
                    >
                      {o.display}
                    </TableCell>
                    <TableCell>{o.specLimit ?? "—"}</TableCell>
                    <TableCell>
                      <IcpStatusBadge status={o.status} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Box>
    );
  }

  const hasProblems = preview.some((p) => p.problem != null);

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 1.5 }}>
        <StatusBadge status="Pending Review" label="Server Preview (Draft)" />
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          Computed server-side from calibration curve and current replicate entries. Not the official result.
        </Typography>
      </Box>

      {hasProblems && (
        <Alert severity="warning" icon={<ErrorOutlinedIcon />} sx={{ mb: 2 }}>
          <strong>Calculation warning:</strong> One or more element parameters have range or calibration problems. Dilution adjustment or re-measurement may be required before submitting.
        </Alert>
      )}

      {preview.length === 0 ? (
        <Alert severity="info">
          No preview results available yet. Enter sample amounts, volumes, dilution factors, and element concentrations above, then click <strong>Save Replicates</strong> to generate the server calculation.
        </Alert>
      ) : (
        <TableContainer
          sx={{
            border: `1px solid ${theme.palette.divider}`,
            borderRadius: 1.5,
            overflowX: "auto"
          }}
        >
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell>Parameter</TableCell>
                <TableCell>Quantity</TableCell>
                <TableCell align="right">Result</TableCell>
                <TableCell>Unit</TableCell>
                <TableCell>Spec Limit</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Problem / Warning</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {preview.map((p, idx) => (
                <TableRow
                  key={`${p.icpMethodElementId}-${p.quantity}-${idx}`}
                  hover
                  sx={{
                    backgroundColor: p.problem ? "rgba(211, 47, 47, 0.04)" : "inherit"
                  }}
                >
                  <TableCell sx={{ fontWeight: 600 }}>{p.parameterName}</TableCell>
                  <TableCell>{formatQuantityLabel(p.quantity)}</TableCell>
                  <TableCell
                    align="right"
                    sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}
                  >
                    {p.display}
                  </TableCell>
                  <TableCell>{p.unit || "—"}</TableCell>
                  <TableCell>{p.specLimit ?? "—"}</TableCell>
                  <TableCell>
                    {p.status ? <IcpStatusBadge status={p.status} /> : "—"}
                  </TableCell>
                  <TableCell>
                    {p.problem ? (
                      <Typography
                        variant="body2"
                        sx={{
                          color: "error.main",
                          fontWeight: 600,
                          fontSize: 13,
                          display: "flex",
                          alignItems: "center",
                          gap: 0.5
                        }}
                      >
                        <ErrorOutlinedIcon fontSize="small" sx={{ fontSize: 16 }} />
                        {p.problem}
                      </Typography>
                    ) : (
                      <Typography variant="body2" sx={{ color: "text.disabled" }}>
                        —
                      </Typography>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
