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
import { Trans } from "react-i18next";
import ErrorOutlinedIcon from "@mui/icons-material/ErrorOutlined";
import { StatusBadge } from "../../../components/StatusBadge";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import { IcpStatusBadge } from "../components/IcpStatusBadge";
import { tableHeadSx } from "../../../theme";
import { formatQuantityLabel } from "./entryMapping";
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
  const { t } = useTranslation("icpWorkspace");
  const theme = useTheme();

  if (submitted) {
    return (
      <Box>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, flexWrap: "wrap", mb: 1.5 }}>
          <StatusBadge status="Completed" label={t("entry.preview.officialBadge")} />
          <Typography variant="body2" sx={{ color: "text.secondary" }}>
            {t("entry.preview.officialSubtitle")}
          </Typography>
        </Box>

        {official.length === 0 ? (
          <Alert severity="info">
            {t("entry.preview.noOfficial")}
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
                  <TableCell>{t("entry.preview.columns.parameter")}</TableCell>
                  <TableCell>{t("entry.preview.columns.quantity")}</TableCell>
                  <TableCell align="right">{t("entry.preview.columns.result")}</TableCell>
                  <TableCell>{t("entry.preview.columns.specLimit")}</TableCell>
                  <TableCell>{t("entry.preview.columns.status")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {official.map((o, idx) => (
                  <TableRow key={`${o.parameterName}-${o.quantity}-${idx}`} hover>
                    <TableCell sx={{ fontWeight: 600 }}><Ltr>{o.parameterName}</Ltr></TableCell>
                    <TableCell>{formatQuantityLabel(o.quantity)}</TableCell>
                    <TableCell
                      align="right"
                      sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}
                    >
                      <Ltr>{o.display}</Ltr>
                    </TableCell>
                    <TableCell>{o.specLimit ? <Ltr>{o.specLimit}</Ltr> : "—"}</TableCell>
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
        <StatusBadge status="Pending Review" label={t("entry.preview.draftBadge")} />
        <Typography variant="body2" sx={{ color: "text.secondary" }}>
          {t("entry.preview.draftSubtitle")}
        </Typography>
      </Box>

      {hasProblems && (
        <Alert severity="warning" icon={<ErrorOutlinedIcon />} sx={{ mb: 2 }}>
          <Trans
            i18nKey="entry.preview.warningAlert"
            ns="icpWorkspace"
            components={{ strong: <strong /> }}
          />
        </Alert>
      )}

      {preview.length === 0 ? (
        <Alert severity="info">
          <Trans
            i18nKey="entry.preview.noPreview"
            ns="icpWorkspace"
            components={{ strong: <strong /> }}
          />
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
                <TableCell>{t("entry.preview.columns.parameter")}</TableCell>
                <TableCell>{t("entry.preview.columns.quantity")}</TableCell>
                <TableCell align="right">{t("entry.preview.columns.result")}</TableCell>
                <TableCell>{t("entry.preview.columns.unit")}</TableCell>
                <TableCell>{t("entry.preview.columns.specLimit")}</TableCell>
                <TableCell>{t("entry.preview.columns.status")}</TableCell>
                <TableCell>{t("entry.preview.columns.problem")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {preview.map((p, idx) => (
                <TableRow
                  key={`${p.icpTestElementId}-${p.quantity}-${idx}`}
                  hover
                  sx={{
                    backgroundColor: p.problem ? "rgba(211, 47, 47, 0.04)" : "inherit"
                  }}
                >
                  <TableCell sx={{ fontWeight: 600 }}><Ltr>{p.parameterName}</Ltr></TableCell>
                  <TableCell>{formatQuantityLabel(p.quantity)}</TableCell>
                  <TableCell
                    align="right"
                    sx={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}
                  >
                    <Ltr>{p.display}</Ltr>
                  </TableCell>
                  <TableCell>{p.unit ? <Ltr>{p.unit}</Ltr> : "—"}</TableCell>
                  <TableCell>{p.specLimit ? <Ltr>{p.specLimit}</Ltr> : "—"}</TableCell>
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
