import React from "react";
import { Card, CardContent, Typography, Box } from "@mui/material";
import { Trans } from "react-i18next";
import { Ltr } from "../../../../components/Ltr";
import { useTranslation } from "../../../../i18n";
import type { WorkingStandardQualificationDto } from "../../types";

interface QualificationSourceSectionProps {
  qualification: WorkingStandardQualificationDto;
}

export const QualificationSourceSection: React.FC<QualificationSourceSectionProps> = ({
  qualification
}) => {
  const { t } = useTranslation("workingStandards");
  return (
    <Card variant="outlined" sx={{ borderRadius: 2 }}>
      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.secondary" }}>
          {t("source.heading")}
        </Typography>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <Box>
            <Typography variant="caption" color="text.secondary">{t("source.masterEntry")}</Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {qualification.materialMasterName}
            </Typography>
            <Typography variant="caption" sx={{ fontFamily: "monospace" }}>
              <Ltr>{qualification.materialMasterCode}</Ltr>
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" color="text.secondary">{t("source.sourceMaterialBatch")}</Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {qualification.sourceMaterialName}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              <Trans
                i18nKey="source.batchLine"
                ns="workingStandards"
                values={{ batch: qualification.sourceBatchNumber }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            </Typography>
          </Box>

          {qualification.sourceSampleReference && (
            <Box>
              <Typography variant="caption" color="text.secondary">{t("source.sourceSampleReference")}</Typography>
              <Typography variant="body2" sx={{ fontFamily: "monospace", fontWeight: 600 }}>
                <Ltr>{qualification.sourceSampleReference}</Ltr>
              </Typography>
            </Box>
          )}

          {qualification.workingStandardCode && (
            <Box>
              <Typography variant="caption" color="text.secondary">{t("source.workingStandardLot")}</Typography>
              <Typography variant="body2" sx={{ fontFamily: "monospace", fontWeight: 600, color: "primary.main" }}>
                <Ltr>{qualification.workingStandardCode}</Ltr>
              </Typography>
            </Box>
          )}

          <Box>
            <Typography variant="caption" color="text.secondary">{t("source.quantity")}</Typography>
            <Typography variant="body2">
              {qualification.quantityGrams != null ? <Ltr>{t("units.grams", { value: qualification.quantityGrams })}</Ltr> : "—"}
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" color="text.secondary">{t("source.location")}</Typography>
            <Typography variant="body2">
              {qualification.location || "—"}
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" color="text.secondary">{t("source.moistureContent")}</Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {qualification.moisturePercent != null ? <Ltr>{t("units.percent", { value: qualification.moisturePercent.toFixed(2) })}</Ltr> : "—"}
            </Typography>
          </Box>
        </Box>

        {(qualification.returnReason || qualification.rejectReason) && (
          <Box sx={{ mt: 2, pt: 1.5, borderTop: 1, borderColor: "divider" }}>
            {qualification.returnReason && (
              <Box sx={{ mb: 1 }}>
                <Typography variant="caption" color="warning.main" sx={{ fontWeight: 700 }}>
                  {t("source.returnReason")}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {qualification.returnReason}
                </Typography>
              </Box>
            )}
            {qualification.rejectReason && (
              <Box>
                <Typography variant="caption" color="error.main" sx={{ fontWeight: 700 }}>
                  {t("source.rejectionReason")}
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {qualification.rejectReason}
                </Typography>
              </Box>
            )}
          </Box>
        )}
      </CardContent>
    </Card>
  );
};
