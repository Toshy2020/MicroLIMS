import { useCallback, useEffect, useState } from "react";
import { Alert, Button, Paper, Stack, Typography } from "@mui/material";
import { LabPage, RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { SectionTitle } from "../../../components/SectionTitle";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { masterDataWorkbookService as api } from "./masterDataWorkbookService";
import { WorkbookReportTable } from "./WorkbookReportTable";
import { canLoad } from "./types";
import type { MasterDataImport, WorkbookReport } from "./types";

// Master data workbook: download, check (dry run), load (signed) and
// second-person verify. Every rule is on the server; this page only
// shows its report.
export function MasterDataWorkbookPage() {
  const [file, setFile] = useState<File | null>(null);
  const [report, setReport] = useState<WorkbookReport | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [imports, setImports] = useState<MasterDataImport[]>([]);
  const [signing, setSigning] = useState<"load" | MasterDataImport | null>(null);
  const [comment, setComment] = useState("");

  const refresh = useCallback(() => api.imports().then(setImports), []);
  useEffect(() => { void refresh(); }, [refresh]);

  const check = async () => {
    if (!file) return;
    setBusy(true); setMessage(null);
    try { setReport(await api.validate(file)); }
    catch (e: any) { setMessage({ text: e?.response?.data?.message ?? "The file could not be checked.", ok: false }); }
    finally { setBusy(false); }
  };

  const sign = async (password: string) => {
    if (signing === "load" && file) {
      const result = await api.load(file, password, comment);
      setReport(result.report);
      if (result.committed) {
        setMessage({ text: "Workbook loaded. A second person must now verify it.", ok: true });
        setFile(null);
      } else {
        setMessage({ text: "Nothing was loaded: the workbook has problems to fix.", ok: false });
      }
    } else if (signing && signing !== "load") {
      await api.verify(signing.id, password, comment);
      setMessage({ text: `Load of ${signing.fileName} verified. Its items are now active.`, ok: true });
    }
    setSigning(null); setComment("");
    await refresh();
  };

  const columns: RegisterColumn<MasterDataImport>[] = [
    { key: "importedAt", label: "Loaded", sortable: true, render: (i) => new Date(i.importedAt).toLocaleString() },
    { key: "importedBy", label: "Loaded by", sortable: true },
    { key: "fileName", label: "File", render: (i) => <Button size="small" onClick={() => api.downloadFile(i)}>{i.fileName}</Button> },
    { key: "sheets", label: "New records", render: (i) => i.sheets.reduce((n, s) => n + s.created, 0) },
    { key: "status", label: "Status", render: (i) => (i.status === "Verified" ? `Verified by ${i.verifiedBy}` : "Waiting for verification") },
    {
      key: "actions", label: "",
      render: (i) => i.status === "Loaded" ? <Button size="small" variant="outlined" onClick={() => setSigning(i)}>Verify</Button> : null,
    },
  ];

  const verifyStatement = (i: MasterDataImport) =>
    i.itemsWithoutSpecifications.length > 0
      ? `I verified this load against the approved source documents. Note: these items have no specifications yet and will become usable: ${i.itemsWithoutSpecifications.join(", ")}.`
      : "I verified this load against the approved source documents.";

  return (
    <LabPage
      title="Master Data Workbook"
      subtitle="Load organisms, departments, rooms, water points, material master, media products, equipment, items and specifications from one Excel workbook."
      actions={<Button variant="outlined" onClick={() => api.download()}>Download workbook</Button>}
    >
      <Stack spacing={3}>
        {message && <Alert severity={message.ok ? "success" : "error"} onClose={() => setMessage(null)}>{message.text}</Alert>}

        <Paper sx={{ p: 2 }}>
          <SectionTitle>1. Check the workbook</SectionTitle>
          <Stack direction="row" spacing={2} sx={{ mt: 1, alignItems: "center", flexWrap: "wrap" }}>
            <Button variant="outlined" component="label">
              Choose file
              <input hidden type="file" accept=".xlsx" onChange={(e) => { setFile(e.target.files?.[0] ?? null); setReport(null); }} />
            </Button>
            <Typography variant="body2">{file?.name ?? "No file chosen"}</Typography>
            <Button variant="contained" disabled={!file || busy} onClick={check}>{busy ? "Checking..." : "Check file"}</Button>
            <Button variant="contained" color="success" disabled={!canLoad(report) || busy} onClick={() => setSigning("load")}>Load and sign</Button>
          </Stack>
          {report && <Stack sx={{ mt: 2 }}><WorkbookReportTable report={report} /></Stack>}
        </Paper>

        <Paper sx={{ p: 2 }}>
          <SectionTitle>2. Loads and verification</SectionTitle>
          <RegisterTable rows={imports} columns={columns} getRowId={(i) => i.id}
            empty={{ title: "No workbook loads yet", description: "Check a workbook above, then load and sign it." }} />
        </Paper>
      </Stack>

      <SignatureDialog
        open={signing !== null}
        meaningStatement={signing === "load" || signing === null
          ? "I am loading this master data workbook into MicroLIMS."
          : verifyStatement(signing)}
        showComment
        comment={comment}
        onCommentChange={setComment}
        onCancel={() => { setSigning(null); setComment(""); }}
        onConfirm={sign}
      />
    </LabPage>
  );
}
