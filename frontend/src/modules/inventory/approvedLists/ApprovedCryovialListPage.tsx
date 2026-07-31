import { useEffect, useState } from "react";
import { Paper, Box, Table, TableHead, TableRow, TableCell, TableBody } from "@mui/material";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { PrintButton } from "../../../components/PrintButton";
import { PrintableTable } from "../../../components/PrintableTable";
import { formatLabDate } from "../../../utils/formatDate";
import { ReferenceStrainService } from "../../laboratoryConfiguration/referenceStrains/services/ReferenceStrainService";

// Read-only view/print of approved cryovials that are not destroyed and
// not expired - computed client-side from GET /reference-strains, the
// same source CryovialsPage.tsx already uses for "approvedStrains".
// No backend change needed for this list.
export function ApprovedCryovialListPage() {
  const [cryovials, setCryovials] = useState<any[]>([]);

  useEffect(() => {
    ReferenceStrainService.getAll().then((strains: any[]) => {
      const now = new Date();
      const approved = strains.flatMap((s) =>
        (s.cryovials ?? [])
          .filter((c: any) => c.approvalStatus === "Approved" && !c.isDestroyed && new Date(c.expiryDate) > now)
          .map((c: any) => ({ ...c, strainCode: s.code, organismName: s.organismName }))
      );
      setCryovials(approved);
    });
  }, []);

  const columns = [
    { label: "Cryovial Code", render: (c: any) => c.code },
    { label: "Reference Strain", render: (c: any) => `${c.strainCode} — ${c.organismName}` },
    { label: "Passage No.", render: (c: any) => c.passageNumber },
    { label: "Manufacturer", render: (c: any) => c.manufacturerName },
    { label: "Storage", render: (c: any) => c.storageCondition },
    { label: "Expiry", render: (c: any) => formatLabDate(c.expiryDate) }
  ];

  return (
    <>
      <PageHeader title="Approved Cryovial List" subtitle="Approved, non-destroyed cryovials available for use." />

      <Box className="no-print">
        <Box sx={{ display: "flex", justifyContent: "flex-end", mb: 1 }}><PrintButton /></Box>
        <SectionTitle>Approved Cryovials</SectionTitle>
        <Paper sx={{ p: 2.5 }}>
          <Table size="small">
            <TableHead><TableRow>{columns.map((c) => <TableCell key={c.label}>{c.label}</TableCell>)}</TableRow></TableHead>
            <TableBody>
              {cryovials.map((c) => (
                <TableRow key={c.id}>{columns.map((col) => <TableCell key={col.label}>{col.render(c)}</TableCell>)}</TableRow>
              ))}
            </TableBody>
          </Table>
        </Paper>
      </Box>

      <PrintableTable
        title="Approved Cryovial List"
        subtitle="Approved, non-destroyed, not-expired cryovials."
        rows={cryovials}
        getRowId={(c) => c.id}
        columns={columns}
      />
    </>
  );
}
