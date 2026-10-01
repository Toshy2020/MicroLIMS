import { Box, Typography } from "@mui/material";
import { RegisterTable } from "../../../../components/lab";
import type { RegisterColumn, RegisterRowAction } from "../../../../components/lab";
import { StatusBadge } from "../../../../components/StatusBadge";
import { formatLabDate } from "../../../../utils/formatDate";
import { CryovialItem } from "../types/cryovialTypes";
import { isMaterialExpiringSoon } from "../../../inventory/materials/components/MaterialKpiCards";
import { monospaceFontFamily } from "../../../../theme/palette";

interface CryovialReviewTableProps {
  items: CryovialItem[];
  loading?: boolean;
  isFiltered?: boolean;
  onApproveClick: (cryovial: CryovialItem, approved: boolean) => void;
  onThawClick: (cryovial: CryovialItem) => void;
  onDestroyClick: (cryovial: CryovialItem) => void;
}

const isExpired = (expiryDateStr: string) => {
  if (!expiryDateStr) return false;
  return new Date(expiryDateStr) <= new Date();
};

const organismOf = (c: CryovialItem) => c.organism?.scientificName ?? c.organismNameSnapshot;

const COLUMNS: RegisterColumn<CryovialItem>[] = [
  {
    key: "code", label: "Code", sortable: true,
    render: (c) => (
      <>
        <Typography sx={{ fontSize: 13, fontWeight: 700, fontFamily: monospaceFontFamily, color: "primary.main" }}>{c.code}</Typography>
        {c.storageCondition && (
          <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap>{c.storageCondition}</Typography>
        )}
      </>
    )
  },
  {
    key: "organism", label: "Organism & Source", sortable: true, sortValue: organismOf,
    render: (c) => {
      const atcc = c.organism?.atccNumber ? ` (ATCC ${c.organism.atccNumber})` : "";
      return (
        <>
          <Typography sx={{ fontSize: 13, fontWeight: 600, color: "text.primary" }}>
            {organismOf(c)}
            {atcc && (
              <Typography component="span" sx={{ fontSize: 11, color: "text.secondary", ml: 0.5 }}>{atcc}</Typography>
            )}
          </Typography>
          {c.material && (
            <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap>
              {c.material.materialName} · Batch {c.material.batchNumber}
            </Typography>
          )}
        </>
      );
    }
  },
  {
    key: "approvalStatus", label: "Status", sortable: true,
    sortValue: (c) => (c.isDestroyed ? "Destroyed" : c.approvalStatus),
    render: (c) => (c.isDestroyed ? <StatusBadge status="Destroyed" label="Destroyed" /> : <StatusBadge status={c.approvalStatus} />)
  },
  {
    key: "vialsRemaining", label: "Vials Stock", sortable: true,
    render: (c) => {
      const depleted = c.vialsRemaining === 0;
      return (
        <>
          <Typography sx={{ fontSize: 13, fontWeight: 700, fontVariantNumeric: "tabular-nums", color: depleted ? "error.main" : "text.primary" }}>
            {c.vialsRemaining} of {c.numberOfVialsPrepared} vials
          </Typography>
          {depleted && !c.isDestroyed && (
            <Box sx={{ mt: 0.25 }}>
              <StatusBadge status="Depleted" label="Depleted" />
            </Box>
          )}
        </>
      );
    }
  },
  {
    key: "preparedAt", label: "Prepared", sortable: true,
    render: (c) => (
      <>
        <Typography sx={{ fontSize: 12, fontWeight: 600, color: "text.primary" }}>{formatLabDate(c.preparedAt)}</Typography>
        {c.preparedByName && (
          <Typography sx={{ fontSize: 11, color: "text.secondary" }} noWrap>{c.preparedByName}</Typography>
        )}
      </>
    )
  },
  {
    key: "expiryDate", label: "Expiry", sortable: true,
    render: (c) => {
      const expired = isExpired(c.expiryDate);
      const expiringSoon = !expired && isMaterialExpiringSoon(c.expiryDate, 30);
      return (
        <>
          <Typography sx={{ fontSize: 12, fontWeight: expired || expiringSoon ? 700 : 500, whiteSpace: "nowrap" }}>
            {formatLabDate(c.expiryDate)}
          </Typography>
          {expired && <Typography sx={{ fontSize: 10, color: "error.main", fontWeight: 700 }}>Expired</Typography>}
          {expiringSoon && <Typography sx={{ fontSize: 10, color: "warning.main", fontWeight: 600 }}>Expiring soon</Typography>}
        </>
      );
    }
  }
];

export function CryovialReviewTable({ items, loading, isFiltered, onApproveClick, onThawClick, onDestroyClick }: CryovialReviewTableProps) {
  // Same availability rules as the former inline buttons.
  const rowActions = (c: CryovialItem): RegisterRowAction[] => {
    const actions: RegisterRowAction[] = [];
    const pending = c.approvalStatus === "PendingReview" && !c.isDestroyed;
    if (pending) {
      actions.push({ label: "Approve batch", onClick: () => onApproveClick(c, true) });
      actions.push({ label: "Reject batch", danger: true, onClick: () => onApproveClick(c, false) });
    } else {
      if (c.approvalStatus === "Approved" && !c.isDestroyed && !isExpired(c.expiryDate) && c.vialsRemaining > 0) {
        actions.push({ label: "Thaw vial", onClick: () => onThawClick(c) });
      }
      if (!c.isDestroyed) {
        actions.push({ label: "Destroy (decommission batch)", danger: true, onClick: () => onDestroyClick(c) });
      }
    }
    actions.push({ label: "View laboratory report record", onClick: () => window.open(`/cryovials/${c.id}/report`, "_blank", "noopener") });
    return actions;
  };

  return (
    <RegisterTable
      columns={COLUMNS}
      rows={items}
      getRowId={(c) => c.id}
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
          ? { title: "No cryovials matching the filter criteria", description: "Reset the filters to see all batches." }
          : { title: "No cryovial batches yet", description: "Prepare the first working cryovial batch." }
      }
    />
  );
}
