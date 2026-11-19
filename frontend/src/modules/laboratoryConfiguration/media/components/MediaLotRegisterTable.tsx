import { useState } from "react";
import { Typography } from "@mui/material";
import { RegisterTable } from "../../../../components/lab";
import type { RegisterColumn, RegisterRowAction } from "../../../../components/lab";
import { TableToolbar } from "../../../../components/lab/table/TableToolbar";
import type { UseTableState } from "../../../../components/lab/table/useTableState";
import { StatusBadge } from "../../../../components/StatusBadge";
import { lifecycleOf } from "./MediaLotKpiCards";
import { monospaceFontFamily } from "../../../../theme/palette";
import { useAuth } from "../../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../../routes/routes";
import { useActionAvailability } from "../../../../hooks/useActionAvailability";
import { useTranslation } from "../../../../i18n";

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
  tableKey?: string;
  tableState?: UseTableState;
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
  tableKey,
  tableState,
  onViewRecord,
  onViewAuditHistory,
  onRequestReleaseDecision
}: Props) {
  const { t } = useTranslation("labConfig");
  const { permissions } = useAuth();
  // Rejecting a lot has no availability answer; it keeps the permission it always had.
  const canRelease = permissions.includes(PERMISSIONS.MEDIA_RELEASE);
  // The server's answer about releasing the lot whose actions menu is open (asked only then).
  const [menuLot, setMenuLot] = useState<{ id: number } | null>(null);
  const availability = useActionAvailability("media", menuLot?.id ?? null);

  const preparedCol: RegisterColumn<any> = {
    key: "preparedAt", label: t("media.lotRegister.columns.preparedOn"), sortable: true, width: isCompact ? 95 : undefined,
    render: (lot) => <Typography component="span" sx={{ fontSize: 12, color: "text.secondary", whiteSpace: "nowrap" }}>{formatDateDDMMYY(lot.preparedAt)}</Typography>
  };
  const statusCol: RegisterColumn<any> = {
    key: "status", label: t("media.lotRegister.columns.status"), sortable: true, sortValue: (lot) => lifecycleOf(lot, awaitingApprovalIds),
    render: (lot) => <StatusBadge status={lifecycleOf(lot, awaitingApprovalIds)} />
  };

  // Compact = master list beside the selected lot's workspace; the selected row
  // is highlighted with the purple tone.
  const columns: RegisterColumn<any>[] = isCompact
    ? [
        preparedCol,
        {
          key: "material", label: t("media.lotRegister.columns.materialOrLot"), sortable: true, sortValue: (lot) => lot.material?.materialName ?? "",
          render: (lot) => (
            <>
              <Typography sx={{ fontWeight: selectedLotId === lot.id ? 700 : 600, fontSize: 12.5, color: selectedLotId === lot.id ? "primary.main" : "text.primary" }}>
                {lot.material?.materialName || t("media.lotRegister.dehydratedMaterialFallback")}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.lotRegister.lotPrefix", { lotNumber: lot.lotNumber })}</Typography>
            </>
          )
        },
        statusCol
      ]
    : [
        preparedCol,
        {
          key: "material", label: t("media.lotRegister.columns.dehydratedMaterial"), sortable: true, sortValue: (lot) => lot.material?.materialName ?? "",
          render: (lot) =>
            lot.material ? (
              <>
                <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>{lot.material.materialName}</Typography>
                {lot.material.batchNumber && (
                  <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.lotRegister.batchPrefix", { batchNumber: lot.material.batchNumber })}</Typography>
                )}
              </>
            ) : (
              "—"
            )
        },
        {
          key: "lotNumber", label: t("media.lotRegister.columns.lotNumber"), sortable: true,
          render: (lot) => (
            <>
              <Typography sx={{ fontWeight: 700, fontSize: 13, fontFamily: monospaceFontFamily, color: selectedLotId === lot.id ? "primary.main" : "text.primary" }}>
                {lot.lotNumber}
              </Typography>
              <Typography sx={{ fontSize: 12, color: "text.secondary" }}>{t("media.lotRegister.idPrefix", { id: lot.id })}</Typography>
            </>
          )
        },
        {
          key: "expiryDate", label: t("media.lotRegister.columns.expiryDate"), sortable: true,
          render: (lot) => <Typography component="span" sx={{ fontSize: 12, color: "text.secondary", whiteSpace: "nowrap" }}>{formatDateDDMMYY(lot.expiryDate)}</Typography>
        },
        statusCol
      ];

  const rowActions = isCompact
    ? undefined
    : (lot: any): RegisterRowAction[] => {
        const actions: RegisterRowAction[] = [];
        if (lifecycleOf(lot, awaitingApprovalIds) === "Awaiting Approval" && onRequestReleaseDecision) {
          const answer = menuLot?.id === lot.id ? availability.get("media.release") : null;
          actions.push({
            label: t("media.lotRegister.actions.releaseLot"),
            reason: answer && !answer.allowed ? answer.reason : undefined,
            onClick: () => onRequestReleaseDecision(lot, true)
          });
          if (canRelease) actions.push({ label: t("media.lotRegister.actions.rejectLot"), danger: true, onClick: () => onRequestReleaseDecision(lot, false) });
        }
        actions.push({ label: t("media.lotRegister.actions.viewRecord"), onClick: () => onViewRecord(lot.id) });
        actions.push({ label: t("media.lotRegister.actions.auditTrail"), onClick: () => onViewAuditHistory(lot.id) });
        return actions;
      };

  return (
    <RegisterTable
      onRowActionsOpen={(lot: { id: number } | null) => setMenuLot(lot ? { id: lot.id } : null)}
      columns={columns}
      rows={lots}
      getRowId={(lot) => lot.id}
      tableKey={!isCompact ? tableKey : undefined}
      tableState={tableState}
      toolbar={
        !isCompact && tableKey && tableState ? (
          <TableToolbar tableKey={tableKey} tableState={tableState} showSearch={false} />
        ) : undefined
      }
      loading={loading}
      onRowClick={onSelectLot}
      rowActions={rowActions}
      rowTone={(lot) => (selectedLotId === lot.id ? "purple" : undefined)}
      empty={
        isFiltered
          ? { title: t("media.lotRegister.emptyFilteredTitle"), description: t("media.lotRegister.emptyFilteredDesc") }
          : { title: t("media.lotRegister.emptyTitle"), description: t("media.lotRegister.emptyDesc") }
      }
    />
  );
}
