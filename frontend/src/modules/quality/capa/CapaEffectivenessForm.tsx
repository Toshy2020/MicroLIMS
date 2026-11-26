import { useState } from "react";
import { Button, FormControl, FormControlLabel, FormLabel, Radio, RadioGroup, TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { PanelFrame } from "../components/PanelFrame";
import { qualityService, type CapaEffectiveness, type QualityRecordDetailDto } from "../../../services/qualityService";
import { GatedButton, SignStepDialog, StepForm, TEXT_MAX, isRaiserOrOwner, todayIsoDate } from "../steps/stepKit";
import { CreateCapaDialog } from "./CreateCapaDialog";

interface CapaEffectivenessFormProps {
  record: QualityRecordDetailDto;
  currentUserId: number | null;
  onChanged: () => Promise<void>;
}

// QA records whether the CAPA worked, on or after the planned date. "Not effective" needs a new CAPA created from
// this one: it is created here first and then named in the signed request.
export function CapaEffectivenessForm({ record, currentUserId, onChanged }: CapaEffectivenessFormProps) {
  const { t } = useTranslation("quality");
  const [result, setResult] = useState<CapaEffectiveness | "">("");
  const [findings, setFindings] = useState("");
  const [successor, setSuccessor] = useState<{ id: number; number: string } | null>(null);
  const [creating, setCreating] = useState(false);
  const [signing, setSigning] = useState(false);

  const plannedDate = record.capa?.effectivenessPlannedDate ?? null;
  const tooEarly = plannedDate === null || plannedDate > todayIsoDate();
  const earlyReason = tooEarly ? t("steps.capa.effectiveness.early", { date: plannedDate ?? t("steps.capa.effectiveness.noDate") }) : null;
  const needsSuccessor = result === "NotEffective" && successor === null;

  const signLock =
    earlyReason ??
    (isRaiserOrOwner(record, currentUserId)
      ? t("steps.common.raiserOrOwner")
      : result === ""
        ? t("steps.capa.effectiveness.chooseResult")
        : !findings.trim()
          ? t("steps.capa.effectiveness.needsFindings")
          : needsSuccessor
            ? t("steps.capa.effectiveness.needsSuccessor")
            : null);

  return (
    <PanelFrame title={t("steps.capa.effectiveness.title")}>
      <StepForm
        error={null}
        actions={
          <>
            {result === "NotEffective" && (
              <Button variant="outlined" disabled={tooEarly} onClick={() => setCreating(true)}>
                {t("steps.capa.effectiveness.createSuccessor")}
              </Button>
            )}
            <GatedButton label={t("steps.capa.effectiveness.sign")} lockedReason={signLock} onClick={() => setSigning(true)} />
          </>
        }
      >
        <Typography variant="body2">
          {t("steps.capa.effectiveness.criteria", { criteria: record.capa?.effectivenessCriteria ?? "" })}
        </Typography>
        <Typography variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
          {t("steps.capa.effectiveness.planned", { date: plannedDate ?? t("steps.capa.effectiveness.noDate") })}
        </Typography>
        {earlyReason && (
          <Typography variant="body2" color="text.secondary" role="note">
            {earlyReason}
          </Typography>
        )}
        <FormControl component="fieldset" disabled={tooEarly}>
          <FormLabel component="legend">{t("steps.capa.effectiveness.result")}</FormLabel>
          <RadioGroup row value={result} onChange={(e) => setResult(e.target.value === "NotEffective" ? "NotEffective" : "Effective")}>
            <FormControlLabel value="Effective" control={<Radio />} label={t("steps.capa.effectiveness.effective")} />
            <FormControlLabel value="NotEffective" control={<Radio />} label={t("steps.capa.effectiveness.notEffective")} />
          </RadioGroup>
        </FormControl>
        <TextField
          required
          multiline
          minRows={3}
          label={t("steps.capa.effectiveness.findings")}
          value={findings}
          disabled={tooEarly}
          onChange={(e) => setFindings(e.target.value)}
          slotProps={{ htmlInput: { maxLength: TEXT_MAX } }}
        />
        {result === "NotEffective" && successor && (
          <Typography variant="body2">{t("steps.capa.effectiveness.successor", { number: successor.number })}</Typography>
        )}
      </StepForm>
      <CreateCapaDialog
        open={creating}
        source={{ id: record.id, number: record.number, sectionId: record.sectionId }}
        onClose={() => setCreating(false)}
        onCreated={(created) => {
          setSuccessor({ id: created.id, number: created.record.number });
          setCreating(false);
        }}
      />
      <SignStepDialog
        open={signing}
        meaning="CapaEffectivenessChecked"
        onCancel={() => setSigning(false)}
        onSign={async (password) => {
          if (result === "") return;
          await qualityService.signCapaEffectiveness(
            record.id,
            { result, findings: findings.trim(), newCapaId: result === "NotEffective" ? (successor?.id ?? null) : null, password },
            record.version
          );
          setSigning(false);
          await onChanged();
        }}
      />
    </PanelFrame>
  );
}
