import { useNavigate } from "react-router-dom";
import { Box, List, ListItemButton, ListItemText, Typography } from "@mui/material";
import StarIcon from "@mui/icons-material/Star";
import { favouriteKey } from "../../modules/navigation/favourites";
import { useFavourites } from "../../modules/navigation/useFavourites";

// Shown above the recent searches while the search box is empty. Favourites the
// user can no longer open are left out; Manage favourites lists them for removal.
export function SearchFavourites({ onSelect }: { onSelect: () => void }) {
  const navigate = useNavigate();
  const { items } = useFavourites();
  const usable = items.filter((i) => i.available && i.to);
  if (usable.length === 0) return null;

  return (
    <Box sx={{ mb: 2 }} role="region" aria-label="Favourites">
      <Typography variant="caption" color="text.secondary" sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
        <StarIcon sx={{ fontSize: 14 }} /> FAVOURITES
      </Typography>
      <List disablePadding>
        {usable.map((item) => (
          <ListItemButton
            key={favouriteKey(item.fav)}
            onClick={() => {
              onSelect();
              navigate(item.to as string);
            }}
            sx={{ borderRadius: 1.5 }}
          >
            <ListItemText primary={item.label} slotProps={{ primary: { sx: { fontSize: 14 } } }} />
          </ListItemButton>
        ))}
      </List>
    </Box>
  );
}
