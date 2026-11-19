import { useEffect, useMemo, useState } from "react";
import { Button, Alert, Tabs, Tab } from "@mui/material";
import { i18n, useTranslation } from "../../../i18n";
import { usePersistedTab } from "../../../hooks/usePersistedTab";
import AddIcon from "@mui/icons-material/Add";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { LabPage } from "../../../components/lab";
import { useTableState } from "../../../components/lab/table/useTableState";
import { CryovialService } from "./services/CryovialService";
import { CryovialItem, CryovialFilterState } from "./types/cryovialTypes";
import { CryovialKpiCards } from "./components/CryovialKpiCards";
import { CryovialFilterBar } from "./components/CryovialFilterBar";
import { CryovialReviewTable } from "./components/CryovialReviewTable";
import { PrepareCryovialBatchDialog } from "./components/PrepareCryovialBatchDialog";
import { ThawVialReasonDialog } from "./components/ThawVialReasonDialog";
import { DestroyCryovialDialog } from "./components/DestroyCryovialDialog";
import { MoveCryovialStorageDialog } from "./components/MoveCryovialStorageDialog";
import { ApprovedCryovialListPage } from "../../inventory/approvedLists/ApprovedCryovialListPage";
import { useAuth } from "../../../contexts/AuthContext";
import { PERMISSIONS } from "../../../routes/routes";
import { PrintLabelsDialog } from "../../../components/labels/PrintLabelsDialog";
import { usePostCreateLabelPrint } from "../../../components/labels/usePostCreateLabelPrint";

const TABLE_KEY = "labConfig.cryovials";
const DROPDOWN_FILTERS = ["organism", "status", "expiryRange"] as const;

function textFilter(filters: Record<string, string | string[]>, name: string): string {
  const value = filters[name];
  return typeof value === "string" ? value : "";
}

const PAGE_TABS = ["register", "approved"] as const;
type PageTab = (typeof PAGE_TABS)[number];

export function CryovialsPage() {
  const { t } = useTranslation("labConfig");
  const [tab, setTab] = usePersistedTab("cryovials", PAGE_TABS, "tab");
  const [cryovials, setCryovials] = useState<CryovialItem[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  const loadData = async () => {
    try {
      setLoading(true);
      const data = await CryovialService.getAll();
      setCryovials(data || []);
    } catch {
      setMessage({ text: t("cryovials.page.loadError"), ok: false });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const tabs = (
    <Tabs
      value={tab}
      onChange={(_, v: PageTab) => setTab(v)}
      sx={{ mb: 2, borderBottom: 1, borderColor: "divider" }}
    >
      <Tab value="register" label={t("cryovials.page.tabRegister")} />
      <Tab value="approved" label={t("cryovials.page.tabApproved")} />
    </Tabs>
  );

  if (tab === "approved") {
    return (
      <>
        {tabs}
        <ApprovedCryovialListPage embedded />
      </>
    );
  }

  return (
    <>
      {tabs}
      <CryovialsRegisterContent
        cryovials={cryovials}
        loading={loading}
        message={message}
        onDismissMessage={() => setMessage(null)}
        onMessage={setMessage}
        onReload={loadData}
      />
    </>
  );
}

interface CryovialsRegisterContentProps {
  cryovials: CryovialItem[] | null;
  loading: boolean;
  message: { text: string; ok: boolean } | null;
  onDismissMessage: () => void;
  onMessage: (msg: { text: string; ok: boolean } | null) => void;
  onReload: () => Promise<void>;
}

function CryovialsRegisterContent({
  cryovials,
  loading,
  message,
  onDismissMessage,
  onMessage,
  onReload
}: CryovialsRegisterContentProps) {
  const { t } = useTranslation("labConfig");
  const { permissions } = useAuth();

  // Search, dropdowns, sort and page live in the address and in saved views; columns in the column chooser.
  const tableState = useTableState(TABLE_KEY);
  const { setFilter, setSearch, applyState } = tableState;
  const stateFilters = tableState.state.filters;
  const stateSearch = tableState.state.search;
  const filters = useMemo<CryovialFilterState>(
    () => ({
      search: stateSearch,
      status: textFilter(stateFilters, "status"),
      organism: textFilter(stateFilters, "organism"),
      expiryRange: textFilter(stateFilters, "expiryRange")
    }),
    [stateFilters, stateSearch]
  );

  // Dialog states
  const [isPrepareOpen, setIsPrepareOpen] = useState(false);
  const [thawItem, setThawItem] = useState<CryovialItem | null>(null);
  const [destroyItem, setDestroyItem] = useState<CryovialItem | null>(null);
  const [moveItem, setMoveItem] = useState<CryovialItem | null>(null);
  const [pendingDecision, setPendingDecision] = useState<{ cryovial: CryovialItem; approved: boolean } | null>(null);
  const [decisionComment, setDecisionComment] = useState("");
  const postCreatePrint = usePostCreateLabelPrint();

  const handleReset = () => {
    applyState(
      { filters: {}, search: "", sort: tableState.state.sort, pageSize: tableState.state.pageSize },
      tableState.viewId
    );
  };

  // The filter bar reports one changed field at a time.
  const handleFilterChange = (newFilters: CryovialFilterState) => {
    if (newFilters.search !== filters.search) {
      setSearch(newFilters.search);
      return;
    }
    const changed = DROPDOWN_FILTERS.find((name) => newFilters[name] !== filters[name]);
    if (changed) setFilter(changed, newFilters[changed]);
  };

  // Filtered dataset driven by form filters
  const filteredCryovials = useMemo(() => {
    if (!cryovials) return [];

    return cryovials.filter((c) => {
      // 1. Text search
      if (filters.search.trim()) {
        const q = filters.search.toLowerCase().trim();
        const codeMatch = c.code?.toLowerCase().includes(q);
        const orgName = (c.organism?.scientificName ?? c.organismNameSnapshot ?? "").toLowerCase();
        const orgMatch = orgName.includes(q);
        const matName = (c.material?.materialName ?? "").toLowerCase();
        const batchMatch = (c.material?.batchNumber ?? "").toLowerCase().includes(q);
        const mfgMatch = (c.manufacturerName ?? "").toLowerCase().includes(q);
        if (!codeMatch && !orgMatch && !matName.includes(q) && !batchMatch && !mfgMatch) {
          return false;
        }
      }

      // 2. Status filter
      if (filters.status) {
        if (filters.status === "Approved") {
          if (c.approvalStatus !== "Approved" || c.isDestroyed) return false;
        } else if (filters.status === "PendingReview") {
          if (c.approvalStatus !== "PendingReview" || c.isDestroyed) return false;
        } else if (filters.status === "Rejected") {
          if (c.approvalStatus !== "Rejected") return false;
        } else if (filters.status === "Destroyed") {
          if (!c.isDestroyed) return false;
        } else if (filters.status === "Depleted") {
          if (c.vialsRemaining > 0 || c.isDestroyed) return false;
        }
      }

      // 3. Organism filter
      if (filters.organism) {
        const orgName = c.organism?.scientificName ?? c.organismNameSnapshot;
        if (orgName !== filters.organism) return false;
      }

      // 4. Expiry filter
      if (filters.expiryRange) {
        const now = new Date();
        const expiry = new Date(c.expiryDate);
        if (filters.expiryRange === "expired") {
          if (expiry > now) return false;
        } else if (filters.expiryRange === "valid") {
          if (expiry <= now) return false;
        } else if (filters.expiryRange === "expiring_30") {
          const diffDays = Math.ceil((expiry.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));
          if (diffDays <= 0 || diffDays > 30) return false;
        }
      }

      return true;
    });
  }, [cryovials, filters]);

  // Actions
  const handleApproveClick = (cryovial: CryovialItem, approved: boolean) => {
    setDecisionComment("");
    setPendingDecision({ cryovial, approved });
  };

  const confirmDecision = async (password: string) => {
    if (!pendingDecision) return;
    await CryovialService.approve(pendingDecision.cryovial.id, pendingDecision.approved, password, decisionComment);
    onMessage({
      text: t(pendingDecision.approved ? "cryovials.page.approved" : "cryovials.page.rejected", {
        code: pendingDecision.cryovial.code
      }),
      ok: true
    });
    setPendingDecision(null);
    setDecisionComment("");
    onReload();
  };

  const handleThawConfirm = async (reason: string) => {
    if (!thawItem) return;
    await CryovialService.thawVial(thawItem.id, reason);
    onMessage({ text: t("cryovials.page.thawed", { code: thawItem.code }), ok: true });
    setThawItem(null);
    onReload();
  };

  const handleDestroyConfirm = async () => {
    if (!destroyItem) return;
    await CryovialService.destroy(destroyItem.id);
    onMessage({
      text: t("cryovials.page.destroyed", { code: destroyItem.code }),
      ok: true
    });
    setDestroyItem(null);
    onReload();
  };

  const isFiltered = Object.values(filters).some((v) => v !== "");

  return (
    <>
      <LabPage
        title={t("cryovials.page.title")}
        subtitle={t("cryovials.page.subtitle")}
        actions={
          <Button variant="contained" color="primary" startIcon={<AddIcon />} onClick={() => setIsPrepareOpen(true)} sx={{ whiteSpace: "nowrap" }}>
            {t("cryovials.page.prepare")}
          </Button>
        }
        kpis={<CryovialKpiCards items={cryovials || []} loading={loading} />}
        filters={
          <CryovialFilterBar
            items={cryovials || []}
            filters={filters}
            onFilterChange={handleFilterChange}
            onReset={handleReset}
            resultCount={filteredCryovials.length}
            onRefresh={onReload}
            refreshing={loading}
          />
        }
      >
        {message && (
          <Alert severity={message.ok ? "success" : "error"} onClose={onDismissMessage}>
            {message.text}
          </Alert>
        )}

        <CryovialReviewTable
          items={filteredCryovials}
          loading={loading}
          isFiltered={isFiltered}
          tableKey={TABLE_KEY}
          tableState={tableState}
          onApproveClick={handleApproveClick}
          onThawClick={(item) => setThawItem(item)}
          onDestroyClick={(item) => setDestroyItem(item)}
          onMoveStorageClick={(item) => setMoveItem(item)}
          canMoveStorage={permissions.includes(PERMISSIONS.CRYOVIALS_APPROVE)}
        />
      </LabPage>

      {/* Preparation Dialog */}
      <PrepareCryovialBatchDialog
        open={isPrepareOpen}
        onClose={() => setIsPrepareOpen(false)}
        onSuccess={(_msg, created) => {
          setIsPrepareOpen(false);
          onMessage({ text: t("cryovials.page.prepared"), ok: true });
          onReload();
          if (created) {
            postCreatePrint.offerPrint([
              {
                entityType: "Cryovial",
                entityId: created.id,
                code: created.code,
                displayName: created.organismName || created.code
              }
            ]);
          }
        }}
      />

      <PrintLabelsDialog
        open={postCreatePrint.dialogOpen}
        targets={postCreatePrint.targets}
        onClose={postCreatePrint.closeDialog}
      />

      {/* Thaw Confirmation Dialog */}
      <ThawVialReasonDialog
        open={thawItem != null}
        cryovial={thawItem}
        onCancel={() => setThawItem(null)}
        onConfirm={handleThawConfirm}
      />

      <MoveCryovialStorageDialog
        cryovial={moveItem}
        onClose={() => setMoveItem(null)}
        onMoved={(text) => {
          setMoveItem(null);
          onMessage({ text, ok: true });
          onReload();
        }}
      />

      {/* Destroy Confirmation Dialog */}
      <DestroyCryovialDialog
        open={destroyItem != null}
        cryovial={destroyItem}
        onCancel={() => setDestroyItem(null)}
        onConfirm={handleDestroyConfirm}
      />

      {/* 21 CFR Part 11 Electronic Signature Dialog */}
      {pendingDecision && (
        <SignatureDialog
          open={true}
          meaningStatement={i18n.getFixedT("en", "labConfig")(
            pendingDecision.approved ? "cryovials.page.approveStatement" : "cryovials.page.rejectStatement",
            { code: pendingDecision.cryovial.code }
          )}
          meaningStatementTranslation={t(
            pendingDecision.approved ? "cryovials.page.approveStatement" : "cryovials.page.rejectStatement",
            { code: pendingDecision.cryovial.code }
          )}
          showComment={true}
          comment={decisionComment}
          onCommentChange={setDecisionComment}
          onConfirm={confirmDecision}
          onCancel={() => setPendingDecision(null)}
        />
      )}
    </>
  );
}
