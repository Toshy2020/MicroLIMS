import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, Chip, FormControlLabel, MenuItem, Paper, Stack, Switch, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, TextField, Typography
} from "@mui/material";
import { toast } from "sonner";
import { PageHeader } from "../../components/PageHeader";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { FloatingDialog } from "../../components/FloatingDialog";
import { SignatureDialog } from "../../components/SignatureDialog";
import {
  AccessReviewDetail, AccessReviewListItem, AccessReviewService, AccessReviewUserEntry, FINDING_KINDS, FindingAction
} from "./services/AccessReviewService";
import { getErrorMessage, serverMessage } from "../../utils/errorMessage";
import { formatAccessReviewMfaState } from "../../utils/mfaStateDisplay";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";
import { Trans } from "react-i18next";
import { Ltr } from "../../components/Ltr";
import { i18n, useTranslation } from "../../i18n";

const tl = (key: string, options?: Record<string, unknown>): string => i18n.t(key, { ns: "admin", ...options });
const messageOf = (e: unknown, fallback: string): string => serverMessage(e) ?? getErrorMessage(e, fallback);

type StatusFilter = "All" | "Active" | "Inactive";

// Periodic access review (EU GMP Annex 11 s.12, 21 CFR 11.10(d)(g)). The server
// builds the report and enforces every rule; this page shows the stored report,
// collects findings while the review is a draft, and collects the signature.
// A requested change is made in Users or Roles, never here.
export function AccessReviewPage() {
  const { t } = useTranslation("admin");
  const { t: tc } = useTranslation("common");
  const formatDate = (iso: string | null) => (iso ? formatDateTimeWithTimeZone(iso) : t("common.never"));
  const dateBy = (iso: string | null, name: string | null | undefined) => (name ? t("accessReview.dateBy", { date: formatDate(iso), name }) : formatDate(iso));
  const [reviews, setReviews] = useState<AccessReviewListItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [detail, setDetail] = useState<AccessReviewDetail | null>(null);

  const [generating, setGenerating] = useState(false);
  const [periodLabel, setPeriodLabel] = useState("");
  const [reason, setReason] = useState("");
  const [generateError, setGenerateError] = useState<string | null>(null);

  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("All");
  const [flaggedOnly, setFlaggedOnly] = useState(false);

  const [findingFor, setFindingFor] = useState<AccessReviewUserEntry | null>(null);
  const [kind, setKind] = useState<string>("Other");
  const [findingDetail, setFindingDetail] = useState("");
  const [action, setAction] = useState<FindingAction>("NoChange");
  const [note, setNote] = useState("");
  const [findingError, setFindingError] = useState<string | null>(null);

  const [signing, setSigning] = useState(false);
  const [conclusion, setConclusion] = useState("");

  const loadList = useCallback(() => {
    AccessReviewService.list()
      .then(setReviews)
      .catch(() => setError(tl("accessReview.loadError")));
  }, []);
  useEffect(loadList, [loadList]);

  const open = (id: number) => {
    AccessReviewService.get(id).then(setDetail).catch((e) => toast.error(messageOf(e, t("accessReview.openError"))));
  };

  const generate = async () => {
    setGenerateError(null);
    try {
      const created = await AccessReviewService.generate(periodLabel.trim(), reason.trim());
      toast.success(t("accessReview.generated"));
      setGenerating(false); setPeriodLabel(""); setReason("");
      setDetail(created);
      loadList();
    } catch (e) {
      setGenerateError(messageOf(e, t("accessReview.generateError")));
    }
  };

  const isFlagged = (u: AccessReviewUserEntry) => u.dormant || u.neverLoggedIn || u.sodConflicts.length > 0;

  const visibleUsers = useMemo(() => {
    if (!detail) return [];
    const q = search.trim().toLowerCase();
    return detail.snapshot.users.filter((u) =>
      (statusFilter === "All" || (statusFilter === "Active") === u.isActive)
      && (!flaggedOnly || isFlagged(u))
      && (!q || u.username.toLowerCase().includes(q) || u.fullName.toLowerCase().includes(q) || u.roleName.toLowerCase().includes(q)));
  }, [detail, search, statusFilter, flaggedOnly]);

  const openFinding = (u: AccessReviewUserEntry) => {
    setFindingFor(u); setFindingError(null); setFindingDetail(""); setNote(""); setAction("NoChange");
    setKind(u.sodConflicts.length > 0 ? "SodConflict" : u.dormant ? "Dormant" : u.neverLoggedIn ? "NeverLoggedIn" : "Other");
  };

  const saveFinding = async () => {
    if (!detail || !findingFor) return;
    try {
      await AccessReviewService.addFinding(detail.id, {
        userId: findingFor.userId, kind, detail: findingDetail.trim(), action, note: note.trim() || undefined
      });
      toast.success(t("accessReview.findingRecorded"));
      setFindingFor(null);
      open(detail.id);
      loadList();
    } catch (e) {
      setFindingError(messageOf(e, t("accessReview.findingError")));
    }
  };

  const sign = async (password: string) => {
    if (!detail) return;
    // A refused password or missing conclusion throws back into the dialog.
    const signed = await AccessReviewService.sign(detail.id, password, conclusion);
    toast.success(t("accessReview.signed"));
    setSigning(false); setConclusion("");
    setDetail(signed);
    loadList();
  };

  const exportCsv = () => {
    if (!detail) return;
    AccessReviewService.exportCsv(detail.id).catch((e) => toast.error(messageOf(e, t("accessReview.exportError"))));
  };

  const isDraft = detail?.status === "Draft";
  const canDecide = Boolean(isDraft && detail?.canDecide);

  return (
    <>
      <PageHeader
        title={t("accessReview.title")}
        subtitle={t("accessReview.subtitle")}
      />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Paper sx={{ mb: 2, p: 2 }}>
        <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
          <Typography variant="h6" component="h2">{t("accessReview.reviews")}</Typography>
          <Button variant="contained" onClick={() => { setGenerating(true); setGenerateError(null); }}>{t("accessReview.generate")}</Button>
        </Stack>
        {!reviews && !error && <LoadingSpinner />}
        {reviews && reviews.length === 0 && <Typography variant="body2" color="text.secondary">{t("accessReview.noReviews")}</Typography>}
        {reviews && reviews.length > 0 && (
          <TableContainer>
            <Table size="small" aria-label={t("accessReview.reviewsTable")}>
              <TableHead>
                <TableRow>
                  <TableCell>{t("accessReview.period")}</TableCell>
                  <TableCell>{t("accessReview.generatedCol")}</TableCell>
                  <TableCell>{t("common.status")}</TableCell>
                  <TableCell>{t("accessReview.flags")}</TableCell>
                  <TableCell>{t("accessReview.findings")}</TableCell>
                  <TableCell>{t("accessReview.signedBy")}</TableCell>
                  <TableCell align="right">{t("common.actions")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {reviews.map((r) => (
                  <TableRow key={r.id} selected={detail?.id === r.id}>
                    <TableCell>{r.periodLabel}</TableCell>
                    <TableCell>{dateBy(r.generatedAt, r.generatedByName)}</TableCell>
                    <TableCell><Chip size="small" color={r.status === "Signed" ? "success" : "warning"} label={r.status} /></TableCell>
                    <TableCell>
                      {t("accessReview.flagsSummary", { dormant: r.summary.dormantUsers, never: r.summary.neverLoggedInUsers, sod: r.summary.sodConflictUsers })}
                    </TableCell>
                    <TableCell>{r.findingCount}</TableCell>
                    <TableCell>{r.reviewerName ? t("accessReview.signedByLine", { name: r.reviewerName, date: formatDate(r.signedAt) }) : ""}</TableCell>
                    <TableCell align="right">
                      <Button size="small" aria-label={t("accessReview.openAria", { period: r.periodLabel })} onClick={() => open(r.id)}>{t("accessReview.open")}</Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Paper>

      {detail && (
        <Paper sx={{ mb: 2, p: 2 }}>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1, mb: 1 }}>
            <Typography variant="h6" component="h2">
              {detail.periodLabel} <Chip size="small" color={isDraft ? "warning" : "success"} label={detail.status} sx={{ ml: 1 }} />
            </Typography>
            <Stack direction="row" spacing={1}>
              <Button variant="outlined" onClick={exportCsv}>{t("accessReview.exportCsv")}</Button>
              {canDecide && <Button variant="contained" onClick={() => setSigning(true)}>{t("accessReview.signReview")}</Button>}
            </Stack>
          </Stack>

          {isDraft && !detail.canDecide && (
            <Alert severity="info" sx={{ mb: 1 }}>
              {t("accessReview.qaOnly")}
            </Alert>
          )}

          {!detail.snapshotIntact && (
            <Alert severity="error" sx={{ mb: 1 }}>{t("accessReview.checksumBad")}</Alert>
          )}
          <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1 }}>
            <Trans
              i18nKey="accessReview.generatedLine"
              ns="admin"
              values={{ generated: dateBy(detail.generatedAt, detail.generatedByName), days: detail.snapshot.dormantDays, sha: detail.snapshotSha256 }}
              components={{ ltr: <Ltr>{null}</Ltr> }}
            />
          </Typography>
          {detail.status === "Signed" && (
            <Alert severity="success" sx={{ mb: 1 }}>
              {t("accessReview.signedAlert", { name: detail.reviewerName ?? "", date: formatDate(detail.signedAt), conclusion: detail.conclusion ?? "" })}
            </Alert>
          )}

          <Stack direction="row" spacing={1} sx={{ mb: 2, flexWrap: "wrap", gap: 1 }}>
            <Chip label={t("accessReview.chipAccounts", { n: detail.snapshot.summary.totalUsers })} />
            <Chip label={t("accessReview.chipActive", { n: detail.snapshot.summary.activeUsers })} />
            <Chip label={t("accessReview.chipInactive", { n: detail.snapshot.summary.inactiveUsers })} />
            <Chip color={detail.snapshot.summary.dormantUsers ? "warning" : "default"} label={t("accessReview.chipDormant", { n: detail.snapshot.summary.dormantUsers })} />
            <Chip color={detail.snapshot.summary.neverLoggedInUsers ? "warning" : "default"} label={t("accessReview.chipNever", { n: detail.snapshot.summary.neverLoggedInUsers })} />
            <Chip color={detail.snapshot.summary.sodConflictUsers ? "error" : "default"} label={t("accessReview.chipSod", { n: detail.snapshot.summary.sodConflictUsers })} />
          </Stack>

          <Stack direction="row" spacing={2} sx={{ mb: 1, alignItems: "center", flexWrap: "wrap" }}>
            <TextField size="small" label={t("accessReview.searchAccounts")} value={search} onChange={(e) => setSearch(e.target.value)} />
            <TextField size="small" select label={t("common.status")} value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as StatusFilter)} sx={{ minWidth: 120 }}>
              <MenuItem value="All">{t("accessReview.all")}</MenuItem>
              <MenuItem value="Active">{t("common.active")}</MenuItem>
              <MenuItem value="Inactive">{t("common.inactive")}</MenuItem>
            </TextField>
            <FormControlLabel control={<Switch checked={flaggedOnly} onChange={(e) => setFlaggedOnly(e.target.checked)} />} label={t("accessReview.flaggedOnly")} />
          </Stack>

          <TableContainer>
            <Table size="small" aria-label={t("accessReview.accountsTable")}>
              <TableHead>
                <TableRow>
                  <TableCell>{t("accessReview.account")}</TableCell>
                  <TableCell>{t("common.status")}</TableCell>
                  <TableCell>{t("common.role")}</TableCell>
                  <TableCell>{t("accessReview.secondFactor")}</TableCell>
                  <TableCell>{t("accessReview.permissions")}</TableCell>
                  <TableCell>{t("accessReview.sections")}</TableCell>
                  <TableCell>{t("accessReview.lastSignIn")}</TableCell>
                  <TableCell>{t("accessReview.statusChanges")}</TableCell>
                  <TableCell>{t("accessReview.flags")}</TableCell>
                  <TableCell align="right">{t("accessReview.findings")}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {visibleUsers.map((u) => {
                  const own = detail.findings.filter((f) => f.userId === u.userId);
                  return (
                    <TableRow key={u.userId}>
                      <TableCell>
                        <Typography variant="body2" sx={{ fontWeight: 600 }}>{u.username}</Typography>
                        <Typography variant="caption" color="text.secondary">{u.fullName}</Typography>
                        {u.signInMethod && (
                          <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                            {u.signInMethod === "Directory" ? t("accessReview.companyLogin") : t("accessReview.laboratoryLogin")}{u.isEmergencyAccount ? t("accessReview.emergencySuffix") : ""}
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>{u.isActive ? t("common.active") : u.deactivatedAt ? t("accessReview.inactiveSince", { date: formatDate(u.deactivatedAt) }) : t("common.inactive")}</TableCell>
                      <TableCell>{u.roleName} <Typography variant="caption" color="text.secondary" component="span">({u.roleType})</Typography></TableCell>
                      <TableCell>{formatAccessReviewMfaState(u.mfaState)}</TableCell>
                      <TableCell sx={{ maxWidth: 280 }}>
                        <Typography variant="caption">
                          {u.permissions.map((p) => (p.hidden ? t("accessReview.hidden", { code: p.code }) : p.code)).join(", ") || t("common.none")}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        {u.sections.map((s) => (s.sectionName ? t("accessReview.sectionOf", { department: s.departmentName, section: s.sectionName }) : t("accessReview.sectionAll", { department: s.departmentName }))).join(", ") || t("common.none")}
                      </TableCell>
                      <TableCell>{formatDate(u.lastSuccessfulLogin)}</TableCell>
                      <TableCell>
                        {u.statusHistory.length === 0 ? t("common.none") : u.statusHistory.map((h) => `${formatDateTimeWithTimeZone(h.timestamp)} ${h.action}`).join("; ")}
                      </TableCell>
                      <TableCell>
                        <Stack direction="row" spacing={0.5} sx={{ flexWrap: "wrap", gap: 0.5 }}>
                          {u.sodConflicts.map((c) => <Chip key={c} size="small" color="error" label={t("accessReview.sodFlag", { conflict: c })} />)}
                          {u.dormant && <Chip size="small" color="warning" label={t("accessReview.dormant")} />}
                          {u.neverLoggedIn && <Chip size="small" color="warning" label={t("accessReview.neverSignedIn")} />}
                        </Stack>
                      </TableCell>
                      <TableCell align="right">
                        {own.length}
                        {canDecide && (
                          <Button size="small" aria-label={t("accessReview.addFindingFor", { username: u.username })} onClick={() => openFinding(u)}>{t("accessReview.addFinding")}</Button>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>

          <Box sx={{ mt: 2 }}>
            <Typography variant="subtitle1" component="h3">{t("accessReview.findings")}</Typography>
            {detail.findings.length === 0 ? (
              <Typography variant="body2" color="text.secondary">{t("accessReview.noFindings")}</Typography>
            ) : (
              <Table size="small" aria-label={t("accessReview.findings")}>
                <TableHead>
                  <TableRow>
                    <TableCell>{t("accessReview.account")}</TableCell><TableCell>{t("accessReview.kind")}</TableCell><TableCell>{t("accessReview.detail")}</TableCell><TableCell>{t("accessReview.action")}</TableCell><TableCell>{t("accessReview.note")}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {detail.findings.map((f) => (
                    <TableRow key={f.id}>
                      <TableCell>{f.username}</TableCell>
                      <TableCell>{f.kind}</TableCell>
                      <TableCell>{f.detail}</TableCell>
                      <TableCell>{f.action === "ChangeRequested" ? t("accessReview.changeRequested") : t("accessReview.noChange")}</TableCell>
                      <TableCell>{f.note}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            )}
          </Box>
        </Paper>
      )}

      <FloatingDialog
        open={generating}
        onClose={() => setGenerating(false)}
        maxWidth="xs"
        title={t("accessReview.generateTitle")}
        actions={
          <>
            <Button onClick={() => setGenerating(false)}>{tc("actions.cancel")}</Button>
            <Button variant="contained" onClick={generate} disabled={!reason.trim()}>{t("accessReview.generateButton")}</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography variant="body2">{t("accessReview.generateIntro")}</Typography>
          {generateError && <Alert severity="error">{generateError}</Alert>}
          <TextField label={t("accessReview.periodLabel")} size="small" value={periodLabel} onChange={(e) => setPeriodLabel(e.target.value)} placeholder="2026 Q4" />
          <TextField label={t("common.reason")} size="small" required multiline minRows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
        </Stack>
      </FloatingDialog>

      <FloatingDialog
        open={Boolean(findingFor)}
        onClose={() => setFindingFor(null)}
        maxWidth="xs"
        title={t("accessReview.findingFor", { username: findingFor?.username ?? "" })}
        actions={
          <>
            <Button onClick={() => setFindingFor(null)}>{tc("actions.cancel")}</Button>
            <Button variant="contained" onClick={saveFinding} disabled={!findingDetail.trim() || (action === "ChangeRequested" && !note.trim())}>{t("accessReview.recordFinding")}</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          {findingError && <Alert severity="error">{findingError}</Alert>}
          <TextField select size="small" label={t("accessReview.kind")} value={kind} onChange={(e) => setKind(e.target.value)}>
            {FINDING_KINDS.map((k) => <MenuItem key={k} value={k}>{k}</MenuItem>)}
          </TextField>
          <TextField label={t("accessReview.detail")} size="small" required multiline minRows={2} value={findingDetail} onChange={(e) => setFindingDetail(e.target.value)} />
          <TextField select size="small" label={t("accessReview.action")} value={action} onChange={(e) => setAction(e.target.value as FindingAction)}>
            <MenuItem value="NoChange">{t("accessReview.noChange")}</MenuItem>
            <MenuItem value="ChangeRequested">{t("accessReview.changeRequested")}</MenuItem>
          </TextField>
          <TextField label={t("accessReview.note")} size="small" required={action === "ChangeRequested"} multiline minRows={2} value={note} onChange={(e) => setNote(e.target.value)}
            helperText={action === "ChangeRequested" ? t("accessReview.noteHelper") : undefined} />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement={i18n.getFixedT("en", "admin")("accessReview.signatureMeaning")}
        meaningStatementTranslation={t("accessReview.signatureMeaning")}
        showComment
        commentLabel={t("accessReview.conclusion")}
        comment={conclusion}
        onCommentChange={setConclusion}
        onCancel={() => setSigning(false)}
        onConfirm={sign}
      />
    </>
  );
}
