import React from "react";
import {
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Typography,
  Chip,
  Box,
  useTheme
} from "@mui/material";
import { Trans } from "react-i18next";
import { tableHeadSx } from "../../../theme";
import { Ltr } from "../../../components/Ltr";
import { StatusBadge } from "../../../components/StatusBadge";
import { useTranslation } from "../../../i18n";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import type { WorkingStandardQualificationDto } from "../types";

interface QualificationsTableProps {
  qualifications: WorkingStandardQualificationDto[];
  onSelectQualification: (qualification: WorkingStandardQualificationDto) => void;
}

export const QualificationsTable: React.FC<QualificationsTableProps> = ({
  qualifications,
  onSelectQualification
}) => {
  const theme = useTheme();
  const { t } = useTranslation("workingStandards");

  if (qualifications.length === 0) {
    return (
      <Paper sx={{ p: 4, textAlign: "center" }}>
        <Typography color="text.secondary">{t("qualificationsTable.empty")}</Typography>
      </Paper>
    );
  }

  return (
    <TableContainer component={Paper} sx={{ borderRadius: 2 }}>
      <Table size="small">
        <TableHead sx={tableHeadSx(theme)}>
          <TableRow>
            <TableCell sx={{ fontWeight: 700 }}>{t("qualificationsTable.columns.code")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("qualificationsTable.columns.kind")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("qualificationsTable.columns.material")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("qualificationsTable.columns.batch")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("qualificationsTable.columns.status")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("qualificationsTable.columns.run")}</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>{t("qualificationsTable.columns.created")}</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {qualifications.map((q) => (
            <TableRow
              key={q.id}
              hover
              onClick={() => onSelectQualification(q)}
              sx={{ cursor: "pointer" }}
            >
              <TableCell>
                <Typography sx={{ fontFamily: "monospace", fontWeight: 600, fontSize: 13, color: "primary.main" }}>
                  <Ltr>{q.code}</Ltr>
                </Typography>
                {q.workingStandardCode && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                    <Trans
                      i18nKey="qualificationsTable.lotCode"
                      ns="workingStandards"
                      values={{ code: q.workingStandardCode }}
                      components={{ ltr: <Ltr>{null}</Ltr> }}
                    />
                  </Typography>
                )}
              </TableCell>
              <TableCell>
                <Chip
                  size="small"
                  label={t(`qualificationKinds.${q.kind}`, { defaultValue: q.kind })}
                  color={q.kind === "Initial" ? "primary" : "secondary"}
                  variant="outlined"
                  sx={{ fontSize: 12, height: 22 }}
                />
              </TableCell>
              <TableCell>
                <Typography sx={{ fontSize: 13, fontWeight: 500 }}>
                  {q.materialMasterName || q.sourceMaterialName}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  <Ltr>{q.materialMasterCode}</Ltr>
                </Typography>
              </TableCell>
              <TableCell sx={{ fontSize: 13 }}><Ltr>{q.sourceBatchNumber}</Ltr></TableCell>
              <TableCell>
                <StatusBadge
                  status={q.status}
                  label={t(`qualificationStatuses.${q.status}`, { defaultValue: q.status })}
                />
              </TableCell>
              <TableCell>
                {q.run ? (
                  <Box>
                    <Typography sx={{ fontFamily: "monospace", fontSize: 12, fontWeight: 600 }}>
                      <Ltr>{q.run.runCode}</Ltr>
                    </Typography>
                    <Typography variant="caption" color="text.secondary">
                      {q.run.status}
                    </Typography>
                  </Box>
                ) : (
                  <Typography color="text.secondary" sx={{ fontSize: 13 }}>
                    —
                  </Typography>
                )}
              </TableCell>
              <TableCell>
                <Typography sx={{ fontSize: 12 }}>
                  <Ltr>{formatDateTimeWithTimeZone(q.createdAt)}</Ltr>
                </Typography>
                {q.createdByUserName && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                    {t("qualificationsTable.byUser", { user: q.createdByUserName })}
                  </Typography>
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
};
