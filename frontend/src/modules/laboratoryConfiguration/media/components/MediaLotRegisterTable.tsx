import { Typography } from "@mui/material";
import { RegisterTable } from "../../../../components/lab";
import type { RegisterColumn, RegisterRowAction } from "../../../../components/lab";
import { StatusBadge } from "../../../../components/StatusBadge";
import { lifecycleOf } from "./MediaLotKpiCards";
import { monospaceFontFamily } from "../../../../theme/palette";
import { useAuth } from "../../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../../routes/routes";

function formatDateDDMMYY(value: string | number | Date | null | undefined): string {
  if (!value) return "—";
  const d = new Date(value);
  if (isNaN(d.getTime())) return "—";
  const day = String(d.getDate()).padStart(2, "0");
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const year = String(d.getFullYear()).slice(-2);
  return `${day}/${month}/${year}`;
}

interface Props {
  lots: any[];
  awaitingApprovalIds: Set<number>;
  selectedLotId?: number | null;
  onSelectLot: (lot: any) => void;
  isCompact?: boolean;
  loading?: boolean;
  isFiltered?: boolean;
  onViewRecord: (lotId: number) => void;
  onViewAuditHistory: (lotId: number) => void;
  onRequestReleaseDecision?: (lot: any, approved: boolean) => void;
}

export function MediaLotRegisterTable({
  lots,
  awaitingApprovalIds,
  selectedLotId,
  onSelectLot,
  isCompact,
  loading,
  isFiltered,
  onViewRecord,
  onViewAuditHistory,
  onRequestReleaseDecision
}: Props) {
  const { permissions } = useAuth();
  const canRelease = permissions.includes(PERMISSIONS.MEDIA_RELEASE);

  const preparedCol: RegisterColumn<any> = {
    key: "preparedAt", label: "Prepared On", sortable: true, width: isCompact ? 95 : undefined,
    render: (lot) => <Typography component="span" sx={{ fontSize: 12, color: "text.secondary", whiteSpace: "nowrap" }}>{formatDateDDMMYY(lot.preparedAt)}</Typography>
  };
  const statusCol: RegisterColumn<any> = {
    key: "status", label: "Status", sortable: true, sortValue: (lot) => lifecycleOf(lot, awaitingApprovalIds),
    render: (lot) => <StatusBadge status={lifecycleOf(lot, awaitingApprovalIds)} />
  };

  // Compact = master list beside the selected lot's workspace; the selected row
  // is highlighted with the purple tone.
  const columns: RegisterColumn<any>[] = isCompact
    ? [
        preparedCol,
        {
          key: "material", label: "Material / Lot", sortable: true, sortValue: (lot) => lot.material?.materialName ?? "",
          render: (lot) => (
            <>
              <Typography sx={{ fontWeight: selectedLotId === lot.id ? 700 : 600, fontSize: 12.5, color: selectedLotId === lot.id ? "primary.main" : "text.primary" }}>
                {lot.material?.materialName || "Dehydrated Material"}
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Lot: {lot.lotNumber}</Typography>
            </>
          )
        },
        statusCol
      ]
    : [
        preparedCol,
        {
          key: "material", label: "Dehydrated Material", sortable: true, sortValue: (lot) => lot.material?.materialName ?? "",
          render: (lot) =>
            lot.material ? (
              <>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>{lot.material.materialName}</Typography>
                {lot.material.batchNumber && (
                  <Typography sx={{ fontSize: 11, color: "text.secondary" }}>Batch: {lot.material.batchNumber}</Typography>
                )}
              </>
            ) : (
              "—"
            )
        },
        {
          key: "lotNumber", label: "Lot Number", sortable: true,
          render: (lot) => (
            <>
              <Typography sx={{ fontWeight: 700, fontSize: 13, fontFamily: monospaceFontFamily, color: selectedLotId === lot.id ? "primary.main" : "text.primary" }}>
                {lot.lotNumber}
              </Typography>
              <Typography sx={{ fontSize: 11, color: "text.secondary" }}>ID #{lot.id}</Typography>
            </>
          )
        },
        {
          key: "expiryDate", label: "Expiry Date", sortable: true,
          render: (lot) => <Typography component="span" sx={{ fontSize: 12, color: "text.secondary", whiteSpace: "nowrap" }}>{formatDateDDMMYY(lot.expiryDate)}</Typography>
        },
        statusCol
      ];

  const rowActions = isCompact
    ? undefined
    : (lot: any): RegisterRowAction[] => {
        const actions: RegisterRowAction[] = [];
        if (lifecycleOf(lot, awaitingApprovalIds) === "Awaiting Approval" && canRelease && onRequestReleaseDecision) {
          actions.push({ label: "Release lot", onClick: () => onRequestReleaseDecision(lot, true) });
          actions.push({ label: "Reject lot", danger: true, onClick: () => onRequestReleaseDecision(lot, false) });
        }
        actions.push({ label: "View lot record (printable)", onClick: () => onViewRecord(lot.id) });
        actions.push({ label: "Audit trail", onClick: () => onViewAuditHistory(lot.id) });
        return actions;
      };

  return (
    <RegisterTable
      columns={columns}
      rows={lots}
      getRowId={(lot) => lot.id}
      loading={loading}
      onRowClick={onSelectLot}
      rowActions={rowActions}
      rowTone={(lot) => (selectedLotId === lot.id ? "purple" : undefined)}
      empty={
        isFiltered
          ? { title: "No media lots match this filter", description: "Reset the filters to see all lots." }
          : { title: "No media lots yet", description: "Prepare the first media lot." }
      }
    />
  );
}
