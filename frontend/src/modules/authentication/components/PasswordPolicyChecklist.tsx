import { Box, List, ListItem, ListItemIcon, ListItemText, Typography } from "@mui/material";
import { visuallyHidden } from "@mui/utils";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import RemoveIcon from "@mui/icons-material/Remove";

// Mirrors the server-side PasswordPolicy (MicroLIMS.Shared.Validation) -
// the server still validates authoritatively regardless of this.
export const PASSWORD_POLICY_RULES: { label: string; test: (pw: string) => boolean }[] = [
  { label: "At least 8 characters", test: (pw) => pw.length >= 8 },
  { label: "At least one uppercase letter", test: (pw) => /[A-Z]/.test(pw) },
  { label: "At least one lowercase letter", test: (pw) => /[a-z]/.test(pw) },
  { label: "At least one digit", test: (pw) => /[0-9]/.test(pw) },
  { label: "At least one special (non-alphanumeric) character", test: (pw) => /[^a-zA-Z0-9]/.test(pw) }
];

interface PasswordPolicyChecklistProps {
  password: string;
  blurred: boolean;
  heading?: string;
}

export function PasswordPolicyChecklist({
  password,
  blurred,
  heading = "Password must contain:"
}: PasswordPolicyChecklistProps) {
  return (
    <Box>
      <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5 }}>{heading}</Typography>
      <List dense disablePadding aria-label="Password policy requirements" aria-live="polite">
        {PASSWORD_POLICY_RULES.map((rule) => {
          const met = rule.test(password);
          // A dash until there is something to judge, rather than a
          // hollow circle that reads as an unselected radio button.
          const state = met ? "met" : blurred && password ? "failed" : "pending";
          const statusDesc = state === "met" ? "Satisfied" : state === "failed" ? "Requirement not met" : "Not met yet";
          return (
            <ListItem key={rule.label} disableGutters disablePadding>
              <ListItemIcon sx={{ minWidth: 28 }}>
                {state === "met" && <CheckCircleIcon fontSize="small" color="success" titleAccess="Met" />}
                {state === "failed" && <CancelIcon fontSize="small" color="error" titleAccess="Not met" />}
                {state === "pending" && <RemoveIcon fontSize="small" sx={{ color: "text.disabled" }} titleAccess="Not met yet" />}
              </ListItemIcon>
              <ListItemText
                primary={
                  <>
                    {rule.label}
                    <Box component="span" sx={visuallyHidden}>: {statusDesc}</Box>
                  </>
                }
                slotProps={{
                  primary: { sx: { fontSize: 13, color: state === "failed" ? "error.main" : met ? "text.primary" : "text.secondary" } }
                }}
              />
            </ListItem>
          );
        })}
      </List>
    </Box>
  );
}
