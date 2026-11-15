import { useId } from "react";
import {
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  List,
  ListItem,
  Typography
} from "@mui/material";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import type { MenuGroup } from "../../routes/menuConfig";
import { favouriteKey, MAX_FAVOURITES } from "./favourites";
import { useFavourites } from "./useFavourites";

interface ManageFavouritesDialogProps {
  open: boolean;
  onClose: () => void;
  groups?: MenuGroup[];
}

// Reorder with Move up / Move down (no dragging needed) and remove. A favourite
// the user can no longer open stays listed, greyed, so it can be removed.
export function ManageFavouritesDialog({ open, onClose, groups }: ManageFavouritesDialogProps) {
  const { items, move, remove } = useFavourites(groups);
  const titleId = useId();

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm" aria-labelledby={titleId}>
      <DialogTitle id={titleId}>Manage favourites</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
          {items.length} of {MAX_FAVOURITES} favourites. The sidebar and search show them in this order.
        </Typography>
        {items.length === 0 ? (
          <Typography variant="body2">No favourites yet. Use the star on a page to add one.</Typography>
        ) : (
          <List disablePadding aria-label="Favourites">
            {items.map((item, index) => (
              <ListItem
                key={favouriteKey(item.fav)}
                divider
                disableGutters
                sx={{ gap: 0.5, opacity: item.available ? 1 : 0.7 }}
              >
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Typography
                    variant="body2"
                    noWrap
                    title={item.label}
                    sx={{ color: item.available ? "text.primary" : "text.disabled" }}
                  >
                    {item.label}
                  </Typography>
                  {!item.available && (
                    <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                      No longer available
                    </Typography>
                  )}
                </Box>
                <IconButton
                  size="small"
                  aria-label={`Move ${item.label} up`}
                  aria-disabled={index === 0}
                  onClick={() => void move(index, -1)}
                >
                  <ArrowUpwardIcon fontSize="small" />
                </IconButton>
                <IconButton
                  size="small"
                  aria-label={`Move ${item.label} down`}
                  aria-disabled={index === items.length - 1}
                  onClick={() => void move(index, 1)}
                >
                  <ArrowDownwardIcon fontSize="small" />
                </IconButton>
                <IconButton size="small" aria-label={`Remove ${item.label}`} onClick={() => void remove(index)}>
                  <DeleteOutlinedIcon fontSize="small" />
                </IconButton>
              </ListItem>
            ))}
          </List>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
