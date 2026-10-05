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
import { tableHeadSx } from "../../../theme";
import { StatusBadge } from "../../../components/StatusBadge";
import { formatLabDateTime } from "../../../utils/formatDate";
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

  if (qualifications.length === 0) {
    return (
      <Paper sx={{ p: 4, textAlign: "center" }}>
        <Typography color="text.secondary">No qualifications found.</Typography>
      </Paper>
    );
  }

  return (
    <TableContainer component={Paper} sx={{ borderRadius: 2 }}>
      <Table size="small">
        <TableHead sx={tableHeadSx(theme)}>
          <TableRow>
            <TableCell sx={{ fontWeight: 700 }}>Code</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Kind</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Material</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Batch</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Status</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Run</TableCell>
            <TableCell sx={{ fontWeight: 700 }}>Created</TableCell>
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
                  {q.code}
                </Typography>
                {q.workingStandardCode && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                    Lot: {q.workingStandardCode}
                  </Typography>
                )}
              </TableCell>
              <TableCell>
                <Chip
                  size="small"
                  label={q.kind}
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
                  {q.materialMasterCode}
                </Typography>
              </TableCell>
              <TableCell sx={{ fontSize: 13 }}>{q.sourceBatchNumber}</TableCell>
              <TableCell>
                <StatusBadge status={q.status} />
              </TableCell>
              <TableCell>
                {q.run ? (
                  <Box>
                    <Typography sx={{ fontFamily: "monospace", fontSize: 12, fontWeight: 600 }}>
                      {q.run.runCode}
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
                  {formatLabDateTime(q.createdAt)}
                </Typography>
                {q.createdByUserName && (
                  <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                    by {q.createdByUserName}
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
