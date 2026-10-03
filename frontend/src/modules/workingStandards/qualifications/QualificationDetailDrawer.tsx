import React, { useState, useEffect, useCallback, useRef } from "react";
import {
  Drawer,
  Box,
  Stack,
  CircularProgress,
  Typography,
  Alert,
  useTheme
} from "@mui/material";
import { toast } from "sonner";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { WorkingStandardService } from "../services/WorkingStandardService";
import type {
  WorkingStandardQualificationDto,
  WorkingStandardDocumentDto
} from "../types";
import { QualificationHeader } from "./drawer/QualificationHeader";
import { QualificationSourceSection } from "./drawer/QualificationSourceSection";
import { QualificationDocumentsSection } from "./drawer/QualificationDocumentsSection";
import { QualificationResultsSection } from "./drawer/QualificationResultsSection";
import { QualificationSignOffSection } from "./drawer/QualificationSignOffSection";
import { SignOffDialog, type SignOffActionType } from "./SignOffDialog";

interface QualificationDetailDrawerProps {
  open: boolean;
  qualificationId: number | null;
  onClose: () => void;
  onUpdated: (qualification: WorkingStandardQualificationDto) => void;
}

export const QualificationDetailDrawer: React.FC<QualificationDetailDrawerProps> = ({
  open,
  qualificationId,
  onClose,
  onUpdated
}) => {
  const theme = useTheme();
  const { permissions } = useAuth();
  const [qualification, setQualification] = useState<WorkingStandardQualificationDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [signOffAction, setSignOffAction] = useState<SignOffActionType | null>(null);
  const activeIdRef = useRef<number | null>(null);

  const canReview = permissions.includes(PERMISSIONS.SAMPLES_REVIEW);
  const canApprove = permissions.includes(PERMISSIONS.SAMPLES_APPROVE);
  const canQualify = permissions.includes(PERMISSIONS.WORKING_STANDARDS_QUALIFY);

  const fetchDetail = useCallback(async (id: number) => {
    activeIdRef.current = id;
    setLoading(true);
    try {
      const data = await WorkingStandardService.getQualification(id);
      if (activeIdRef.current === id) {
        setQualification(data);
      }
    } catch {
      if (activeIdRef.current === id) {
        toast.error("Failed to load qualification details");
      }
    } finally {
      if (activeIdRef.current === id) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    if (open && qualificationId) {
      fetchDetail(qualificationId);
    } else {
      activeIdRef.current = null;
      setQualification(null);
    }
  }, [open, qualificationId, fetchDetail]);

  const handleDocumentUploaded = (doc: WorkingStandardDocumentDto) => {
    if (!qualification) return;
    const updated = {
      ...qualification,
      documents: [...qualification.documents, doc]
    };
    setQualification(updated);
    onUpdated(updated);
  };

  const handleSignOffSuccess = (updated: WorkingStandardQualificationDto) => {
    setQualification(updated);
    onUpdated(updated);
  };

  const needsSourceReport = Boolean(
    qualification &&
    qualification.kind === "Initial" &&
    qualification.sourceSampleId == null &&
    qualification.status === "Draft" &&
    !qualification.documents.some((d) => d.kind === "SourceReport" && d.isCurrent)
  );

  return (
    <>
      <Drawer
        anchor="right"
        open={open}
        onClose={onClose}
        slotProps={{
          paper: {
            sx: {
              width: { xs: "100%", sm: 540, md: 620 },
              p: 0,
              boxShadow: theme.palette.mode === "dark"
                ? "-4px 0 20px rgba(0,0,0,0.4)"
                : "-4px 0 20px rgba(0,0,0,0.08)"
            }
          }
        }}
      >
        {loading ? (
          <Box sx={{ display: "flex", justifyContent: "center", alignItems: "center", height: "100%", p: 4 }}>
            <CircularProgress size={32} />
          </Box>
        ) : !qualification ? (
          <Box sx={{ p: 4, textAlign: "center" }}>
            <Typography color="text.secondary">No qualification selected.</Typography>
          </Box>
        ) : (
          <Box sx={{ display: "flex", flexDirection: "column", height: "100%" }}>
            <QualificationHeader qualification={qualification} onClose={onClose} />

            <Box sx={{ p: 2.5, overflowY: "auto", flexGrow: 1 }}>
              <Stack spacing={2.5}>
                {needsSourceReport && (
                  <Alert severity="info">
                    Upload the raw material&apos;s first test report before this qualification can be assigned to an HPLC run.
                  </Alert>
                )}

                <QualificationSourceSection qualification={qualification} />
                <QualificationResultsSection qualification={qualification} />
                <QualificationDocumentsSection
                  qualificationId={qualification.id}
                  documents={qualification.documents}
                  isDraft={qualification.status === "Draft"}
                  canQualify={canQualify}
                  onDocumentUploaded={handleDocumentUploaded}
                />
                <QualificationSignOffSection
                  qualification={qualification}
                  canReview={canReview}
                  canApprove={canApprove}
                  onInitiateSignOff={setSignOffAction}
                />
              </Stack>
            </Box>
          </Box>
        )}
      </Drawer>

      {qualification && (
        <SignOffDialog
          open={Boolean(signOffAction)}
          actionType={signOffAction}
          qualificationId={qualification.id}
          qualificationCode={qualification.code}
          onClose={() => setSignOffAction(null)}
          onSuccess={handleSignOffSuccess}
        />
      )}
    </>
  );
};
