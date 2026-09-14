import { useEffect, useState, useMemo } from "react";
import { Box, Button, Alert, Grid, Typography, Stack } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { PageHeader } from "../../../components/PageHeader";
import { useAuth } from "../../../contexts/AuthContext";
import { masterDataOptions } from "../../../services/masterDataOptions";
import { OrganismOption } from "../../../hooks/useOrganisms";
import {
  MediaProductOption,
  MediaConfigurationItem,
  MediaIncubationConditionOption,
} from "./types/mediaConfigurationTypes";
import { MediaProductFilterBar } from "./components/MediaProductFilterBar";
import { MediaProductList } from "./components/MediaProductList";
import { MediaProductWorkspace, WORKSPACE_TABS } from "./components/MediaProductWorkspace";
import { AddMediaProductDialog } from "./dialogs/AddMediaProductDialog";
import { RenameMediaProductDialog } from "./dialogs/RenameMediaProductDialog";
import { ChangeMediaProductCodeDialog } from "./dialogs/ChangeMediaProductCodeDialog";

export function MediaConfigurationPage() {
  const { role } = useAuth();
  const isManager = role === "SectionHead" || role === "SystemAdministrator";
  const isSectionHead = role === "SectionHead";

  const [products, setProducts] = useState<MediaProductOption[]>([]);
  const [conditions, setConditions] = useState<MediaIncubationConditionOption[]>([]);
  const [configurations, setConfigurations] = useState<MediaConfigurationItem[]>([]);
  const [organisms, setOrganisms] = useState<OrganismOption[]>([]);
  const [selectedProductId, setSelectedProductId] = useState<number | null>(null);
  const [workspaceTab, setWorkspaceTab] = useState<number>(WORKSPACE_TABS.overview);

  // Search state
  const [searchQuery, setSearchQuery] = useState("");
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  // Dialog states
  const [addDialogOpen, setAddDialogOpen] = useState(false);
  const [renameProduct, setRenameProduct] = useState<MediaProductOption | null>(null);
  const [changeCodeProduct, setChangeCodeProduct] = useState<MediaProductOption | null>(null);

  const [loaded, setLoaded] = useState(false);

  const loadData = async () => {
    try {
      const [configs, prods, conditionList, orgList] = await Promise.all([
        masterDataOptions.getMediaConfigurations(),
        masterDataOptions.getMediaProducts(),
        masterDataOptions.getMediaIncubationConditions(),
        masterDataOptions.getOrganisms(),
      ]);
      setConfigurations(configs);
      setProducts(prods);
      setConditions(conditionList);
      setOrganisms(orgList);
    } catch (err: unknown) {
      const text =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        "Failed to load media configuration data.";
      setMessage({ text, ok: false });
    } finally {
      setLoaded(true);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleWorkspaceUpdated = (successMessage?: string) => {
    if (successMessage) setMessage({ text: successMessage, ok: true });
    loadData();
  };

  const emptyListMessage = !loaded
    ? "Loading media products..."
    : products.length === 0
      ? isManager
        ? "No media products yet. Add one to start configuring it."
        : "No media products have been configured yet."
      : "No media products match your search.";

  // Client-side fast search by media name or code
  const filteredProducts = useMemo(() => {
    if (!searchQuery.trim()) return products;
    const q = searchQuery.toLowerCase().trim();
    return products.filter((p) => {
      const nameMatch = p.name?.toLowerCase().includes(q);
      const codeMatch = p.code?.toLowerCase().includes(q);
      return nameMatch || codeMatch;
    });
  }, [products, searchQuery]);

  const selectedProduct = useMemo(() => {
    return products.find((p) => p.id === selectedProductId) || null;
  }, [products, selectedProductId]);

  const handleProductCreated = (newProduct: MediaProductOption) => {
    setMessage({ text: `Media product "${newProduct.name}" created successfully.`, ok: true });
    setSelectedProductId(newProduct.id);
    // A new medium needs an incubation condition before it can be configured.
    setWorkspaceTab(WORKSPACE_TABS.conditions);
    loadData();
  };

  const handleDeleteProduct = async (productToDelete: MediaProductOption) => {
    setMessage(null);
    try {
      await masterDataOptions.deleteMediaProduct(productToDelete.id);
      setMessage({ text: `Media product "${productToDelete.name}" deleted.`, ok: true });
      if (selectedProductId === productToDelete.id) {
        setSelectedProductId(null);
      }
      loadData();
    } catch (err: unknown) {
      const text =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        "Could not delete this media product.";
      setMessage({ text, ok: false });
    }
  };

  const handleProductRenamed = () => {
    setMessage({ text: "Media product renamed successfully.", ok: true });
    loadData();
  };

  const handleCodeChanged = () => {
    setMessage({ text: "Media product code changed successfully.", ok: true });
    loadData();
  };

  return (
    <Box sx={{ pb: 4 }}>
      {/* Top Header with Add Media Product button */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 2 }}>
        <PageHeader
          title="Media Configurations"
          subtitle="Configure each dehydrated medium and how it is used."
        />
        {isManager && (
          <Button
            variant="contained"
            color="primary"
            startIcon={<AddIcon />}
            onClick={() => setAddDialogOpen(true)}
            sx={{ textTransform: "none", fontWeight: 700, px: 2.5, py: 1 }}
          >
            Add Media Product
          </Button>
        )}
      </Box>

      {message && (
        <Alert severity={message.ok ? "success" : "error"} sx={{ mb: 2 }} onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}

      {/* Search and Filters Bar */}
      <MediaProductFilterBar
        searchQuery={searchQuery}
        onSearchChange={setSearchQuery}
        onReset={() => setSearchQuery("")}
      />

      {/* Main Content Area: Split Workspace Layout */}
      {selectedProduct ? (
        <Grid container spacing={2.5}>
          {/* Left Panel: ~35-40% compact register */}
          <Grid
            size={{
              xs: 12,
              md: 4.5,
              lg: 4,
            }}
          >
            <Stack
              direction="row"
              sx={{
                justifyContent: "space-between",
                alignItems: "center",
                mb: 1,
              }}
            >
              <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.primary" }}>
                Configured Media ({filteredProducts.length})
              </Typography>
            </Stack>
            <MediaProductList
              products={filteredProducts}
              selectedProductId={selectedProductId}
              onSelectProduct={(p) => setSelectedProductId(p.id)}
              onRename={(p) => setRenameProduct(p)}
              onDelete={handleDeleteProduct}
              isManager={isManager}
              emptyMessage={emptyListMessage}
            />
          </Grid>

          {/* Right Workspace Panel: ~60-65% detailed workspace */}
          <Grid
            size={{
              xs: 12,
              md: 7.5,
              lg: 8,
            }}
          >
            <MediaProductWorkspace
              product={selectedProduct}
              conditions={conditions.filter((c) => c.mediaProductId === selectedProduct.id)}
              configurations={configurations.filter((c) => c.mediaProductId === selectedProduct.id)}
              organisms={organisms}
              onClose={() => setSelectedProductId(null)}
              onUpdated={handleWorkspaceUpdated}
              onChangeCode={() => setChangeCodeProduct(selectedProduct)}
              isManager={isManager}
              isSectionHead={isSectionHead}
              activeTab={workspaceTab}
              onTabChange={setWorkspaceTab}
            />
          </Grid>
        </Grid>
      ) : (
        /* Full width register when no media product is selected */
        <Box sx={{ mt: 1 }}>
          <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.primary" }}>
            Configured Media ({filteredProducts.length})
          </Typography>
          <MediaProductList
            products={filteredProducts}
            selectedProductId={selectedProductId}
            onSelectProduct={(p) => setSelectedProductId(p.id)}
            onRename={(p) => setRenameProduct(p)}
            onDelete={handleDeleteProduct}
            isManager={isManager}
            emptyMessage={emptyListMessage}
          />
        </Box>
      )}

      {/* Add Media Product Dialog */}
      <AddMediaProductDialog
        open={addDialogOpen}
        onClose={() => setAddDialogOpen(false)}
        onSuccess={handleProductCreated}
      />

      {/* Rename Media Product Dialog */}
      <RenameMediaProductDialog
        open={renameProduct != null}
        product={renameProduct}
        onClose={() => setRenameProduct(null)}
        onSuccess={handleProductRenamed}
      />

      {/* Change Code Dialog */}
      <ChangeMediaProductCodeDialog
        open={changeCodeProduct != null}
        product={changeCodeProduct}
        onClose={() => setChangeCodeProduct(null)}
        onSuccess={handleCodeChanged}
      />
    </Box>
  );
}
