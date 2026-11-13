import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  Checkbox,
  FormControlLabel,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography
} from "@mui/material";
import { useTheme } from "@mui/material/styles";
import { FloatingDialog } from "../../components/FloatingDialog";
import { ToneBadge } from "../../components/StatusBadge";
import { tableHeadSx } from "../../theme";
import { getErrorMessage, serverMessage } from "../../utils/errorMessage";
import {
  SsoAdminService,
  type DirectoryLinkCsvRow,
  type DirectoryLinkPreviewRow
} from "./services/SsoAdminService";

export interface ImportDirectoryLinksDialogProps {
  open: boolean;
  onClose: () => void;
  onImported: () => void;
}

const MAX_ROWS = 2000;

function parseCsvRows(raw: string): DirectoryLinkCsvRow[] {
  const lines = raw
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
  if (lines.length === 0) return [];

  const parsed: DirectoryLinkCsvRow[] = [];
  for (let i = 0; i < lines.length; i += 1) {
    const parts = lines[i].split(",").map((p) => p.trim());
    const username = parts[0] ?? "";
    const companyUsername = parts[1] ?? "";
    if (
      i === 0 &&
      username.toLowerCase() === "username" &&
      companyUsername.toLowerCase().replace(/\s+/g, "") === "companyusername"
    ) {
      continue;
    }
    parsed.push({ username, companyUsername });
  }
  return parsed;
}

export function ImportDirectoryLinksDialog({ open, onClose, onImported }: ImportDirectoryLinksDialogProps) {
  const theme = useTheme();
  const [csvText, setCsvText] = useState("");
  const [parsedRows, setParsedRows] = useState<DirectoryLinkCsvRow[]>([]);
  const [previewRows, setPreviewRows] = useState<DirectoryLinkPreviewRow[]>([]);
  const [skipInvalidRows, setSkipInvalidRows] = useState(false);
  const [previewing, setPreviewing] = useState(false);
  const [importing, setImporting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [summary, setSummary] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const hasInvalidRows = previewRows.some((r) => r.status !== "Ok");
  const hasOkRows = previewRows.some((r) => r.status === "Ok");
  const canImport = previewRows.length > 0 && hasOkRows && (!hasInvalidRows || skipInvalidRows) && !importing && !done;

  const handleClose = () => {
    setCsvText("");
    setParsedRows([]);
    setPreviewRows([]);
    setSkipInvalidRows(false);
    setError(null);
    setSummary(null);
    setDone(false);
    onClose();
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      setCsvText(await file.text());
    } catch (err: unknown) {
      setError(getErrorMessage(err, "Could not read the file."));
      return;
    }
    setPreviewRows([]);
    setSummary(null);
    setDone(false);
    setError(null);
  };

  const handlePreview = async () => {
    setDone(false);
    setError(null);
    setSummary(null);
    const rows = parseCsvRows(csvText);
    if (rows.length === 0) {
      setError("Paste or upload at least one CSV row (username,companyUsername).");
      return;
    }
    if (rows.length > MAX_ROWS) {
      setError(`Maximum ${MAX_ROWS} rows per import.`);
      return;
    }
    setPreviewing(true);
    try {
      const res = await SsoAdminService.previewLinks(rows);
      setParsedRows(rows);
      setPreviewRows(res.rows);
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, "Could not preview directory links."));
    } finally {
      setPreviewing(false);
    }
  };

  const handleImport = async () => {
    if (!canImport) return;
    setError(null);
    setImporting(true);
    try {
      const res = await SsoAdminService.importLinks(parsedRows);
      setPreviewRows(res.rows);
      const count = res.applied ?? res.rows.filter((r) => r.status === "Ok").length;
      const skipped = res.rows.length - count;
      setSummary(`Imported ${count} directory ${count === 1 ? "link" : "links"}${skipped > 0 ? `; ${skipped} ${skipped === 1 ? "row" : "rows"} skipped` : ""}.`);
      setDone(true);
      onImported();
    } catch (err: unknown) {
      setError(serverMessage(err) ?? getErrorMessage(err, "Could not import directory links."));
    } finally {
      setImporting(false);
    }
  };

  return (
    <FloatingDialog
      open={open}
      onClose={handleClose}
      maxWidth="md"
      title="Import directory links"
      actions={
        <>
          <Button onClick={handleClose}>Close</Button>
          <Button variant="outlined" onClick={handlePreview} disabled={previewing || !csvText.trim()}>
            {previewing ? "Previewing..." : "Preview"}
          </Button>
          <Button variant="contained" onClick={handleImport} disabled={!canImport}>
            {importing ? "Importing..." : "Import"}
          </Button>
        </>
      }
    >
      <Stack spacing={2} sx={{ mt: 1 }}>
        <Typography variant="body2" color="text.secondary">
          Paste CSV lines or upload a CSV file with columns <code>username,companyUsername</code> (maximum {MAX_ROWS} rows).
        </Typography>

        {error && <Alert severity="error" role="alert">{error}</Alert>}
        <Box role="status" aria-live="polite">{summary && <Alert severity="success" role="none">{summary}</Alert>}</Box>

        <Box>
          <Button component="label" variant="outlined" size="small">
            Upload CSV file
            <input type="file" accept=".csv,text/csv" hidden aria-label="CSV file" onChange={handleFileChange} />
          </Button>
        </Box>

        <TextField
          label="CSV rows"
          multiline
          rows={5}
          value={csvText}
          onChange={(e) => {
            setCsvText(e.target.value);
            setPreviewRows([]);
            setSummary(null);
            setDone(false);
          }}
          placeholder={"username,companyUsername\nj.smith,john.smith"}
          fullWidth
        />

        {previewRows.length > 0 && (
          <>
            <TableContainer sx={{ maxHeight: 260 }}>
              <Table size="small" aria-label="Directory link preview">
                <TableHead>
                  <TableRow sx={tableHeadSx(theme)}>
                    <TableCell>Row</TableCell>
                    <TableCell>Username</TableCell>
                    <TableCell>Status</TableCell>
                    <TableCell>Message</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {previewRows.map((row) => (
                    <TableRow key={`${row.row}-${row.username}`}>
                      <TableCell>{row.row}</TableCell>
                      <TableCell>{row.username}</TableCell>
                      <TableCell>
                        <ToneBadge tone={row.status === "Ok" ? "notDetected" : "action"} label={row.status} />
                      </TableCell>
                      <TableCell>{row.message}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>

            {hasInvalidRows && (
              <FormControlLabel
                control={
                  <Checkbox
                    checked={skipInvalidRows}
                    onChange={(e) => setSkipInvalidRows(e.target.checked)}
                  />
                }
                label="Skip invalid rows"
              />
            )}
          </>
        )}
      </Stack>
    </FloatingDialog>
  );
}
