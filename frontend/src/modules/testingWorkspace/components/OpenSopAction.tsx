import { useState } from "react";
import { Box, Button, CircularProgress, Typography } from "@mui/material";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import { i18n as appI18n, useTranslation } from "../../../i18n";
import { ItemStmService, type ItemStmDto } from "../../laboratoryConfiguration/items/services/ItemStmService";

/** How an item's SOP resolves, from Document Control and the reader's access. */
export type SopLinkState = "NoSopNumber" | "NotInDocumentControl" | "NoEffectiveRevision" | "NoAccess" | "Linked";

export function sopLinkState(stm: ItemStmDto): SopLinkState {
  switch (stm.status) {
    case "NotLinked":
      return "NoSopNumber";
    case "NotFound":
      return "NotInDocumentControl";
    case "NoEffectiveRevision":
      return "NoEffectiveRevision";
    default:
      return stm.documentMasterId == null ? "NotInDocumentControl" : "Linked";
  }
}

function messageFor(state: Exclude<SopLinkState, "Linked">, sopNumber: string | null): string {
  switch (state) {
    case "NoSopNumber":
      return appI18n.t("testingWorkspace:openSop.noSopNumber");
    case "NotInDocumentControl":
      return appI18n.t("testingWorkspace:openSop.notInDocumentControl", { sopNumber: sopNumber ?? "" }).replace("  ", " ");
    case "NoEffectiveRevision":
      return appI18n.t("testingWorkspace:openSop.noEffectiveRevision", { sopNumber: sopNumber ?? "" }).replace("  ", " ");
    case "NoAccess":
      return appI18n.t("testingWorkspace:openSop.noAccess");
  }
}

/** "Open SOP" for the item of the sample being tested: opens the SOP's Document Control page in a new tab. */
export function OpenSopAction({ itemId }: { itemId: number }) {
  const { t } = useTranslation("testingWorkspace");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const open = async () => {
    setBusy(true);
    setMessage(null);
    try {
      const stm = await ItemStmService.getForItem(itemId);
      const state = sopLinkState(stm);
      if (state === "Linked") {
        window.open(`/document-control/documents/${stm.documentMasterId}`, "_blank", "noopener,noreferrer");
      } else {
        setMessage(messageFor(state, stm.sopNumber));
      }
    } catch (e: unknown) {
      const status = (e as { response?: { status?: number } })?.response?.status;
      setMessage(status === 403 || status === 401 ? messageFor("NoAccess", null) : t("openSop.checkFailed"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Box sx={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 1.5, flexWrap: "wrap", mb: 1 }}>
      {message && (
        <Typography role="status" variant="body2" color="text.secondary">
          {message}
        </Typography>
      )}
      <Button
        type="button"
        size="small"
        variant="outlined"
        onClick={() => { void open(); }}
        disabled={busy}
        startIcon={busy ? <CircularProgress size={14} aria-hidden="true" /> : <DescriptionOutlinedIcon fontSize="small" />}
      >
        {t("openSop.button")}
      </Button>
    </Box>
  );
}
