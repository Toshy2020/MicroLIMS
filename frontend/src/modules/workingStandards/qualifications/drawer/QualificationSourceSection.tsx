import React from "react";
import { Card, CardContent, Typography, Box } from "@mui/material";
import type { WorkingStandardQualificationDto } from "../../types";

interface QualificationSourceSectionProps {
  qualification: WorkingStandardQualificationDto;
}

export const QualificationSourceSection: React.FC<QualificationSourceSectionProps> = ({
  qualification
}) => {
  return (
    <Card variant="outlined" sx={{ borderRadius: 2 }}>
      <CardContent sx={{ p: 2, "&:last-child": { pb: 2 } }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.secondary" }}>
          SOURCE & SPECIFICATIONS
        </Typography>

        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "1fr 1fr" }, gap: 2 }}>
          <Box>
            <Typography variant="caption" color="text.secondary">Master Entry</Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {qualification.materialMasterName}
            </Typography>
            <Typography variant="caption" sx={{ fontFamily: "monospace" }}>
              {qualification.materialMasterCode}
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" color="text.secondary">Source Material & Batch</Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {qualification.sourceMaterialName}
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Batch: {qualification.sourceBatchNumber}
            </Typography>
          </Box>

          {qualification.sourceSampleReference && (
            <Box>
              <Typography variant="caption" color="text.secondary">Source Sample Reference</Typography>
              <Typography variant="body2" sx={{ fontFamily: "monospace", fontWeight: 600 }}>
                {qualification.sourceSampleReference}
              </Typography>
            </Box>
          )}

          {qualification.workingStandardCode && (
            <Box>
              <Typography variant="caption" color="text.secondary">Working Standard Lot</Typography>
              <Typography variant="body2" sx={{ fontFamily: "monospace", fontWeight: 600, color: "primary.main" }}>
                {qualification.workingStandardCode}
              </Typography>
            </Box>
          )}

          <Box>
            <Typography variant="caption" color="text.secondary">Quantity</Typography>
            <Typography variant="body2">
              {qualification.quantityGrams != null ? `${qualification.quantityGrams} g` : "—"}
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" color="text.secondary">Location</Typography>
            <Typography variant="body2">
              {qualification.location || "—"}
            </Typography>
          </Box>

          <Box>
            <Typography variant="caption" color="text.secondary">Moisture Content</Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {qualification.moisturePercent != null ? `${qualification.moisturePercent.toFixed(2)} %` : "—"}
            </Typography>
          </Box>
        </Box>

        {(qualification.returnReason || qualification.rejectReason) && (
          <Box sx={{ mt: 2, pt: 1.5, borderTop: 1, borderColor: "divider" }}>
            {qualification.returnReason && (
              <Box sx={{ mb: 1 }}>
                <Typography variant="caption" color="warning.main" sx={{ fontWeight: 700 }}>
                  Return Reason:
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {qualification.returnReason}
                </Typography>
              </Box>
            )}
            {qualification.rejectReason && (
              <Box>
                <Typography variant="caption" color="error.main" sx={{ fontWeight: 700 }}>
                  Rejection Reason:
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
