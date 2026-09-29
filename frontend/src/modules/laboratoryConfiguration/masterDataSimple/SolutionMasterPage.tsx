import { useState, useEffect, useMemo, useCallback } from "react";
import {
  Box,
  Paper,
  Typography,
  Button,
  TextField,
  Select,
  MenuItem,
  FormControl,
  InputLabel,
  Table,
  TableHead,
  TableRow,
  TableCell,
  TableBody,
  TableContainer,
  Chip,
  Stack,
  IconButton,
  Tooltip,
  CircularProgress,
  Alert,
  RadioGroup,
  Radio,
  FormControlLabel,
  FormLabel,
  Switch,
  useTheme,
  FormHelperText
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import BlockIcon from "@mui/icons-material/Block";
import RefreshIcon from "@mui/icons-material/Refresh";
import CheckCircleOutlinedIcon from "@mui/icons-material/CheckCircleOutlined";
import WavesOutlinedIcon from "@mui/icons-material/WavesOutlined";
import OpacityOutlinedIcon from "@mui/icons-material/OpacityOutlined";
import ScaleOutlinedIcon from "@mui/icons-material/ScaleOutlined";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import ScienceOutlinedIcon from "@mui/icons-material/ScienceOutlined";
import { PageHeader } from "../../../components/PageHeader";
import { FloatingDialog } from "../../../components/FloatingDialog";
import { tableHeadSx } from "../../../theme";
import { toast } from "sonner";
import {
  SolutionMasterService,
  SolutionMaster,
  SolutionType,
  ShelfLifeUnit,
  SolutionComponentUnit,
  TitrantStrengthUnit,
  StandardizationMode,
  SaveSolutionMasterRequest
} from "./services/SolutionMasterService";
import {
  MaterialMasterService,
  MaterialMasterEntry
} from "./services/MaterialMasterService";
import { useLaboratorySections } from "../../../hooks/useLaboratorySections";
import { getMySections, LaboratorySection } from "../../../services/laboratorySectionService";

interface ComponentRowState {
  materialMasterEntryId: string | number;
  quantity: string | number;
  unit: SolutionComponentUnit;
}

const SOLUTION_TYPE_OPTIONS: Array<{ value: SolutionType; label: string }> = [
  { value: "MobilePhase", label: "Mobile Phase" },
  { value: "Diluent", label: "Diluent" },
  { value: "Titrant", label: "Titrant" }
];

const SHELF_LIFE_UNIT_OPTIONS: Array<{ value: ShelfLifeUnit; label: string }> = [
  { value: "Hours", label: "Hours" },
  { value: "Days", label: "Days" }
];

const COMPONENT_UNIT_OPTIONS: Array<{ value: SolutionComponentUnit; label: string }> = [
  { value: "Gram", label: "g (Gram)" },
  { value: "Milligram", label: "mg (Milligram)" },
  { value: "Milliliter", label: "mL (Milliliter)" },
  { value: "Liter", label: "L (Liter)" },
  { value: "PercentVolume", label: "% v/v (Percent Volume)" },
  { value: "Parts", label: "Parts" }
];

const STRENGTH_UNIT_OPTIONS: Array<{ value: TitrantStrengthUnit; label: string }> = [
  { value: "Normal", label: "Normal (N)" },
  { value: "Molar", label: "Molar (M)" }
];

export function SolutionMasterPage() {
  const theme = useTheme();
  const { sections, sectionName } = useLaboratorySections();

  const [solutions, setSolutions] = useState<SolutionMaster[]>([]);
  const [materialMasters, setMaterialMasters] = useState<MaterialMasterEntry[]>([]);
  const [mySections, setMySections] = useState<LaboratorySection[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [searchQuery, setSearchQuery] = useState("");
  const [typeFilter, setTypeFilter] = useState<"ALL" | SolutionType>("ALL");
  const [statusFilter, setStatusFilter] = useState<"ALL" | "ACTIVE" | "INACTIVE">("ALL");
  const [sectionFilter, setSectionFilter] = useState<string>("ALL");

  // Create / Edit Dialog State
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingEntry, setEditingEntry] = useState<SolutionMaster | null>(null);

  // General fields
  const [formName, setFormName] = useState("");
  const [formType, setFormType] = useState<SolutionType>("MobilePhase");
  const [formShelfLifeValue, setFormShelfLifeValue] = useState<string | number>("24");
  const [formShelfLifeUnit, setFormShelfLifeUnit] = useState<ShelfLifeUnit>("Hours");
  const [formStorageCondition, setFormStorageCondition] = useState("");
  const [formFinalVolumeMl, setFormFinalVolumeMl] = useState<string | number>("1000");
  const [formInstructions, setFormInstructions] = useState("");
  const [formSectionId, setFormSectionId] = useState<string | number>("");

  // pH fields
  const [formPhTarget, setFormPhTarget] = useState<string | number>("");
  const [formPhTolerance, setFormPhTolerance] = useState<string | number>("");
  const [formPhAdjustingEntryId, setFormPhAdjustingEntryId] = useState<string | number>("");

  // Components list
  const [formComponents, setFormComponents] = useState<ComponentRowState[]>([]);

  // Titrant fields
  const [formNominalStrength, setFormNominalStrength] = useState<string | number>("");
  const [formStrengthUnit, setFormStrengthUnit] = useState<TitrantStrengthUnit>("Normal");
  const [formStandardizationMode, setFormStandardizationMode] = useState<StandardizationMode>("PrimaryStandard");
  const [formStandardEntryId, setFormStandardEntryId] = useState<string | number>("");
  const [formEquivalenceMgPerMl, setFormEquivalenceMgPerMl] = useState<string | number>("");
  const [formReferenceSolutionId, setFormReferenceSolutionId] = useState<string | number>("");
  const [formBlankRequired, setFormBlankRequired] = useState(false);
  const [formReplicateCount, setFormReplicateCount] = useState<string | number>("3");
  const [formFactorMin, setFormFactorMin] = useState<string | number>("0.97000");
  const [formFactorMax, setFormFactorMax] = useState<string | number>("1.03000");
  const [formMaxRsdPercent, setFormMaxRsdPercent] = useState<string | number>("0.20");
  const [formValidityDays, setFormValidityDays] = useState<string | number>("30");

  const [dialogError, setDialogError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // Pending Save & Audit Reason Dialog for edits
  const [saveReasonDialogOpen, setSaveReasonDialogOpen] = useState(false);
  const [saveReason, setSaveReason] = useState("");
  const [pendingSavePayload, setPendingSavePayload] = useState<SaveSolutionMasterRequest | null>(null);

  // Activate / Deactivate Toggle Dialog with Reason
  const [solutionToToggle, setSolutionToToggle] = useState<SolutionMaster | null>(null);
  const [toggleReason, setToggleReason] = useState("");
  const [togglingActive, setTogglingActive] = useState(false);
  const [toggleError, setToggleError] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [solutionData, materialData, mySecData] = await Promise.all([
        SolutionMasterService.getAll(),
        MaterialMasterService.getAll(undefined, true),
        getMySections().catch(() => [])
      ]);
      setSolutions(Array.isArray(solutionData) ? solutionData : []);
      setMaterialMasters(Array.isArray(materialData) ? materialData : []);
      setMySections(Array.isArray(mySecData) ? mySecData : []);
    } catch (err: unknown) {
      console.error("Failed to load solution masters:", err);
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setError(errorObj.response?.data?.message ?? errorObj.message ?? "Could not load solution masters.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Selected Section ID for the current form
  const currentSectionId = useMemo(() => {
    if (editingEntry) return editingEntry.sectionId;
    if (formSectionId) return Number(formSectionId);
    if (mySections.length === 1) return mySections[0].sectionId;
    return null;
  }, [editingEntry, formSectionId, mySections]);

  // Available Material Master entries for current section (includes existing entries if deactivated)
  const availableMaterialEntries = useMemo(() => {
    if (!currentSectionId) return [];
    const list: MaterialMasterEntry[] = materialMasters.filter(
      (m) => m.sectionId === currentSectionId
    );

    // If editing, preserve any component entries or pH adjusting/standard entries already used
    if (editingEntry) {
      for (const comp of editingEntry.components) {
        if (!list.some((m) => m.id === comp.materialMasterEntryId)) {
          list.push({
            id: comp.materialMasterEntryId,
            code: comp.entryCode,
            name: comp.entryName,
            category: "Reagent",
            baseUnit: "Gram",
            isActive: false,
            sectionId: editingEntry.sectionId
          });
        }
      }
      if (
        editingEntry.phAdjustingEntryId &&
        !list.some((m) => m.id === editingEntry.phAdjustingEntryId)
      ) {
        list.push({
          id: editingEntry.phAdjustingEntryId,
          code: editingEntry.phAdjustingEntryCode ?? `Entry #${editingEntry.phAdjustingEntryId}`,
          name: editingEntry.phAdjustingEntryCode ?? `Entry #${editingEntry.phAdjustingEntryId}`,
          category: "Reagent",
          baseUnit: "Gram",
          isActive: false,
          sectionId: editingEntry.sectionId
        });
      }
      if (
        editingEntry.standardEntryId &&
        !list.some((m) => m.id === editingEntry.standardEntryId)
      ) {
        list.push({
          id: editingEntry.standardEntryId,
          code: editingEntry.standardEntryCode ?? `Standard #${editingEntry.standardEntryId}`,
          name: editingEntry.standardEntryCode ?? `Standard #${editingEntry.standardEntryId}`,
          category: "ReferenceStandard",
          baseUnit: "Gram",
          isActive: false,
          sectionId: editingEntry.sectionId
        });
      }
    }

    return list;
  }, [currentSectionId, materialMasters, editingEntry]);

  // Reagent entries for pH adjusting reagent picker
  const availablePhAdjustingEntries = useMemo(() => {
    return availableMaterialEntries.filter((m) => m.category === "Reagent");
  }, [availableMaterialEntries]);

  // Standards / Reagents for Titrant Primary Standard picker
  const availableTitrantStandardEntries = useMemo(() => {
    return availableMaterialEntries.filter(
      (m) => m.category === "ReferenceStandard" || m.category === "Reagent"
    );
  }, [availableMaterialEntries]);

  // Active Titrants for Reference Solution picker (same section, excluding self)
  const availableReferenceTitrants = useMemo(() => {
    if (!currentSectionId) return [];
    return solutions.filter(
      (s) =>
        s.type === "Titrant" &&
        s.isActive &&
        s.sectionId === currentSectionId &&
        (!editingEntry || s.id !== editingEntry.id)
    );
  }, [solutions, currentSectionId, editingEntry]);

  // Open Add Dialog
  const handleOpenAdd = () => {
    setEditingEntry(null);
    setFormName("");
    setFormType("MobilePhase");
    setFormShelfLifeValue("24");
    setFormShelfLifeUnit("Hours");
    setFormStorageCondition("");
    setFormFinalVolumeMl("1000");
    setFormInstructions("");
    const defaultSecId = mySections.length === 1 ? mySections[0].sectionId : "";
    setFormSectionId(defaultSecId);
    setFormPhTarget("");
    setFormPhTolerance("");
    setFormPhAdjustingEntryId("");
    setFormComponents([{ materialMasterEntryId: "", quantity: "", unit: "Milliliter" }]);

    // Titrant defaults
    setFormNominalStrength("");
    setFormStrengthUnit("Normal");
    setFormStandardizationMode("PrimaryStandard");
    setFormStandardEntryId("");
    setFormEquivalenceMgPerMl("");
    setFormReferenceSolutionId("");
    setFormBlankRequired(false);
    setFormReplicateCount("3");
    setFormFactorMin("0.97000");
    setFormFactorMax("1.03000");
    setFormMaxRsdPercent("0.20");
    setFormValidityDays("30");

    setDialogError(null);
    setDialogOpen(true);
  };

  // Open Edit Dialog
  const handleOpenEdit = (entry: SolutionMaster) => {
    setEditingEntry(entry);
    setFormName(entry.name);
    setFormType(entry.type);
    setFormShelfLifeValue(entry.shelfLifeValue);
    setFormShelfLifeUnit(entry.shelfLifeUnit);
    setFormStorageCondition(entry.storageCondition);
    setFormFinalVolumeMl(entry.finalVolumeMl);
    setFormInstructions(entry.instructions);
    setFormSectionId(entry.sectionId);
    setFormPhTarget(entry.phTarget ?? "");
    setFormPhTolerance(entry.phTolerance ?? "");
    setFormPhAdjustingEntryId(entry.phAdjustingEntryId ?? "");

    if (entry.components && entry.components.length > 0) {
      setFormComponents(
        entry.components.map((c) => ({
          materialMasterEntryId: c.materialMasterEntryId,
          quantity: c.quantity,
          unit: c.unit
        }))
      );
    } else {
      setFormComponents([{ materialMasterEntryId: "", quantity: "", unit: "Milliliter" }]);
    }

    // Titrant fields
    setFormNominalStrength(entry.nominalStrength ?? "");
    setFormStrengthUnit(entry.strengthUnit ?? "Normal");
    setFormStandardizationMode(entry.standardizationMode ?? "PrimaryStandard");
    setFormStandardEntryId(entry.standardEntryId ?? "");
    setFormEquivalenceMgPerMl(entry.equivalenceMgPerMl ?? "");
    setFormReferenceSolutionId(entry.referenceSolutionId ?? "");
    setFormBlankRequired(Boolean(entry.blankRequired));
    setFormReplicateCount(entry.replicateCount ?? "3");
    setFormFactorMin(entry.factorMin ?? "0.97000");
    setFormFactorMax(entry.factorMax ?? "1.03000");
    setFormMaxRsdPercent(entry.maxRsdPercent ?? "0.20");
    setFormValidityDays(entry.validityDays ?? "30");

    setDialogError(null);
    setDialogOpen(true);
  };

  // Component row handlers
  const handleAddComponentRow = () => {
    setFormComponents((prev) => [
      ...prev,
      { materialMasterEntryId: "", quantity: "", unit: "Milliliter" }
    ]);
  };

  const handleRemoveComponentRow = (index: number) => {
    setFormComponents((prev) => prev.filter((_, i) => i !== index));
  };

  const handleMoveComponentRow = (index: number, direction: "up" | "down") => {
    setFormComponents((prev) => {
      const copy = [...prev];
      const targetIndex = direction === "up" ? index - 1 : index + 1;
      if (targetIndex < 0 || targetIndex >= copy.length) return copy;
      const temp = copy[index];
      copy[index] = copy[targetIndex];
      copy[targetIndex] = temp;
      return copy;
    });
  };

  const handleComponentChange = (
    index: number,
    field: keyof ComponentRowState,
    value: string | number
  ) => {
    setFormComponents((prev) => {
      const copy = [...prev];
      copy[index] = { ...copy[index], [field]: value };
      return copy;
    });
  };

  // Validate form and initiate save
  const handleInitiateSave = () => {
    setDialogError(null);
    const trimmedName = formName.trim();
    const trimmedStorage = formStorageCondition.trim();
    const trimmedInstructions = formInstructions.trim();

    if (!trimmedName) {
      setDialogError("Solution Name is required.");
      return;
    }

    if (!editingEntry && mySections.length > 1 && !formSectionId) {
      setDialogError("Laboratory Section is required.");
      return;
    }

    const shelfLifeNum = Number(formShelfLifeValue);
    if (!formShelfLifeValue || isNaN(shelfLifeNum) || shelfLifeNum <= 0) {
      setDialogError("Shelf Life Value must be greater than zero.");
      return;
    }

    const finalVolumeNum = Number(formFinalVolumeMl);
    if (!formFinalVolumeMl || isNaN(finalVolumeNum) || finalVolumeNum <= 0) {
      setDialogError("Final Volume (mL) must be greater than zero.");
      return;
    }

    if (!trimmedStorage) {
      setDialogError("Storage Condition is required.");
      return;
    }

    if (!trimmedInstructions) {
      setDialogError("Preparation Instructions are required.");
      return;
    }

    // Component validation
    if (formComponents.length === 0) {
      setDialogError("At least one recipe component is required.");
      return;
    }

    const usedEntryIds = new Set<number>();
    for (let i = 0; i < formComponents.length; i++) {
      const comp = formComponents[i];
      const entryId = Number(comp.materialMasterEntryId);
      const qty = Number(comp.quantity);

      if (!comp.materialMasterEntryId || isNaN(entryId) || entryId <= 0) {
        setDialogError(`Component #${i + 1} must select a Material Master entry.`);
        return;
      }

      if (usedEntryIds.has(entryId)) {
        setDialogError(`Material entry is listed more than once in the components recipe.`);
        return;
      }
      usedEntryIds.add(entryId);

      if (!comp.quantity || isNaN(qty) || qty <= 0) {
        setDialogError(`Component #${i + 1} quantity must be greater than zero.`);
        return;
      }
    }

    // pH validation
    const hasPhTarget = formPhTarget !== "";
    const phTargetNum = Number(formPhTarget);
    if (hasPhTarget && (isNaN(phTargetNum) || phTargetNum < 0 || phTargetNum > 14)) {
      setDialogError("pH Target must be between 0.00 and 14.00.");
      return;
    }

    if (formPhTolerance !== "") {
      if (!hasPhTarget) {
        setDialogError("pH Target is required when pH Tolerance is specified.");
        return;
      }
      const tolNum = Number(formPhTolerance);
      if (isNaN(tolNum) || tolNum <= 0) {
        setDialogError("pH Tolerance must be greater than zero.");
        return;
      }
    }

    if (formPhAdjustingEntryId && !hasPhTarget) {
      setDialogError("pH Target is required when a pH adjusting reagent is selected.");
      return;
    }

    // Titrant validation (only when type is Titrant)
    const isTitrant = formType === "Titrant";
    if (isTitrant) {
      const strengthNum = Number(formNominalStrength);
      if (!formNominalStrength || isNaN(strengthNum) || strengthNum <= 0) {
        setDialogError("Nominal Strength must be greater than zero for titrants.");
        return;
      }

      if (formStandardizationMode === "PrimaryStandard") {
        if (!formStandardEntryId) {
          setDialogError("Primary Standard entry is required for PrimaryStandard mode.");
          return;
        }
        const equivNum = Number(formEquivalenceMgPerMl);
        if (!formEquivalenceMgPerMl || isNaN(equivNum) || equivNum <= 0) {
          setDialogError("Equivalence (mg/mL) must be greater than zero.");
          return;
        }
      } else if (formStandardizationMode === "AgainstVolumetricSolution") {
        if (!formReferenceSolutionId) {
          setDialogError("Reference Titrant is required for AgainstVolumetricSolution mode.");
          return;
        }
        if (editingEntry && Number(formReferenceSolutionId) === editingEntry.id) {
          setDialogError("A titrant cannot standardize against itself.");
          return;
        }
      }

      const repNum = Number(formReplicateCount);
      if (!formReplicateCount || isNaN(repNum) || repNum < 1 || !Number.isInteger(repNum)) {
        setDialogError("Replicate Count must be an integer of at least 1.");
        return;
      }

      const minNum = Number(formFactorMin);
      const maxNum = Number(formFactorMax);
      if (!formFactorMin || isNaN(minNum) || minNum <= 0) {
        setDialogError("Factor Min must be greater than zero.");
        return;
      }
      if (!formFactorMax || isNaN(maxNum) || maxNum <= 0) {
        setDialogError("Factor Max must be greater than zero.");
        return;
      }
      if (minNum > maxNum) {
        setDialogError("Factor Min cannot be greater than Factor Max.");
        return;
      }

      const rsdNum = Number(formMaxRsdPercent);
      if (!formMaxRsdPercent || isNaN(rsdNum) || rsdNum <= 0) {
        setDialogError("Max RSD % must be greater than zero.");
        return;
      }

      const valDaysNum = Number(formValidityDays);
      if (
        formValidityDays === "" ||
        isNaN(valDaysNum) ||
        valDaysNum < 0 ||
        !Number.isInteger(valDaysNum)
      ) {
        setDialogError("Standardization Validity Days must be 0 or greater.");
        return;
      }
    }

    const payload: SaveSolutionMasterRequest = {
      name: trimmedName,
      type: formType,
      shelfLifeValue: Number(formShelfLifeValue),
      shelfLifeUnit: formShelfLifeUnit,
      storageCondition: trimmedStorage,
      finalVolumeMl: Number(formFinalVolumeMl),
      instructions: trimmedInstructions,
      sectionId: currentSectionId,
      components: formComponents.map((c) => ({
        materialMasterEntryId: Number(c.materialMasterEntryId),
        quantity: Number(c.quantity),
        unit: c.unit
      })),
      phTarget: hasPhTarget ? Number(formPhTarget) : null,
      phTolerance: formPhTolerance !== "" ? Number(formPhTolerance) : null,
      phAdjustingEntryId: formPhAdjustingEntryId ? Number(formPhAdjustingEntryId) : null,

      // Titrant fields only when type is Titrant; otherwise send null/false
      nominalStrength: isTitrant && formNominalStrength !== "" ? Number(formNominalStrength) : null,
      strengthUnit: isTitrant ? formStrengthUnit : null,
      standardizationMode: isTitrant ? formStandardizationMode : null,
      standardEntryId:
        isTitrant && formStandardizationMode === "PrimaryStandard" && formStandardEntryId
          ? Number(formStandardEntryId)
          : null,
      equivalenceMgPerMl:
        isTitrant && formStandardizationMode === "PrimaryStandard" && formEquivalenceMgPerMl !== ""
          ? Number(formEquivalenceMgPerMl)
          : null,
      referenceSolutionId:
        isTitrant &&
        formStandardizationMode === "AgainstVolumetricSolution" &&
        formReferenceSolutionId
          ? Number(formReferenceSolutionId)
          : null,
      blankRequired: isTitrant ? Boolean(formBlankRequired) : false,
      replicateCount: isTitrant && formReplicateCount !== "" ? Number(formReplicateCount) : null,
      factorMin: isTitrant && formFactorMin !== "" ? Number(formFactorMin) : null,
      factorMax: isTitrant && formFactorMax !== "" ? Number(formFactorMax) : null,
      maxRsdPercent: isTitrant && formMaxRsdPercent !== "" ? Number(formMaxRsdPercent) : null,
      validityDays: isTitrant && formValidityDays !== "" ? Number(formValidityDays) : null
    };

    if (editingEntry) {
      // Editing an existing solution requires an audit reason
      setPendingSavePayload(payload);
      setSaveReason("");
      setSaveReasonDialogOpen(true);
    } else {
      // Creating a new solution does not require a reason
      executeCreate(payload);
    }
  };

  // Direct Create Execution
  const executeCreate = async (payload: SaveSolutionMasterRequest) => {
    setSaving(true);
    setDialogError(null);
    try {
      await SolutionMasterService.create(payload);
      toast.success(`Solution master "${payload.name}" created.`);
      setDialogOpen(false);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setDialogError(errorObj.response?.data?.message ?? errorObj.message ?? "Could not create solution master.");
    } finally {
      setSaving(false);
    }
  };

  // Confirm Edit Execution with Reason
  const handleConfirmSaveWithReason = async () => {
    if (!editingEntry || !pendingSavePayload) return;
    const trimmedReason = saveReason.trim();
    if (!trimmedReason) return;

    setSaving(true);
    try {
      const finalPayload: SaveSolutionMasterRequest = {
        ...pendingSavePayload,
        reason: trimmedReason
      };
      await SolutionMasterService.update(editingEntry.id, finalPayload, editingEntry.version);
      toast.success(`Solution master "${editingEntry.name}" updated.`);
      setSaveReasonDialogOpen(false);
      setPendingSavePayload(null);
      setDialogOpen(false);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      const msg = errorObj.response?.data?.message ?? errorObj.message ?? "Could not update solution master.";
      setDialogError(msg);
      setSaveReasonDialogOpen(false);
    } finally {
      setSaving(false);
    }
  };

  // Open Activate / Deactivate Toggle Dialog with Reason
  const handleOpenToggleActive = (solution: SolutionMaster) => {
    setSolutionToToggle(solution);
    setToggleReason("");
    setToggleError(null);
  };

  // Confirm Activate / Deactivate Toggle
  const handleConfirmToggleActive = async () => {
    if (!solutionToToggle) return;
    const trimmedReason = toggleReason.trim();
    if (!trimmedReason) {
      setToggleError("A reason is required to activate or deactivate a solution master.");
      return;
    }

    const nextActive = !solutionToToggle.isActive;
    setTogglingActive(true);
    setToggleError(null);
    try {
      await SolutionMasterService.setActive(
        solutionToToggle.id,
        nextActive,
        trimmedReason,
        solutionToToggle.version
      );
      toast.success(
        `Solution master "${solutionToToggle.name}" ${nextActive ? "activated" : "deactivated"}.`
      );
      setSolutionToToggle(null);
      await loadData();
    } catch (err: unknown) {
      const errorObj = err as { response?: { data?: { message?: string } }; message?: string };
      setToggleError(
        errorObj.response?.data?.message ?? errorObj.message ?? "Could not update solution status."
      );
    } finally {
      setTogglingActive(false);
    }
  };

  // Filtered solutions
  const filteredSolutions = useMemo(() => {
    return solutions.filter((sol) => {
      // Search
      if (searchQuery.trim()) {
        const q = searchQuery.toLowerCase().trim();
        const nameMatch = sol.name.toLowerCase().includes(q);
        const instructionsMatch = sol.instructions ? sol.instructions.toLowerCase().includes(q) : false;
        const storageMatch = sol.storageCondition ? sol.storageCondition.toLowerCase().includes(q) : false;
        const compMatch = sol.components.some(
          (c) =>
            c.entryCode.toLowerCase().includes(q) ||
            c.entryName.toLowerCase().includes(q)
        );
        if (!nameMatch && !instructionsMatch && !storageMatch && !compMatch) return false;
      }

      // Type
      if (typeFilter !== "ALL" && sol.type !== typeFilter) {
        return false;
      }

      // Status
      if (statusFilter === "ACTIVE" && !sol.isActive) return false;
      if (statusFilter === "INACTIVE" && sol.isActive) return false;

      // Section
      if (sectionFilter !== "ALL" && String(sol.sectionId) !== sectionFilter) {
        return false;
      }

      return true;
    });
  }, [solutions, searchQuery, typeFilter, statusFilter, sectionFilter]);

  const resolveSectionDisplay = (secId: number, secNameProp?: string) => {
    if (secNameProp) return secNameProp;
    const fromHook = sectionName(secId);
    if (fromHook) return fromHook;
    const foundSec = sections.find((s) => s.sectionId === secId);
    if (foundSec?.sectionName) return foundSec.sectionName;
    return `Section #${secId}`;
  };

  const renderTypeChip = (type: SolutionType) => {
    switch (type) {
      case "MobilePhase":
        return (
          <Chip
            icon={<WavesOutlinedIcon fontSize="small" />}
            label="Mobile Phase"
            size="small"
            color="primary"
            variant="outlined"
            sx={{ fontSize: 11, fontWeight: 600 }}
          />
        );
      case "Diluent":
        return (
          <Chip
            icon={<OpacityOutlinedIcon fontSize="small" />}
            label="Diluent"
            size="small"
            color="secondary"
            variant="outlined"
            sx={{ fontSize: 11, fontWeight: 600 }}
          />
        );
      case "Titrant":
        return (
          <Chip
            icon={<ScaleOutlinedIcon fontSize="small" />}
            label="Titrant"
            size="small"
            color="info"
            variant="outlined"
            sx={{ fontSize: 11, fontWeight: 600 }}
          />
        );
      default:
        return <Chip label={type} size="small" variant="outlined" sx={{ fontSize: 11 }} />;
    }
  };

  return (
    <Box sx={{ p: 3 }}>
      <PageHeader
        title="Solutions Master"
        subtitle="Manage master recipes for mobile phases, diluents, and volumetric titrants including component stoichiometry, shelf life, pH specifications, and standardization criteria."
      />

      {error && (
        <Alert severity="error" sx={{ mb: 3 }}>
          {error}
        </Alert>
      )}

      {/* Filter & Action Toolbar */}
      <Paper sx={{ p: 2.5, mb: 3 }}>
        <Stack
          direction={{ xs: "column", sm: "row" }}
          spacing={2}
          sx={{
            alignItems: { sm: "center" },
            justifyContent: "space-between",
            flexWrap: "wrap"
          }}
        >
          <Stack direction={{ xs: "column", sm: "row" }} spacing={1.5} sx={{ flexWrap: "wrap", flex: 1 }}>
            <TextField
              size="small"
              placeholder="Search name, recipe, storage, instructions..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              sx={{ minWidth: 280 }}
            />

            <FormControl size="small" sx={{ minWidth: 160 }}>
              <InputLabel id="solution-type-filter-label">Type</InputLabel>
              <Select
                labelId="solution-type-filter-label"
                label="Type"
                value={typeFilter}
                onChange={(e) => setTypeFilter(e.target.value as "ALL" | SolutionType)}
              >
                <MenuItem value="ALL">All Types</MenuItem>
                <MenuItem value="MobilePhase">Mobile Phase</MenuItem>
                <MenuItem value="Diluent">Diluent</MenuItem>
                <MenuItem value="Titrant">Titrant</MenuItem>
              </Select>
            </FormControl>

            <FormControl size="small" sx={{ minWidth: 150 }}>
              <InputLabel id="solution-status-filter-label">Status</InputLabel>
              <Select
                labelId="solution-status-filter-label"
                label="Status"
                value={statusFilter}
                onChange={(e) => setStatusFilter(e.target.value as "ALL" | "ACTIVE" | "INACTIVE")}
              >
                <MenuItem value="ALL">All Statuses</MenuItem>
                <MenuItem value="ACTIVE">Active Only</MenuItem>
                <MenuItem value="INACTIVE">Inactive Only</MenuItem>
              </Select>
            </FormControl>

            <FormControl size="small" sx={{ minWidth: 180 }}>
              <InputLabel id="solution-section-filter-label">Section</InputLabel>
              <Select
                labelId="solution-section-filter-label"
                label="Section"
                value={sectionFilter}
                onChange={(e) => setSectionFilter(e.target.value)}
              >
                <MenuItem value="ALL">All Sections</MenuItem>
                {sections.map((sec) => (
                  <MenuItem key={sec.sectionId} value={String(sec.sectionId)}>
                    {sec.sectionName}
                  </MenuItem>
                ))}
              </Select>
            </FormControl>

            <Tooltip title="Refresh">
              <IconButton onClick={loadData} size="small" sx={{ alignSelf: "center" }}>
                <RefreshIcon />
              </IconButton>
            </Tooltip>
          </Stack>

          <Button
            variant="contained"
            startIcon={<AddIcon />}
            onClick={handleOpenAdd}
            sx={{ fontWeight: 600, textTransform: "none", height: 40 }}
          >
            Add Solution
          </Button>
        </Stack>
      </Paper>

      {/* Solutions Table */}
      <TableContainer
        component={Paper}
        elevation={0}
        sx={{ border: "1px solid", borderColor: "divider", borderRadius: 2 }}
      >
        <Table size="small">
          <TableHead>
            <TableRow sx={tableHeadSx(theme)}>
              <TableCell sx={{ fontWeight: 600 }}>Name</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Type</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Shelf Life</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Final Volume</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Components</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Section</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
              <TableCell align="right" sx={{ fontWeight: 600 }}>Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell colSpan={8} align="center" sx={{ py: 6 }}>
                  <CircularProgress size={32} />
                  <Typography variant="body2" sx={{ mt: 1, color: "text.secondary" }}>
                    Loading solution masters...
                  </Typography>
                </TableCell>
              </TableRow>
            ) : filteredSolutions.length === 0 ? (
              <TableRow>
                <TableCell colSpan={8} align="center" sx={{ py: 6 }}>
                  <ScienceOutlinedIcon sx={{ fontSize: 40, color: "text.disabled", mb: 1 }} />
                  <Typography variant="body1" sx={{ color: "text.secondary", fontWeight: 500 }}>
                    No solution masters found
                  </Typography>
                  <Typography variant="body2" sx={{ color: "text.disabled", mt: 0.5 }}>
                    {searchQuery || typeFilter !== "ALL" || statusFilter !== "ALL" || sectionFilter !== "ALL"
                      ? "Try adjusting your search or filters."
                      : "Click 'Add Solution' to register your first mobile phase, diluent, or titrant master."}
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              filteredSolutions.map((sol) => (
                <TableRow key={sol.id} hover>
                  <TableCell sx={{ fontWeight: 600 }}>
                    <div>{sol.name}</div>
                    <Stack direction="row" spacing={1} sx={{ mt: 0.5, flexWrap: "wrap" }}>
                      {sol.phTarget != null && (
                        <Typography variant="caption" color="text.secondary">
                          pH {sol.phTarget}
                          {sol.phTolerance != null ? ` ± ${sol.phTolerance}` : ""}
                        </Typography>
                      )}
                      {sol.type === "Titrant" && sol.nominalStrength != null && (
                        <Typography variant="caption" color="text.secondary">
                          • {sol.nominalStrength} {sol.strengthUnit === "Molar" ? "M" : "N"} (
                          {sol.standardizationMode === "PrimaryStandard"
                            ? "Primary Standard"
                            : "vs Volumetric Solution"}
                          )
                        </Typography>
                      )}
                    </Stack>
                  </TableCell>
                  <TableCell>{renderTypeChip(sol.type)}</TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: 500 }}>
                      {sol.shelfLifeValue} {sol.shelfLifeUnit}
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Typography variant="body2" sx={{ fontWeight: 500 }}>
                      {Number(sol.finalVolumeMl).toLocaleString()} mL
                    </Typography>
                  </TableCell>
                  <TableCell>
                    <Tooltip
                      arrow
                      title={
                        <Box sx={{ p: 0.5 }}>
                          <Typography variant="caption" sx={{ fontWeight: 700, display: "block", mb: 0.5 }}>
                            Recipe Breakdown:
                          </Typography>
                          {sol.components.map((c) => (
                            <Typography key={c.materialMasterEntryId} variant="caption" sx={{ display: "block" }}>
                              • {c.entryCode} - {c.entryName}: {c.quantity} {c.unit}
                            </Typography>
                          ))}
                        </Box>
                      }
                    >
                      <Chip
                        label={`${sol.components.length} ${sol.components.length === 1 ? "component" : "components"}`}
                        size="small"
                        variant="outlined"
                        sx={{ fontSize: 11, cursor: "pointer" }}
                      />
                    </Tooltip>
                  </TableCell>
                  <TableCell>
                    <Chip
                      label={resolveSectionDisplay(sol.sectionId, sol.sectionName)}
                      size="small"
                      variant="outlined"
                      sx={{ fontSize: 12 }}
                    />
                  </TableCell>
                  <TableCell>
                    <Chip
                      icon={sol.isActive ? <CheckCircleOutlinedIcon fontSize="small" /> : <BlockIcon fontSize="small" />}
                      label={sol.isActive ? "Active" : "Inactive"}
                      size="small"
                      color={sol.isActive ? "success" : "default"}
                      sx={{ fontSize: 11, fontWeight: 600 }}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={0.5} sx={{ justifyContent: "flex-end" }}>
                      <Tooltip title="Edit Solution">
                        <IconButton size="small" onClick={() => handleOpenEdit(sol)}>
                          <EditIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                      <Tooltip title={sol.isActive ? "Deactivate Solution" : "Activate Solution"}>
                        <IconButton
                          size="small"
                          color={sol.isActive ? "error" : "success"}
                          onClick={() => handleOpenToggleActive(sol)}
                        >
                          {sol.isActive ? <BlockIcon fontSize="small" /> : <CheckCircleOutlinedIcon fontSize="small" />}
                        </IconButton>
                      </Tooltip>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Add / Edit Floating Dialog */}
      <FloatingDialog
        open={dialogOpen}
        title={editingEntry ? `Edit Solution Master: ${editingEntry.name}` : "Add Solution Master"}
        onClose={() => setDialogOpen(false)}
        maxWidth="md"
        actions={
          <>
            <Button onClick={() => setDialogOpen(false)} disabled={saving} sx={{ textTransform: "none" }}>
              Cancel
            </Button>
            <Button
              variant="contained"
              onClick={handleInitiateSave}
              disabled={saving}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? "Saving..." : editingEntry ? "Save Changes" : "Create Solution"}
            </Button>
          </>
        }
      >
        <Stack spacing={2.5} sx={{ pt: 1 }}>
          {dialogError && (
            <Alert severity="error" onClose={() => setDialogError(null)}>
              {dialogError}
            </Alert>
          )}

          {/* Section: General Information */}
          <Paper variant="outlined" sx={{ p: 2, borderRadius: 1.5, bgcolor: "background.default" }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.primary" }}>
              General Information
            </Typography>
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
              <TextField
                label="Solution Name"
                value={formName}
                onChange={(e) => setFormName(e.target.value)}
                required
                fullWidth
                size="small"
                placeholder="e.g. 0.1M Phosphate Buffer pH 3.0, Mobile Phase A"
                helperText="Descriptive name of the solution recipe"
                sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
              />

              <FormControl fullWidth size="small" required>
                <InputLabel id="solution-type-select-label">Solution Type</InputLabel>
                <Select
                  labelId="solution-type-select-label"
                  label="Solution Type"
                  value={formType}
                  onChange={(e) => setFormType(e.target.value as SolutionType)}
                >
                  {SOLUTION_TYPE_OPTIONS.map((opt) => (
                    <MenuItem key={opt.value} value={opt.value}>
                      {opt.label}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              {/* Laboratory Section Select (if creating and user has multiple sections, or display current) */}
              {(!editingEntry && mySections.length > 1) || (editingEntry && sections.length > 0) ? (
                <FormControl fullWidth size="small" required={!editingEntry && mySections.length > 1}>
                  <InputLabel id="solution-section-select-label">Laboratory Section</InputLabel>
                  <Select
                    labelId="solution-section-select-label"
                    label="Laboratory Section"
                    value={formSectionId}
                    onChange={(e) => setFormSectionId(e.target.value)}
                    disabled={Boolean(editingEntry)}
                  >
                    {(!editingEntry && mySections.length > 1 ? mySections : sections).map((sec) => (
                      <MenuItem key={sec.sectionId} value={sec.sectionId}>
                        {sec.sectionName}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              ) : null}

              <Stack direction="row" spacing={1} sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
                <TextField
                  label="Shelf Life Value"
                  type="number"
                  value={formShelfLifeValue}
                  onChange={(e) => setFormShelfLifeValue(e.target.value)}
                  required
                  size="small"
                  sx={{ width: 140 }}
                  slotProps={{ htmlInput: { min: "1", step: "1" } }}
                />
                <FormControl size="small" sx={{ width: 130 }} required>
                  <InputLabel id="shelf-life-unit-select-label">Unit</InputLabel>
                  <Select
                    labelId="shelf-life-unit-select-label"
                    label="Unit"
                    value={formShelfLifeUnit}
                    onChange={(e) => setFormShelfLifeUnit(e.target.value as ShelfLifeUnit)}
                  >
                    {SHELF_LIFE_UNIT_OPTIONS.map((u) => (
                      <MenuItem key={u.value} value={u.value}>
                        {u.label}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                <TextField
                  label="Final Volume (mL)"
                  type="number"
                  value={formFinalVolumeMl}
                  onChange={(e) => setFormFinalVolumeMl(e.target.value)}
                  required
                  size="small"
                  fullWidth
                  placeholder="e.g. 1000"
                  slotProps={{ htmlInput: { min: "0.1", step: "any" } }}
                  helperText="Nominal total prepared volume in mL"
                />
              </Stack>

              <TextField
                label="Storage Condition"
                value={formStorageCondition}
                onChange={(e) => setFormStorageCondition(e.target.value)}
                required
                fullWidth
                size="small"
                placeholder="e.g. Store at 20-25°C in amber glass bottle"
                helperText="Temperature, container, and light protection requirements"
                sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
              />

              <TextField
                label="Preparation Instructions"
                value={formInstructions}
                onChange={(e) => setFormInstructions(e.target.value)}
                required
                fullWidth
                multiline
                rows={3}
                size="small"
                placeholder="Step-by-step preparation protocol, sonication, degassing, and filtering requirements..."
                helperText="Detailed SOP instructions for the laboratory analyst"
                sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
              />
            </Box>
          </Paper>

          {/* Section: pH Specifications */}
          <Paper variant="outlined" sx={{ p: 2, borderRadius: 1.5, bgcolor: "background.default" }}>
            <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 1.5, color: "text.primary" }}>
              pH Specifications (Optional)
            </Typography>
            <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(3, 1fr)" }, gap: 2 }}>
              <TextField
                label="pH Target"
                type="number"
                value={formPhTarget}
                onChange={(e) => setFormPhTarget(e.target.value)}
                size="small"
                placeholder="e.g. 7.00"
                slotProps={{ htmlInput: { min: "0", max: "14", step: "0.01" } }}
                helperText="Range: 0.00 – 14.00"
              />

              <TextField
                label="pH Tolerance (±)"
                type="number"
                value={formPhTolerance}
                onChange={(e) => setFormPhTolerance(e.target.value)}
                size="small"
                disabled={formPhTarget === ""}
                placeholder="e.g. 0.05"
                slotProps={{ htmlInput: { min: "0.01", step: "0.01" } }}
                helperText={formPhTarget === "" ? "Requires target pH" : "Acceptable ± deviation"}
              />

              <FormControl size="small" disabled={formPhTarget === ""}>
                <InputLabel id="ph-adjusting-reagent-select-label">pH Adjusting Reagent</InputLabel>
                <Select
                  labelId="ph-adjusting-reagent-select-label"
                  label="pH Adjusting Reagent"
                  value={formPhAdjustingEntryId}
                  onChange={(e) => setFormPhAdjustingEntryId(e.target.value)}
                >
                  <MenuItem value="">
                    <em>(None)</em>
                  </MenuItem>
                  {availablePhAdjustingEntries.map((m) => (
                    <MenuItem key={m.id} value={m.id}>
                      {m.code} - {m.name} {!m.isActive ? "(Inactive)" : ""}
                    </MenuItem>
                  ))}
                </Select>
                <FormHelperText>Reagents in current section</FormHelperText>
              </FormControl>
            </Box>
          </Paper>

          {/* Section: Recipe Components */}
          <Paper variant="outlined" sx={{ p: 2, borderRadius: 1.5, bgcolor: "background.default" }}>
            <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
              <div>
                <Typography variant="subtitle2" sx={{ fontWeight: 700, color: "text.primary" }}>
                  Recipe Components *
                </Typography>
                <Typography variant="caption" sx={{ color: "text.secondary" }}>
                  Ordered list of active material master entries from the same laboratory section.
                </Typography>
              </div>
              <Button
                size="small"
                startIcon={<AddIcon />}
                onClick={handleAddComponentRow}
                variant="outlined"
                sx={{ textTransform: "none", fontWeight: 600 }}
              >
                Add Component
              </Button>
            </Stack>

            <Stack spacing={1.5}>
              {formComponents.map((comp, idx) => (
                <Paper
                  key={idx}
                  variant="outlined"
                  sx={{
                    p: 1.5,
                    bgcolor: "background.paper",
                    borderRadius: 1.5,
                    display: "flex",
                    alignItems: "center",
                    gap: 1.5,
                    flexWrap: { xs: "wrap", md: "nowrap" }
                  }}
                >
                  <Typography
                    variant="caption"
                    sx={{
                      fontWeight: 700,
                      width: 24,
                      color: "text.secondary",
                      textAlign: "center"
                    }}
                  >
                    #{idx + 1}
                  </Typography>

                  <FormControl size="small" sx={{ flex: 2, minWidth: 200 }} required>
                    <InputLabel id={`comp-entry-label-${idx}`}>Material Master Entry</InputLabel>
                    <Select
                      labelId={`comp-entry-label-${idx}`}
                      label="Material Master Entry"
                      value={comp.materialMasterEntryId}
                      onChange={(e) =>
                        handleComponentChange(idx, "materialMasterEntryId", e.target.value)
                      }
                    >
                      {availableMaterialEntries.map((m) => (
                        <MenuItem key={m.id} value={m.id}>
                          {m.code} - {m.name} {!m.isActive ? "(Inactive)" : ""}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>

                  <TextField
                    label="Quantity"
                    type="number"
                    size="small"
                    required
                    value={comp.quantity}
                    onChange={(e) => handleComponentChange(idx, "quantity", e.target.value)}
                    sx={{ width: { xs: "100%", sm: 120 } }}
                    slotProps={{ htmlInput: { min: "0.0001", step: "any" } }}
                  />

                  <FormControl size="small" sx={{ width: { xs: "100%", sm: 160 } }} required>
                    <InputLabel id={`comp-unit-label-${idx}`}>Unit</InputLabel>
                    <Select
                      labelId={`comp-unit-label-${idx}`}
                      label="Unit"
                      value={comp.unit}
                      onChange={(e) =>
                        handleComponentChange(idx, "unit", e.target.value as SolutionComponentUnit)
                      }
                    >
                      {COMPONENT_UNIT_OPTIONS.map((opt) => (
                        <MenuItem key={opt.value} value={opt.value}>
                          {opt.label}
                        </MenuItem>
                      ))}
                    </Select>
                  </FormControl>

                  <Stack direction="row" spacing={0.5} sx={{ ml: "auto" }}>
                    <Tooltip title="Move Up">
                      <span>
                        <IconButton
                          size="small"
                          disabled={idx === 0}
                          onClick={() => handleMoveComponentRow(idx, "up")}
                        >
                          <ArrowUpwardIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                    <Tooltip title="Move Down">
                      <span>
                        <IconButton
                          size="small"
                          disabled={idx === formComponents.length - 1}
                          onClick={() => handleMoveComponentRow(idx, "down")}
                        >
                          <ArrowDownwardIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                    <Tooltip title="Remove Component">
                      <span>
                        <IconButton
                          size="small"
                          color="error"
                          disabled={formComponents.length <= 1}
                          onClick={() => handleRemoveComponentRow(idx)}
                        >
                          <DeleteOutlinedIcon fontSize="small" />
                        </IconButton>
                      </span>
                    </Tooltip>
                  </Stack>
                </Paper>
              ))}
            </Stack>
          </Paper>

          {/* Section: Titrant Standardization Settings (Only when Type == Titrant) */}
          {formType === "Titrant" && (
            <Paper
              variant="outlined"
              sx={{
                p: 2,
                borderRadius: 1.5,
                bgcolor: "background.default",
                border: "1px solid",
                borderColor: theme.palette.info.light
              }}
            >
              <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5, color: "text.primary" }}>
                Titrant Standardization Settings (USP Volumetric Solutions)
              </Typography>
              <Typography variant="caption" sx={{ color: "text.secondary", display: "block", mb: 2 }}>
                Configure primary standard titration or secondary volumetric standardization criteria.
              </Typography>

              <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" }, gap: 2 }}>
                <TextField
                  label="Nominal Strength"
                  type="number"
                  size="small"
                  required
                  value={formNominalStrength}
                  onChange={(e) => setFormNominalStrength(e.target.value)}
                  placeholder="e.g. 0.1, 0.05"
                  slotProps={{ htmlInput: { min: "0.00001", step: "any" } }}
                />

                <FormControl size="small" required>
                  <InputLabel id="titrant-strength-unit-select-label">Strength Unit</InputLabel>
                  <Select
                    labelId="titrant-strength-unit-select-label"
                    label="Strength Unit"
                    value={formStrengthUnit}
                    onChange={(e) => setFormStrengthUnit(e.target.value as TitrantStrengthUnit)}
                  >
                    {STRENGTH_UNIT_OPTIONS.map((u) => (
                      <MenuItem key={u.value} value={u.value}>
                        {u.label}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>

                {/* Standardization Mode */}
                <FormControl component="fieldset" sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
                  <FormLabel component="legend" sx={{ fontSize: 13, fontWeight: 600 }}>
                    Standardization Mode *
                  </FormLabel>
                  <RadioGroup
                    row
                    value={formStandardizationMode}
                    onChange={(e) => setFormStandardizationMode(e.target.value as StandardizationMode)}
                  >
                    <FormControlLabel
                      value="PrimaryStandard"
                      control={<Radio size="small" />}
                      label="Primary Standard (Direct Weighing)"
                    />
                    <FormControlLabel
                      value="AgainstVolumetricSolution"
                      control={<Radio size="small" />}
                      label="Against Volumetric Solution (Secondary Titrant)"
                    />
                  </RadioGroup>
                </FormControl>

                {/* Primary Standard Mode Inputs */}
                {formStandardizationMode === "PrimaryStandard" ? (
                  <>
                    <FormControl fullWidth size="small" required>
                      <InputLabel id="primary-standard-entry-label">Primary Standard</InputLabel>
                      <Select
                        labelId="primary-standard-entry-label"
                        label="Primary Standard"
                        value={formStandardEntryId}
                        onChange={(e) => setFormStandardEntryId(e.target.value)}
                      >
                        {availableTitrantStandardEntries.map((m) => (
                          <MenuItem key={m.id} value={m.id}>
                            {m.code} - {m.name} ({m.category})
                          </MenuItem>
                        ))}
                      </Select>
                      <FormHelperText>Reference Standards or Reagents in section</FormHelperText>
                    </FormControl>

                    <TextField
                      label="Equivalence (mg/mL)"
                      type="number"
                      size="small"
                      required
                      value={formEquivalenceMgPerMl}
                      onChange={(e) => setFormEquivalenceMgPerMl(e.target.value)}
                      placeholder="e.g. 20.422"
                      slotProps={{ htmlInput: { min: "0.0001", step: "any" } }}
                      helperText="mg of standard reacting per mL of nominal titrant"
                    />
                  </>
                ) : (
                  /* Against Volumetric Solution Mode Inputs */
                  <FormControl fullWidth size="small" required sx={{ gridColumn: { xs: "1", sm: "span 2" } }}>
                    <InputLabel id="reference-titrant-select-label">Reference Titrant</InputLabel>
                    <Select
                      labelId="reference-titrant-select-label"
                      label="Reference Titrant"
                      value={formReferenceSolutionId}
                      onChange={(e) => setFormReferenceSolutionId(e.target.value)}
                    >
                      {availableReferenceTitrants.map((s) => (
                        <MenuItem key={s.id} value={s.id}>
                          {s.name} ({s.nominalStrength} {s.strengthUnit})
                        </MenuItem>
                      ))}
                    </Select>
                    <FormHelperText>Active titrant in current section (excluding self)</FormHelperText>
                  </FormControl>
                )}

                <FormControlLabel
                  control={
                    <Switch
                      checked={formBlankRequired}
                      onChange={(e) => setFormBlankRequired(e.target.checked)}
                      size="small"
                    />
                  }
                  label="Blank Titration Required"
                  sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
                />

                <TextField
                  label="Replicate Count"
                  type="number"
                  size="small"
                  required
                  value={formReplicateCount}
                  onChange={(e) => setFormReplicateCount(e.target.value)}
                  placeholder="e.g. 3 or 6"
                  slotProps={{ htmlInput: { min: "1", step: "1" } }}
                  helperText="Minimum runs required (e.g. 3)"
                />

                <TextField
                  label="Max RSD %"
                  type="number"
                  size="small"
                  required
                  value={formMaxRsdPercent}
                  onChange={(e) => setFormMaxRsdPercent(e.target.value)}
                  placeholder="e.g. 0.20"
                  slotProps={{ htmlInput: { min: "0.01", step: "0.01" } }}
                  helperText="Acceptable relative standard deviation %"
                />

                <TextField
                  label="Factor Min"
                  type="number"
                  size="small"
                  required
                  value={formFactorMin}
                  onChange={(e) => setFormFactorMin(e.target.value)}
                  placeholder="e.g. 0.97000"
                  slotProps={{ htmlInput: { min: "0.00001", step: "any" } }}
                  helperText="Lower acceptance factor limit"
                />

                <TextField
                  label="Factor Max"
                  type="number"
                  size="small"
                  required
                  value={formFactorMax}
                  onChange={(e) => setFormFactorMax(e.target.value)}
                  placeholder="e.g. 1.03000"
                  slotProps={{ htmlInput: { min: "0.00001", step: "any" } }}
                  helperText="Upper acceptance factor limit"
                />

                <TextField
                  label="Standardization Validity (Days)"
                  type="number"
                  size="small"
                  required
                  value={formValidityDays}
                  onChange={(e) => setFormValidityDays(e.target.value)}
                  placeholder="e.g. 30"
                  slotProps={{ htmlInput: { min: "0", step: "1" } }}
                  helperText="0 = restandardize before each use"
                  sx={{ gridColumn: { xs: "1", sm: "span 2" } }}
                />
              </Box>
            </Paper>
          )}
        </Stack>
      </FloatingDialog>

      {/* Audit Reason Dialog for Editing Solution */}
      <FloatingDialog
        open={saveReasonDialogOpen}
        title="Reason for Modification"
        onClose={() => {
          if (!saving) setSaveReasonDialogOpen(false);
        }}
        maxWidth="sm"
        actions={
          <>
            <Button
              onClick={() => setSaveReasonDialogOpen(false)}
              disabled={saving}
              sx={{ textTransform: "none" }}
            >
              Cancel
            </Button>
            <Button
              variant="contained"
              onClick={handleConfirmSaveWithReason}
              disabled={!saveReason.trim() || saving}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {saving ? "Saving..." : "Confirm & Save"}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          <Alert severity="info" sx={{ fontSize: 13 }}>
            GMP and ALCOA+ data integrity guidelines require a documented reason when modifying an
            existing solution master recipe.
          </Alert>

          {editingEntry && (
            <Box
              sx={{
                p: 1.5,
                bgcolor: "background.default",
                borderRadius: 1,
                border: "1px solid",
                borderColor: "divider"
              }}
            >
              <Typography variant="body2">
                Solution: <strong>{editingEntry.name}</strong>
              </Typography>
              <Typography variant="caption" color="text.secondary">
                Type: {editingEntry.type} · Section: {resolveSectionDisplay(editingEntry.sectionId, editingEntry.sectionName)}
              </Typography>
            </Box>
          )}

          <TextField
            label="Reason for Modification *"
            placeholder="Explain why this solution master recipe is being modified..."
            multiline
            rows={3}
            fullWidth
            required
            autoFocus
            size="small"
            value={saveReason}
            onChange={(e) => setSaveReason(e.target.value.slice(0, 500))}
            helperText={`${saveReason.trim().length} / 500 characters (required)`}
          />
        </Stack>
      </FloatingDialog>

      {/* Activate / Deactivate Confirmation Dialog with Reason */}
      <FloatingDialog
        open={Boolean(solutionToToggle)}
        title={solutionToToggle?.isActive ? "Deactivate Solution Master" : "Activate Solution Master"}
        onClose={() => {
          if (!togglingActive) setSolutionToToggle(null);
        }}
        maxWidth="sm"
        actions={
          <>
            <Button
              onClick={() => setSolutionToToggle(null)}
              disabled={togglingActive}
              sx={{ textTransform: "none" }}
            >
              Cancel
            </Button>
            <Button
              variant="contained"
              color={solutionToToggle?.isActive ? "error" : "success"}
              onClick={handleConfirmToggleActive}
              disabled={!toggleReason.trim() || togglingActive}
              sx={{ textTransform: "none", fontWeight: 600 }}
            >
              {togglingActive
                ? "Updating..."
                : solutionToToggle?.isActive
                ? "Deactivate Solution"
                : "Activate Solution"}
            </Button>
          </>
        }
      >
        <Stack spacing={2} sx={{ pt: 1 }}>
          {toggleError && <Alert severity="error">{toggleError}</Alert>}

          <Alert severity={solutionToToggle?.isActive ? "warning" : "info"} sx={{ fontSize: 13 }}>
            {solutionToToggle?.isActive
              ? `Are you sure you want to deactivate "${solutionToToggle?.name}"? Inactive solution masters cannot be selected for new preparations.`
              : `Are you sure you want to activate "${solutionToToggle?.name}"?`}
          </Alert>

          <TextField
            label="Reason *"
            placeholder={
              solutionToToggle?.isActive
                ? "State the operational reason for deactivating this solution master..."
                : "State the operational reason for activating this solution master..."
            }
            multiline
            rows={3}
            fullWidth
            required
            autoFocus
            size="small"
            value={toggleReason}
            onChange={(e) => setToggleReason(e.target.value.slice(0, 500))}
            helperText={`${toggleReason.trim().length} / 500 characters (required)`}
          />
        </Stack>
      </FloatingDialog>
    </Box>
  );
}
