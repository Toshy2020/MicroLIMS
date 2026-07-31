import { useEffect, useState } from "react";
import { Paper, Box, Table, TableHead, TableRow, TableCell, TableBody } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { PrintButton } from "../../../components/PrintButton";
import { PrintableTable } from "../../../components/PrintableTable";
import { formatLabDate } from "../../../utils/formatDate";
import { masterDataOptions } from "../../../services/masterDataOptions";

// Read-only view/print of prepared media that has passed GPT and is
// released for use - reuses GET /media/released (MediaPreparationService.
// GetReleasedAsync), which already excludes anything not Active/Released
// or past its expiry date. No backend change needed for this list.
export function ApprovedMediaListPage() {
  const [media, setMedia] = useState<any[]>([]);
  useEffect(() => { masterDataOptions.getReleasedMedia().then(setMedia); }, []);

  const columns = [
    { label: "Lot Number", render: (m: any) => m.lotNumber },
    { label: "Media Type", render: (m: any) => m.mediaType?.name ?? "—" },
    { label: "Manufacturer Lot", render: (m: any) => m.manufacturerLot },
    { label: "Manufacturer", render: (m: any) => m.manufacturerName },
    { label: "Prepared", render: (m: any) => formatLabDate(m.preparedAt) },
    { label: "Expiry", render: (m: any) => formatLabDate(m.expiryDate) }
  ];

  return (
    <>
      <PageHeader title="Approved Media List" subtitle="Prepared media lots that passed GPT and are released for routine use." />

      <Box className="no-print">
        <Box sx={{ display: "flex", justifyContent: "flex-end", mb: 1 }}><PrintButton /></Box>
        <SectionTitle>Released Media</SectionTitle>
        <Paper sx={{ p: 2.5 }}>
          <Table size="small">
            <TableHead><TableRow>{columns.map((c) => <TableCell key={c.label}>{c.label}</TableCell>)}</TableRow></TableHead>
            <TableBody>
              {media.map((m) => (
                <TableRow key={m.id}>{columns.map((c) => <TableCell key={c.label}>{c.render(m)}</TableCell>)}</TableRow>
              ))}
            </TableBody>
          </Table>
        </Paper>
      </Box>

      <PrintableTable
        title="Approved Media List"
        subtitle="GPT-released media, not expired."
        rows={media}
        getRowId={(m) => m.id}
        columns={columns}
      />
    </>
  );
}
