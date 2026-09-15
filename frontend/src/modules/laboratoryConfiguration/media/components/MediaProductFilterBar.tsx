import { Box, TextField, Button, Stack, InputAdornment } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import RestartAltIcon from "@mui/icons-material/RestartAlt";

interface MediaProductFilterBarProps {
  searchQuery: string;
  onSearchChange: (value: string) => void;
  onReset: () => void;
}

export function MediaProductFilterBar({
  searchQuery,
  onSearchChange,
  onReset,
}: MediaProductFilterBarProps) {
  const isFiltered = searchQuery.trim() !== "";

  return (
    <Box
      sx={{
        mb: 2.5,
        p: 2,
        bgcolor: "background.paper",
        borderRadius: 1.5,
        border: "1px solid",
        borderColor: "divider",
      }}
    >
      <Stack
        direction={{ xs: "column", sm: "row" }}
        spacing={1.5}
        sx={{
          alignItems: "center",
        }}
      >
        <TextField
          size="small"
          placeholder="Search by media name or code..."
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          sx={{ flexGrow: 1, minWidth: { xs: "100%", sm: 280 } }}
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" sx={{ color: "text.secondary" }} />
                </InputAdornment>
              ),
            },
          }}
        />

        {isFiltered && (
          <Button
            size="small"
            variant="outlined"
            color="inherit"
            startIcon={<RestartAltIcon fontSize="small" />}
            onClick={onReset}
            sx={{ textTransform: "none", height: 40 }}
          >
            Reset
          </Button>
        )}
      </Stack>
    </Box>
  );
}
