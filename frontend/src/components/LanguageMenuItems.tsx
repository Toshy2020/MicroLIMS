import { Fragment } from "react";
import { ListItemIcon, ListItemText, MenuItem, Typography } from "@mui/material";
import CheckIcon from "@mui/icons-material/Check";
import { SUPPORTED_LANGUAGES, useTranslation, type Language } from "../i18n";
import { useOptionalLanguage } from "../i18n/LanguageProvider";

// Each language is named in itself, so a user who cannot read the current
// interface language can still find theirs.
const OWN_NAME_KEY: Record<Language, "language.english" | "language.arabic"> = {
  en: "language.english",
  ar: "language.arabic"
};

/**
 * @component LanguageMenuItems
 * @purpose The interface-language choice inside the account menu. The choice is saved to the user's
 * interface state (key "language") and applies at once; it is not a security or GMP control.
 * @a11y Two `menuitemradio` entries, each with its own `lang`; the current one has `aria-checked`.
 */
export function LanguageMenuItems({ onChosen }: { onChosen: () => void }) {
  const { t } = useTranslation();
  const language = useOptionalLanguage();
  if (!language) return null;
  return (
    <Fragment>
      <Typography
        component="li"
        role="presentation"
        sx={{ px: 2, pt: 1, pb: 0.25, fontSize: 12, fontWeight: 600, color: "text.secondary" }}
      >
        {t("language.label")}
      </Typography>
      {SUPPORTED_LANGUAGES.map((code) => {
        const selected = language.language === code;
        return (
          <MenuItem
            key={code}
            role="menuitemradio"
            aria-checked={selected}
            lang={code}
            onClick={() => {
              onChosen();
              void language.setLanguage(code).catch(() => {
                // The page keeps its current language; the choice can be made again.
              });
            }}
          >
            <ListItemIcon>{selected ? <CheckIcon fontSize="small" /> : null}</ListItemIcon>
            <ListItemText primary={t(OWN_NAME_KEY[code], { lng: code })} />
          </MenuItem>
        );
      })}
    </Fragment>
  );
}
