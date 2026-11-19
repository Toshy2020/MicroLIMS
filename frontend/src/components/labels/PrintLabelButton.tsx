import { useEffect, useId, useMemo, useState } from "react";
import PrintOutlinedIcon from "@mui/icons-material/PrintOutlined";
import { Button, Tooltip } from "@mui/material";
import { visuallyHidden } from "@mui/utils";
import { useAuth } from "../../contexts/AuthContext";
import { useTranslation } from "../../i18n";
import { PERMISSIONS } from "../../routes/routes";
import { labelService, type LabelEntityType, type LabelPrintStatus, type LabelTarget } from "../../services/labelService";
import { PrintLabelsDialog } from "./PrintLabelsDialog";

export interface PrintLabelButtonProps {
  target: LabelTarget;
}

function hasCreatePermission(entityType: LabelEntityType, permissions: readonly string[]): boolean {
  switch (entityType) {
    case "Sample":
      return (
        permissions.includes(PERMISSIONS.SAMPLES_RECEIVE_OWN_LAB) ||
        permissions.includes(PERMISSIONS.SAMPLES_RECEIVE)
      );
    case "MediaLot":
      return permissions.includes(PERMISSIONS.MEDIA_PREPARE);
    case "SolutionPreparation":
      return permissions.includes(PERMISSIONS.SOLUTIONS_PREPARE);
    case "Cryovial":
      return permissions.includes(PERMISSIONS.CRYOVIALS_MANAGE);
    case "MaterialStockLot":
      return permissions.includes(PERMISSIONS.MATERIALS_MANAGE);
    case "EquipmentAsset":
      return permissions.includes(PERMISSIONS.EQUIPMENT_MANAGE);
  }
}

export function PrintLabelButton({ target }: PrintLabelButtonProps) {
  const { t } = useTranslation("common");
  const { permissions } = useAuth();
  const reasonId = useId();
  const [status, setStatus] = useState<LabelPrintStatus | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [refreshNonce, setRefreshNonce] = useState(0);

  const hasCode = target.code.trim().length > 0 && target.entityId > 0;

  useEffect(() => {
    if (!hasCode) {
      setStatus(null);
      return;
    }

    const controller = new AbortController();

    labelService
      .getStatus([{ entityType: target.entityType, entityId: target.entityId }], controller.signal)
      .then((results) => {
        if (controller.signal.aborted) return;
        setStatus(results[0] ?? null);
      })
      .catch(() => {
        if (controller.signal.aborted) return;
        // Let the dialog surface status errors on click if needed; the server enforces all rules.
        setStatus(null);
      });

    return () => {
      controller.abort();
    };
  }, [hasCode, target.entityType, target.entityId, refreshNonce]);

  const isReprint = (status?.printCount ?? 0) > 0;
  const label = isReprint ? t("labels.printButton.reprint") : t("labels.printButton.print");
  const canCreate = hasCreatePermission(target.entityType, permissions);
  const canReprint = permissions.includes(PERMISSIONS.LABELS_REPRINT);

  let disabledReason: string | null = null;
  if (!hasCode) {
    disabledReason = t("labels.printButton.noCode");
  } else if (status && !status.canPrint) {
    disabledReason = status.blockedReason ?? t("labels.printButton.cannotPrint");
  } else if (isReprint && !canReprint) {
    disabledReason = t("labels.printButton.noReprintPermission");
  } else if (status && !isReprint && !canCreate) {
    disabledReason = t("labels.printButton.noPrintPermission");
  } else if (!status && !canCreate && !canReprint) {
    disabledReason = t("labels.printButton.noPrintPermission");
  }

  const dialogTargets = useMemo(() => [target], [target]);

  const handleClose = () => {
    setDialogOpen(false);
    if (hasCode) {
      setRefreshNonce((n) => n + 1);
    }
  };

  return (
    <>
      <Tooltip title={disabledReason ?? ""}>
        <span style={{ display: "inline-flex" }}>
          <Button
            variant="outlined"
            size="small"
            startIcon={<PrintOutlinedIcon />}
            disabled={Boolean(disabledReason)}
            aria-describedby={disabledReason ? reasonId : undefined}
            onClick={() => setDialogOpen(true)}
          >
            {label}
          </Button>
        </span>
      </Tooltip>
      {disabledReason && (
        <span id={reasonId} style={visuallyHidden}>
          {disabledReason}
        </span>
      )}
      <PrintLabelsDialog open={dialogOpen} targets={dialogTargets} onClose={handleClose} />
    </>
  );
}
