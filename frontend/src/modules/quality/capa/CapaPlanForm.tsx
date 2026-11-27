import { useState } from "react";
import { TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type QualityRecordDetailDto } from "../../../services/qualityService";
import { useUserOptions } from "../steps/lookups";
import { ActionRowsEditor, emptyActionRow, isCompleteRow, type ActionRow } from "../steps/ActionRowsEditor";
import { GatedButton, StepForm, TEXT_MAX, useStepRunner } from "../steps/stepKit";

interface CapaPlanFormProps {
  record: QualityRecordDetailDto;
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

// The owner writes the plan: actions (each corrective or preventive, with a responsible person and a due date) and
// the effectiveness check (what, how, and when). The plan can be saved again until QA approves it.
export function CapaPlanForm({ record, currentUserId, onChanged }: CapaPlanFormProps) {
  const { t } = useTranslation("quality");
  const capa = record.capa;
  const users = useUserOptions();
  const saved = record.actions.filter((action) => action.kind === "Corrective" || action.kind === "Preventive");
  const [rows, setRows] = useState<ActionRow[]>(
    saved.length > 0
      ? saved.map((action) => ({
          kind: action.kind,
          description: action.description,
          responsibleUserId: action.responsibleUserId,
          dueDate: action.dueDate
        }))
      : [emptyActionRow("Corrective")]
  );
  const [criteria, setCriteria] = useState(capa?.effectivenessCriteria ?? "");
  const [method, setMethod] = useState(capa?.effectivenessMethod ?? "");
  const [plannedDate, setPlannedDate] = useState(capa?.effectivenessPlannedDate ?? "");
  const runner = useStepRunner(onChanged);

  const isOwner = currentUserId !== null && record.ownerUserId === currentUserId;
  const complete = rows.length > 0 && rows.every(isCompleteRow) && Boolean(criteria.trim()) && Boolean(method.trim()) && Boolean(plannedDate);

  const save = () =>
    runner.run(
      () =>
        qualityService.saveCapaPlan(
          record.id,
          {
            actions: rows.map((row) => ({
              kind: row.kind,
              description: row.description.trim(),
              responsibleUserId: Number(row.responsibleUserId),
              dueDate: row.dueDate
            })),
            effectivenessCriteria: criteria.trim(),
            effectivenessMethod: method.trim(),
            effectivenessPlannedDate: plannedDate
          },
          record.version
        ),
      t("steps.capa.plan.saveFailed")
    );

  return (
    <PanelFrame title={t("steps.capa.plan.title")}>
      <StepForm
        error={runner.error}
        actions={
          isOwner ? (
            <GatedButton
              label={t("steps.capa.plan.save")}
              lockedReason={complete ? null : t("steps.capa.plan.needsFields")}
              busy={runner.busy}
              onClick={() => void save()}
            />
          ) : null
        }
      >
        {!isOwner && (
          <Typography variant="body2" color="text.secondary">
            {t("steps.capa.plan.withOwner")}
          </Typography>
        )}
        <ActionRowsEditor rows={rows} onChange={setRows} lockedCount={saved.length} noun="action" users={users} disabled={!isOwner} />
        <TextField
          required
          multiline
          minRows={2}
          label={t("steps.capa.plan.criteria")}
          helperText={t("steps.capa.plan.criteriaHelp")}
          value={criteria}
          disabled={!isOwner}
          onChange={(e) => setCriteria(e.target.value)}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
        <TextField
          required
          multiline
          minRows={2}
          label={t("steps.capa.plan.method")}
          value={method}
          disabled={!isOwner}
          onChange={(e) => setMethod(e.target.value)}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
        <TextField
          required
          type="date"
          label={t("steps.capa.plan.plannedDate")}
          helperText={t("steps.capa.plan.plannedDateHelp")}
          value={plannedDate}
          disabled={!isOwner}
          onChange={(e) => setPlannedDate(e.target.value)}
          slotProps={{ inputLabel: { shrink: true } }}
        />
      </StepForm>
    </PanelFrame>
  );
}
