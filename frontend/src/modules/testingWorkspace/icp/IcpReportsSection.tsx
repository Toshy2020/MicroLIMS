import { useState, useEffect } from "react";
import {
  Box,
  Typography,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Chip,
  Button,
  CircularProgress,
  useTheme
} from "@mui/material";
import PictureAsPdfIcon from "@mui/icons-material/PictureAsPdf";
import ImageIcon from "@mui/icons-material/Image";
import InsertDriveFileIcon from "@mui/icons-material/InsertDriveFile";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import { tableHeadSx } from "../../../theme";
import { IcpReviewApi, type IcpEvidenceDto } from "./icpReviewApi";

export interface IcpReportsSectionProps {
  testOrderId: number;
}

function formatEvidenceKind(kind: string): string {
  switch (kind) {
    case "CalibrationReport":
      return "Calibration Report";
    case "SampleReport":
      return "Sample Report";
    default:
      return kind.replace(/([a-z])([A-Z])/g, "$1 $2");
  }
}

function getFileIcon(contentType: string) {
  if (contentType?.includes("pdf")) return <PictureAsPdfIcon color="error" sx={{ fontSize: 16 }} />;
  if (contentType?.includes("image")) return <ImageIcon color="primary" sx={{ fontSize: 16 }} />;
  return <InsertDriveFileIcon color="action" sx={{ fontSize: 16 }} />;
}

export function IcpReportsSection({ testOrderId }: IcpReportsSectionProps) {
  const theme = useTheme();
  const [evidenceList, setEvidenceList] = useState<IcpEvidenceDto[]>([]);
  const [evidenceLoading, setEvidenceLoading] = useState(false);
  const [evidenceError, setEvidenceError] = useState<string | null>(null);
  const [openingEvidenceId, setOpeningEvidenceId] = useState<number | null>(null);

  useEffect(() => {
    let active = true;
    if (!testOrderId) return;
    setEvidenceLoading(true);
    setEvidenceError(null);

    IcpReviewApi.getTestOrderEvidence(testOrderId)
      .then((data) => {
        if (active) {
          setEvidenceList(Array.isArray(data) ? data : []);
        }
      })
      .catch(() => {
        if (active) {
          setEvidenceError("Unable to load reports.");
        }
      })
      .finally(() => {
        if (active) {
          setEvidenceLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, [testOrderId]);

  const handleOpenEvidence = async (id: number) => {
    setOpeningEvidenceId(id);
    try {
      await IcpReviewApi.openEvidenceInNewTab(id);
    } catch {
      // Quiet error handling
    } finally {
      setOpeningEvidenceId(null);
    }
  };

  return (
    <Box sx={{ mt: 1.5, mb: 1.5 }}>
      <Typography
        sx={{
          fontSize: 12,
          fontWeight: 700,
          color: "text.secondary",
          mb: 0.75,
          textTransform: "uppercase"
        }}
      >
        Evidence Reports
      </Typography>

      {evidenceLoading && (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 0.5 }}>
          <CircularProgress size={14} />
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            Loading reports...
          </Typography>
        </Box>
      )}

      {evidenceError && (
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
          {evidenceError}
        </Typography>
      )}

      {!evidenceLoading && !evidenceError && evidenceList.length === 0 && (
        <Typography variant="caption" sx={{ color: "text.secondary", display: "block" }}>
          No reports available for this test order.
        </Typography>
      )}

      {!evidenceLoading && !evidenceError && evidenceList.length > 0 && (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ fontSize: 12 }}>Report / Context</TableCell>
                <TableCell sx={{ fontSize: 12 }}>File Name</TableCell>
                <TableCell sx={{ fontSize: 12 }}>Uploaded By</TableCell>
                <TableCell sx={{ fontSize: 12 }}>Uploaded At</TableCell>
                <TableCell sx={{ fontSize: 12 }} align="right">Action</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {evidenceList.map((e) => (
                <TableRow key={e.id} hover>
                  <TableCell sx={{ fontSize: 12 }}>
                    <Chip
                      label={`${formatEvidenceKind(e.kind)} (${e.context})`}
                      size="small"
                      variant="outlined"
                      sx={{ fontSize: 12, height: 20 }}
                    />
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
                      {getFileIcon(e.contentType)}
                      <Typography sx={{ fontSize: 12, fontWeight: 600 }}>
                        {e.fileName}
                      </Typography>
                    </Box>
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>
                    {e.uploadedByUserName ?? (e.uploadedByUserId ? `User #${e.uploadedByUserId}` : "—")}
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>
                    {e.uploadedAt ? new Date(e.uploadedAt).toLocaleString() : "—"}
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }} align="right">
                    <Button
                      size="small"
                      variant="outlined"
                      startIcon={
                        openingEvidenceId === e.id ? (
                          <CircularProgress size={12} color="inherit" />
                        ) : (
                          <VisibilityOutlinedIcon sx={{ fontSize: 14 }} />
                        )
                      }
                      onClick={() => handleOpenEvidence(e.id)}
                      disabled={openingEvidenceId === e.id}
                      sx={{ textTransform: "none", fontSize: 12, py: 0.25, px: 1, minHeight: 24, cursor: "pointer" }}
                    >
                      View
                    </Button>
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
