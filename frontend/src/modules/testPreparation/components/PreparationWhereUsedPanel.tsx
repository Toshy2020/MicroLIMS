import { useEffect, useState } from "react";
import {
  Alert, Box, CircularProgress, Link as MuiLink, Paper, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Typography, useTheme
} from "@mui/material";
import { Link } from "react-router-dom";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { tableHeadSx } from "../../../theme";
import { serverMessage } from "../../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import {
  SamplePreparationService,
  type PreparationWhereUsedRow
} from "../services/SamplePreparationService";

export type WhereUsedLot =
  | { mediaId: number }
  | { solutionPreparationId: number }
  | { materialId: number };

interface Props {
  lot: WhereUsedLot;
  title?: string;
}

// Every sample whose test-preparation diluent or neutralizer was this lot,
// so a failed or recalled lot shows everything it touched.
export function PreparationWhereUsedPanel({ lot, title }: Props) {
  const { t } = useTranslation("testPreparation");
  const theme = useTheme();
  const [rows, setRows] = useState<PreparationWhereUsedRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const key = JSON.stringify(lot);

  useEffect(() => {
    let cancelled = false;
    setRows(null);
    setError(null);
    SamplePreparationService.getWhereUsed(lot)
      .then((r) => { if (!cancelled) setRows(r); })
      .catch((e: unknown) => {
        if (!cancelled) setError(serverMessage(e) ?? t("whereUsed.loadError"));
      });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  return (
    <Box>
      <Typography sx={{ fontSize: 13, fontWeight: 700, mb: 1 }}>{title ?? t("whereUsed.defaultTitle")}</Typography>
      {error && <Alert severity="error">{error}</Alert>}
      {!error && rows === null && <CircularProgress size={18} />}
      {rows !== null && rows.length === 0 && (
        <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
          {t("whereUsed.empty")}
        </Typography>
      )}
      {rows !== null && rows.length > 0 && (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ fontWeight: 700 }}>{t("whereUsed.columns.sample")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("whereUsed.columns.item")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("whereUsed.columns.role")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("whereUsed.columns.preparedAt")}</TableCell>
                <TableCell sx={{ fontWeight: 700 }}>{t("whereUsed.columns.preparedBy")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {rows.map((r) => (
                <TableRow key={`${r.sampleId}-${r.role}`}>
                  <TableCell>
                    <MuiLink component={Link} to={`/samples/${r.sampleId}/report`} underline="hover">
                      <Ltr>{r.sampleNumber}</Ltr>
                    </MuiLink>
                  </TableCell>
                  <TableCell>{r.itemName}</TableCell>
                  <TableCell>{r.role}</TableCell>
                  <TableCell><Ltr>{formatDateTimeWithTimeZone(r.preparedAt)}</Ltr></TableCell>
                  <TableCell>{r.preparedByName ?? "—"}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
