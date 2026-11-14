import type { ReactNode } from "react";
import { Box, InputAdornment, TextField } from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import { getTable } from "./tableRegistry";
import type { UseTableState } from "./useTableState";
import { ColumnsMenu } from "./ColumnsMenu";
import { DensityToggle } from "./DensityToggle";

export interface TableToolbarProps {
  tableKey: string;
  tableState: UseTableState;
  searchPlaceholder?: string;
  showSearch?: boolean;
  children?: ReactNode;
  actions?: ReactNode;
}

export function TableToolbar({
  tableKey,
  tableState,
  searchPlaceholder = "Search...",
  showSearch = true,
  children,
  actions,
}: TableToolbarProps) {
  const def = getTable(tableKey);

  return (
    <Box
      role="toolbar"
      aria-label={`${def.title} toolbar`}
      sx={{
        display: "flex",
        flexWrap: "wrap",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 1.5,
        p: 1.5,
        borderBottom: "1px solid",
        borderColor: "divider",
        bgcolor: "background.paper",
      }}
    >
      <Box
        sx={{
          display: "flex",
          flexWrap: "wrap",
          alignItems: "center",
          gap: 1.5,
          flex: 1,
          minWidth: 0,
        }}
      >
        {showSearch && (
          <TextField
            size="small"
            value={tableState.state.search}
            onChange={(e) => tableState.setSearch(e.target.value)}
            placeholder={searchPlaceholder}
            slotProps={{
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon fontSize="small" />
                  </InputAdornment>
                ),
                inputProps: {
                  "aria-label": `Search ${def.title}`,
                },
              },
            }}
            sx={{ minWidth: 200 }}
          />
        )}
        {children}
      </Box>
      <Box
        sx={{
          display: "flex",
          flexWrap: "wrap",
          alignItems: "center",
          gap: 1,
        }}
      >
        {actions}
        <DensityToggle
          density={tableState.state.density}
          onChange={tableState.setDensity}
        />
        <ColumnsMenu
          columns={def.columns}
          visibleColumns={tableState.state.columns}
          idColumn={def.idColumn}
          onChange={tableState.setColumns}
          onReset={() =>
            tableState.setColumns(
              def.columns.filter((c) => c.defaultVisible).map((c) => c.key)
            )
          }
        />
      </Box>
    </Box>
  );
}
