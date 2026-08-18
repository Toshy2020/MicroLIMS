import { useEffect, useMemo, useState } from "react";
import { Box, Button, Alert } from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { PageHeader } from "../../../components/PageHeader";
import { SectionTitle } from "../../../components/SectionTitle";
import { SignatureDialog } from "../../../components/SignatureDialog";
import { LoadingSpinner } from "../../../components/LoadingSpinner";
import { CryovialService } from "./services/CryovialService";
import { CryovialItem, CryovialFilterState } from "./types/cryovialTypes";
import { CryovialKpiCards } from "./components/CryovialKpiCards";
import { CryovialFilterBar } from "./components/CryovialFilterBar";
import { CryovialReviewTable } from "./components/CryovialReviewTable";
import { PrepareCryovialBatchDialog } from "./components/PrepareCryovialBatchDialog";
import { ThawVialReasonDialog } from "./components/ThawVialReasonDialog";
import { DestroyCryovialDialog } from "./components/DestroyCryovialDialog";
import { brandColors } from "../../../theme";

const INITIAL_FILTERS: CryovialFilterState = {
  search: "",
  status: "",
  organism: "",
  expiryRange: ""
};

export function CryovialsPage() {
  const [cryovials, setCryovials] = useState<CryovialItem[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);

  // Filters state
  const [filters, setFilters] = useState<CryovialFilterState>(INITIAL_FILTERS);

  // Pagination state
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(25);

  // Dialog states
  const [isPrepareOpen, setIsPrepareOpen] = useState(false);
  const [thawItem, setThawItem] = useState<CryovialItem | null>(null);
  const [destroyItem, setDestroyItem] = useState<CryovialItem | null>(null);
  const [pendingDecision, setPendingDecision] = useState<{ cryovial: CryovialItem; approved: boolean } | null>(null);
  const [decisionComment, setDecisionComment] = useState("");

  const loadData = async () => {
    try {
      setLoading(true);
      const data = await CryovialService.getAll();
      setCryovials(data || []);
    } catch {
      setMessage({ text: "Failed to load cryovials register.", ok: false });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleReset = () => {
    setFilters(INITIAL_FILTERS);
    setPage(0);
  };

  const handleFilterChange = (newFilters: CryovialFilterState) => {
    setFilters(newFilters);
    setPage(0);
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
    setMessage({
      text: `Cryovial batch ${pendingDecision.cryovial.code} has been ${
        pendingDecision.approved ? "Approved" : "Rejected"
      }.`,
      ok: true
    });
    setPendingDecision(null);
    setDecisionComment("");
    loadData();
  };

  const handleThawConfirm = async (reason: string) => {
    if (!thawItem) return;
    await CryovialService.thawVial(thawItem.id, reason);
    setMessage({ text: `Vial from ${thawItem.code} successfully thawed and logged.`, ok: true });
    setThawItem(null);
    loadData();
  };

  const handleDestroyConfirm = async () => {
    if (!destroyItem) return;
    await CryovialService.destroy(destroyItem.id);
    setMessage({
      text: `Batch ${destroyItem.code} has been decommissioned and destroyed.`,
      ok: true
    });
    setDestroyItem(null);
    loadData();
  };

  return (
    <>
      {/* Top Header with Primary Action Button */}
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", mb: 1.5, flexWrap: "wrap", gap: 1.5 }}>
        <PageHeader
          title="Cryovials"
          subtitle="Prepare working cryovial batches from an approved lyophilized microorganism material."
        />
        <Button
          variant="contained"
          startIcon={<AddIcon />}
          onClick={() => setIsPrepareOpen(true)}
          sx={{
            bgcolor: brandColors.pageTitle,
            "&:hover": { bgcolor: "#581c87" },
            px: 2.5,
            py: 1,
            fontWeight: 700,
            fontSize: 13,
            boxShadow: "0 2px 6px rgba(88, 28, 135, 0.25)"
          }}
        >
          + Prepare Cryovial Batch
        </Button>
      </Box>

      {message && (
        <Alert
          severity={message.ok ? "success" : "error"}
          onClose={() => setMessage(null)}
          sx={{ mb: 2 }}
        >
          {message.text}
        </Alert>
      )}

      {/* KPI Cards: Operational Insights */}
      <CryovialKpiCards items={cryovials || []} />

      {/* Main Review Queue Section */}
      <SectionTitle>Cryovial Review Queue</SectionTitle>

      {/* Search & Filter Bar */}
      <CryovialFilterBar
        items={cryovials || []}
        filters={filters}
        onFilterChange={handleFilterChange}
        onReset={handleReset}
      />

      {/* Register Table / Queue */}
      {loading ? (
        <Box sx={{ py: 8, display: "flex", justifyContent: "center" }}>
          <LoadingSpinner />
        </Box>
      ) : (
        <CryovialReviewTable
          items={filteredCryovials}
          page={page}
          rowsPerPage={rowsPerPage}
          onPageChange={setPage}
          onRowsPerPageChange={(r) => {
            setRowsPerPage(r);
            setPage(0);
          }}
          onApproveClick={handleApproveClick}
          onThawClick={(item) => setThawItem(item)}
          onDestroyClick={(item) => setDestroyItem(item)}
        />
      )}

      {/* Preparation Dialog */}
      <PrepareCryovialBatchDialog
        open={isPrepareOpen}
        onClose={() => setIsPrepareOpen(false)}
        onSuccess={() => {
          setIsPrepareOpen(false);
          setMessage({ text: "Cryovial batch prepared successfully. Awaiting Section Head approval.", ok: true });
          loadData();
        }}
      />

      {/* Thaw Confirmation Dialog */}
      <ThawVialReasonDialog
        open={thawItem != null}
        cryovial={thawItem}
        onCancel={() => setThawItem(null)}
        onConfirm={handleThawConfirm}
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
          meaningStatement={
            pendingDecision.approved
              ? `I am approving Cryovial Batch ${pendingDecision.cryovial.code} for laboratory testing and media evaluation.`
              : `I am rejecting and decommissioning Cryovial Batch ${pendingDecision.cryovial.code}.`
          }
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
