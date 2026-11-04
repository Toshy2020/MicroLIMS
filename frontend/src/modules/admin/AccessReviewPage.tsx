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

const formatDate = (iso: string | null) => (iso ? new Date(iso).toLocaleString() : "Never");
const messageOf = (e: any, fallback: string): string => e?.response?.data?.message ?? fallback;

type StatusFilter = "All" | "Active" | "Inactive";

// Periodic access review (EU GMP Annex 11 s.12, 21 CFR 11.10(d)(g)). The server
// builds the report and enforces every rule; this page shows the stored report,
// collects findings while the review is a draft, and collects the signature.
// A requested change is made in Users or Roles, never here.
export function AccessReviewPage() {
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
      .catch(() => setError("Could not load the access reviews. Reload the page to try again."));
  }, []);
  useEffect(loadList, [loadList]);

  const open = (id: number) => {
    AccessReviewService.get(id).then(setDetail).catch((e) => toast.error(messageOf(e, "Could not open the review.")));
  };

  const generate = async () => {
    setGenerateError(null);
    try {
      const created = await AccessReviewService.generate(periodLabel.trim(), reason.trim());
      toast.success("Access review generated as a draft.");
      setGenerating(false); setPeriodLabel(""); setReason("");
      setDetail(created);
      loadList();
    } catch (e) {
      setGenerateError(messageOf(e, "Could not generate the review."));
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
      toast.success("Finding recorded.");
      setFindingFor(null);
      open(detail.id);
      loadList();
    } catch (e) {
      setFindingError(messageOf(e, "Could not record the finding."));
    }
  };

  const sign = async (password: string) => {
    if (!detail) return;
    // A refused password or missing conclusion throws back into the dialog.
    const signed = await AccessReviewService.sign(detail.id, password, conclusion);
    toast.success("Access review signed.");
    setSigning(false); setConclusion("");
    setDetail(signed);
    loadList();
  };

  const exportCsv = () => {
    if (!detail) return;
    AccessReviewService.exportCsv(detail.id).catch((e) => toast.error(messageOf(e, "Could not export the review.")));
  };

  const isDraft = detail?.status === "Draft";

  return (
    <>
      <PageHeader
        title="Access Review"
        subtitle="Periodic review of who holds which access. The report is fixed when generated; the reviewer records findings and signs it."
      />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      <Paper sx={{ mb: 2, p: 2 }}>
        <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1 }}>
          <Typography variant="h6" component="h2">Reviews</Typography>
          <Button variant="contained" onClick={() => { setGenerating(true); setGenerateError(null); }}>Generate review</Button>
        </Stack>
        {!reviews && !error && <LoadingSpinner />}
        {reviews && reviews.length === 0 && <Typography variant="body2" color="text.secondary">No access review has been generated yet.</Typography>}
        {reviews && reviews.length > 0 && (
          <TableContainer>
            <Table size="small" aria-label="Access reviews">
              <TableHead>
                <TableRow>
                  <TableCell>Period</TableCell>
                  <TableCell>Generated</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Flags</TableCell>
                  <TableCell>Findings</TableCell>
                  <TableCell>Signed by</TableCell>
                  <TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {reviews.map((r) => (
                  <TableRow key={r.id} selected={detail?.id === r.id}>
                    <TableCell>{r.periodLabel}</TableCell>
                    <TableCell>{formatDate(r.generatedAt)}{r.generatedByName ? ` by ${r.generatedByName}` : ""}</TableCell>
                    <TableCell><Chip size="small" color={r.status === "Signed" ? "success" : "warning"} label={r.status} /></TableCell>
                    <TableCell>
                      {r.summary.dormantUsers} dormant, {r.summary.neverLoggedInUsers} never signed in, {r.summary.sodConflictUsers} SoD
                    </TableCell>
                    <TableCell>{r.findingCount}</TableCell>
                    <TableCell>{r.reviewerName ? `${r.reviewerName}, ${formatDate(r.signedAt)}` : ""}</TableCell>
                    <TableCell align="right">
                      <Button size="small" aria-label={`Open ${r.periodLabel}`} onClick={() => open(r.id)}>Open</Button>
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
              <Button variant="outlined" onClick={exportCsv}>Export CSV</Button>
              {isDraft && <Button variant="contained" onClick={() => setSigning(true)}>Sign review</Button>}
            </Stack>
          </Stack>

          {!detail.snapshotIntact && (
            <Alert severity="error" sx={{ mb: 1 }}>The stored report no longer matches its checksum. Do not rely on it.</Alert>
          )}
          <Typography variant="caption" color="text.secondary" sx={{ display: "block", mb: 1 }}>
            Generated {formatDate(detail.generatedAt)}{detail.generatedByName ? ` by ${detail.generatedByName}` : ""}. Dormant after {detail.snapshot.dormantDays} days.
            Checksum (SHA-256) {detail.snapshotSha256}
          </Typography>
          {detail.status === "Signed" && (
            <Alert severity="success" sx={{ mb: 1 }}>
              Signed by {detail.reviewerName} on {formatDate(detail.signedAt)}. Conclusion: {detail.conclusion}
            </Alert>
          )}

          <Stack direction="row" spacing={1} sx={{ mb: 2, flexWrap: "wrap", gap: 1 }}>
            <Chip label={`${detail.snapshot.summary.totalUsers} accounts`} />
            <Chip label={`${detail.snapshot.summary.activeUsers} active`} />
            <Chip label={`${detail.snapshot.summary.inactiveUsers} inactive`} />
            <Chip color={detail.snapshot.summary.dormantUsers ? "warning" : "default"} label={`${detail.snapshot.summary.dormantUsers} dormant`} />
            <Chip color={detail.snapshot.summary.neverLoggedInUsers ? "warning" : "default"} label={`${detail.snapshot.summary.neverLoggedInUsers} never signed in`} />
            <Chip color={detail.snapshot.summary.sodConflictUsers ? "error" : "default"} label={`${detail.snapshot.summary.sodConflictUsers} SoD conflicts`} />
          </Stack>

          <Stack direction="row" spacing={2} sx={{ mb: 1, alignItems: "center", flexWrap: "wrap" }}>
            <TextField size="small" label="Search accounts" value={search} onChange={(e) => setSearch(e.target.value)} />
            <TextField size="small" select label="Status" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as StatusFilter)} sx={{ minWidth: 120 }}>
              <MenuItem value="All">All</MenuItem>
              <MenuItem value="Active">Active</MenuItem>
              <MenuItem value="Inactive">Inactive</MenuItem>
            </TextField>
            <FormControlLabel control={<Switch checked={flaggedOnly} onChange={(e) => setFlaggedOnly(e.target.checked)} />} label="Flagged only" />
          </Stack>

          <TableContainer>
            <Table size="small" aria-label="Accounts in the report">
              <TableHead>
                <TableRow>
                  <TableCell>Account</TableCell>
                  <TableCell>Status</TableCell>
                  <TableCell>Role</TableCell>
                  <TableCell>Permissions</TableCell>
                  <TableCell>Sections</TableCell>
                  <TableCell>Last sign-in</TableCell>
                  <TableCell>Status changes (12 months)</TableCell>
                  <TableCell>Flags</TableCell>
                  <TableCell align="right">Findings</TableCell>
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
                      </TableCell>
                      <TableCell>{u.isActive ? "Active" : `Inactive${u.deactivatedAt ? ` since ${formatDate(u.deactivatedAt)}` : ""}`}</TableCell>
                      <TableCell>{u.roleName} <Typography variant="caption" color="text.secondary" component="span">({u.roleType})</Typography></TableCell>
                      <TableCell sx={{ maxWidth: 280 }}>
                        <Typography variant="caption">
                          {u.permissions.map((p) => (p.hidden ? `${p.code} (hidden)` : p.code)).join(", ") || "None"}
                        </Typography>
                      </TableCell>
                      <TableCell>
                        {u.sections.map((s) => (s.sectionName ? `${s.departmentName} / ${s.sectionName}` : `${s.departmentName} (all)`)).join(", ") || "None"}
                      </TableCell>
                      <TableCell>{formatDate(u.lastSuccessfulLogin)}</TableCell>
                      <TableCell>
                        {u.statusHistory.length === 0 ? "None" : u.statusHistory.map((h) => `${new Date(h.timestamp).toLocaleDateString()} ${h.action}`).join("; ")}
                      </TableCell>
                      <TableCell>
                        <Stack direction="row" spacing={0.5} sx={{ flexWrap: "wrap", gap: 0.5 }}>
                          {u.sodConflicts.map((c) => <Chip key={c} size="small" color="error" label={`SoD: ${c}`} />)}
                          {u.dormant && <Chip size="small" color="warning" label="Dormant" />}
                          {u.neverLoggedIn && <Chip size="small" color="warning" label="Never signed in" />}
                        </Stack>
                      </TableCell>
                      <TableCell align="right">
                        {own.length}
                        {isDraft && (
                          <Button size="small" aria-label={`Add finding for ${u.username}`} onClick={() => openFinding(u)}>Add finding</Button>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>

          <Box sx={{ mt: 2 }}>
            <Typography variant="subtitle1" component="h3">Findings</Typography>
            {detail.findings.length === 0 ? (
              <Typography variant="body2" color="text.secondary">No findings recorded.</Typography>
            ) : (
              <Table size="small" aria-label="Findings">
                <TableHead>
                  <TableRow>
                    <TableCell>Account</TableCell><TableCell>Kind</TableCell><TableCell>Detail</TableCell><TableCell>Action</TableCell><TableCell>Note</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {detail.findings.map((f) => (
                    <TableRow key={f.id}>
                      <TableCell>{f.username}</TableCell>
                      <TableCell>{f.kind}</TableCell>
                      <TableCell>{f.detail}</TableCell>
                      <TableCell>{f.action === "ChangeRequested" ? "Change requested" : "No change"}</TableCell>
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
        title="Generate access review"
        actions={
          <>
            <Button onClick={() => setGenerating(false)}>Cancel</Button>
            <Button variant="contained" onClick={generate} disabled={!reason.trim()}>Generate</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Typography variant="body2">Takes a snapshot of every account, active or not. The snapshot cannot be changed afterwards.</Typography>
          {generateError && <Alert severity="error">{generateError}</Alert>}
          <TextField label="Period label" size="small" value={periodLabel} onChange={(e) => setPeriodLabel(e.target.value)} placeholder="2026 Q4" />
          <TextField label="Reason" size="small" required multiline minRows={2} value={reason} onChange={(e) => setReason(e.target.value)} />
        </Stack>
      </FloatingDialog>

      <FloatingDialog
        open={Boolean(findingFor)}
        onClose={() => setFindingFor(null)}
        maxWidth="xs"
        title={`Finding for ${findingFor?.username ?? ""}`}
        actions={
          <>
            <Button onClick={() => setFindingFor(null)}>Cancel</Button>
            <Button variant="contained" onClick={saveFinding} disabled={!findingDetail.trim() || (action === "ChangeRequested" && !note.trim())}>Record finding</Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          {findingError && <Alert severity="error">{findingError}</Alert>}
          <TextField select size="small" label="Kind" value={kind} onChange={(e) => setKind(e.target.value)}>
            {FINDING_KINDS.map((k) => <MenuItem key={k} value={k}>{k}</MenuItem>)}
          </TextField>
          <TextField label="Detail" size="small" required multiline minRows={2} value={findingDetail} onChange={(e) => setFindingDetail(e.target.value)} />
          <TextField select size="small" label="Action" value={action} onChange={(e) => setAction(e.target.value as FindingAction)}>
            <MenuItem value="NoChange">No change</MenuItem>
            <MenuItem value="ChangeRequested">Change requested</MenuItem>
          </TextField>
          <TextField label="Note" size="small" required={action === "ChangeRequested"} multiline minRows={2} value={note} onChange={(e) => setNote(e.target.value)}
            helperText={action === "ChangeRequested" ? "Describe the change; it is made in Users or Roles with its own reason and signature." : undefined} />
        </Stack>
      </FloatingDialog>

      <SignatureDialog
        open={signing}
        meaningStatement="I have reviewed this access review report and its findings and attest to the conclusion below."
        showComment
        commentLabel="Conclusion"
        comment={conclusion}
        onCommentChange={setConclusion}
        onCancel={() => setSigning(false)}
        onConfirm={sign}
      />
    </>
  );
}
