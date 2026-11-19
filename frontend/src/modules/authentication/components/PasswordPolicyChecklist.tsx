import { Box, List, ListItem, ListItemIcon, ListItemText, Typography } from "@mui/material";
import { visuallyHidden } from "@mui/utils";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import CancelIcon from "@mui/icons-material/Cancel";
import RemoveIcon from "@mui/icons-material/Remove";
import { useTranslation } from "../../../i18n";

// Mirrors the server-side PasswordPolicy (MicroLIMS.Shared.Validation) -
// the server still validates authoritatively regardless of this.
// `label` is the English text; the screen shows the translation under `key`.
export const PASSWORD_POLICY_RULES: { key: string; label: string; test: (pw: string) => boolean }[] = [
  { key: "minLength", label: "At least 8 characters", test: (pw) => pw.length >= 8 },
  { key: "uppercase", label: "At least one uppercase letter", test: (pw) => /[A-Z]/.test(pw) },
  { key: "lowercase", label: "At least one lowercase letter", test: (pw) => /[a-z]/.test(pw) },
  { key: "digit", label: "At least one digit", test: (pw) => /[0-9]/.test(pw) },
  { key: "special", label: "At least one special (non-alphanumeric) character", test: (pw) => /[^a-zA-Z0-9]/.test(pw) }
];

interface PasswordPolicyChecklistProps {
  password: string;
  blurred: boolean;
  heading?: string;
}

export function PasswordPolicyChecklist({
  password,
  blurred,
  heading
}: PasswordPolicyChecklistProps) {
  const { t } = useTranslation("account");
  return (
    <Box>
      <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 0.5 }}>{heading ?? t("passwordPolicy.heading")}</Typography>
      <List dense disablePadding aria-label={t("passwordPolicy.listAria")} aria-live="polite">
        {PASSWORD_POLICY_RULES.map((rule) => {
          const met = rule.test(password);
          // A dash until there is something to judge, rather than a
          // hollow circle that reads as an unselected radio button.
          const state = met ? "met" : blurred && password ? "failed" : "pending";
          const statusDesc = state === "met" ? t("passwordPolicy.satisfied") : state === "failed" ? t("passwordPolicy.requirementNotMet") : t("passwordPolicy.notMetYet");
          return (
            <ListItem key={rule.label} disableGutters disablePadding>
              <ListItemIcon sx={{ minWidth: 28 }}>
                {state === "met" && <CheckCircleIcon fontSize="small" color="success" titleAccess={t("passwordPolicy.met")} />}
                {state === "failed" && <CancelIcon fontSize="small" color="error" titleAccess={t("passwordPolicy.notMet")} />}
                {state === "pending" && <RemoveIcon fontSize="small" sx={{ color: "text.disabled" }} titleAccess={t("passwordPolicy.notMetYet")} />}
              </ListItemIcon>
              <ListItemText
                primary={
                  <>
                    {t(`passwordPolicy.${rule.key}`)}
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
