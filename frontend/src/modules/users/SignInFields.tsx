import { Box, Checkbox, FormControlLabel, FormHelperText, MenuItem, TextField } from "@mui/material";
import type { SignInMethod } from "./services/UserService";

export const LINK_STATE_LABELS: Record<string, string> = {
  NotLinked: "Not linked",
  LinkedNeverSignedIn: "Linked, never signed in",
  Linked: "Linked",
  SignInRefused: "Sign-in refused"
};

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
  const reasonId = `${idPrefix}-emergency-reason`;
  return (
    <>
      <TextField
        select size="small" label="Sign-in method" value={method} fullWidth
        onChange={(e) => onMethodChange(e.target.value as SignInMethod)}
      >
        <MenuItem value="Local">Laboratory login</MenuItem>
        <MenuItem value="Directory">Company login</MenuItem>
      </TextField>
      {method === "Directory" && (
        <TextField
          size="small" label="Company username" value={directoryUsername} required fullWidth
          onChange={(e) => onDirectoryUsernameChange(e.target.value)}
          error={!directoryUsername.trim()} helperText="The user's name in the company directory"
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
          label="Emergency account"
        />
        <FormHelperText id={reasonId}>
          {roleIsAdministrator
            ? "A local sign-in kept for when the company directory is unavailable."
            : "Only available when the role is System Administrator."}
        </FormHelperText>
      </Box>
    </>
  );
}
