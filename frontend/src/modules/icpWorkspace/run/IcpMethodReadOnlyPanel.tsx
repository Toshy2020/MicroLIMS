import {
  Box,
  Paper,
  Typography,
  Grid,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  Divider,
  Stack,
  useTheme
} from "@mui/material";
import BiotechIcon from "@mui/icons-material/Biotech";
import ScienceIcon from "@mui/icons-material/Science";
import TuneIcon from "@mui/icons-material/Tune";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import HighlightOffIcon from "@mui/icons-material/HighlightOff";
import { tableHeadSx } from "../../../theme";
import { monospaceFontFamily } from "../../../theme/palette";
import type { IcpMethodResponse } from "../types";

export interface IcpMethodReadOnlyPanelProps {
  method: IcpMethodResponse;
}

export function IcpMethodReadOnlyPanel({ method }: IcpMethodReadOnlyPanelProps) {
  const theme = useTheme();

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
      {/* Header */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2.5 }}>
        <Box>
          <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
            <BiotechIcon color="primary" />
            <Typography variant="h6" sx={{ fontWeight: 700, fontSize: 16 }}>
              {method.name} ({method.abbreviation})
            </Typography>
          </Box>
          <Typography variant="body2" sx={{ color: "text.secondary", mt: 0.5 }}>
            Method Snapshot frozen at run initialization.
          </Typography>
        </Box>
        <Chip
          label={method.mode === "MineralAssay" ? "Mineral Assay" : "Elemental Impurities"}
          color={method.mode === "MineralAssay" ? "primary" : "secondary"}
          size="small"
          sx={{ fontWeight: 600 }}
        />
      </Box>

      <Divider sx={{ mb: 2.5 }} />

      <Grid container spacing={3}>
        {/* Method Calibration Parameters */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Box sx={{ p: 2, borderRadius: 1.5, border: `1px solid ${theme.palette.divider}`, height: "100%" }}>
            <Stack direction="row" spacing={1} sx={{ mb: 1.5, alignItems: "center" }}>
              <TuneIcon fontSize="small" color="action" />
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Calibration Parameters
              </Typography>
            </Stack>

            <Grid container spacing={1.5}>
              <Grid size={{ xs: 6 }}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Min Correlation (r)
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontFamily: monospaceFontFamily }}>
                  ≥ {method.minCorrelation.toFixed(4)}
                </Typography>
              </Grid>

              <Grid size={{ xs: 6 }}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Max Calibration Age
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {method.maxCalibrationAgeHours} hours
                </Typography>
              </Grid>

              <Grid size={{ xs: 12 }}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Standard Levels (mg/L)
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontFamily: monospaceFontFamily }}>
                  {method.standardLevelsMgPerL}
                </Typography>
              </Grid>

              <Grid size={{ xs: 12 }}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Calibration Standard Entry
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600 }}>
                  {method.calibrationStandardEntryCode || `Entry #${method.calibrationStandardEntryId}`}
                </Typography>
              </Grid>
            </Grid>
          </Box>
        </Grid>

        {/* Quality Checks & Sample Prep Defaults */}
        <Grid size={{ xs: 12, md: 6 }}>
          <Box sx={{ p: 2, borderRadius: 1.5, border: `1px solid ${theme.palette.divider}`, height: "100%" }}>
            <Stack direction="row" spacing={1} sx={{ mb: 1.5, alignItems: "center" }}>
              <ScienceIcon fontSize="small" color="action" />
              <Typography variant="subtitle2" sx={{ fontWeight: 700 }}>
                Quality Checks & Sample Prep
              </Typography>
            </Stack>

            <Grid container spacing={1.5}>
              <Grid size={{ xs: 6 }}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Default Sample Volume
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                  {method.sampleVolumeMl} mL
                </Typography>
              </Grid>

              <Grid size={{ xs: 6 }}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
                  Default Dilution Factor
                </Typography>
                <Typography variant="body2" sx={{ fontWeight: 600, fontVariantNumeric: "tabular-nums" }}>
                  {method.dilutionFactor}×
                </Typography>
              </Grid>

              <Grid size={{ xs: 12 }}>
                <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 0.5 }}>
                  Configured Quality Checks
                </Typography>
                <Stack spacing={0.75}>
                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    {method.requireBlank ? <CheckCircleIcon color="success" sx={{ fontSize: 16 }} /> : <HighlightOffIcon color="disabled" sx={{ fontSize: 16 }} />}
                    <Typography variant="body2" sx={{ fontSize: 13 }}>
                      Blank check: {method.requireBlank ? `Required (Max: ${method.blankMaxMgPerL ?? "—"} mg/L)` : "Not required"}
                    </Typography>
                  </Box>

                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    {method.requireIcv ? <CheckCircleIcon color="success" sx={{ fontSize: 16 }} /> : <HighlightOffIcon color="disabled" sx={{ fontSize: 16 }} />}
                    <Typography variant="body2" sx={{ fontSize: 13 }}>
                      ICV check: {method.requireIcv ? `Required (${method.icvNominalMgPerL} mg/L, ${method.icvRecoveryLowPercent}%–${method.icvRecoveryHighPercent}%)` : "Not required"}
                    </Typography>
                  </Box>

                  <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
                    {method.requireCcv ? <CheckCircleIcon color="success" sx={{ fontSize: 16 }} /> : <HighlightOffIcon color="disabled" sx={{ fontSize: 16 }} />}
                    <Typography variant="body2" sx={{ fontSize: 13 }}>
                      CCV check: {method.requireCcv ? `Required (${method.ccvNominalMgPerL} mg/L, ${method.ccvRecoveryLowPercent}%–${method.ccvRecoveryHighPercent}%)` : "Not required"}
                    </Typography>
                  </Box>
                </Stack>
              </Grid>
            </Grid>
          </Box>
        </Grid>

        {/* Elements Table */}
        <Grid size={{ xs: 12 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1 }}>
            Method Analytes / Elements ({method.elements?.length ?? 0})
          </Typography>

          <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1.5, overflowX: "auto" }}>
            <Table size="small">
              <TableHead sx={tableHeadSx}>
                <TableRow>
                  <TableCell sx={{ width: 60 }}>#</TableCell>
                  <TableCell>Symbol</TableCell>
                  <TableCell>Wavelength (nm)</TableCell>
                  <TableCell>Plasma View</TableCell>
                  <TableCell align="right">Conversion Factor</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {method.elements?.map((el, index) => (
                  <TableRow key={el.id ?? index}>
                    <TableCell>{el.displayOrder ?? index + 1}</TableCell>
                    <TableCell sx={{ fontWeight: 700 }}>{el.symbol}</TableCell>
                    <TableCell sx={{ fontFamily: monospaceFontFamily }}>
                      {typeof el.wavelengthNm === "number" ? el.wavelengthNm.toFixed(3) : el.wavelengthNm}
                    </TableCell>
                    <TableCell>
                      <Chip
                        label={el.view}
                        size="small"
                        variant="outlined"
                        color={el.view === "Axial" ? "primary" : "default"}
                        sx={{ fontSize: 11, height: 20 }}
                      />
                    </TableCell>
                    <TableCell align="right" sx={{ fontFamily: monospaceFontFamily }}>
                      {el.conversionFactor}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Grid>
      </Grid>
    </Paper>
  );
}
