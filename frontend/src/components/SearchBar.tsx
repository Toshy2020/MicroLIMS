import { Box, InputBase, Button } from "@mui/material";
import { useState } from "react";

// .search-bar from the design: input + solid purple button, square-joined.
export function SearchBar({ value, onChange, onSubmit, placeholder }: { value: string; onChange: (v: string) => void; onSubmit?: () => void; placeholder?: string }) {
  const [local, setLocal] = useState(value);

  return (
    <Box sx={{ display: "flex", mb: 2.25 }}>
      <InputBase
        value={local}
        onChange={(e) => { setLocal(e.target.value); onChange(e.target.value); }}
        onKeyUp={(e) => e.key === "Enter" && onSubmit?.()}
        placeholder={placeholder ?? "Filter records by any field..."}
        sx={{
          flex: 1, px: 1.5, py: 1, border: "1px solid #d1d5db", borderRight: "none",
          borderRadius: "6px 0 0 6px", fontSize: 14
        }}
      />
      <Button onClick={onSubmit} variant="contained" sx={{ borderRadius: "0 6px 6px 0", px: 2, minWidth: 0 }}>
        🔍
      </Button>
    </Box>
  );
}
