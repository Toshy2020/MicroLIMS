import { useEffect, useState, type ReactNode } from "react";
import { Paper, Typography, Box, Alert } from "@mui/material";
import { PageHeader } from "../components/PageHeader";
import { SectionTitle } from "../components/SectionTitle";
import { DialogBodySkeleton } from "../components/DialogBodySkeleton";
import { authenticationService } from "../modules/authentication/services/authenticationService";
import { ProfileSecuritySection } from "../modules/authentication/components/ProfileSecuritySection";
import { CurrentUserInfo } from "../modules/authentication/types/authTypes";
import { NotificationPreferences } from "../modules/notifications/NotificationPreferences";
import { NotificationMandatoryPanel } from "../modules/notifications/NotificationMandatoryPanel";
import { formatRoleName } from "../utils/userDisplay";
import { formatDateTimeWithTimeZone } from "../utils/formatDate";
import { useTranslation } from "../i18n";
import { Ltr } from "../components/Ltr";

function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <Box sx={{ mb: 1.5, "&:last-of-type": { mb: 0 } }}>
      <Typography component="dt" variant="caption" sx={{ color: "text.secondary" }}>{label}</Typography>
      <Typography component="dd" sx={{ m: 0, fontWeight: 600 }}>{value}</Typography>
    </Box>
  );
}

// Read-only - in a GMP system, users don't self-serve name/role changes;
// an administrator does that via the Users module.
export function ProfilePage() {
  const { t } = useTranslation("account");
  const [info, setInfo] = useState<CurrentUserInfo | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [prefsRefreshKey, setPrefsRefreshKey] = useState(0);

  useEffect(() => {
    authenticationService.me().then(setInfo).catch(() => setError(t("profile.loadFailed")));
  }, [t]);

  if (error) return <Alert severity="error">{error}</Alert>;
  if (!info) return <DialogBodySkeleton blocks={4} blockHeight={32} />;

  return (
    <>
      <PageHeader title={t("profile.title")} subtitle={t("profile.subtitle")} />
      <SectionTitle>{t("profile.accountSection")}</SectionTitle>
      <Paper component="dl" sx={{ p: 2.5, maxWidth: 440, m: 0 }}>
        <Field label={t("profile.fullName")} value={info.fullName} />
        <Field label={t("profile.userId")} value={<Ltr>{info.username}</Ltr>} />
        <Field label={t("profile.role")} value={formatRoleName(info.role, info.roleName)} />
        <Field label={t("profile.lastLogin")} value={info.lastLoginAt ? <Ltr>{formatDateTimeWithTimeZone(info.lastLoginAt)}</Ltr> : t("profile.never")} />
        <Field label={t("profile.passwordLastChanged")} value={info.passwordChangedAt ? <Ltr>{formatDateTimeWithTimeZone(info.passwordChangedAt)}</Ltr> : t("profile.never")} />
      </Paper>
      <ProfileSecuritySection />
      <NotificationPreferences refreshKey={prefsRefreshKey} />
      <NotificationMandatoryPanel onSaved={() => setPrefsRefreshKey((n) => n + 1)} />
    </>
  );
}
