import { useId, useRef, useState } from "react";
import {
  Box,
  Button,
  Checkbox,
  Divider,
  IconButton,
  Popover,
  Stack,
  Typography,
} from "@mui/material";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ViewColumnIcon from "@mui/icons-material/ViewColumn";
import { useTranslation } from "../../../i18n";
import type { TableColumnDefinition } from "./tableRegistry";

export interface ColumnsMenuProps {
  columns: TableColumnDefinition[];
  visibleColumns: string[];
  idColumn: string;
  onChange: (nextColumns: string[]) => void;
  onReset: () => void;
}

export function ColumnsMenu({
  columns,
  visibleColumns,
  idColumn,
  onChange,
  onReset,
}: ColumnsMenuProps) {
  const { t } = useTranslation();
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const popoverId = useId();
  const reasonId = useId();
  const open = Boolean(anchorEl);

  const visibleSet = new Set(visibleColumns);
  const byKey = new Map(columns.map((c) => [c.key, c]));

  const orderedColumns: TableColumnDefinition[] = [
    ...visibleColumns
      .map((k) => byKey.get(k))
      .filter((c): c is TableColumnDefinition => Boolean(c)),
    ...columns.filter((c) => !visibleSet.has(c.key)),
  ];

  const handleClose = () => {
    setAnchorEl(null);
    triggerRef.current?.focus();
  };

  const toggleColumn = (key: string) => {
    if (key === idColumn) return;
    if (visibleSet.has(key)) {
      onChange(visibleColumns.filter((k) => k !== key));
    } else {
      onChange([...visibleColumns, key]);
    }
  };

  const moveColumn = (key: string, delta: -1 | 1) => {
    const idx = visibleColumns.indexOf(key);
    if (idx === -1) return;
    const target = idx + delta;
    if (target < 0 || target >= visibleColumns.length) return;
    const next = [...visibleColumns];
    [next[idx], next[target]] = [next[target], next[idx]];
    onChange(next);
  };

  return (
    <>
      <Button
        ref={triggerRef}
        size="small"
        variant="outlined"
        startIcon={<ViewColumnIcon fontSize="small" />}
        aria-haspopup="true"
        aria-expanded={open}
        aria-controls={open ? popoverId : undefined}
        onClick={(e) => setAnchorEl(e.currentTarget)}
      >
        {t("components.columnsMenu.columns")}
      </Button>
      <Popover
        id={popoverId}
        open={open}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
        transformOrigin={{ vertical: "top", horizontal: "right" }}
      >
        <Box
          role="region"
          aria-label={t("components.columnsMenu.columns")}
          sx={{ p: 1.5, minWidth: 280, maxWidth: 360 }}
        >
          <Typography variant="subtitle2" sx={{ mb: 1 }}>
            {t("components.columnsMenu.columns")}
          </Typography>
          <Stack spacing={0.5}>
            {orderedColumns.map((col) => {
              const isVisible = visibleSet.has(col.key);
              const isId = col.key === idColumn;
              const visibleIndex = visibleColumns.indexOf(col.key);

              return (
                <Box
                  key={col.key}
                  sx={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    gap: 1,
                  }}
                >
                  <Box sx={{ display: "flex", alignItems: "center", minWidth: 0, flex: 1 }}>
                    <Checkbox
                      size="small"
                      checked={isVisible}
                      disabled={isId}
                      onChange={() => toggleColumn(col.key)}
                      slotProps={{
                        input: {
                          "aria-label": col.label,
                          ...(isId ? { "aria-describedby": reasonId } : {}),
                        },
                      }}
                    />
                    <Box sx={{ minWidth: 0 }}>
                      <Typography variant="body2" noWrap>
                        {col.label}
                      </Typography>
                      {isId && (
                        <Typography
                          id={reasonId}
                          variant="caption"
                          color="text.secondary"
                          sx={{ display: "block" }}
                        >
                          {t("components.columnsMenu.requiredIdentifier")}
                        </Typography>
                      )}
                    </Box>
                  </Box>
                  <Box sx={{ display: "flex", alignItems: "center", flexShrink: 0 }}>
                    <IconButton
                      size="small"
                      aria-label={t("components.columnsMenu.moveUpAria", { label: col.label })}
                      disabled={!isVisible || visibleIndex <= 0}
                      onClick={() => moveColumn(col.key, -1)}
                    >
                      <ArrowUpwardIcon fontSize="small" />
                    </IconButton>
                    <IconButton
                      size="small"
                      aria-label={t("components.columnsMenu.moveDownAria", { label: col.label })}
                      disabled={
                        !isVisible ||
                        visibleIndex < 0 ||
                        visibleIndex >= visibleColumns.length - 1
                      }
                      onClick={() => moveColumn(col.key, 1)}
                    >
                      <ArrowDownwardIcon fontSize="small" />
                    </IconButton>
                  </Box>
                </Box>
              );
            })}
          </Stack>
          <Divider sx={{ my: 1 }} />
          <Box sx={{ display: "flex", justifyContent: "flex-end" }}>
            <Button size="small" onClick={onReset}>
              {t("components.columnsMenu.resetToDefault")}
            </Button>
          </Box>
        </Box>
      </Popover>
    </>
  );
}
