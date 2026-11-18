import { useState } from "react";
import { Alert, Box, Button, MenuItem, Stack, TextField, Typography } from "@mui/material";
import AutoFixHighOutlinedIcon from "@mui/icons-material/AutoFixHighOutlined";
import { IcpWorkspaceService } from "../services/IcpWorkspaceService";
import { Ltr } from "../../../components/Ltr";
import { useTranslation } from "../../../i18n";
import type { IcpReportValuesDto } from "../types";

export interface IcpReportAutofillProps {
  runSampleId: number;
  hasReport: boolean;
  disabled?: boolean;
  // Receives the proposed readings; the parent only puts them into the draft, nothing is saved.
  onUse: (values: IcpReportValuesDto["elements"]) => void;
}

// Reads the uploaded sample report on the server and proposes the ppm per element. The analyst picks the
// solution when the report does not identify it, reviews the values, and still saves the entry themselves.
export function IcpReportAutofill({ runSampleId, hasReport, disabled = false, onUse }: IcpReportAutofillProps) {
  const { t } = useTranslation("icpWorkspace");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [values, setValues] = useState<IcpReportValuesDto | null>(null);

  const read = async (solutionIndex?: number | null) => {
    setBusy(true);
    setError(null);
    try {
      setValues(await IcpWorkspaceService.getReportValues(runSampleId, solutionIndex));
    } catch (err: unknown) {
      const e = err as { response?: { data?: { message?: string } }; message?: string };
      setValues(null);
      setError(e.response?.data?.message ?? e.message ?? t("entry.autofill.readError"));
    } finally {
      setBusy(false);
    }
  };

  const usable = values?.elements.filter((e) => e.resultPpm != null && e.resultPpm >= 0) ?? [];

  return (
    <Box sx={{ mb: 2 }}>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: "center", flexWrap: "wrap" }}>
        <Button
          variant="outlined"
          startIcon={<AutoFixHighOutlinedIcon />}
          onClick={() => read(values?.selectedIndex)}
          disabled={disabled || busy || !hasReport}
          sx={{ textTransform: "none", fontWeight: 600 }}
        >
          {busy ? t("entry.autofill.reading") : t("entry.autofill.readButton")}
        </Button>
        {!hasReport && (
          <Typography variant="caption" sx={{ color: "text.secondary" }}>
            {t("entry.autofill.uploadFirst")}
          </Typography>
        )}
      </Stack>

      {error && <Alert severity="error" sx={{ mt: 1.5 }}>{error}</Alert>}

      {values && (
        <Box sx={{ mt: 1.5 }}>
          {values.requiresVerification && (
            <Alert severity="warning" sx={{ mb: 1 }}>
              {t("entry.autofill.uncertaintyWarning", { source: values.source })}
            </Alert>
          )}
          {values.problems.map((p) => (
            <Alert key={p} severity="warning" sx={{ mb: 1 }}>{p}</Alert>
          ))}

          <TextField
            select
            size="small"
            label={t("entry.autofill.solutionLabel")}
            value={values.selectedIndex ?? ""}
            onChange={(e) => read(Number(e.target.value))}
            disabled={busy}
            sx={{ minWidth: 320, mb: 1.5 }}
            slotProps={{ htmlInput: { "aria-label": t("entry.autofill.solutionLabel") } }}
          >
            {values.solutions.map((s) => (
              <MenuItem key={s.index} value={s.index}>
                {s.label}{t("entry.autofill.solutionPage", { page: s.page })}
                {s.elements ? ` (${s.elements})` : ""}
                {s.matchesSample ? t("entry.autofill.matchesSample") : ""}
              </MenuItem>
            ))}
          </TextField>

          {values.selectedIndex != null && (
            <>
              <Stack spacing={0.25} sx={{ mb: 1.5 }}>
                {values.elements.map((e) => (
                  <Typography key={e.icpTestElementId} variant="body2" sx={{ fontVariantNumeric: "tabular-nums" }}>
                    <strong><Ltr>{e.symbol}</Ltr></strong>: <Ltr>{e.resultPpm != null ? `${e.resultPpm} ppm` : "—"}</Ltr>
                    {e.wavelengthNm != null ? <Ltr>{` (${e.wavelengthNm} nm)`}</Ltr> : ""}
                    {e.note ? ` — ${e.note}` : ""}
                  </Typography>
                ))}
              </Stack>
              <Button
                variant="contained"
                onClick={() => onUse(values.elements)}
                disabled={disabled || usable.length === 0}
                sx={{ textTransform: "none", fontWeight: 600 }}
              >
                {t("entry.autofill.useValues")}
              </Button>
              <Typography variant="caption" sx={{ ml: 1.5, color: "text.secondary" }}>
                {t("entry.autofill.useHint")}
              </Typography>
            </>
          )}
        </Box>
      )}
    </Box>
  );
}
