import { Dialog, DialogTitle, DialogContent, DialogActions, Button, Table, TableHead, TableRow, TableCell, TableBody, Typography, Box , useTheme} from "@mui/material";
import FileDownloadIcon from "@mui/icons-material/FileDownload";
import { NumericTrendPoint, QualitativeTrendPoint } from "../types/reportingTypes";
import { StatusBadge } from "../../../components/StatusBadge";
import { brandColors } from "../../../theme";

interface TrendingDataDialogProps {
  open: boolean;
  onClose: () => void;
  isNumeric: boolean;
  testName: string;
  subjectName: string;
  unit: string | null;
  numericPoints?: NumericTrendPoint[];
  qualitativePoints?: QualitativeTrendPoint[];
  onSelectRecord?: (recordId: number) => void;
}

export function TrendingDataDialog({
  open,
  onClose,
  isNumeric,
  testName,
  subjectName,
  unit,
  numericPoints = [],
  qualitativePoints = [],
  onSelectRecord
}: TrendingDataDialogProps) {
  const theme = useTheme();
  const handleExportCsv = () => {
    let csv = "";
    if (isNumeric) {
      csv = "Period,Reference,Reported Value,Numeric Value,Result Level,Mean,Upper Limit,Alert Level,Action Level\n" +
        numericPoints.map((p) =>
          `"${p.label}","${p.referenceNumber}","${p.reportedValue}","${p.value ?? ""}","${p.resultLevel}","${p.mean ?? ""}","${p.upperLimit ?? ""}","${p.alertLevel ?? ""}","${p.actionLevel ?? ""}"`
        ).join("\n");
    } else {
      csv = "Period,Detected Count,Absent Count,Total Samples\n" +
        qualitativePoints.map((p) =>
          `"${p.label}","${p.detectedCount}","${p.absentCount}","${p.totalCount}"`
        ).join("\n");
    }

    const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `trend_data_${testName.toLowerCase()}_${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderBottom: 1, borderColor: "divider" }}>
        <Box>
          <Typography sx={{ fontSize: 16, fontWeight: 700, color: theme.palette.primary.main }}>
            Trend Data Table — {testName}
          </Typography>
          <Typography sx={{ fontSize: 12, color: "text.secondary" }}>
            Subject: <strong>{subjectName}</strong> {unit ? `(${unit})` : ""}
          </Typography>
        </Box>
        <Button size="small" variant="outlined" startIcon={<FileDownloadIcon />} onClick={handleExportCsv}>
          Export CSV
        </Button>
      </DialogTitle>

      <DialogContent sx={{ pt: 2 }}>
        {isNumeric ? (
          <Table size="small" sx={{ "& th": { fontWeight: 700, fontSize: 11.5 } }}>
            <TableHead>
              <TableRow>
                <TableCell>Period / Date</TableCell>
                <TableCell>Reference</TableCell>
                <TableCell>Reported Result</TableCell>
                <TableCell>Result Level</TableCell>
                <TableCell>Mean</TableCell>
                <TableCell>Upper Limit</TableCell>
                <TableCell>Alert Level</TableCell>
                <TableCell>Action Level</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {numericPoints.map((p, idx) => (
                <TableRow
                  key={idx}
                  hover
                  sx={{ cursor: onSelectRecord ? "pointer" : "default" }}
                  onClick={() => onSelectRecord && onSelectRecord(p.recordId)}
                >
                  <TableCell>{p.label}</TableCell>
                  <TableCell sx={{ fontWeight: 600, color: theme.palette.primary.main }}>{p.referenceNumber}</TableCell>
                  <TableCell sx={{ fontWeight: 700 }}>{p.reportedValue} {unit ?? ""}</TableCell>
                  <TableCell><StatusBadge status={p.resultLevel} /></TableCell>
                  <TableCell>{p.mean != null ? Number(p.mean).toFixed(1) : "—"}</TableCell>
                  <TableCell>{p.upperLimit ?? "—"}</TableCell>
                  <TableCell>{p.alertLevel ?? "—"}</TableCell>
                  <TableCell>{p.actionLevel ?? "—"}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        ) : (
          <Table size="small" sx={{ "& th": { fontWeight: 700, fontSize: 11.5 } }}>
            <TableHead>
              <TableRow>
                <TableCell>Period / Date</TableCell>
                <TableCell>Detected Count</TableCell>
                <TableCell>Absent / Conform Count</TableCell>
                <TableCell>Total Evaluated</TableCell>
                <TableCell>Status</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {qualitativePoints.map((p, idx) => (
                <TableRow key={idx} hover>
                  <TableCell>{p.label}</TableCell>
                  <TableCell sx={{ color: p.detectedCount > 0 ? brandColors.err : "inherit", fontWeight: p.detectedCount > 0 ? 700 : 400 }}>
                    {p.detectedCount}
                  </TableCell>
                  <TableCell sx={{ color: brandColors.ok }}>{p.absentCount}</TableCell>
                  <TableCell>{p.totalCount}</TableCell>
                  <TableCell>
                    <StatusBadge status={p.detectedCount > 0 ? "Detected" : "Conform"} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </DialogContent>

      <DialogActions sx={{ px: 3, py: 1.5, borderTop: 1, borderColor: "divider" }}>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
