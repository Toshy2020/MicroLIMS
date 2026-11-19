import { useNavigate } from "react-router-dom";
import { Box, List, ListItemButton, ListItemText, Typography } from "@mui/material";
import HistoryIcon from "@mui/icons-material/History";
import { inAppRoute } from "../../modules/navigation/recentRecordRoutes";
import { useRecentRecords } from "../../modules/navigation/useRecentRecords";
import type { RecentRecordDto } from "../../modules/navigation/services/recentRecordsService";

interface Row {
  r: RecentRecordDto;
  to: string;
}

// Shown under the favourites while the search box is empty. Hidden when the
// list is empty or could not be loaded.
export function SearchRecents({ onSelect }: { onSelect: () => void }) {
  const navigate = useNavigate();
  const { items, failed } = useRecentRecords();
  const rows: Row[] = [];
  for (const r of items) {
    const to = inAppRoute(r.route);
    if (to !== null) rows.push({ r, to });
  }
  if (failed || rows.length === 0) return null;

  return (
    <Box sx={{ mb: 2 }} role="region" aria-label="Recent records">
      <Typography variant="caption" color="text.secondary" sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
        <HistoryIcon aria-hidden sx={{ fontSize: 14 }} /> RECENT
      </Typography>
      <List disablePadding>
        {rows.map(({ r, to }) => (
          <ListItemButton
            key={`${r.recordType}:${r.recordId}`}
            onClick={() => {
              onSelect();
              navigate(to);
            }}
            sx={{ borderRadius: 1.5 }}
          >
            <ListItemText
              primary={r.title ? `${r.identifier} ${r.title}` : r.identifier}
              slotProps={{ primary: { sx: { fontSize: 14 } } }}
            />
          </ListItemButton>
        ))}
      </List>
    </Box>
  );
}
