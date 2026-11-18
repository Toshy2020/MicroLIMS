import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Drawer,
  IconButton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { useHasPermission } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { workflowProfileService } from "../services/workflowProfileService";
import {
  profileErrorMessage,
  workflowTypeLabel,
  type LiveWorkflowType,
  type WorkflowProfileVersion
} from "../types/workflowProfileTypes";
import { AddProfileVersionDialog } from "./AddProfileVersionDialog";
import { useTranslation } from "../../../i18n";

interface ProfileHistoryDrawerProps {
  open: boolean;
  onClose: () => void;
  workflowType: LiveWorkflowType;
  testCode: string | null;
  onChanged?: () => void;
}

const fmt = (n: number | null) => (n === null ? "-" : String(n));

// Every version of one scope, newest first. A test shows all of its own rows whatever
// type they were written under (the test may have been re-typed since).
export function ProfileHistoryDrawer({ open, onClose, workflowType, testCode, onChanged }: ProfileHistoryDrawerProps) {
  const { t } = useTranslation("kpi");
  const canConfigure = useHasPermission(PERMISSIONS.KPI_CONFIGURE_PROFILES);
  const [rows, setRows] = useState<WorkflowProfileVersion[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [signing, setSigning] = useState<WorkflowProfileVersion | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setRows(await workflowProfileService.list(testCode ? { testCode } : { workflowType, defaultOnly: true }));
    } catch (e) {
      setError(profileErrorMessage(e, t("history.loadError")));
    } finally {
      setLoading(false);
    }
  }, [workflowType, testCode, t]);

  useEffect(() => {
    if (open) void load();
  }, [open, load]);

  return (
    <>
      <Drawer anchor="right" open={open} onClose={onClose}>
        <Box sx={{ width: { xs: "100vw", sm: 720 }, p: 2 }}>
          <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", mb: 2 }}>
            <Typography variant="h6">{t("history.title", { scope: testCode ?? t("history.defaultScope", { type: workflowTypeLabel(workflowType) }) })}</Typography>
            <IconButton aria-label={t("history.close")} onClick={onClose}>
              <CloseIcon />
            </IconButton>
          </Stack>

          {error && <Alert severity="error">{error}</Alert>}
          {loading && <CircularProgress size={24} />}
          {!loading && !error && rows.length === 0 && (
            <Typography color="text.secondary">{t("history.none")}</Typography>
          )}

          {rows.length > 0 && (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>{t("history.columns.effectiveFrom")}</TableCell>
                  <TableCell align="right">{t("history.columns.workUnits")}</TableCell>
                  <TableCell align="right">{t("history.columns.target")}</TableCell>
                  <TableCell>{t("history.columns.status")}</TableCell>
                  <TableCell>{t("history.columns.reason")}</TableCell>
                  <TableCell>{t("history.columns.by")}</TableCell>
                  <TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map((r) => (
                  <TableRow key={r.id}>
                    <TableCell>{r.effectiveFrom}</TableCell>
                    <TableCell align="right">{fmt(r.workUnits)}</TableCell>
                    <TableCell align="right">{fmt(r.targetAnalystDays)}</TableCell>
                    <TableCell>
                      <Chip size="small" label={r.status} color={r.status === "Effective" ? "success" : "default"} />
                    </TableCell>
                    <TableCell>{r.reasonForChange}</TableCell>
                    <TableCell>
                      {r.createdByName}
                      {r.signedAt && (
                        <Typography variant="caption" sx={{ display: "block" }} color="text.secondary">
                          {t("history.signed", { date: r.signedAt.slice(0, 10) })}
                        </Typography>
                      )}
                    </TableCell>
                    <TableCell>
                      {canConfigure && r.status === "Draft" && (
                        <Button size="small" onClick={() => setSigning(r)}>
                          {t("history.reviewAndSign")}
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </Box>
      </Drawer>

      {signing && (
        <AddProfileVersionDialog
          open
          draft={signing}
          workflowType={signing.workflowType}
          testCode={signing.testCode}
          onClose={() => setSigning(null)}
          onDone={() => {
            void load();
            onChanged?.();
          }}
        />
      )}
    </>
  );
}
