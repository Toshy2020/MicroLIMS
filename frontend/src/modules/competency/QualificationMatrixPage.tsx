import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Box,
  Button,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import { LabPage, EmptyState, PartialOutageBanner } from "../../components/lab";
import { StatusBadge } from "../../components/StatusBadge";
import { Ltr } from "../../components/Ltr";
import { useAuth } from "../../contexts/AuthContext";
import { useTranslation } from "../../i18n";
import { PERMISSIONS } from "../../routes/routes";
import { tableHeadSx } from "../../theme";
import {
  DEFAULT_MATRIX_CELL_STATUS,
  competencyService,
  type AnalystQualificationDto,
  type CompetencyMatrixDto,
  type MatrixCellStatus
} from "../../services/competencyService";
import {
  laboratorySectionService,
  type LaboratorySection
} from "../../services/laboratorySectionService";
import { QualifyDialog } from "./QualifyDialog";
import { WithdrawDialog } from "./WithdrawDialog";
import { GateSettingsCard } from "./GateSettingsCard";

const CELL_STATUSES: MatrixCellStatus[] = [
  "Qualified",
  "ExpiringSoon",
  "Expired",
  "Withdrawn",
  "NotQualified"
];

interface QualifyTarget {
  userId: number;
  analystName: string;
  testDefinitionId: number;
  testLabel: string;
}

interface WithdrawTarget {
  qualificationId: number;
  analystName: string;
  testLabel: string;
}

export function QualificationMatrixPage() {
  const { t } = useTranslation("competency");
  const { permissions } = useAuth();
  const canManage = permissions.includes(PERMISSIONS.COMPETENCY_MANAGE);

  const [sections, setSections] = useState<LaboratorySection[]>([]);
  const [sectionId, setSectionId] = useState<number | "">("");
  const [sectionsFailed, setSectionsFailed] = useState(false);
  const [settingsFailed, setSettingsFailed] = useState(false);
  const [matrixFailed, setMatrixFailed] = useState(false);

  const [analystSearch, setAnalystSearch] = useState("");
  const [testSearch, setTestSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<MatrixCellStatus | "ALL">("ALL");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  const [matrix, setMatrix] = useState<CompetencyMatrixDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [qualifyTarget, setQualifyTarget] = useState<QualifyTarget | null>(null);
  const [withdrawTarget, setWithdrawTarget] = useState<WithdrawTarget | null>(null);
  const [sessionQualIds, setSessionQualIds] = useState<Record<string, number>>({});

  const loadSections = useCallback(async (signal?: AbortSignal) => {
    setSectionsFailed(false);
    try {
      const list = await laboratorySectionService.getMySections(signal);
      if (signal?.aborted) return;
      const items = list ?? [];
      setSections(items);
      if (items.length > 0) {
        setSectionId((prev) => (prev === "" ? items[0].sectionId : prev));
      } else {
        setLoading(false);
      }
    } catch {
      if (!signal?.aborted) {
        setSectionsFailed(true);
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    void loadSections(controller.signal);
    return () => controller.abort();
  }, [loadSections]);

  const loadMatrix = useCallback(
    async (signal?: AbortSignal) => {
      if (sectionId === "") return;
      setLoading(true);
      setMatrixFailed(false);
      try {
        const data = await competencyService.getMatrix(
          sectionId,
          {
            analystSearch: analystSearch.trim() || undefined,
            testSearch: testSearch.trim() || undefined,
            page,
            pageSize
          },
          signal
        );
        if (!signal?.aborted) setMatrix(data);
      } catch {
        if (!signal?.aborted) {
          setMatrixFailed(true);
          setMatrix(null);
        }
      } finally {
        if (!signal?.aborted) setLoading(false);
      }
    },
    [sectionId, analystSearch, testSearch, page, pageSize]
  );

  useEffect(() => {
    if (sectionId === "") return;
    const controller = new AbortController();
    void loadMatrix(controller.signal);
    return () => controller.abort();
  }, [sectionId, loadMatrix]);

  const filteredAnalysts = useMemo(() => {
    if (!matrix) return [];
    if (statusFilter === "ALL") return matrix.analysts;
    return matrix.analysts.filter((row) =>
      row.cells.some((cell) => cell.status === statusFilter)
    );
  }, [matrix, statusFilter]);

  const outageSources = useMemo(() => {
    const sources: string[] = [];
    if (sectionsFailed) sources.push(t("matrix.outageSections"));
    if (settingsFailed) sources.push(t("matrix.outageSettings"));
    if (matrixFailed) sources.push(t("matrix.outageMatrix"));
    return sources;
  }, [sectionsFailed, settingsFailed, matrixFailed, t]);

  const handleQualified = (qualification: AnalystQualificationDto) => {
    setSessionQualIds((prev) => ({
      ...prev,
      [`${qualification.userId}:${qualification.testDefinitionId}`]: qualification.id
    }));
    void loadMatrix();
  };

  const handleWithdrawn = (qualification: AnalystQualificationDto) => {
    setSessionQualIds((prev) => {
      const next = { ...prev };
      delete next[`${qualification.userId}:${qualification.testDefinitionId}`];
      return next;
    });
    void loadMatrix();
  };

  return (
    <LabPage
      title={t("matrix.title")}
      subtitle={t("matrix.subtitle")}
      filters={
        <Stack direction={{ xs: "column", md: "row" }} spacing={1.5} sx={{ alignItems: { md: "center" } }}>
          {sections.length > 0 && (
            <FormControl size="small" sx={{ minWidth: 200 }}>
              <InputLabel id="matrix-section-label">{t("matrix.sectionLabel")}</InputLabel>
              <Select
                labelId="matrix-section-label"
                label={t("matrix.sectionLabel")}
                value={sectionId}
                onChange={(e) => {
                  setSectionId(Number(e.target.value));
                  setPage(1);
                }}
              >
                {sections.map((s) => (
                  <MenuItem key={s.sectionId} value={s.sectionId}>
                    {s.sectionName}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          <TextField
            size="small"
            label={t("matrix.analystSearch")}
            value={analystSearch}
            onChange={(e) => {
              setAnalystSearch(e.target.value);
              setPage(1);
            }}
          />

          <TextField
            size="small"
            label={t("matrix.testSearch")}
            value={testSearch}
            onChange={(e) => {
              setTestSearch(e.target.value);
              setPage(1);
            }}
          />

          <FormControl size="small" sx={{ minWidth: 180 }}>
            <InputLabel id="matrix-status-filter-label">{t("matrix.statusFilter")}</InputLabel>
            <Select
              labelId="matrix-status-filter-label"
              label={t("matrix.statusFilter")}
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as MatrixCellStatus | "ALL")}
            >
              <MenuItem value="ALL">{t("matrix.allStatuses")}</MenuItem>
              {CELL_STATUSES.map((status) => (
                <MenuItem key={status} value={status}>
                  {status}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        </Stack>
      }
    >
      <PartialOutageBanner
        sources={outageSources}
        onRetry={() => {
          if (sectionsFailed) void loadSections();
          if (matrixFailed) void loadMatrix();
        }}
      />

      {canManage && typeof sectionId === "number" && (
        <GateSettingsCard
          sectionId={sectionId}
          onLoadError={setSettingsFailed}
          onChanged={() => void loadMatrix()}
        />
      )}

      <Typography variant="caption" color="text.secondary" aria-live="polite">
        {t("matrix.resultsCount", { count: filteredAnalysts.length })}
      </Typography>

      {loading ? (
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Stack spacing={1}>
            <Skeleton variant="rectangular" height={36} />
            <Skeleton variant="rectangular" height={48} />
            <Skeleton variant="rectangular" height={48} />
          </Stack>
        </Paper>
      ) : !matrix || filteredAnalysts.length === 0 || matrix.tests.length === 0 ? (
        <Paper variant="outlined">
          <EmptyState title={t("matrix.emptyTitle")} description={t("matrix.emptyDescription")} />
        </Paper>
      ) : (
        <Paper variant="outlined">
          <TableContainer>
            <Table size="small" aria-label={t("matrix.title")}>
              <TableHead>
                <TableRow>
                  <TableCell sx={tableHeadSx}>{t("matrix.analystColumn")}</TableCell>
                  {matrix.tests.map((test) => (
                    <TableCell key={test.testDefinitionId} sx={tableHeadSx}>
                      <Typography variant="body2" sx={{ fontWeight: 700 }}>
                        <Ltr>{test.code}</Ltr>
                      </Typography>
                      <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                        {test.displayName}
                      </Typography>
                      {test.sop && (
                        <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                          {t("matrix.sopPrefix", {
                            code: test.sop.documentCode,
                            rev: test.sop.revisionNumber
                          })}
                        </Typography>
                      )}
                    </TableCell>
                  ))}
                </TableRow>
              </TableHead>
              <TableBody>
                {filteredAnalysts.map((row) => {
                  const cellByTest = new Map(row.cells.map((c) => [c.testDefinitionId, c]));
                  return (
                    <TableRow key={row.userId} hover>
                      <TableCell sx={{ fontWeight: 600 }}>{row.analystName}</TableCell>
                      {matrix.tests.map((test) => {
                        const cell = cellByTest.get(test.testDefinitionId);
                        const status: MatrixCellStatus = cell?.status ?? DEFAULT_MATRIX_CELL_STATUS;
                        const sessionQualId = sessionQualIds[`${row.userId}:${test.testDefinitionId}`];
                        const testLabel = `${test.code} — ${test.displayName}`;
                        return (
                          <TableCell key={test.testDefinitionId}>
                            <Stack spacing={0.5} sx={{ alignItems: "flex-start" }}>
                              <StatusBadge status={status} />
                              {cell?.expiresOn && (
                                <Typography
                                  variant="caption"
                                  color="text.secondary"
                                  sx={{ fontVariantNumeric: "tabular-nums" }}
                                >
                                  <Ltr>{t("matrix.expiresOn", { date: cell.expiresOn })}</Ltr>
                                </Typography>
                              )}
                              {canManage && (
                                <Box sx={{ display: "flex", gap: 0.5 }}>
                                  <Button
                                    size="small"
                                    variant="text"
                                    sx={{ minWidth: 0, px: 0.5, py: 0, textTransform: "none", fontSize: 12 }}
                                    onClick={() =>
                                      setQualifyTarget({
                                        userId: row.userId,
                                        analystName: row.analystName,
                                        testDefinitionId: test.testDefinitionId,
                                        testLabel
                                      })
                                    }
                                  >
                                    {t("matrix.qualifyAction")}
                                  </Button>
                                  {sessionQualId && status !== "Withdrawn" && (
                                    <Button
                                      size="small"
                                      color="warning"
                                      variant="text"
                                      sx={{ minWidth: 0, px: 0.5, py: 0, textTransform: "none", fontSize: 12 }}
                                      onClick={() =>
                                        setWithdrawTarget({
                                          qualificationId: sessionQualId,
                                          analystName: row.analystName,
                                          testLabel
                                        })
                                      }
                                    >
                                      {t("matrix.withdrawAction")}
                                    </Button>
                                  )}
                                </Box>
                              )}
                            </Stack>
                          </TableCell>
                        );
                      })}
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>

          {matrix.totalCount > 25 && (
            <TablePagination
              component="div"
              count={matrix.totalCount}
              page={page - 1}
              rowsPerPage={pageSize}
              rowsPerPageOptions={[25, 50, 100]}
              onPageChange={(_, nextPage) => setPage(nextPage + 1)}
              onRowsPerPageChange={(e) => {
                setPageSize(Number(e.target.value));
                setPage(1);
              }}
            />
          )}
        </Paper>
      )}

      {qualifyTarget && (
        <QualifyDialog
          open
          userId={qualifyTarget.userId}
          analystName={qualifyTarget.analystName}
          testDefinitionId={qualifyTarget.testDefinitionId}
          testLabel={qualifyTarget.testLabel}
          onClose={() => setQualifyTarget(null)}
          onQualified={handleQualified}
        />
      )}

      {withdrawTarget && (
        <WithdrawDialog
          open
          qualificationId={withdrawTarget.qualificationId}
          analystName={withdrawTarget.analystName}
          testLabel={withdrawTarget.testLabel}
          onClose={() => setWithdrawTarget(null)}
          onWithdrawn={handleWithdrawn}
        />
      )}
    </LabPage>
  );
}
