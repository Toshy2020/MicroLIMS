import { useState } from "react";
import { IconButton, Tooltip } from "@mui/material";
import { visuallyHidden } from "@mui/utils";
import StarIcon from "@mui/icons-material/Star";
import StarBorderIcon from "@mui/icons-material/StarBorder";
import { FAVOURITES_FULL_MESSAGE, type Favourite } from "./favourites";
import { useFavourites } from "./useFavourites";
import { useTranslation } from "../../i18n";

interface FavouriteStarProps {
  favourite: Favourite;
  // What is being starred, for the accessible name ("Add Dashboard to favourites").
  name: string;
  size?: "small" | "medium";
}

export function FavouriteStar({ favourite, name, size = "medium" }: FavouriteStarProps) {
  const { t } = useTranslation("navigation");
  const { isFavourite, toggle, full, loaded } = useFavourites();
  const [announcement, setAnnouncement] = useState("");
  const active = isFavourite(favourite);
  const label = active ? t("favourites.removeAria", { name }) : t("favourites.addAria", { name });

  const onClick = async () => {
    if (!active && full) {
      // Refused before any request is sent; say why where a screen reader will hear it.
      setAnnouncement(t("favourites.full", { defaultValue: FAVOURITES_FULL_MESSAGE }));
      await toggle(favourite);
      return;
    }
    const wasActive = active;
    const changed = await toggle(favourite);
    if (changed) setAnnouncement(wasActive ? t("favourites.removed") : t("favourites.added"));
  };

  return (
    <>
      <Tooltip title={label}>
        <span>
          <IconButton
            size={size}
            aria-label={label}
            aria-pressed={active}
            disabled={!loaded}
            onClick={() => void onClick()}
            sx={{ color: active ? "warning.main" : "text.secondary" }}
          >
            {active ? <StarIcon fontSize="inherit" /> : <StarBorderIcon fontSize="inherit" />}
          </IconButton>
        </span>
      </Tooltip>
      <span role="status" aria-live="polite" style={visuallyHidden}>
        {announcement}
      </span>
    </>
  );
}
