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
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type { TFunction } from "i18next";
import { HplcWorkspaceService } from "../services/HplcWorkspaceService";
import type { HplcEvidenceDto } from "../types";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

export interface HplcReportsSectionProps {
  testOrderId: number;
}

function formatEvidenceKind(kind: string, t: TFunction<"hplcWorkspace">): string {
  switch (kind) {
    case "StandardReport":
      return t("review.reports.kindStandard");
    case "SampleReport":
      return t("review.reports.kindSample");
    default:
      return kind;
  }
}

function getFileIcon(contentType: string) {
  if (contentType?.includes("pdf")) return <PictureAsPdfIcon color="error" sx={{ fontSize: 16 }} />;
  if (contentType?.includes("image")) return <ImageIcon color="primary" sx={{ fontSize: 16 }} />;
  return <InsertDriveFileIcon color="action" sx={{ fontSize: 16 }} />;
}

export function HplcReportsSection({ testOrderId }: HplcReportsSectionProps) {
  const { t } = useTranslation("hplcWorkspace");
  const theme = useTheme();
  const [evidenceList, setEvidenceList] = useState<HplcEvidenceDto[]>([]);
  const [evidenceLoading, setEvidenceLoading] = useState(false);
  const [evidenceError, setEvidenceError] = useState<string | null>(null);
  const [openingEvidenceId, setOpeningEvidenceId] = useState<number | null>(null);

  useEffect(() => {
    let active = true;
    if (!testOrderId) return;
    setEvidenceLoading(true);
    setEvidenceError(null);

    HplcWorkspaceService.getTestOrderEvidence(testOrderId)
      .then((data) => {
        if (active) {
          setEvidenceList(Array.isArray(data) ? data : []);
        }
      })
      .catch(() => {
        if (active) {
          setEvidenceError(t("review.reports.loadError"));
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
  }, [testOrderId, t]);

  const handleOpenEvidence = async (id: number) => {
    setOpeningEvidenceId(id);
    try {
      const svc = HplcWorkspaceService as unknown as Record<string, (id: number) => Promise<unknown>>;
      if (typeof svc.viewEvidence === "function") {
        await svc.viewEvidence(id);
      } else {
        await HplcWorkspaceService.openEvidenceInNewTab(id);
      }
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
        {t("review.reports.title")}
      </Typography>

      {evidenceLoading && (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 0.5 }}>
          <CircularProgress size={14} />
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {t("review.reports.loading")}
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
          {t("review.reports.empty")}
        </Typography>
      )}

      {!evidenceLoading && !evidenceError && evidenceList.length > 0 && (
        <TableContainer sx={{ border: `1px solid ${theme.palette.divider}`, borderRadius: 1 }}>
          <Table size="small">
            <TableHead sx={tableHeadSx(theme)}>
              <TableRow>
                <TableCell sx={{ fontSize: 12 }}>{t("review.reports.columns.reportContext")}</TableCell>
                <TableCell sx={{ fontSize: 12 }}>{t("review.reports.columns.fileName")}</TableCell>
                <TableCell sx={{ fontSize: 12 }}>{t("review.reports.columns.uploadedBy")}</TableCell>
                <TableCell sx={{ fontSize: 12 }}>{t("review.reports.columns.uploadedAt")}</TableCell>
                <TableCell sx={{ fontSize: 12 }} align="right">{t("review.reports.columns.action")}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {evidenceList.map((e) => (
                <TableRow key={e.id} hover>
                  <TableCell sx={{ fontSize: 12 }}>
                    <Chip
                      label={`${formatEvidenceKind(e.kind, t)} (${e.context})`}
                      size="small"
                      variant="outlined"
                      sx={{ fontSize: 12, height: 20 }}
                    />
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
                      {getFileIcon(e.contentType)}
                      <Typography sx={{ fontSize: 12, fontWeight: 600 }}>
                        <Ltr>{e.fileName}</Ltr>
                      </Typography>
                    </Box>
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>
                    {e.uploadedByUserName ?? (e.uploadedByUserId ? t("review.reports.userFallback", { id: e.uploadedByUserId }) : "—")}
                  </TableCell>
                  <TableCell sx={{ fontSize: 12 }}>
                    {e.uploadedAt ? <Ltr>{formatDateTimeWithTimeZone(e.uploadedAt)}</Ltr> : "—"}
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
                      sx={{ textTransform: "none", fontSize: 12, py: 0.25, px: 1, minHeight: 24 }}
                    >
                      {t("review.reports.view")}
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
