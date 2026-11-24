import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  Box,
  Button,
  Paper,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography
} from "@mui/material";
import { LabPage, EmptyState, PartialOutageBanner } from "../../components/lab";
import { SectionTitle } from "../../components/SectionTitle";
import { StatusBadge } from "../../components/StatusBadge";
import { Ltr } from "../../components/Ltr";
import { useTranslation } from "../../i18n";
import { APP_ROUTES } from "../../routes/routes";
import { tableHeadSx } from "../../theme";
import {
  competencyService,
  type CompetencyMatrixDto
} from "../../services/competencyService";
import { trainingAssignmentService } from "../documentControl/services/trainingAssignmentService";
import type { DocumentTrainingAssignmentDto } from "../documentControl/types/trainingAssignmentTypes";
import { formatLabCalendarDate } from "../../utils/formatDate";

const PENDING_SOP_STATUSES = new Set(["Assigned", "Reading", "Overdue", "KafPending"]);

export function MyQualificationsPage() {
  const { t } = useTranslation("competency");
  const [matrix, setMatrix] = useState<CompetencyMatrixDto | null>(null);
  const [assignments, setAssignments] = useState<DocumentTrainingAssignmentDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [matrixFailed, setMatrixFailed] = useState(false);
  const [sopsFailed, setSopsFailed] = useState(false);

  const loadAll = useCallback(async (signal?: AbortSignal) => {
    setLoading(true);
    setMatrixFailed(false);
    setSopsFailed(false);

    const [matrixResult, sopsResult] = await Promise.allSettled([
      competencyService.getMyMatrix(signal),
      trainingAssignmentService.getMyAssignments(undefined, signal)
    ]);

    if (signal?.aborted) return;

    if (matrixResult.status === "fulfilled") {
      setMatrix(matrixResult.value);
    } else {
      setMatrix(null);
      setMatrixFailed(true);
    }

    if (sopsResult.status === "fulfilled") {
      setAssignments(sopsResult.value ?? []);
    } else {
      setAssignments([]);
      setSopsFailed(true);
    }

    setLoading(false);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void loadAll(controller.signal);
    return () => controller.abort();
  }, [loadAll]);

  const isNonAnalyst = matrix !== null && matrix.analysts.length === 0;

  const qualificationRows = useMemo(() => {
    if (!matrix || matrix.analysts.length === 0) return [];
    const ownRow = matrix.analysts[0];
    const cellsByTest = new Map((ownRow?.cells ?? []).map((c) => [c.testDefinitionId, c]));
    return matrix.tests.map((test) => {
      const cell = cellsByTest.get(test.testDefinitionId);
      return {
        testDefinitionId: test.testDefinitionId,
        code: test.code,
        displayName: test.displayName,
        status: cell?.status ?? "NotQualified",
        expiresOn: cell?.expiresOn ?? null,
        sop: test.sop
      };
    });
  }, [matrix]);

  const pendingSops = useMemo(
    () => assignments.filter((a) => !a.supersededAtUtc && (a.isOverdue || PENDING_SOP_STATUSES.has(a.status))),
    [assignments]
  );

  const outageSources = useMemo(() => {
    const sources: string[] = [];
    if (matrixFailed) sources.push(t("my.outageQualifications"));
    if (sopsFailed) sources.push(t("my.outageSops"));
    return sources;
  }, [matrixFailed, sopsFailed, t]);

  return (
    <LabPage title={t("my.title")} subtitle={t("my.subtitle")}>
      <PartialOutageBanner sources={outageSources} onRetry={() => void loadAll()} />

      <Stack spacing={3}>
        <Box component="section" aria-label={t("my.qualificationsHeading")}>
          <SectionTitle>{t("my.qualificationsHeading")}</SectionTitle>
          {loading ? (
            <Paper variant="outlined" sx={{ p: 2, mt: 1 }}>
              <Skeleton variant="rectangular" height={120} />
            </Paper>
          ) : isNonAnalyst ? (
            <Paper variant="outlined" sx={{ mt: 1 }}>
              <EmptyState
                title={t("my.notApplicableTitle")}
                description={t("my.notApplicableDescription")}
              />
            </Paper>
          ) : qualificationRows.length === 0 ? (
            <Paper variant="outlined" sx={{ mt: 1 }}>
              <EmptyState
                title={t("my.noQualificationsTitle")}
                description={t("my.noQualificationsDescription")}
              />
            </Paper>
          ) : (
            <Paper variant="outlined" sx={{ mt: 1 }}>
              <TableContainer>
                <Table size="small" aria-label={t("my.qualificationsHeading")}>
                  <TableHead>
                    <TableRow>
                      <TableCell sx={tableHeadSx}>{t("my.testColumn")}</TableCell>
                      <TableCell sx={tableHeadSx}>{t("my.statusColumn")}</TableCell>
                      <TableCell sx={tableHeadSx}>{t("my.expiryColumn")}</TableCell>
                      <TableCell sx={tableHeadSx}>{t("my.sopColumn")}</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {qualificationRows.map((row) => (
                      <TableRow key={row.testDefinitionId} hover>
                        <TableCell component="th" scope="row">
                          <Typography variant="body2" sx={{ fontWeight: 700 }}>
                            <Ltr>{row.code}</Ltr>
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            {row.displayName}
                          </Typography>
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={row.status} />
                        </TableCell>
                        <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>
                          {row.expiresOn ? formatLabCalendarDate(row.expiresOn) : t("matrix.noExpiry")}
                        </TableCell>
                        <TableCell>
                          {row.sop
                            ? t("matrix.sopPrefix", {
                                code: row.sop.documentCode,
                                rev: row.sop.revisionNumber
                              })
                            : "—"}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </Paper>
          )}
        </Box>

        <Box component="section" aria-label={t("my.sopsHeading")}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
            <SectionTitle>{t("my.sopsHeading")}</SectionTitle>
            <Button
              component={Link}
              to={APP_ROUTES.DOCUMENT_CONTROL_MY_READING_LIST}
              variant="outlined"
              size="small"
            >
              {t("my.openReadingList")}
            </Button>
          </Box>

          {loading ? (
            <Paper variant="outlined" sx={{ p: 2, mt: 1 }}>
              <Skeleton variant="rectangular" height={100} />
            </Paper>
          ) : pendingSops.length === 0 ? (
            <Paper variant="outlined" sx={{ mt: 1 }}>
              <EmptyState title={t("my.noSopsTitle")} description={t("my.noSopsDescription")} />
            </Paper>
          ) : (
            <Paper variant="outlined" sx={{ mt: 1 }}>
              <TableContainer>
                <Table size="small" aria-label={t("my.sopsHeading")}>
                  <TableBody>
                    {pendingSops.map((sop) => (
                      <TableRow key={sop.id} hover>
                        <TableCell component="th" scope="row">
                          <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            <Ltr>{sop.companyDocumentCode}</Ltr> — {sop.documentTitle}
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            {t("matrix.sopPrefix", {
                              code: sop.companyDocumentCode,
                              rev: sop.revisionNumber
                            })}
                          </Typography>
                        </TableCell>
                        <TableCell>
                          <StatusBadge status={sop.isOverdue ? "Overdue" : sop.status} />
                        </TableCell>
                        <TableCell sx={{ fontVariantNumeric: "tabular-nums" }}>
                          {sop.dueDateUtc ? t("my.dueDate", { date: formatLabCalendarDate(sop.dueDateUtc) }) : "—"}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TableContainer>
            </Paper>
          )}
        </Box>
      </Stack>
    </LabPage>
  );
}
