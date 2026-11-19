import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Alert, Box, Button, Chip, Stack, Tooltip } from "@mui/material";
import { getMySections, getSections, LaboratorySection } from "../../../services/laboratorySectionService";
import AddIcon from "@mui/icons-material/Add";
import { LabPage, FilterBar, RegisterTable, RegisterColumn } from "../../../components/lab";
import { monospaceFontFamily } from "../../../theme/palette";
import { StatusBadge } from "../../../components/StatusBadge";
import { useTranslation } from "../../../i18n";
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
  const { t: tr } = useTranslation("masterData");
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
      const test = pendingReasonAction.test;
      await setActive(test.id, !test.isActive, reason);
      setMessage({
        text: test.isActive
          ? tr("testMasterPage.frozenMsg", { code: test.code })
          : tr("testMasterPage.unfrozenMsg", { code: test.code }),
        ok: true
      });
      setPendingReasonAction(null);
    } catch (e: unknown) {
      const err = e as { response?: { data?: { message?: string } } };
      setReasonDialogError(err?.response?.data?.message ?? tr("testMasterPage.changeError"));
    } finally {
      setReasonSaving(false);
    }
  };

  const columns: RegisterColumn<TestDefinitionOption>[] = [
    { key: "code", label: tr("testMasterPage.columns.code"), sortable: true, render: (t) => <span style={{ fontFamily: monospaceFontFamily }}>{t.code}</span> },
    {
      key: "displayName",
      label: tr("testMasterPage.columns.displayName"),
      sortable: true,
      render: (t) => (
                    <Stack useFlexGap direction="row" spacing={1} sx={{ alignItems: "center", flexWrap: "wrap" }}>
                      <span>{t.displayName}</span>
                      {t.workflowType === "HplcMethodAssay" && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label={tr("testMasterPage.badges.hplcAssay")}
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "IcpMethodAssay" && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label={tr("testMasterPage.badges.icpAssay")}
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "Dissolution" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label={tr("testMasterPage.badges.dissolution")}
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "Disintegration" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label={tr("testMasterPage.badges.disintegration")}
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "WeightVariation" && (
                        <Chip
                          size="small"
                          color="info"
                          variant="outlined"
                          label={tr("testMasterPage.badges.weightVariation")}
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.workflowType === "UvSpectrophotometry" && (
                        <Chip
                          size="small"
                          color="primary"
                          variant="outlined"
                          label={t.uvCalculationMode ? tr("testMasterPage.badges.uvWithMode", { mode: t.uvCalculationMode }) : tr("testMasterPage.badges.uv")}
                          sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                        />
                      )}
                      {t.requiresSystemSuitability && (
                        <Tooltip
                          title={[
                            t.sstMaxRsdPercent != null ? tr("testMasterPage.badges.sstMaxRsd", { value: t.sstMaxRsdPercent }) : null,
                            t.sstMinResolution != null ? tr("testMasterPage.badges.sstMinRes", { value: t.sstMinResolution }) : null,
                            t.sstMaxTailingFactor != null ? tr("testMasterPage.badges.sstMaxTailing", { value: t.sstMaxTailingFactor }) : null,
                            t.sstMinTheoreticalPlates != null ? tr("testMasterPage.badges.sstMinPlates", { value: t.sstMinTheoreticalPlates }) : null
                          ].filter(Boolean).join(" | ") || tr("testMasterPage.badges.sstTooltipFallback")}
                        >
                          <Chip
                            size="small"
                            color="info"
                            variant="outlined"
                            label={tr("testMasterPage.badges.sst", { abbr: t.methodAbbreviation ?? tr("testMasterPage.badges.sstRequired") })}
                            sx={{ height: 20, fontSize: "0.75rem", fontWeight: 700 }}
                          />
                        </Tooltip>
                      )}
                    </Stack>
      )
    },
    { key: "section", label: tr("testMasterPage.columns.section"), sortable: true, sortValue: (t) => t.section?.name ?? "", render: (t) => t.section?.name ?? tr("common.dash") },
    {
      key: "isActive",
      label: tr("testMasterPage.columns.status"),
      sortable: true,
      sortValue: (t) => (t.isActive ? "Active" : "Frozen"),
      render: (t) => <StatusBadge status={t.isActive ? "Active" : "Frozen"} label={t.isActive ? tr("testMasterPage.statusActive") : tr("testMasterPage.statusFrozen")} />
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
        title={isFp ? (area === "rmpm" ? tr("testMasterPage.titleRmPm") : tr("testMasterPage.titleFp")) : tr("testMasterPage.titleMicro")}
        subtitle={isFp
          ? tr("testMasterPage.subtitleFp")
          : tr("testMasterPage.subtitleMicro")}
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>{tr("testMasterPage.addTest")}</Button>}
        filters={
          <FilterBar
            search={search}
            onSearch={setSearch}
            placeholder={tr("testMasterPage.searchPlaceholder")}
            resultCount={visibleOptions.length}
            onRefresh={() => { void reload(); }}
          />
        }
      >
        {message && <Alert severity={message.ok ? "success" : "error"}>{message.text}</Alert>}
        {sectionsState === "failed" && (
          <LoadErrorAlert
            message={tr("testMasterPage.sectionsLoadError")}
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
            { label: tr("testMasterPage.rowActions.detailsSteps"), onClick: () => setExpandedId(t.id) },
            { label: tr("testMasterPage.rowActions.edit"), onClick: () => startEdit(t) },
            { label: tr("testMasterPage.rowActions.newFromExisting"), onClick: () => startDuplicate(t) },
            { label: t.isActive ? tr("testMasterPage.rowActions.freeze") : tr("testMasterPage.rowActions.unfreeze"), onClick: () => { void toggleFreeze(t); }, danger: t.isActive }
          ]}
          empty={{
            title: tr("testMasterPage.emptyTitle"),
            description: q ? tr("testMasterPage.emptySearchDesc") : tr("testMasterPage.emptyDesc"),
            action: q ? undefined : <Button variant="contained" startIcon={<AddIcon />} onClick={openCreateDialog}>{tr("testMasterPage.addTest")}</Button>
          }}
        />
      </LabPage>

      <FloatingDialog
        open={detailsTest !== null}
        title={detailsTest ? tr("testMasterPage.detailsTitle", { code: detailsTest.code }) : ""}
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
        onUpdateTest={(id, payload, reason, version) => update(id, payload, undefined, undefined, reason, version)}
        onReloadDefinitions={reload}
        onClose={() => setWizardMode(null)}
        onSaved={(saved, savedModel, mode) => {
          const savedCode = savedModel.code.trim();
          if (mode === "edit") {
            setMessage({ text: tr("testMasterPage.updatedMsg", { code: savedCode }), ok: true });
          } else {
            setMessage({ text: tr("testMasterPage.addedMsg", { code: savedCode }), ok: true });
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
              ? tr("testMasterPage.freezeDialogTitle", { code: pendingReasonAction.test.code })
              : tr("testMasterPage.unfreezeDialogTitle", { code: pendingReasonAction.test.code })
            : tr("testMasterPage.reasonFallbackTitle")
        }
        message={
          pendingReasonAction?.test.isActive
            ? tr("testMasterPage.freezeDialogMessage")
            : tr("testMasterPage.unfreezeDialogMessage")
        }
        confirmText={pendingReasonAction?.test.isActive ? tr("testMasterPage.rowActions.freeze") : tr("testMasterPage.rowActions.unfreeze")}
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
