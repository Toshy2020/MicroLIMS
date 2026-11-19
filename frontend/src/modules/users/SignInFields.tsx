import { Box, Checkbox, FormControlLabel, FormHelperText, MenuItem, TextField } from "@mui/material";
import type { TFunction } from "i18next";
import { i18n, useTranslation } from "../../i18n";
import type { SignInMethod } from "./services/UserService";

export function getLinkStateLabels(t: TFunction<"users">): Record<string, string> {
  return {
    NotLinked: t("linkState.NotLinked"),
    LinkedNeverSignedIn: t("linkState.LinkedNeverSignedIn"),
    Linked: t("linkState.Linked"),
    SignInRefused: t("linkState.SignInRefused")
  };
}

export const LINK_STATE_LABELS: Record<string, string> = getLinkStateLabels(i18n.getFixedT("en", "users"));

interface SignInFieldsProps {
  idPrefix: string;
  method: SignInMethod;
  onMethodChange: (m: SignInMethod) => void;
  directoryUsername: string;
  onDirectoryUsernameChange: (v: string) => void;
  emergency: boolean;
  onEmergencyChange: (v: boolean) => void;
  // Emergency accounts are System Administrators only.
  roleIsAdministrator: boolean;
}

// Sign-in method, company username and emergency flag, shared by the create form and the edit dialog.
export function SignInFields({
  idPrefix, method, onMethodChange, directoryUsername, onDirectoryUsernameChange, emergency, onEmergencyChange, roleIsAdministrator
}: SignInFieldsProps) {
  const { t } = useTranslation("users");
  const reasonId = `${idPrefix}-emergency-reason`;
  return (
    <>
      <TextField
        select size="small" label={t("fields.signInMethod")} value={method} fullWidth
        onChange={(e) => onMethodChange(e.target.value as SignInMethod)}
      >
        <MenuItem value="Local">{t("fields.localLogin")}</MenuItem>
        <MenuItem value="Directory">{t("fields.directoryLogin")}</MenuItem>
      </TextField>
      {method === "Directory" && (
        <TextField
          size="small" label={t("fields.companyUsername")} value={directoryUsername} required fullWidth
          onChange={(e) => onDirectoryUsernameChange(e.target.value)}
          error={!directoryUsername.trim()} helperText={t("fields.companyUsernameHelper")}
        />
      )}
      <Box>
        <FormControlLabel
          control={
            <Checkbox
              checked={emergency && roleIsAdministrator} disabled={!roleIsAdministrator}
              onChange={(e) => onEmergencyChange(e.target.checked)}
              slotProps={{ input: { "aria-describedby": reasonId } }}
            />
          }
          label={t("fields.emergencyAccount")}
        />
        <FormHelperText id={reasonId}>
          {roleIsAdministrator
            ? t("fields.emergencyHelperAdmin")
            : t("fields.emergencyHelperNonAdmin")}
        </FormHelperText>
      </Box>
    </>
  );
}
