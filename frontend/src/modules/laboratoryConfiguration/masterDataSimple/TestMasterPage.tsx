import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Alert, Box, Button, Chip, Stack, Tooltip } from "@mui/material";
import { getMySections, getSections, LaboratorySection } from "../../../services/laboratorySectionService";
import AddIcon from "@mui/icons-material/Add";
import { LabPage, FilterBar, RegisterTable, RegisterColumn } from "../../../components/lab";
import { monospaceFontFamily } from "../../../theme/palette";
import { StatusBadge } from "../../../components/StatusBadge";
import { LoadErrorAlert } from "../../../components/LoadErrorAlert";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { useTestDefinitions, TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import { ChangeReasonDialog } from "../../../components/config/ChangeReasonDialog";
import { HplcMethodService, HplcMethodListItem } from "./services/HplcMethodService";
import { PageArea, areaIncludes } from "./testMasterArea";
import { FP_SECTION_CODE, TestMasterLab, WORKFLOW_TYPES_BY_LAB } from "./testMaster/testMasterConstants";
import { WorkflowStepsSection } from "./testMaster/WorkflowStepsSection";
import { TestItemsPanel } from "./TestItemsPanel";
import { TestMasterWizard } from "./testWizard/TestMasterWizard";

// The Test Master - one canonical Code/DisplayName per test, referenced
// everywhere a TestCode is assigned (Items, Water Sampling Points, Room
// Test Configurations, Machine Part Configurations) via TestCodePicker/
// TestCodePickerMulti. Those pickers only choose existing tests; this
// page is the only place a test is created, so every test gets its
// section and workflow type.
//
// Freezing a test hides it from those pickers' dropdown for *new*
// selections without touching anything that already references its
// Code - see useTestDefinitions.activeOptions.
// Query parameter naming the test whose "Details & Workflow Steps" dialog is open.
const OPEN_TEST_PARAM = "test";

export function TestMasterPage({ lab = "micro", area }: { lab?: TestMasterLab; area?: PageArea }) {
  const { options: allOptions, loading: definitionsLoading, addWithSteps, update, setActive, reload } = useTestDefinitions();
  const isFp = lab === "fp";
  const workflowTypes = WORKFLOW_TYPES_BY_LAB[lab];
  const [fpSectionId, setFpSectionId] = useState<number | null>(null);
  const [allSections, setAllSections] = useState<LaboratorySection[]>([]);
  // Separate from fpSectionId: null meant "still loading", "failed" and "no
  // FP section exists" alike, so a failure (or a site without an FP section)
  // left the register showing skeleton rows forever.
  const [sectionsState, setSectionsState] = useState<"loading" | "loaded" | "failed">("loading");
  const [sectionsReloadKey, setSectionsReloadKey] = useState(0);
  useEffect(() => {
    setSectionsState("loading");
    getSections()
      .then((secs) => {
        setAllSections(secs);
        setFpSectionId(secs.find((s) => s.sectionCode === FP_SECTION_CODE)?.sectionId ?? null);
        setSectionsState("loaded");
      })
      .catch(() => setSectionsState("failed"));
  }, [sectionsReloadKey]);
  const inLab = (sid?: number | null) =>
    isFp ? fpSectionId !== null && sid === fpSectionId : sid !== fpSectionId;
  const options = allOptions.filter(
    (t) =>
      sectionsState === "loaded" &&
      inLab(t.sectionId) &&
      (!isFp || !area || areaIncludes(t.physchemArea, area))
  );
  const [pendingReasonAction, setPendingReasonAction] = useState<
    { kind: "freeze"; test: TestDefinitionOption } | null
  >(null);
  const [reasonSaving, setReasonSaving] = useState(false);
  const [reasonDialogError, setReasonDialogError] = useState<string | null>(null);
  const [hplcMethods, setHplcMethods] = useState<HplcMethodListItem[]>([]);

  useEffect(() => {
    HplcMethodService.getAll(false)
      .then((data) => setHplcMethods(data))
      .catch(() => {});
  }, []);

  const [wizardMode, setWizardMode] = useState<
    | { kind: "create" }
    | { kind: "edit"; test: TestDefinitionOption }
    | { kind: "duplicate"; test: TestDefinitionOption }
    | null
  >(null);

  const [allMySections, setMySections] = useState<LaboratorySection[]>([]);
  const mySections = allMySections.filter((s) => sectionsState === "loaded" && inLab(s.sectionId));
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null);
  // The test whose details dialog is open lives in the URL (?test=<id>), so the
  // address can be bookmarked or shared and survives a reload.
  const [searchParams, setSearchParams] = useSearchParams();
  const testParam = searchParams.get(OPEN_TEST_PARAM);
  const expandedId = testParam !== null && /^\d+$/.test(testParam) ? Number(testParam) : null;
  const setExpandedId = (id: number | null) => {
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        if (id === null) next.delete(OPEN_TEST_PARAM);
        else next.set(OPEN_TEST_PARAM, String(id));
        return next;
      },
      { replace: true }
    );
  };
  const [search, setSearch] = useState("");

  useEffect(() => {
    getMySections()
      .then((secs) => setMySections(secs))
      .catch(() => {});
  }, []);

  // A ?test=<id> that names nothing on this page (wrong lab, deleted, mistyped) is
  // ignored silently and dropped from the address once the lists have loaded.
  const listsLoaded = sectionsState === "loaded" && !definitionsLoading;
  const testParamMatches = expandedId !== null && options.some((t) => t.id === expandedId);
  useEffect(() => {
    if (!listsLoaded || testParam === null || testParamMatches) return;
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.delete(OPEN_TEST_PARAM);
        return next;
      },
      { replace: true }
    );
  }, [listsLoaded, testParam, testParamMatches, setSearchParams]);

  const openCreateDialog = () => {
    setMessage(null);
    setWizardMode({ kind: "create" });
  };

  const startEdit = (t: TestDefinitionOption) => {
    setMessage(null);
    setWizardMode({ kind: "edit", test: t });
  };

  const startDuplicate = (t: TestDefinitionOption) => {
    setMessage(null);
    setWizardMode({ kind: "duplicate", test: t });
  };

  const toggleFreeze = (t: TestDefinitionOption) => {
    setMessage(null);
    setReasonDialogError(null);
    setPendingReasonAction({ kind: "freeze", test: t });
  };

  const confirmReasonAction = async (reason: string) => {
    if (!pendingReasonAction) return;
    setReasonSaving(true);
    setReasonDialogError(null);
    try {
      const t = pendingReasonAction.test;
      await setActive(t.id, !t.isActive, reason);
      setMessage({ text: `Test "${t.code}" ${t.isActive ? "frozen" : "unfrozen"}.`, ok: true });
      setPendingReasonAction(null);
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setReasonDialogError(err?.response?.data?.message ?? "Could not complete this change.");
    } finally {
      setReasonSaving(false);
    }
  };

  const columns: RegisterColumn<TestDefinitionOption>[] = [
    { key: "code", label: "Code", sortable: true, render: (t) => <span style={{ fontFamily: monospaceFontFamily }}>{t.code}</span> },
    {
      key: "displayName",
      label: "Display Name",
      sortable: true,
      render: (t) => (
                    <Stack useFlexGap direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
                      <span>{t.displayName}</span>
                      {t.workflowType === "HplcMethodAssay" && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label="HPLC Assay"
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "IcpMethodAssay" && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label="ICP Assay"
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "Dissolution" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label="Dissolution"
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "Disintegration" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label="Disintegration"
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "WeightVariation" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label="Weight Variation"
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "UvSpectrophotometry" && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label={t.uvCalculationMode ? `UV · ${t.uvCalculationMode}` : "UV"}
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.requiresSystemSuitability && (
                        <Tooltip
                          title={[
                            t.sstMaxRsdPercent != null ? `Max RSD: ${t.sstMaxRsdPercent}%` : null,
                            t.sstMinResolution != null ? `Min Res: ${t.sstMinResolution}` : null,
                            t.sstMaxTailingFactor != null ? `Max Tailing: ${t.sstMaxTailingFactor}` : null,
                            t.sstMinTheoreticalPlates != null ? `Min Plates: ${t.sstMinTheoreticalPlates}` : null
                          ].filter(Boolean).join(" | ") || "System suitability required"}
                        >
                          <Chip
                            size="small"
                            color="info"
                            variant="outlined"
                            label={`SST: ${t.methodAbbreviation ?? "Required"}`}
                            sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                          />
                        </Tooltip>
                      )}
                    </Stack>
      )
    },
    { key: "section", label: "Section", sortable: true, sortValue: (t) => t.section?.name ?? "", render: (t) => t.section?.name ?? "—" },
    {
      key: "isActive",
      label: "Status",
      sortable: true,
      sortValue: (t) => (t.isActive ? "Active" : "Frozen"),
      render: (t) => <StatusBadge status={t.isActive ? "Active" : "Frozen"} />
    }
  ];

  const q = search.trim().toLowerCase();
  const visibleOptions = q
    ? options.filter((t) => t.code.toLowerCase().includes(q) || t.displayName.toLowerCase().includes(q) || (t.section?.name ?? "").toLowerCase().includes(q))
    : options;
  const detailsTest = expandedId !== null ? options.find((t) => t.id === expandedId) ?? null : null;

  return (
    <>
      <LabPage
        title={isFp ? (area === "rmpm" ? "RM & PM Test Master" : "FP Test Master") : "Microbiology Test Master"}
        subtitle={isFp
          ? "Physicochemical tests: HPLC methods, equation type and system suitability criteria."
          : "Microbiology tests available to assign to Items, Sampling Points, Rooms, and Machine Parts."}
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>Add Test</Button>}
        filters={
          <FilterBar
            search={search}
            onSearch={setSearch}
            placeholder="Search code, name, section"
            resultCount={visibleOptions.length}
            onRefresh={() => { void reload(); }}
          />
        }
      >
        {message && <Alert severity={message.ok ? "success" : "error"}>{message.text}</Alert>}
        {sectionsState === "failed" && (
          <LoadErrorAlert
            message="The laboratory sections could not be loaded, so the tests cannot be sorted into this laboratory and none are shown."
            onRetry={() => setSectionsReloadKey((k) => k + 1)}
          />
        )}
        <RegisterTable
          columns={columns}
          rows={visibleOptions}
          getRowId={(t) => t.id}
          loading={sectionsState === "loading"}
          onRowClick={(t) => setExpandedId(t.id)}
          rowActions={(t) => [
            { label: "Details & Workflow Steps", onClick: () => setExpandedId(t.id) },
            { label: "Edit", onClick: () => startEdit(t) },
            { label: "New from existing", onClick: () => startDuplicate(t) },
            { label: t.isActive ? "Freeze" : "Unfreeze", onClick: () => { void toggleFreeze(t); }, danger: t.isActive }
          ]}
          empty={{
            title: "No tests found",
            description: q ? "No test matches your search." : "Add a test to get started.",
            action: q ? undefined : <Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>Add Test</Button>
          }}
        />
      </LabPage>

      <FloatingDialog
        open={detailsTest !== null}
        title={detailsTest ? `Details & Workflow Steps: ${detailsTest.code}` : ""}
        onClose={() => setExpandedId(null)}
        maxWidth="lg"
      >
        {detailsTest && (
          <Box sx={{ display: "grid", gap: 3, gridTemplateColumns: { xs: "1fr", md: "minmax(0, 3fr) minmax(0, 2fr)" }, alignItems: "start" }}>
            <WorkflowStepsSection test={detailsTest} workflowTypes={workflowTypes} onWorkflowTypeChanged={reload} />
            <TestItemsPanel key={detailsTest.id} testId={detailsTest.id} />
          </Box>
        )}
      </FloatingDialog>

      <TestMasterWizard
        open={wizardMode !== null}
        lab={lab}
        area={area}
        editingTest={wizardMode?.kind === "edit" ? wizardMode.test : null}
        duplicateFrom={wizardMode?.kind === "duplicate" ? wizardMode.test : null}
        defaultSectionId={mySections.length === 1 ? mySections[0].sectionId : ""}
        sections={allSections.filter((s) => sectionsState === "loaded" && inLab(s.sectionId))}
        mySections={mySections}
        hplcMethods={hplcMethods}
        onCreateWithSteps={addWithSteps}
        onUpdateTest={(id, payload, reason) => update(id, payload, undefined, undefined, reason)}
        onReloadDefinitions={reload}
        onClose={() => setWizardMode(null)}
        onSaved={(saved, savedModel, mode) => {
          const savedCode = savedModel.code.trim();
          if (mode === "edit") {
            setMessage({ text: `Test "${savedCode}" updated.`, ok: true });
          } else {
            setMessage({ text: `Test "${savedCode}" added to the Test Master.`, ok: true });
            // A new microbiology test with no workflow steps yet: open details straight away.
            if (!isFp && saved?.id != null && savedModel.steps.length === 0) {
              setExpandedId(saved.id);
            }
          }
        }}
      />

      <ChangeReasonDialog
        open={pendingReasonAction !== null}
        title={
          pendingReasonAction
            ? pendingReasonAction.test.isActive
              ? `Freeze test "${pendingReasonAction.test.code}"?`
              : `Unfreeze test "${pendingReasonAction.test.code}"?`
            : "Reason for change"
        }
        message={
          pendingReasonAction?.test.isActive
            ? "Freezing prevents this test from being selected for new work. Please record why it is being frozen."
            : "Unfreezing makes this test available for new work again. Please record why it is being unfrozen."
        }
        confirmText={pendingReasonAction?.test.isActive ? "Freeze" : "Unfreeze"}
        loading={reasonSaving}
        error={reasonDialogError}
        onConfirm={confirmReasonAction}
        onCancel={() => {
          if (!reasonSaving) {
            setPendingReasonAction(null);
            setReasonDialogError(null);
          }
        }}
      />
    </>
  );
}
