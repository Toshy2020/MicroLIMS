import { useCallback, useEffect, useState } from "react";
import { i18n, useTranslation } from "../../../i18n";
import { Alert, Button, Paper, Stack, Typography } from "@mui/material";
import { LabPage, RegisterTable } from "../../../components/lab";
import type { RegisterColumn } from "../../../components/lab";
import { SectionTitle } from "../../../components/SectionTitle";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { ChangeControlNumberField, normaliseChangeControlNumber } from "../../../components/ChangeControlNumberField";
import { useAuth } from "../../../contexts/AuthContext";
import { masterDataWorkbookService as api } from "./masterDataWorkbookService";
import { WorkbookReportTable } from "./WorkbookReportTable";
import { canLoad } from "./types";
import type { MasterDataImport, WorkbookReport } from "./types";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";

const QUALITY_VIEW_PERMISSION = "Quality.View";

// Master data workbook: download, check (dry run), load (signed) and
// second-person verify. Every rule is on the server; this page only
// shows its report.
export function MasterDataWorkbookPage() {
  const { t } = useTranslation("labConfig");
  const { permissions } = useAuth();
  const [file, setFile] = useState<File | null>(null);
  const [report, setReport] = useState<WorkbookReport | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  const [imports, setImports] = useState<MasterDataImport[]>([]);
  const [signing, setSigning] = useState<"load" | MasterDataImport | null>(null);
  const [comment, setComment] = useState("");
  // Optional approved change control for a laboratory whose gate is on (CONF-227); sent with the import only.
  const [changeControlInput, setChangeControlInput] = useState("");
  const [importRefused, setImportRefused] = useState(false);
  const changeControl = normaliseChangeControlNumber(changeControlInput);
  const changeControlInvalid = changeControlInput.trim() !== "" && !changeControl.valid;

  const refresh = useCallback(() => api.imports().then(setImports), []);
  useEffect(() => { void refresh(); }, [refresh]);

  const check = async () => {
    if (!file) return;
    setBusy(true); setMessage(null);
    try { setReport(await api.validate(file)); }
    catch (e: any) { setMessage({ text: e?.response?.data?.message ?? t("masterDataWorkbook.checkError"), ok: false }); }
    finally { setBusy(false); }
  };

  const sign = async (password: string) => {
    if (signing === "load" && file) {
      const result = await api.load(file, password, comment, changeControl.valid ? changeControl.number : undefined);
      setReport(result.report);
      setImportRefused(!result.committed && result.report.hasErrors);
      if (result.committed) {
        setMessage({ text: t("masterDataWorkbook.loaded"), ok: true });
        setFile(null);
      } else {
        setMessage({ text: t("masterDataWorkbook.notLoaded"), ok: false });
      }
    } else if (signing && signing !== "load") {
      await api.verify(signing.id, password, comment);
      setMessage({ text: t("masterDataWorkbook.verified", { fileName: signing.fileName }), ok: true });
    }
    setSigning(null); setComment("");
    await refresh();
  };

  const columns: RegisterColumn<MasterDataImport>[] = [
    { key: "importedAt", label: t("masterDataWorkbook.columns.loaded"), sortable: true, render: (i) => formatDateTimeWithTimeZone(i.importedAt) },
    { key: "importedBy", label: t("masterDataWorkbook.columns.loadedBy"), sortable: true },
    { key: "fileName", label: t("masterDataWorkbook.columns.file"), render: (i) => <Button size="small" onClick={() => api.downloadFile(i)}>{i.fileName}</Button> },
    { key: "sheets", label: t("masterDataWorkbook.columns.newRecords"), render: (i) => i.sheets.reduce((n, s) => n + s.created, 0) },
    { key: "status", label: t("masterDataWorkbook.columns.status"), render: (i) => (i.status === "Verified" ? t("masterDataWorkbook.verifiedBy", { name: i.verifiedBy }) : t("masterDataWorkbook.waiting")) },
    {
      key: "actions", label: "",
      render: (i) => i.status === "Loaded" ? <Button size="small" variant="outlined" onClick={() => setSigning(i)}>{t("masterDataWorkbook.verify")}</Button> : null,
    },
  ];

  // The signed meaning is recorded in English; the page language only adds a translation beside it.
  const verifyStatement = (i: MasterDataImport, tr: typeof t) =>
    i.itemsWithoutSpecifications.length > 0
      ? tr("masterDataWorkbook.verifyStatementNote", { items: i.itemsWithoutSpecifications.join(", ") })
      : tr("masterDataWorkbook.verifyStatement");
  const enT = i18n.getFixedT("en", "labConfig");

  return (
    <LabPage
      title={t("masterDataWorkbook.title")}
      subtitle={t("masterDataWorkbook.subtitle")}
      actions={<Button variant="outlined" onClick={() => api.download()}>{t("masterDataWorkbook.download")}</Button>}
    >
      <Stack spacing={3}>
        {message && <Alert severity={message.ok ? "success" : "error"} onClose={() => setMessage(null)}>{message.text}</Alert>}

        <Paper sx={{ p: 2 }}>
          <SectionTitle>{t("masterDataWorkbook.step1")}</SectionTitle>
          <Stack direction="row" spacing={2} sx={{ mt: 1, alignItems: "center", flexWrap: "wrap" }}>
            <Button variant="outlined" component="label">
              {t("masterDataWorkbook.chooseFile")}
              <input hidden type="file" accept=".xlsx" onChange={(e) => { setFile(e.target.files?.[0] ?? null); setReport(null); }} />
            </Button>
            <Typography variant="body2">{file?.name ?? t("masterDataWorkbook.noFile")}</Typography>
            <Button variant="contained" disabled={!file || busy} onClick={check}>{busy ? t("masterDataWorkbook.checking") : t("masterDataWorkbook.checkFile")}</Button>
            <Button variant="contained" color="success" disabled={!file || !canLoad(report) || busy || changeControlInvalid} onClick={() => setSigning("load")}>{t("masterDataWorkbook.loadAndSign")}</Button>
          </Stack>
          <Stack sx={{ mt: 2, maxWidth: 520 }}>
            <Typography variant="subtitle2" component="h3" sx={{ mb: 1 }}>{t("changeControl:workbook.title")}</Typography>
            <ChangeControlNumberField
              sectionId={null}
              value={changeControlInput}
              onChange={setChangeControlInput}
              listEnabled={permissions.includes(QUALITY_VIEW_PERMISSION)}
            />
          </Stack>
          {importRefused && <Alert severity="info" sx={{ mt: 2 }}>{t("changeControl:workbook.hint")}</Alert>}
          {report && <Stack sx={{ mt: 2 }}><WorkbookReportTable report={report} /></Stack>}
        </Paper>

        <Paper sx={{ p: 2 }}>
          <SectionTitle>{t("masterDataWorkbook.step2")}</SectionTitle>
          <RegisterTable rows={imports} columns={columns} getRowId={(i) => i.id}
            empty={{ title: t("masterDataWorkbook.emptyTitle"), description: t("masterDataWorkbook.emptyDesc") }} />
        </Paper>
      </Stack>

      <SignatureDialog
        open={signing !== null}
        meaningStatement={signing === "load" || signing === null
          ? enT("masterDataWorkbook.loadStatement")
          : verifyStatement(signing, enT)}
        meaningStatementTranslation={signing === "load" || signing === null
          ? t("masterDataWorkbook.loadStatement")
          : verifyStatement(signing, t)}
        showComment
        comment={comment}
        onCommentChange={setComment}
        onCancel={() => { setSigning(null); setComment(""); }}
        onConfirm={sign}
      />
    </LabPage>
  );
}
