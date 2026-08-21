import { Dialog, DialogTitle, DialogContent, DialogActions, Button, Grid, Typography, Box, Divider , useTheme} from "@mui/material";
import { ResultRecordItem } from "../types/reportingTypes";
import { StatusBadge, CategoryBadge } from "../../../components/StatusBadge";
import { brandColors } from "../../../theme";
import { useState } from "react";
import { AuditHistoryDialog } from "../../../components/AuditHistoryDialog";

interface RecordDetailDialogProps {
  open: boolean;
  onClose: () => void;
  record: ResultRecordItem | null;
}

export function RecordDetailDialog({ open, onClose, record }: RecordDetailDialogProps) {
  const theme = useTheme();
  const [auditOpen, setAuditOpen] = useState(false);

  if (!record) return null;

  return (
    <>
      <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
        <DialogTitle sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderBottom: 1, borderColor: "divider", pb: 1.5 }}>
          <Box>
            <Typography sx={{ fontSize: 18, fontWeight: 700, color: theme.palette.primary.main }}>
              Laboratory Result Record
            </Typography>
            <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
              Reference: <strong>{record.referenceNumber}</strong> · Read-only source record
            </Typography>
          </Box>
          <Box sx={{ display: "flex", gap: 1 }}>
            <CategoryBadge category={record.category} />
            <StatusBadge status={record.approvalStatus} />
          </Box>
        </DialogTitle>

        <DialogContent sx={{ pt: 2.5 }}>
          <Grid container spacing={2}>
            {/* Section 1: Sample & Identification */}
            <Grid item xs={12}>
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1, textTransform: "uppercase", letterSpacing: 0.5 }}>
                Sample & Item Details
              </Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Sample / Reference</Typography>
              <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{record.referenceNumber}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Item / Subject Name</Typography>
              <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{record.subjectName}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Item Detail / Location</Typography>
              <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{record.subjectDetail ?? "—"}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Batch Number</Typography>
              <Typography sx={{ fontSize: 13 }}>{record.batchNumber ?? "—"}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Control Number</Typography>
              <Typography sx={{ fontSize: 13 }}>{record.controlNumber ?? "—"}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Category</Typography>
              <Typography sx={{ fontSize: 13 }}>{record.category}</Typography>
            </Grid>

            <Grid item xs={12}>
              <Divider sx={{ my: 1 }} />
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1, textTransform: "uppercase", letterSpacing: 0.5 }}>
                Test & Result Execution
              </Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Test Code & Name</Typography>
              <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{record.testCode} — {record.testDisplayName}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Reported Result Value</Typography>
              <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>
                {record.reportedValue} {record.unit ?? ""}
              </Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Result Level</Typography>
              <Box sx={{ mt: 0.5 }}>
                <StatusBadge status={record.resultLevel} />
              </Box>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Specification Limit</Typography>
              <Typography sx={{ fontSize: 13 }}>{record.specLimit ?? "—"}</Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Alert / Action Limits</Typography>
              <Typography sx={{ fontSize: 13 }}>
                Alert: {record.alertLimit ?? "—"} · Action: {record.actionLimit ?? "—"}
              </Typography>
            </Grid>

            <Grid item xs={12} sm={4}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Detection Limit / Round</Typography>
              <Typography sx={{ fontSize: 13 }}>
                {record.detectionLimit != null ? `${record.detectionLimit} ${record.unit ?? ""}` : "—"} (Round {record.round})
              </Typography>
            </Grid>

            <Grid item xs={12}>
              <Divider sx={{ my: 1 }} />
              <Typography sx={{ fontSize: 13, fontWeight: 700, color: theme.palette.primary.main, mb: 1, textTransform: "uppercase", letterSpacing: 0.5 }}>
                Attribution, Timing & Review Chain
              </Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Result Entered By (Analyst)</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{record.resultEnteredByName}</Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                {new Date(record.resultEnteredAt).toLocaleString("en-GB")}
              </Typography>
            </Grid>

            <Grid item xs={12} sm={6}>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Approved By (Section Head / Reviewer)</Typography>
              <Typography sx={{ fontSize: 13, fontWeight: 600 }}>{record.approvedByName ?? "Pending Approval"}</Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>
                {record.approvedAt ? new Date(record.approvedAt).toLocaleString("en-GB") : "—"}
              </Typography>
            </Grid>
          </Grid>
        </DialogContent>

        <DialogActions sx={{ px: 3, py: 2, borderTop: 1, borderColor: "divider", justifyContent: "space-between" }}>
          <Button variant="outlined" size="small" onClick={() => setAuditOpen(true)}>
            View Audit Trail
          </Button>
          <Button variant="contained" size="small" onClick={onClose}>
            Close
          </Button>
        </DialogActions>
      </Dialog>

      <AuditHistoryDialog
        open={auditOpen}
        onClose={() => setAuditOpen(false)}
        entityName="ResultRecord"
        entityId={record.id}
      />
    </>
  );
}
