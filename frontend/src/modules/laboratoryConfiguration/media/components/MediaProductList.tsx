import { useState } from "react";
import { Paper, Box, Typography, Stack, IconButton, Chip, useTheme } from "@mui/material";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/Delete";
import { MediaProductOption } from "../types/mediaConfigurationTypes";
import { ConfirmationDialog } from "../../../../components/ConfirmationDialog";

interface MediaProductListProps {
  products: MediaProductOption[];
  selectedProductId: number | null;
  onSelectProduct: (product: MediaProductOption) => void;
  onRename: (product: MediaProductOption) => void;
  onDelete: (product: MediaProductOption) => void;
  isManager: boolean;
  emptyMessage: string;
}

export function MediaProductList({
  products,
  selectedProductId,
  onSelectProduct,
  onRename,
  onDelete,
  isManager,
  emptyMessage,
}: MediaProductListProps) {
  const [pendingDelete, setPendingDelete] = useState<MediaProductOption | null>(null);

  if (products.length === 0) {
    return (
      <Typography sx={{ color: "text.secondary", fontSize: 13, p: 2 }}>
        {emptyMessage}
      </Typography>
    );
  }

  return (
    <>
      <Stack spacing={1.5}>
        {products.map((product) => {
          const isSelected = selectedProductId === product.id;

          return (
            <MediaProductRowCard
              key={product.id}
              product={product}
              isSelected={isSelected}
              onSelectProduct={onSelectProduct}
              onRename={onRename}
              onDelete={(p) => setPendingDelete(p)}
              isManager={isManager}
            />
          );
        })}
      </Stack>

      <ConfirmationDialog
        open={pendingDelete != null}
        message={
          pendingDelete
            ? `Delete media product "${pendingDelete.name}" (${pendingDelete.code})? This cannot be undone. A product that has configurations or stock batches can't be deleted.`
            : ""
        }
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => {
          if (pendingDelete) {
            onDelete(pendingDelete);
          }
          setPendingDelete(null);
        }}
      />
    </>
  );
}

function MediaProductRowCard({
  product,
  isSelected,
  onSelectProduct,
  onRename,
  onDelete,
  isManager,
}: {
  product: MediaProductOption;
  isSelected: boolean;
  onSelectProduct: (product: MediaProductOption) => void;
  onRename: (product: MediaProductOption) => void;
  onDelete: (product: MediaProductOption) => void;
  isManager: boolean;
}) {
  const theme = useTheme();

  return (
    <Paper
      onClick={() => onSelectProduct(product)}
      sx={{
        overflow: "hidden",
        border: "1px solid",
        borderColor: isSelected ? "primary.main" : "divider",
        borderRadius: 1.5,
        transition: "all 0.15s ease-in-out",
        cursor: "pointer",
        bgcolor: isSelected ? theme.custom.status.purple.bg : "background.paper",
        boxShadow: isSelected ? "0 0 0 2px rgba(124, 58, 237, 0.2)" : "none",
        "&:hover": {
          borderColor: isSelected ? "primary.main" : "primary.light",
          bgcolor: isSelected ? theme.custom.status.purple.bg : "action.hover",
        },
      }}
    >
      <Box
        sx={{
          p: 2,
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
        }}
      >
        <Box>
          <Typography sx={{ fontWeight: 700, fontSize: 14, color: "text.primary" }}>
            {product.name}{" "}
            <Typography component="span" sx={{ color: "text.secondary", fontWeight: 400, fontSize: 12 }}>
              ({product.code})
            </Typography>
          </Typography>

          <Stack
            direction="row"
            spacing={0.75}
            sx={{
              alignItems: "center",
              flexWrap: "wrap",
              mt: 0.5,
            }}
          >
            <Chip
              label={`${product.configurationCount} ${product.configurationCount === 1 ? "Configuration" : "Configurations"}`}
              size="small"
              sx={{
                fontSize: 11,
                fontWeight: 600,
                color: theme.custom.status.purple.text,
                bgcolor: theme.custom.status.purple.bg,
                height: 20,
              }}
            />

            <Chip
              label={`${product.batchCount} ${product.batchCount === 1 ? "Batch" : "Batches"}`}
              size="small"
              variant="outlined"
              sx={{
                fontSize: 11,
                fontWeight: 600,
                height: 20,
              }}
            />
          </Stack>
        </Box>

        {isManager && (
          <Stack direction="row" spacing={0.5} onClick={(e) => e.stopPropagation()}>
            <IconButton
              size="small"
              onClick={(e) => {
                e.stopPropagation();
                onRename(product);
              }}
              title="Rename Product"
            >
              <EditIcon fontSize="small" />
            </IconButton>
            <IconButton
              size="small"
              color="error"
              onClick={(e) => {
                e.stopPropagation();
                onDelete(product);
              }}
              title="Delete Product"
            >
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Stack>
        )}
      </Box>
    </Paper>
  );
}
