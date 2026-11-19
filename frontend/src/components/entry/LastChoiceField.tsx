import { useCallback, useState, type ReactNode } from "react";
import { Box, Button, Typography } from "@mui/material";
import { i18n, useTranslation } from "../../i18n";

export const LAST_CHOICE_CAPTION = i18n.getFixedT("en", "common")("entry.fromLastEntry");
export const UNCONFIRMED_LOT_SAVE_REASON = i18n.getFixedT("en", "common")("entry.confirmLotReason");

export type LastChoiceKind = "lot" | "equipment" | "field";

export function isInitialDefaultConfirmed(
  kind: LastChoiceKind = "equipment",
): boolean {
  return kind !== "lot";
}

export interface LastChoiceController<T = number | string> {
  isDefault: boolean;
  confirmed: boolean;
  saveBlockedReason: string | null;
  applyDefault: (value?: T) => void;
  notifyValueChanged: () => void;
  confirm: () => void;
  reset: () => void;
}

export function useLastChoiceDefault<T = number | string>(
  kind: LastChoiceKind = "equipment",
): LastChoiceController<T> {
  const [isDefault, setIsDefault] = useState(false);
  const [confirmed, setConfirmed] = useState(true);

  const applyDefault = useCallback(() => {
    setIsDefault(true);
    setConfirmed(isInitialDefaultConfirmed(kind));
  }, [kind]);

  const notifyValueChanged = useCallback(() => {
    setIsDefault(false);
    setConfirmed(true);
  }, []);

  const confirm = useCallback(() => {
    setConfirmed(true);
  }, []);

  const reset = useCallback(() => {
    setIsDefault(false);
    setConfirmed(true);
  }, []);

  const { t } = useTranslation("common");
  const saveBlockedReason =
    kind === "lot" && isDefault && !confirmed
      ? t("entry.confirmLotReason")
      : null;

  return {
    isDefault,
    confirmed,
    saveBlockedReason,
    applyDefault,
    notifyValueChanged,
    confirm,
    reset,
  };
}

export interface LastChoiceFieldProps {
  kind?: LastChoiceKind;
  isDefault: boolean;
  confirmed?: boolean;
  onConfirm?: () => void;
  children: ReactNode;
}

export function LastChoiceField({
  kind = "equipment",
  isDefault,
  confirmed,
  onConfirm,
  children,
}: LastChoiceFieldProps) {
  const { t } = useTranslation("common");
  const isConfirmed = confirmed ?? isInitialDefaultConfirmed(kind);
  const showConfirmButton = kind === "lot" && !isConfirmed;

  // The wrapper element is rendered in both states so that clearing the default
  // (on the first keystroke) never remounts the field and drops focus mid-typing.
  return (
    <Box
      data-testid={isDefault ? "last-choice-field" : undefined}
      sx={
        isDefault
          ? {
              border: "1px solid",
              borderColor: "primary.main",
              borderRadius: 1,
              p: 0.75,
            }
          : { display: "contents" }
      }
    >
      {children}
      {isDefault && (
        <Box
          sx={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 1,
            mt: 0.5,
          }}
        >
          <Typography
            variant="caption"
            sx={{ color: "text.secondary", fontWeight: 500, fontSize: 11 }}
          >
            {t("entry.fromLastEntry")}
          </Typography>
          {showConfirmButton && (
            <Button
              size="small"
              variant="outlined"
              onClick={onConfirm}
              sx={{
                py: 0,
                px: 1,
                minHeight: 24,
                fontSize: 11,
                textTransform: "none",
              }}
            >
              {t("entry.confirm")}
            </Button>
          )}
        </Box>
      )}
    </Box>
  );
}
