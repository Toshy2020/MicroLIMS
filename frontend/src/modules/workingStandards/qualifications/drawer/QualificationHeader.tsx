import React from "react";
import { Box, Typography, IconButton, Stack, Chip } from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { StatusBadge } from "../../../../components/StatusBadge";
import { formatLabDateTime } from "../../../../utils/formatDate";
import type { WorkingStandardQualificationDto } from "../../types";

interface QualificationHeaderProps {
  qualification: WorkingStandardQualificationDto;
  onClose: () => void;
}

export const QualificationHeader: React.FC<QualificationHeaderProps> = ({
  qualification,
  onClose
}) => {
  return (
    <Box sx={{ p: 2.5, borderBottom: 1, borderColor: "divider" }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
        <Box>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center", mb: 0.5 }}>
            <Typography variant="h6" sx={{ fontFamily: "monospace", fontWeight: 700 }}>
              {qualification.code}
            </Typography>
            <Chip
              size="small"
              label={qualification.kind}
              color={qualification.kind === "Initial" ? "primary" : "secondary"}
              variant="outlined"
              sx={{ fontSize: 11, height: 20 }}
            />
            <StatusBadge status={qualification.status} />
          </Stack>
          <Typography variant="body2" color="text.secondary">
            Created on {formatLabDateTime(qualification.createdAt)}
            {qualification.createdByUserName && ` by ${qualification.createdByUserName}`}
          </Typography>
        </Box>
        <IconButton onClick={onClose} size="small" aria-label="Close">
          <CloseIcon fontSize="small" />
        </IconButton>
      </Box>
    </Box>
  );
};
