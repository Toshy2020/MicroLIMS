import type { TFunction } from "i18next";
import { Box, Typography } from "@mui/material";
import { useTranslation } from "../../../../i18n";
import { RegisterTable } from "../../../../components/lab";
import type { RegisterColumn, RegisterRowAction } from "../../../../components/lab";
import { TableToolbar } from "../../../../components/lab/table/TableToolbar";
import type { UseTableState } from "../../../../components/lab/table/useTableState";
import { StatusBadge } from "../../../../components/StatusBadge";
import { formatLabDate, formatLabCalendarDate } from "../../../../utils/formatDate";
import { CryovialItem } from "../types/cryovialTypes";
import { isMaterialExpiringSoon } from "../../../inventory/materials/components/MaterialKpiCards";
import { monospaceFontFamily } from "../../../../theme/palette";

interface CryovialReviewTableProps {
  items: CryovialItem[];
  loading?: boolean;
  isFiltered?: boolean;
  tableKey?: string;
  tableState?: UseTableState;
  onApproveClick: (cryovial: CryovialItem, approved: boolean) => void;
  onThawClick: (cryovial: CryovialItem) => void;
  onDestroyClick: (cryovial: CryovialItem) => void;
  onMoveStorageClick: (cryovial: CryovialItem) => void;
  // Moving a batch is limited to System Administrators and Section Heads
  // (the server enforces it; this only hides the action from others).
  canMoveStorage?: boolean;
}

const isExpired = (expiryDateStr: string) => {
  if (!expiryDateStr) return false;
  return new Date(expiryDateStr) <= new Date();
};

const organismOf = (c: CryovialItem) => c.organism?.scientificName ?? c.organismNameSnapshot;

function getColumns(t: TFunction<"labConfig">): RegisterColumn<CryovialItem>[] {
  return [
  {
    key: "code", label: t("cryovials.table.columns.code"), sortable: true,
    render: (c) => (
      <>
        <Typography sx={{ fontSize: 13, fontWeight: 700, fontFamily: monospaceFontFamily, color: "primary.main" }}>{c.code}</Typography>
        {c.storageCondition && (
          <Typography sx={{ fontSize: 12, color: "text.secondary" }} noWrap>{c.storageCondition}</Typography>
        )}
      </>
    )
  },
  {
    key: "organism", label: t("cryovials.table.columns.organismSource"), sortable: true, sortValue: organismOf,
    render: (c) => {
      const atcc = c.organism?.atccNumber ? t("cryovials.table.atcc", { number: c.organism.atccNumber }) : "";
      return (
        <>
          <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
            {organismOf(c)}
            {atcc && (
              <Typography component="span" sx={{ fontSize: 12, color: "text.secondary", ml: 0.5 }}>{atcc}</Typography>
            )}
          </Typography>
          {c.referenceOrganismCode && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }} noWrap>
              {t("cryovials.table.strain", {
                code: c.referenceOrganismCode,
                collection: c.cultureCollection,
                number: c.strainNumber ? ` ${c.strainNumber}` : ""
              })}
            </Typography>
          )}
          {c.material && (
            <Typography sx={{ fontSize: 12, color: "text.secondary" }} noWrap>
              {t("cryovials.table.materialBatch", { name: c.material.materialName, batch: c.material.batchNumber })}
            </Typography>
          )}
        </>
      );
    }
  },
  {
    key: "approvalStatus", label: t("cryovials.table.columns.status"), sortable: true,
    sortValue: (c) => (c.isDestroyed ? "Destroyed" : c.approvalStatus),
    render: (c) => (c.isDestroyed ? <StatusBadge status="Destroyed" label={t("cryovials.table.destroyed")} /> : <StatusBadge status={c.approvalStatus} />)
  },
  {
    key: "vialsRemaining", label: t("cryovials.table.columns.vialsStock"), sortable: true,
    render: (c) => {
      const depleted = c.vialsRemaining === 0;
      return (
        <>
          <Typography sx={{ fontSize: 13, fontWeight: 700, fontVariantNumeric: "tabular-nums", color: depleted ? "error.main" : "text.primary" }}>
            {t("cryovials.table.vialsOf", { remaining: c.vialsRemaining, prepared: c.numberOfVialsPrepared })}
          </Typography>
          {depleted && !c.isDestroyed && (
            <Box sx={{ mt: 0.25 }}>
              <StatusBadge status="Depleted" label={t("cryovials.table.depleted")} />
            </Box>
          )}
        </>
      );
    }
  },
  {
    key: "preparedAt", label: t("cryovials.table.columns.prepared"), sortable: true,
    render: (c) => (
      <>
        <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>{formatLabDate(c.preparedAt)}</Typography>
        {c.preparedByName && (
          <Typography sx={{ fontSize: 12, color: "text.secondary" }} noWrap>{c.preparedByName}</Typography>
        )}
      </>
    )
  },
  {
    key: "expiryDate", label: t("cryovials.table.columns.expiry"), sortable: true,
    render: (c) => {
      const expired = isExpired(c.expiryDate);
      const expiringSoon = !expired && isMaterialExpiringSoon(c.expiryDate, 30);
      return (
        <>
          <Typography sx={{ fontSize: 12, fontWeight: expired || expiringSoon ? 700 : 500, whiteSpace: "nowrap" }}>
            {formatLabCalendarDate(c.expiryDate)}
          </Typography>
          {expired && <Typography sx={{ fontSize: 12, color: "error.main", fontWeight: 700 }}>{t("cryovials.table.expired")}</Typography>}
          {expiringSoon && <Typography sx={{ fontSize: 12, color: "warning.main", fontWeight: 600 }}>{t("cryovials.table.expiringSoon")}</Typography>}
        </>
      );
    }
  }
  ];
}

export function CryovialReviewTable({
  items,
  loading,
  isFiltered,
  tableKey,
  tableState,
  onApproveClick,
  onThawClick,
  onDestroyClick,
  onMoveStorageClick,
  canMoveStorage = false
}: CryovialReviewTableProps) {
  const { t } = useTranslation("labConfig");
  const columns = getColumns(t);
  // Same availability rules as the former inline buttons.
  const rowActions = (c: CryovialItem): RegisterRowAction[] => {
    const actions: RegisterRowAction[] = [];
    const pending = c.approvalStatus === "PendingReview" && !c.isDestroyed;
    if (pending) {
      actions.push({ label: t("cryovials.table.actions.approve"), onClick: () => onApproveClick(c, true) });
      actions.push({ label: t("cryovials.table.actions.reject"), danger: true, onClick: () => onApproveClick(c, false) });
    } else {
      if (c.approvalStatus === "Approved" && !c.isDestroyed && !isExpired(c.expiryDate) && c.vialsRemaining > 0) {
        actions.push({ label: t("cryovials.table.actions.thaw"), onClick: () => onThawClick(c) });
      }
      if (canMoveStorage && c.approvalStatus === "Approved" && !c.isDestroyed) {
        actions.push({ label: t("cryovials.table.actions.moveStorage"), onClick: () => onMoveStorageClick(c) });
      }
      if (!c.isDestroyed) {
        actions.push({ label: t("cryovials.table.actions.destroy"), danger: true, onClick: () => onDestroyClick(c) });
      }
    }
    actions.push({ label: t("cryovials.table.actions.viewReport"), onClick: () => window.open(`/cryovials/${c.id}/report`, "_blank", "noopener") });
    return actions;
  };

  return (
    <RegisterTable
      columns={columns}
      rows={items}
      getRowId={(c) => c.id}
      tableKey={tableKey}
      tableState={tableState}
      toolbar={
        tableKey && tableState ? (
          <TableToolbar tableKey={tableKey} tableState={tableState} showSearch={false} />
        ) : undefined
      }
      loading={loading}
      rowActions={rowActions}
      rowTone={(c) => {
        if (c.isDestroyed) return undefined;
        if (isExpired(c.expiryDate)) return "detected";
        if (isMaterialExpiringSoon(c.expiryDate, 30)) return "action";
        return undefined;
      }}
      empty={
        isFiltered
          ? { title: t("cryovials.table.emptyFilteredTitle"), description: t("cryovials.table.emptyFilteredDesc") }
          : { title: t("cryovials.table.emptyTitle"), description: t("cryovials.table.emptyDesc") }
      }
    />
  );
}
