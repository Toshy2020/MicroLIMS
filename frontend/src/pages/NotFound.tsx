import { Button, Paper } from "@mui/material";
import SearchOffOutlinedIcon from "@mui/icons-material/SearchOffOutlined";
import { Link, useLocation } from "react-router-dom";
import { PageHeader } from "../components/PageHeader";
import { EmptyState } from "../components/lab/EmptyState";
import { useTranslation } from "../i18n";

// Shown for any address inside the app that matches no page - a mistyped or
// outdated link used to leave an empty content area with no explanation.
export function NotFoundPage() {
  const { t } = useTranslation("account");
  const { pathname } = useLocation();
  return (
    <>
      <PageHeader title={t("notFound.title")} />
      <Paper variant="outlined">
        <EmptyState
          icon={<SearchOffOutlinedIcon />}
          title={t("notFound.heading")}
          description={t("notFound.description", { pathname })}
          action={<Button component={Link} to="/dashboard" variant="contained">{t("notFound.goToDashboard")}</Button>}
        />
      </Paper>
    </>
  );
}
