import React from "react";
import { Box, Typography, IconButton, Stack, Chip } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { Trans } from "react-i18next";
import { Ltr } from "../../../../components/Ltr";
import { StatusBadge } from "../../../../components/StatusBadge";
import { useTranslation } from "../../../../i18n";
import { formatDateTimeWithTimeZone } from "../../../../utils/formatDate";
import type { WorkingStandardQualificationDto } from "../../types";

interface QualificationHeaderProps {
  qualification: WorkingStandardQualificationDto;
  onClose: () => void;
}

export const QualificationHeader: React.FC<QualificationHeaderProps> = ({
  qualification,
  onClose
}) => {
  const { t } = useTranslation("workingStandards");
  const dateText = formatDateTimeWithTimeZone(qualification.createdAt);

  return (
    <Box sx={{ p: 2.5, borderBottom: 1, borderColor: "divider" }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
        <Box>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 0.5 }}>
            <Typography variant="h6" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
              <Ltr>{qualification.code}</Ltr>
            </Typography>
            <Chip
              size="small"
              label={t(`qualificationKinds.${qualification.kind}`, { defaultValue: qualification.kind })}
              color={qualification.kind === "Initial" ? "primary" : "secondary"}
              variant="outlined"
              sx={{ fontSize: 12, height: 20 }}
            />
            <StatusBadge
              status={qualification.status}
              label={t(`qualificationStatuses.${qualification.status}`, { defaultValue: qualification.status })}
            />
          </Stack>
          <Typography variant="body2" color="text.secondary">
            {qualification.createdByUserName ? (
              <Trans
                i18nKey="drawer.createdOnBy"
                ns="workingStandards"
                values={{ date: dateText, user: qualification.createdByUserName }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            ) : (
              <Trans
                i18nKey="drawer.createdOn"
                ns="workingStandards"
                values={{ date: dateText }}
                components={{ ltr: <Ltr>{null}</Ltr> }}
              />
            )}
          </Typography>
        </Box>
        <IconButton onClick={onClose} size="small" aria-label={t("actions.close")}>
          <CloseIcon fontSize="small" />
        </IconButton>
      </Box>
    </Box>
  );
};
