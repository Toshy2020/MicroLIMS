import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  Paper, TextField, Button, Stack, Typography, Alert, Box, Stepper, Step, StepLabel,
  RadioGroup, FormControlLabel, Radio, Select, MenuItem, Divider, Chip
} from "@mui/material";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { PageHeader } from "../../components/PageHeader";
import { SignatureDialog } from "../../components/SignatureDialog";
import { RoleService, RoleRecord, PermissionRecord, SodRules } from "./services/RoleService";
import { PermissionMatrix } from "./components/PermissionMatrix";
import { useLoadFailures } from "../../hooks/useLoadFailures";
import { Role as RoleType } from "../../contexts/AuthContext";
import { LoadFailuresAlert } from "../../components/LoadErrorAlert";
import { formatRoleName } from "../../utils/userDisplay";
import { useBreadcrumb } from "../../components/breadcrumbContext";
import { Trans } from "react-i18next";
import { Ltr } from "../../components/Ltr";
import { i18n, useTranslation } from "../../i18n";

const BASE_TYPES: RoleType[] = ["SystemAdministrator", "SectionHead", "Reviewer", "Analyst", "QcManager", "Qa"];
const STEP_KEYS = ["name", "baseType", "permissions", "review"] as const;

export function CreateRolePage() {
  const { t } = useTranslation("roles");
  const navigate = useNavigate();
  const [step, setStep] = useState(0);
  useBreadcrumb(t("create.breadcrumb"));

  const [existingRoles, setExistingRoles] = useState<RoleRecord[]>([]);
  const [allPermissions, setAllPermissions] = useState<PermissionRecord[]>([]);

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [baseType, setBaseType] = useState<RoleType>("Analyst");
  const [checkedCodes, setCheckedCodes] = useState<Set<string>>(new Set());
  const [cloneFromRoleId, setCloneFromRoleId] = useState<number | "">("");

  const [sodRules, setSodRules] = useState<SodRules | null>(null);
  const [qaForbiddenCodes, setQaForbiddenCodes] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [reason, setReason] = useState("");
  const [signing, setSigning] = useState(false);
  // Kept so a refused password can be retried without creating the role twice.
  const [createdId, setCreatedId] = useState<number | null>(null);

  const { failed: failedLists, fail } = useLoadFailures();

  useEffect(() => {
    const controller = new AbortController();
    RoleService.getAll().then(setExistingRoles).catch(fail("the existing roles"));
    RoleService.getAllPermissions().then(setAllPermissions).catch(fail("the permission list"));
    RoleService.getSodRules().then(setSodRules).catch(fail("the segregation-of-duties rules"));
    RoleService.getQaForbiddenCodes(controller.signal)
      .then((codes) => { if (!controller.signal.aborted) setQaForbiddenCodes(codes); })
      .catch(() => { if (!controller.signal.aborted) fail("the QA-restricted permissions")(); });
    return () => controller.abort();
  }, [fail]);

  const qaForbiddenSet = useMemo(
    () => (baseType === "Qa" ? new Set(qaForbiddenCodes) : undefined),
    [baseType, qaForbiddenCodes]
  );

  // Strip any forbidden permission if the QA forbidden list finishes loading after Qa was chosen.
  useEffect(() => {
    if (!qaForbiddenSet || qaForbiddenSet.size === 0) return;
    setCheckedCodes((prev) => {
      const hasForbidden = Array.from(prev).some((c) => qaForbiddenSet.has(c));
      if (!hasForbidden) return prev;
      return new Set(Array.from(prev).filter((c) => !qaForbiddenSet.has(c)));
    });
  }, [qaForbiddenSet]);

  const withoutQaForbidden = (codes: string[], type: RoleType): string[] => {
    if (type !== "Qa" || qaForbiddenCodes.length === 0) return codes;
    const forbidden = new Set(qaForbiddenCodes);
    return codes.filter((c) => !forbidden.has(c));
  };

  // Default-check the selected base type's current grants whenever it
  // changes, fetched live rather than hardcoded so this never drifts
  // from what the base role actually holds.
  const applyBaseTypeDefaults = async (type: RoleType) => {
    const baseRole = existingRoles.find((r) => r.isSystemRole && r.type === type);
    if (!baseRole) return;
    try {
      const detail = await RoleService.getById(baseRole.id);
      setCheckedCodes(new Set(withoutQaForbidden(detail.permissionCodes, type)));
    } catch {
      // Leave whatever was selected before - not worth blocking the wizard over.
    }
  };

  const handleBaseTypeChange = (type: RoleType) => {
    setBaseType(type);
    setCloneFromRoleId("");
    if (type === "Qa" && qaForbiddenCodes.length > 0) {
      const forbidden = new Set(qaForbiddenCodes);
      setCheckedCodes((prev) => new Set(Array.from(prev).filter((c) => !forbidden.has(c))));
    }
    applyBaseTypeDefaults(type);
  };

  const handleCloneFrom = async (roleId: number) => {
    setCloneFromRoleId(roleId);
    try {
      const detail = await RoleService.getById(roleId);
      setCheckedCodes(new Set(withoutQaForbidden(detail.permissionCodes, baseType)));
    } catch {
      // Leave the matrix as-is on failure.
    }
  };

  const handleToggle = (code: string, checked: boolean) => {
    if (qaForbiddenSet?.has(code)) return;
    setCheckedCodes((prev) => {
      const next = new Set(prev);
      if (checked) next.add(code); else next.delete(code);
      return next;
    });
  };

  const canAdvanceFromStep = (s: number) => {
    if (s === 0) return name.trim().length > 0;
    return true;
  };

  // The permission grant is electronically signed, so creating opens the signature step.
  const handleSubmit = () => {
    if (!reason.trim()) {
      setError(t("create.reasonRequired"));
      return;
    }
    setError(null);
    setSigning(true);
  };

  // Errors are thrown back to the signature dialog, which shows them and stays open.
  const confirmSignature = async (password: string) => {
    setSubmitting(true);
    try {
      let id = createdId;
      if (id === null) {
        const created = await RoleService.create(name, description || null, baseType, reason.trim());
        id = created.id;
        setCreatedId(id);
      }
      const codesToSave = withoutQaForbidden(Array.from(checkedCodes), baseType);
      await RoleService.updatePermissions(id, codesToSave, reason.trim(), password);
      setSigning(false);
      navigate(`/roles/${id}`);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Button startIcon={<ArrowBackIcon />} onClick={() => navigate("/roles")} sx={{ mb: 1 }}>
        {t("common.backToRoles")}
      </Button>
      <PageHeader title={t("create.title")} subtitle={t("create.subtitle")} />

      <Stepper activeStep={step} sx={{ mb: 3 }}>
        {STEP_KEYS.map((key) => <Step key={key}><StepLabel>{t(`create.steps.${key}`)}</StepLabel></Step>)}
      </Stepper>

      <LoadFailuresAlert failed={failedLists} retryHint={t("common.retryHint")} sx={{ mb: 2 }} />
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

      {step === 0 && (
        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2} sx={{ maxWidth: 480 }}>
            <TextField label={t("create.roleName")} size="small" value={name} onChange={(e) => setName(e.target.value)} autoFocus required fullWidth />
            <TextField label={t("create.description")} size="small" value={description} onChange={(e) => setDescription(e.target.value)} multiline rows={3} fullWidth />
          </Stack>
        </Paper>
      )}

      {step === 1 && (
        <Paper sx={{ p: 2.5 }}>
          <Alert severity="info" sx={{ mb: 2.5 }}>
            <Trans i18nKey="create.baseTypeInfo" ns="roles" values={{ role: formatRoleName(baseType, undefined, t) }} components={{ role: <strong /> }} />
          </Alert>
          <RadioGroup value={baseType} onChange={(e) => handleBaseTypeChange(e.target.value as RoleType)}>
            {BASE_TYPES.map((type) => (
              <FormControlLabel key={type} value={type} control={<Radio />} label={formatRoleName(type, undefined, t)} />
            ))}
          </RadioGroup>
        </Paper>
      )}

      {step === 2 && (
        <>
          <Paper sx={{ p: 2.5, mb: 2 }}>
            <Typography sx={{ fontSize: 13, fontWeight: 600, mb: 1 }}>
              {t("create.startingPoint", { role: formatRoleName(baseType, undefined, t) })}
            </Typography>
            <Stack direction="row" spacing={1.5} sx={{
              alignItems: "center"
            }}>
              <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{t("create.cloneIntro")}</Typography>
              <Select
                size="small"
                displayEmpty
                value={cloneFromRoleId}
                onChange={(e) => handleCloneFrom(Number(e.target.value))}
                sx={{ minWidth: 220 }}
                inputProps={{ "aria-label": t("create.cloneAria") }}
              >
                <MenuItem value=""><em>{t("create.cloneSelect")}</em></MenuItem>
                {existingRoles.map((r) => <MenuItem key={r.id} value={r.id}>{r.name}</MenuItem>)}
              </Select>
            </Stack>
          </Paper>
          <PermissionMatrix
            permissions={allPermissions}
            checkedCodes={checkedCodes}
            onToggle={handleToggle}
            sodRules={sodRules}
            qaForbiddenCodes={qaForbiddenSet}
          />
        </>
      )}

      {step === 3 && (
        <Paper sx={{ p: 2.5 }}>
          <Stack spacing={2}>
            <Box>
              <Typography sx={{ fontSize: 12, color: "text.secondary", textTransform: "uppercase" }}>{t("create.reviewName")}</Typography>
              <Typography sx={{ fontSize: 15, fontWeight: 700 }}>{name}</Typography>
            </Box>
            {description && (
              <Box>
                <Typography sx={{ fontSize: 12, color: "text.secondary", textTransform: "uppercase" }}>{t("create.reviewDescription")}</Typography>
                <Typography sx={{ fontSize: 14 }}>{description}</Typography>
              </Box>
            )}
            <Box>
              <Typography sx={{ fontSize: 12, color: "text.secondary", textTransform: "uppercase" }}>{t("create.reviewBaseType")}</Typography>
              <Typography sx={{ fontSize: 14 }}>{formatRoleName(baseType, undefined, t)}</Typography>
            </Box>
            <Divider />
            <Box>
              <Typography sx={{ fontSize: 12, color: "text.secondary", textTransform: "uppercase", mb: 1 }}>
                {t("create.reviewPermissions", { count: checkedCodes.size })}
              </Typography>
              <Stack direction="row" spacing={1} useFlexGap sx={{
                flexWrap: "wrap"
              }}>
                {Array.from(checkedCodes).sort().map((c) => <Chip key={c} label={<Ltr>{c}</Ltr>} size="small" />)}
                {checkedCodes.size === 0 && <Typography sx={{ fontSize: 13, color: "text.secondary" }}>{t("create.noneSelected")}</Typography>}
              </Stack>
            </Box>
            <Divider />
            <TextField label={t("create.reasonLabel")} size="small" value={reason} onChange={(e) => setReason(e.target.value)} required fullWidth multiline rows={2} placeholder={t("common.reasonPlaceholder")} />
          </Stack>
        </Paper>
      )}

      <Stack direction="row" spacing={1.5} sx={{ mt: 3 }}>
        <Button disabled={step === 0} onClick={() => setStep((s) => s - 1)}>{t("create.back")}</Button>
        {step < STEP_KEYS.length - 1 ? (
          <Button variant="contained" disabled={!canAdvanceFromStep(step)} onClick={() => setStep((s) => s + 1)}>
            {t("create.next")}
          </Button>
        ) : (
          <Button variant="contained" color="primary" disabled={submitting || !reason.trim()} onClick={handleSubmit}>
            {submitting ? t("create.creating") : t("create.submit")}
          </Button>
        )}
      </Stack>

      <SignatureDialog
        open={signing}
        meaningStatement={i18n.getFixedT("en", "roles")("create.signatureMeaning", { name, count: checkedCodes.size, reason: reason.trim() })}
        meaningStatementTranslation={t("create.signatureMeaning", { name, count: checkedCodes.size, reason: reason.trim() })}
        onCancel={() => setSigning(false)}
        onConfirm={confirmSignature}
      />
    </>
  );
}
