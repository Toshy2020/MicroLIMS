import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { TextField, Button, Stack, Alert, Select, MenuItem, Box, Typography, FormControlLabel, Chip, Tooltip, FormControl, InputLabel, FormHelperText, Switch } from "@mui/material";
import { getMySections, getSections, LaboratorySection } from "../../../services/laboratorySectionService";
import AddIcon from "@mui/icons-material/Add";
import { LabPage, FilterBar, FormDialog, RegisterTable, RegisterColumn } from "../../../components/lab";
import { monospaceFontFamily } from "../../../theme/palette";
import { StatusBadge } from "../../../components/StatusBadge";
import { LoadErrorAlert, LoadFailuresAlert } from "../../../components/LoadErrorAlert";
import { useLoadFailures } from "../../../hooks/useLoadFailures";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { useTestDefinitions, TestDefinitionOption } from "../../../hooks/useTestDefinitions";
import { CreateTestDefinitionPayload, UpdateTestDefinitionPayload, MoistureRole } from "../../../services/masterDataOptions";
import { isReasonRequiredError } from "../../../services/apiClient";
import { ChangeReasonDialog } from "../../../components/config/ChangeReasonDialog";
import { HplcMethodService, HplcMethodListItem } from "./services/HplcMethodService";
import { TitrationConfigSection } from "./TitrationConfigSection";
import { PageArea, areaIncludes } from "./testMasterArea";
import { TestAreaField } from "./TestAreaField";
import { TitrationChangeReasonField, hasTitrationSettingsChanged } from "./TitrationChangeReasonField";
import { EQUATION_TYPES, EQUATION_TYPE_LABELS, FP_SECTION_CODE, TestMasterLab, WORKFLOW_TYPES_BY_LAB, WORKFLOW_TYPE_LABELS } from "./testMaster/testMasterConstants";
import { TestStageReplicatesSection } from "./testMaster/TestStageReplicatesSection";
import { WorkflowStepsSection } from "./testMaster/WorkflowStepsSection";
import { TestItemsPanel } from "./TestItemsPanel";
import { UvTestFields } from "./testMaster/UvTestFields";
import { IcpTestFields } from "./testMaster/IcpTestFields";
import {
  TEST_WIZARD_STEP_ORDER,
  TestFormModel,
  createEmptyTestFormModel,
  fromTestDefinition,
  toTestPayload,
  validateStep,
  withEquationType,
  withWorkflowType
} from "./testWizard/testFormModel";

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
  const { options: allOptions, loading: definitionsLoading, addNew, update, setActive, reload } = useTestDefinitions();
  const isFp = lab === "fp";
  const workflowTypes = WORKFLOW_TYPES_BY_LAB[lab];
  const [fpSectionId, setFpSectionId] = useState<number | null>(null);
  // Separate from fpSectionId: null meant "still loading", "failed" and "no
  // FP section exists" alike, so a failure (or a site without an FP section)
  // left the register showing skeleton rows forever.
  const [sectionsState, setSectionsState] = useState<"loading" | "loaded" | "failed">("loading");
  const [sectionsReloadKey, setSectionsReloadKey] = useState(0);
  useEffect(() => {
    setSectionsState("loading");
    getSections()
      .then((secs) => {
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
  const [form, setForm] = useState<TestFormModel>(() => createEmptyTestFormModel({ lab, area }));
  const patchForm = (patch: Partial<TestFormModel>) => setForm((prev) => ({ ...prev, ...patch }));
  const [changeReasonError, setChangeReasonError] = useState<string | null>(null);
  const [pendingReasonAction, setPendingReasonAction] = useState<
    | { kind: "freeze"; test: TestDefinitionOption }
    | { kind: "edit"; id: number; payload: UpdateTestDefinitionPayload; code: string }
    | null
  >(null);
  const [reasonSaving, setReasonSaving] = useState(false);
  const [reasonDialogError, setReasonDialogError] = useState<string | null>(null);
  const [hplcMethods, setHplcMethods] = useState<HplcMethodListItem[]>([]);

  // Lists the Add/Edit Test dialog picks from; a failure is named in the
  // dialog instead of leaving an empty picker.
  const { failed: dialogListFailures, fail: failDialogList } = useLoadFailures();

  useEffect(() => {
    HplcMethodService.getAll(false)
      .then((data) => setHplcMethods(data))
      .catch(failDialogList("HPLC methods"));
  }, [failDialogList]);

  const [dialogOpen, setDialogOpen] = useState(false);
  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

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
      .catch(failDialogList("your laboratory sections"));
  }, [failDialogList]);

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
    setForm(
      createEmptyTestFormModel({
        lab,
        area,
        sectionId: mySections.length === 1 ? mySections[0].sectionId : ""
      })
    );
    setChangeReasonError(null);
    setDialogError(null);
    setDialogOpen(true);
  };

  const startEdit = (t: TestDefinitionOption) => {
    setForm(
      fromTestDefinition(t, {
        lab,
        area,
        defaultSectionId: mySections.length === 1 ? mySections[0].sectionId : ""
      })
    );
    setChangeReasonError(null);
    setDialogError(null);
    setDialogOpen(true);
  };

  // Opens the ADD dialog prefilled from an existing test. Nothing is saved until
  // the user presses "Add Test", so the server validates it like any new test.
  const startDuplicate = (t: TestDefinitionOption) => {
    const base = fromTestDefinition(t, {
      lab,
      area,
      defaultSectionId: mySections.length === 1 ? mySections[0].sectionId : ""
    });
    const canUseSourceSection = t.sectionId != null && mySections.some((s) => s.sectionId === t.sectionId);
    setForm({
      ...base,
      editingId: null,
      editingTest: null,
      editingSectionId: null,
      duplicateOfCode: t.code,
      code: "",
      displayName: `${t.displayName} (copy)`,
      coaDisplayOrder: "",
      sectionId: canUseSourceSection ? base.sectionId : (mySections.length === 1 ? mySections[0].sectionId : ""),
      // The copy gets its own elements; keeping the source ids would point it at the original's.
      icp: { ...base.icp, elements: base.icp.elements.map((e) => ({ ...e, id: undefined })) }
    });
    setChangeReasonError(null);
    setDialogError(null);
    setDialogOpen(true);
  };

  const closeDialog = () => {
    setDialogOpen(false);
    patchForm({
      duplicateOfCode: null,
      editingId: null,
      editingTest: null,
      physchemArea: null,
      changeReason: ""
    });
    setChangeReasonError(null);
    setDialogError(null);
  };

  const save = async () => {
    setDialogError(null);
    setChangeReasonError(null);
    setMessage(null);

    for (const step of TEST_WIZARD_STEP_ORDER) {
      const stepErrors = validateStep(step, form);
      const entries = Object.entries(stepErrors);
      if (entries.length > 0) {
        const [field, msg] = entries[0];
        if (field === "changeReason") {
          setChangeReasonError(msg);
        } else {
          setDialogError(msg);
        }
        return;
      }
    }

    const trimmedCode = form.code.trim();
    const payload = toTestPayload(form);

    setSaving(true);
    let attemptedEdit: { id: number; payload: UpdateTestDefinitionPayload; code: string } | null = null;
    try {
      if (form.editingId) {
        const updatePayload = payload as UpdateTestDefinitionPayload;
        const currentEdit = { id: form.editingId, payload: updatePayload, code: trimmedCode };
        attemptedEdit = currentEdit;
        await update(form.editingId, updatePayload);
        setMessage({ text: `Test "${trimmedCode}" updated.`, ok: true });
      } else {
        const created = await addNew(payload as CreateTestDefinitionPayload);
        setMessage({ text: `Test "${trimmedCode}" added to the Test Master.`, ok: true });
        // A new microbiology test has no workflow steps yet: open them straight away.
        if (!isFp && created?.id != null) setExpandedId(created.id);
      }
      closeDialog();
    } catch (e: unknown) {
      if (attemptedEdit && isReasonRequiredError(e)) {
        closeDialog();
        setReasonDialogError(null);
        setPendingReasonAction({ kind: "edit", ...attemptedEdit });
        return;
      }
      const err = e as { response?: { data?: { message?: string } } };
      setDialogError(err?.response?.data?.message ?? `Could not ${form.editingId ? "update" : "add"} this test.`);
    } finally {
      setSaving(false);
    }
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
      if (pendingReasonAction.kind === "freeze") {
        const t = pendingReasonAction.test;
        await setActive(t.id, !t.isActive, reason);
        setMessage({ text: `Test "${t.code}" ${t.isActive ? "frozen" : "unfrozen"}.`, ok: true });
      } else {
        const { id, payload, code } = pendingReasonAction;
        await update(id, payload, undefined, undefined, reason);
        setMessage({ text: `Test "${code}" updated.`, ok: true });
      }
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

  const isDissolutionMethod = form.workflowType === "Dissolution";

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
            { label: "Duplicate", onClick: () => startDuplicate(t) },
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

      {/* A form, so Enter in any field saves the test. */}
      <FormDialog
        open={dialogOpen}
        title={form.editingId ? "Edit Test" : form.duplicateOfCode !== null ? `Add Test (copy of ${form.duplicateOfCode})` : "Add Test"}
        onClose={closeDialog}
        onSubmit={save}
        submitLabel={form.editingId ? "Save Changes" : "Add Test"}
        submitting={saving}
        error={dialogError}
        maxWidth="md"
      >
        <LoadFailuresAlert failed={dialogListFailures} retryHint="Reload the page to try again." />

        <Stack spacing={2}>
          <Stack useFlexGap direction="row" spacing={2} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <TextField
              size="small"
              label="Code"
              placeholder="e.g. HPLC_VITC"
              value={form.code}
              onChange={(e) => patchForm({ code: e.target.value })}
              required
              sx={{ flex: 1, minWidth: 200 }}
            />
            <TextField
              size="small"
              label="Display Name"
              placeholder="e.g. Vitamin C Assay"
              value={form.displayName}
              onChange={(e) => patchForm({ displayName: e.target.value })}
              required
              sx={{ flex: 1.5, minWidth: 240 }}
            />
            <TextField
              size="small"
              label="CoA order"
              placeholder="e.g. 1"
              value={form.coaDisplayOrder}
              onChange={(e) => patchForm({ coaDisplayOrder: e.target.value })}
              helperText="Position on the Certificate of Analysis within its group"
              slotProps={{ htmlInput: { inputMode: "numeric" } }}
              sx={{ width: 170 }}
            />
          </Stack>

          <Stack useFlexGap direction="row" spacing={2} sx={{ flexWrap: "wrap", alignItems: "center" }}>
            <FormControl size="small" sx={{ flex: 1, minWidth: 200 }} required>
              <InputLabel id="test-section-select-label">Section</InputLabel>
              <Select<number | "">
                labelId="test-section-select-label"
                label="Section"
                value={form.sectionId}
                onChange={(e) => patchForm({ sectionId: e.target.value === "" ? "" : Number(e.target.value) })}
                inputProps={{ "aria-label": "Section" }}
              >
                {mySections.length > 1 && <MenuItem value=""><em>Select Section</em></MenuItem>}
                {form.editingSectionId !== null && !mySections.some((s) => s.sectionId === form.editingSectionId) && (
                  <MenuItem value={form.editingSectionId}>
                    {form.editingTest?.section?.name ?? `Section #${form.editingSectionId}`} (Current)
                  </MenuItem>
                )}
                {mySections.map((s) => (
                  <MenuItem key={s.sectionId} value={s.sectionId}>
                    {s.sectionName} ({s.departmentName})
                  </MenuItem>
                ))}
              </Select>
              {form.editingId && form.editingSectionId !== null && form.sectionId !== "" && form.sectionId !== form.editingSectionId && (
                <FormHelperText>Existing test orders keep their current section.</FormHelperText>
              )}
            </FormControl>

            <FormControl size="small" sx={{ flex: 1, minWidth: 200 }}>
              <InputLabel id="dialog-workflow-type-label">Workflow Type</InputLabel>
              <Select
                labelId="dialog-workflow-type-label"
                label="Workflow Type"
                value={form.workflowType}
                onChange={(e) => setForm((prev) => withWorkflowType(prev, e.target.value))}
              >
                {workflowTypes.map((w) => (
                  <MenuItem key={w} value={w}>
                    {WORKFLOW_TYPE_LABELS[w] ?? w}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          </Stack>

          {isFp && (
            <FormControl size="small" fullWidth>
              <InputLabel id="dialog-equation-type-label">Equation Type</InputLabel>
              <Select
                labelId="dialog-equation-type-label"
                label="Equation Type"
                value={form.equationType}
                onChange={(e) => setForm((prev) => withEquationType(prev, e.target.value))}
              >
                {EQUATION_TYPES.filter((eq) => {
                  if (form.workflowType === "HplcMethodAssay") {
                    return eq === "HplcMethodAssay";
                  }
                  if (form.workflowType === "IcpMethodAssay") {
                    return eq === "IcpMethodAssay";
                  }
                  if (form.workflowType === "Disintegration") {
                    return eq === "Disintegration";
                  }
                  if (form.workflowType === "WeightVariation") {
                    return eq === "WeightVariation";
                  }
                  if (form.workflowType === "Titration") {
                    return eq === "Titration";
                  }
                  if (form.workflowType === "UvSpectrophotometry") {
                    return eq === "UvSpectrophotometry";
                  }
                  if (form.workflowType === "Dissolution") {
                    return eq === "Dissolution";
                  }
                  if (form.workflowType === "Measurement") {
                    return eq === "Measurement";
                  }
                  if (form.workflowType === "Gravimetric") {
                    return eq === "GravimetricLoss" || eq === "GravimetricResidue";
                  }
                  if (form.workflowType === "Qualitative") {
                    return eq === "Qualitative";
                  }
                  return eq !== "Measurement" && eq !== "GravimetricLoss" && eq !== "GravimetricResidue" && eq !== "Qualitative" && eq !== "Dissolution" && eq !== "Disintegration" && eq !== "WeightVariation" && eq !== "Titration" && eq !== "HplcMethodAssay" && eq !== "IcpMethodAssay" && eq !== "UvSpectrophotometry";
                }).map((eq) => (
                  <MenuItem key={eq} value={eq}>
                    {EQUATION_TYPE_LABELS[eq] ?? eq}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>
          )}

          {(form.workflowType === "HplcMethodAssay" || form.workflowType === "Dissolution") && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                {isDissolutionMethod ? "HPLC Method (supplies the dissolution standard)" : "HPLC Method Assay Configuration"}
              </Typography>
              <Stack spacing={2}>
                <FormControl size="small" fullWidth required={!isDissolutionMethod}>
                  <InputLabel id="dialog-hplc-method-label">{isDissolutionMethod ? "HPLC Method" : "HPLC Method *"}</InputLabel>
                  <Select<number | "">
                    labelId="dialog-hplc-method-label"
                    label={isDissolutionMethod ? "HPLC Method" : "HPLC Method *"}
                    value={form.hplcMethodId}
                    onChange={(e) => {
                      const val = e.target.value === "" ? "" : Number(e.target.value);
                      const chosen = hplcMethods.find((m) => m.id === val);
                      setForm((prev) => ({
                        ...prev,
                        hplcMethodId: val,
                        methodAbbreviation: chosen && prev.workflowType === "HplcMethodAssay" ? chosen.abbreviation : prev.methodAbbreviation,
                      }));
                    }}
                    inputProps={{ "aria-label": "HPLC Method" }}
                  >
                    <MenuItem value=""><em>Select HPLC Method</em></MenuItem>
                    {hplcMethods
                      .filter((m) => m.isActive || m.id === form.hplcMethodId)
                      .map((m) => (
                        <MenuItem key={m.id} value={m.id}>
                          {m.name} ({m.abbreviation}){!m.isActive ? " (Inactive)" : ""}
                        </MenuItem>
                      ))}
                  </Select>
                  <FormHelperText>Select the active master HPLC method that defines this test&apos;s chromatographic conditions and analytes.</FormHelperText>
                </FormControl>

                <TextField
                  size="small"
                  label="Method Abbreviation"
                  value={form.methodAbbreviation}
                  slotProps={{
                    input: {
                      readOnly: true,
                    },
                  }}
                  helperText={form.workflowType === "Dissolution" ? "Dissolution keeps its own abbreviation." : "Auto-populated from the selected HPLC method (read-only)."}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {form.workflowType === "IcpMethodAssay" && (
            <IcpTestFields form={form.icp} onChange={(nextIcp) => patchForm({ icp: nextIcp })} />
          )}

          {form.workflowType === "Measurement" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Measurement Settings
              </Typography>
              <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                <TextField
                  size="small"
                  type="number"
                  label="Replicate Count *"
                  value={form.replicateCount}
                  onChange={(e) => patchForm({ replicateCount: e.target.value })}
                  slotProps={{ htmlInput: { min: 1, max: 30, step: 1 } }}
                  helperText="Replicates per parameter (1–30)"
                  required
                  sx={{ flex: 1 }}
                />
                <FormControl size="small" sx={{ flex: 1 }} required>
                  <InputLabel id="dialog-eval-basis-label">Evaluation Basis *</InputLabel>
                  <Select
                    labelId="dialog-eval-basis-label"
                    label="Evaluation Basis *"
                    value={form.evaluationBasis}
                    onChange={(e) => patchForm({ evaluationBasis: e.target.value as "Mean" | "EachValue" | "Min" | "Max" })}
                  >
                    <MenuItem value="Mean">Mean</MenuItem>
                    <MenuItem value="EachValue">Each Value</MenuItem>
                    <MenuItem value="Min">Minimum</MenuItem>
                    <MenuItem value="Max">Maximum</MenuItem>
                  </Select>
                </FormControl>
              </Stack>
            </Box>
          )}

          {form.workflowType === "Gravimetric" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Gravimetric Settings
              </Typography>
              <Stack spacing={2}>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ alignItems: "center" }}>
                  <TextField
                    size="small"
                    type="number"
                    label="Replicate Count *"
                    value={form.replicateCount}
                    onChange={(e) => patchForm({ replicateCount: e.target.value })}
                    slotProps={{ htmlInput: { min: 1, max: 30, step: 1 } }}
                    helperText="Replicates per parameter (1–30)"
                    required
                    sx={{ flex: 1 }}
                  />
                  <FormControlLabel
                    control={
                      <Switch
                        checked={form.usesTare}
                        onChange={(e) => patchForm({ usesTare: e.target.checked })}
                      />
                    }
                    label="Uses tare (container weight)"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <TextField
                  size="small"
                  label="Condition fields"
                  placeholder="e.g. Temperature (°C),Time (h)"
                  value={form.conditionFields}
                  onChange={(e) => patchForm({ conditionFields: e.target.value })}
                  helperText="comma-separated, e.g. Temperature (°C),Time (h)"
                  slotProps={{ htmlInput: { maxLength: 500 } }}
                  fullWidth
                />
                {form.equationType === "GravimetricLoss" && (
                  <FormControl size="small" sx={{ maxWidth: 300 }}>
                    <InputLabel id="gravimetric-moisture-role-label">Moisture role</InputLabel>
                    <Select<MoistureRole>
                      labelId="gravimetric-moisture-role-label"
                      label="Moisture role"
                      value={form.moistureRole === "Water" ? "None" : form.moistureRole}
                      onChange={(e) => patchForm({ moistureRole: e.target.value as MoistureRole })}
                    >
                      <MenuItem value="None">None</MenuItem>
                      <MenuItem value="LossOnDrying">Loss on drying</MenuItem>
                    </Select>
                  </FormControl>
                )}
              </Stack>
            </Box>
          )}

          {form.workflowType === "Titration" && (
            <TitrationConfigSection
              form={form.titration}
              onChange={(nextTitration) => {
                setForm((prev) => ({
                  ...prev,
                  titration: nextTitration,
                  moistureRole: nextTitration.type !== "KarlFischer" && prev.moistureRole === "Water" ? "None" : prev.moistureRole,
                }));
              }}
              sectionId={form.sectionId}
              moistureRole={form.moistureRole}
              onMoistureRoleChange={(nextRole) => patchForm({ moistureRole: nextRole })}
            />
          )}

          {form.workflowType === "UvSpectrophotometry" && (
            <UvTestFields
              mode={form.uvCalculationMode}
              onModeChange={(nextMode) => patchForm({ uvCalculationMode: nextMode })}
              specificAbsorbance={form.uvSpecificAbsorbance}
              onSpecificAbsorbanceChange={(nextVal) => patchForm({ uvSpecificAbsorbance: nextVal })}
              totalVolumeMl={form.uvTotalVolumeMl}
              onTotalVolumeMlChange={(nextVal) => patchForm({ uvTotalVolumeMl: nextVal })}
              standardVolumeMl={form.uvStandardVolumeMl}
              onStandardVolumeMlChange={(nextVal) => patchForm({ uvStandardVolumeMl: nextVal })}
              standardEntryId={form.uvStandardEntryId}
              onStandardEntryIdChange={(nextId) => patchForm({ uvStandardEntryId: nextId })}
              sectionId={form.sectionId}
              replicateCount={form.replicateCount}
              onReplicateCountChange={(nextCount) => patchForm({ replicateCount: nextCount })}
            />
          )}

          {isFp && area && <TestAreaField pageArea={area} value={form.physchemArea} onChange={(nextArea) => patchForm({ physchemArea: nextArea })} />}

          {form.editingId && hasTitrationSettingsChanged(form.editingTest, form.titration, form.workflowType, isFp ? form.physchemArea : null, form.moistureRole) && (
            <TitrationChangeReasonField value={form.changeReason} onChange={(nextReason) => patchForm({ changeReason: nextReason })} error={changeReasonError} />
          )}

          {form.workflowType === "Qualitative" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 600, fontSize: 13, color: "text.secondary", mb: 1.5 }}>
                Qualitative test: records compliance observation against specifications directly (no replicates or steps).
              </Typography>
              <FormControlLabel
                control={
                  <Switch
                    checked={form.perContainer}
                    onChange={(e) => patchForm({ perContainer: e.target.checked })}
                  />
                }
                label="Recorded per container"
              />
            </Box>
          )}

          {form.workflowType === "Dissolution" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Dissolution Acceptance Offsets (USP &lt;711&gt; / EP 2.9.3)
              </Typography>
              <Stack spacing={2}>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <TextField
                    size="small"
                    type="number"
                    label="S1: each unit ≥ Q + "
                    value={form.dissolutionS1Offset}
                    onChange={(e) => patchForm({ dissolutionS1Offset: e.target.value })}
                    slotProps={{ htmlInput: { min: 0, step: "any" } }}
                    helperText="Default: 5 (%)"
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="S2: no unit < Q − "
                    value={form.dissolutionS2MinOffset}
                    onChange={(e) => patchForm({ dissolutionS2MinOffset: e.target.value })}
                    slotProps={{ htmlInput: { min: 0, step: "any" } }}
                    helperText="Default: 15 (%)"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <TextField
                    size="small"
                    type="number"
                    label="S3: no unit < Q − "
                    value={form.dissolutionS3MinOffset}
                    onChange={(e) => patchForm({ dissolutionS3MinOffset: e.target.value })}
                    slotProps={{ htmlInput: { min: 0, step: "any" } }}
                    helperText="Default: 25 (%)"
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="S3: max units < Q − S2 value"
                    value={form.dissolutionS3MaxBelowS2Min}
                    onChange={(e) => patchForm({ dissolutionS3MaxBelowS2Min: e.target.value })}
                    slotProps={{ htmlInput: { min: 0, step: 1 } }}
                    helperText="Default: 2 (units)"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <TextField
                  size="small"
                  label="Condition fields"
                  placeholder="Apparatus,RPM,Medium,Temperature (°C),Time (min)"
                  value={form.conditionFields}
                  onChange={(e) => patchForm({ conditionFields: e.target.value })}
                  helperText="comma-separated, e.g. Apparatus,RPM,Medium,Temperature (°C),Time (min)"
                  slotProps={{ htmlInput: { maxLength: 500 } }}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {form.workflowType === "Disintegration" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Disintegration Acceptance (USP &lt;701&gt; / EP 2.9.1)
              </Typography>
              <Stack spacing={2}>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <TextField
                    size="small"
                    type="number"
                    label="Stage 1 Units"
                    value={form.disintegrationStage1Units}
                    onChange={(e) => patchForm({ disintegrationStage1Units: e.target.value })}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    helperText="Default: 6"
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="Stage 2 Units"
                    value={form.disintegrationStage2Units}
                    onChange={(e) => patchForm({ disintegrationStage2Units: e.target.value })}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    helperText="Default: 12"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                  <TextField
                    size="small"
                    type="number"
                    label="Max S1 Failures"
                    value={form.disintegrationMaxStage1Failures}
                    onChange={(e) => patchForm({ disintegrationMaxStage1Failures: e.target.value })}
                    slotProps={{ htmlInput: { min: 0, step: 1 } }}
                    helperText="Default: 2 (0..S1-1)"
                    sx={{ flex: 1 }}
                  />
                  <TextField
                    size="small"
                    type="number"
                    label="Min Pass Total"
                    value={form.disintegrationMinPassTotal}
                    onChange={(e) => patchForm({ disintegrationMinPassTotal: e.target.value })}
                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                    helperText="Default: 16 (1..S1+S2)"
                    sx={{ flex: 1 }}
                  />
                </Stack>
                <TextField
                  size="small"
                  label="Condition fields"
                  placeholder="Medium,Temperature (°C),Discs"
                  value={form.conditionFields}
                  onChange={(e) => patchForm({ conditionFields: e.target.value })}
                  helperText="comma-separated, e.g. Medium,Temperature (°C),Discs"
                  slotProps={{ htmlInput: { maxLength: 500 } }}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {form.workflowType === "WeightVariation" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 1.5 }}>
                Weight Variation Acceptance (USP &lt;2091&gt;)
              </Typography>
              <Stack spacing={2}>
                <TextField
                  size="small"
                  type="number"
                  label="Unit Count (Stage 1)"
                  value={form.wvUnitCount}
                  onChange={(e) => patchForm({ wvUnitCount: e.target.value })}
                  slotProps={{ htmlInput: { min: 1, step: 1 } }}
                  helperText="Default: 20"
                  sx={{ maxWidth: 220 }}
                />

                {/* Group: Tablets */}
                <Box sx={{ p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
                  <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1.5, color: "text.secondary" }}>
                    Tablets
                  </Typography>
                  <Stack spacing={2}>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Band 1 Max (mg)"
                        value={form.wvTabletBand1MaxMg}
                        onChange={(e) => patchForm({ wvTabletBand1MaxMg: e.target.value })}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 130"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Band 1 Deviation (%)"
                        value={form.wvTabletBand1Percent}
                        onChange={(e) => patchForm({ wvTabletBand1Percent: e.target.value })}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 10"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Band 2 Max (mg)"
                        value={form.wvTabletBand2MaxMg}
                        onChange={(e) => patchForm({ wvTabletBand2MaxMg: e.target.value })}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 324"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Band 2 Deviation (%)"
                        value={form.wvTabletBand2Percent}
                        onChange={(e) => patchForm({ wvTabletBand2Percent: e.target.value })}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 7.5"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Band 3 Deviation (%)"
                        value={form.wvTabletBand3Percent}
                        onChange={(e) => patchForm({ wvTabletBand3Percent: e.target.value })}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 5 (> Band 2)"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Max Outside Count"
                        value={form.wvTabletMaxOutside}
                        onChange={(e) => patchForm({ wvTabletMaxOutside: e.target.value })}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        helperText="Default: 2"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                  </Stack>
                </Box>

                {/* Group: Capsules */}
                <Box sx={{ p: 1.5, bgcolor: "background.paper", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
                  <Typography sx={{ fontWeight: 700, fontSize: 12, mb: 1.5, color: "text.secondary" }}>
                    Capsules
                  </Typography>
                  <Stack spacing={2}>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Inner Limit (%)"
                        value={form.wvCapsuleInnerPercent}
                        onChange={(e) => patchForm({ wvCapsuleInnerPercent: e.target.value })}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 10"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Outer Limit (%)"
                        value={form.wvCapsuleOuterPercent}
                        onChange={(e) => patchForm({ wvCapsuleOuterPercent: e.target.value })}
                        slotProps={{ htmlInput: { min: 0.01, step: "any" } }}
                        helperText="Default: 25"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Stage 1 Max Outside"
                        value={form.wvCapsuleS1MaxOutside}
                        onChange={(e) => patchForm({ wvCapsuleS1MaxOutside: e.target.value })}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        helperText="Default: 2 (pass without retest)"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Stage 1 Max For Retest"
                        value={form.wvCapsuleS1MaxForRetest}
                        onChange={(e) => patchForm({ wvCapsuleS1MaxForRetest: e.target.value })}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        helperText="Default: 6 (qualifies for S2)"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                    <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
                      <TextField
                        size="small"
                        type="number"
                        label="Stage 2 Extra Units"
                        value={form.wvCapsuleS2ExtraUnits}
                        onChange={(e) => patchForm({ wvCapsuleS2ExtraUnits: e.target.value })}
                        slotProps={{ htmlInput: { min: 1, step: 1 } }}
                        helperText="Default: 40"
                        sx={{ flex: 1 }}
                      />
                      <TextField
                        size="small"
                        type="number"
                        label="Stage 2 Max Outside"
                        value={form.wvCapsuleS2MaxOutside}
                        onChange={(e) => patchForm({ wvCapsuleS2MaxOutside: e.target.value })}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        helperText="Default: 6 (across S1+S2)"
                        sx={{ flex: 1 }}
                      />
                    </Stack>
                  </Stack>
                </Box>

                <TextField
                  size="small"
                  label="Condition fields"
                  placeholder="Balance ID,Temperature (°C)"
                  value={form.conditionFields}
                  onChange={(e) => patchForm({ conditionFields: e.target.value })}
                  helperText="comma-separated, e.g. Balance ID,Temperature (°C)"
                  slotProps={{ htmlInput: { maxLength: 500 } }}
                  fullWidth
                />
              </Stack>
            </Box>
          )}

          {form.workflowType === "Dissolution" && (
            <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
              <FormControlLabel
                control={<Switch checked={form.requiresSystemSuitability} disabled />}
                label="Requires system suitability (standard from the assigned HPLC run)"
              />

              <Box sx={{ mt: 2, pt: 2, borderTop: "1px dashed", borderTopColor: "divider" }}>
                <TextField
                  size="small"
                  label="Method Abbreviation"
                  placeholder="e.g. VIT-C"
                  value={form.methodAbbreviation}
                  onChange={(e) => patchForm({ methodAbbreviation: e.target.value.toUpperCase() })}
                  required
                  fullWidth
                  slotProps={{
                    htmlInput: {
                      maxLength: 20
                    }
                  }}
                  helperText="1–20 uppercase alphanumeric characters or hyphens (auto-uppercased)"
                />
              </Box>
            </Box>
          )}

          {/* Titration and UV use their own replicate count, not replicates per stage. */}
          {form.workflowType !== "Titration" && form.workflowType !== "UvSpectrophotometry" && (isFp || (fpSectionId !== null && (form.sectionId === fpSectionId || form.editingTest?.sectionId === fpSectionId))) && (
            form.editingId ? (
              <TestStageReplicatesSection testDefinitionId={form.editingId} />
            ) : (
              <Box sx={{ p: 2, bgcolor: "action.hover", borderRadius: 1, border: "1px solid", borderColor: "divider" }}>
                <Typography sx={{ fontWeight: 700, fontSize: 13, mb: 0.5 }}>
                  Replicates per stage
                </Typography>
                <Typography variant="body2" sx={{ color: "text.secondary" }}>
                  Save this test first to configure replicates per stage.
                </Typography>
              </Box>
            )
          )}
        </Stack>
      </FormDialog>
      <ChangeReasonDialog
        open={pendingReasonAction !== null}
        title={
          pendingReasonAction?.kind === "freeze"
            ? pendingReasonAction.test.isActive
              ? `Freeze test "${pendingReasonAction.test.code}"?`
              : `Unfreeze test "${pendingReasonAction.test.code}"?`
            : pendingReasonAction
              ? `Reason required to edit test "${pendingReasonAction.code}"`
              : "Reason for change"
        }
        message={
          pendingReasonAction?.kind === "freeze"
            ? pendingReasonAction.test.isActive
              ? "Freezing prevents this test from being selected for new work. Please record why it is being frozen."
              : "Unfreezing makes this test available for new work again. Please record why it is being unfrozen."
            : "This test is referenced by open laboratory work. Enter a reason to apply your changes."
        }
        confirmText={
          pendingReasonAction?.kind === "freeze"
            ? pendingReasonAction.test.isActive
              ? "Freeze"
              : "Unfreeze"
            : "Save Change"
        }
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
