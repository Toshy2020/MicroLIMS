import { useCallback, useEffect, useState } from "react";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Stack,
  Typography
} from "@mui/material";
import { toast } from "sonner";
import { SectionTitle } from "../../components/SectionTitle";
import { StatusBadge } from "../../components/StatusBadge";
import { SignatureDialog } from "../../components/SignatureDialog";
import { useTranslation } from "../../i18n";
import {
  competencyService,
  type CompetencySettingDto,
  type GateImpactDto
} from "../../services/competencyService";
import { getErrorMessage } from "../../utils/errorMessage";
import { formatDateTimeWithTimeZone } from "../../utils/formatDate";

export interface GateSettingsCardProps {
  sectionId: number;
  onChanged?: (setting: CompetencySettingDto) => void;
  onLoadError?: (failed: boolean) => void;
}

export function GateSettingsCard({ sectionId, onChanged, onLoadError }: GateSettingsCardProps) {
  const { t } = useTranslation("competency");
  const [setting, setSetting] = useState<CompetencySettingDto | null>(null);
  const [impact, setImpact] = useState<GateImpactDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [impactLoading, setImpactLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [impactError, setImpactError] = useState<string | null>(null);
  const [signingOpen, setSigningOpen] = useState(false);
  const [reason, setReason] = useState("");

  const loadData = useCallback(
    async (signal?: AbortSignal) => {
      setLoading(true);
      setError(null);
      setImpactError(null);
      try {
        const loaded = await competencyService.getSettings(sectionId, signal);
        if (signal?.aborted) return;
        setSetting(loaded);
        onLoadError?.(false);

        if (!loaded.gateEnabled) {
          setImpactLoading(true);
          try {
            const impactRes = await competencyService.getImpact(sectionId, signal);
            if (!signal?.aborted) setImpact(impactRes);
          } catch (err: unknown) {
            if (!signal?.aborted) {
              setImpact(null);
              setImpactError(getErrorMessage(err, t("gate.impactError")));
            }
          } finally {
            if (!signal?.aborted) setImpactLoading(false);
          }
        } else {
          setImpact(null);
        }
      } catch (err: unknown) {
        if (signal?.aborted) return;
        setError(getErrorMessage(err, t("gate.loadError")));
        onLoadError?.(true);
      } finally {
        if (!signal?.aborted) setLoading(false);
      }
    },
    [sectionId, onLoadError, t]
  );

  useEffect(() => {
    const controller = new AbortController();
    void loadData(controller.signal);
    return () => controller.abort();
  }, [loadData]);

  const handleConfirmGate = async (password: string) => {
    if (!setting) return;
    const nextEnabled = !setting.gateEnabled;
    const updated = await competencyService.setGate(sectionId, {
      enabled: nextEnabled,
      reason: reason.trim(),
      password,
      expectedVersion: setting.version
    });
    setSetting(updated);
    setReason("");
    setSigningOpen(false);
    toast.success(nextEnabled ? t("gate.enabledToast") : t("gate.disabledToast"));
    onChanged?.(updated);
    if (!updated.gateEnabled) {
      void loadData();
    } else {
      setImpact(null);
    }
  };

  if (loading) {
    return (
      <Card variant="outlined">
        <CardContent sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
          <CircularProgress size={20} />
          <Typography variant="body2" color="text.secondary">
            {t("gate.impactLoading")}
          </Typography>
        </CardContent>
      </Card>
    );
  }

  if (error || !setting) {
    return error ? <Alert severity="error">{error}</Alert> : null;
  }

  const nextEnabled = !setting.gateEnabled;

  return (
    <Card variant="outlined">
      <CardContent>
        <Stack spacing={1.5}>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
              <SectionTitle>{t("gate.title")}</SectionTitle>
              <StatusBadge
                status={setting.gateEnabled ? "Active" : "Inactive"}
                label={setting.gateEnabled ? t("gate.enabled") : t("gate.disabled")}
              />
            </Box>
            <Button
              variant={setting.gateEnabled ? "outlined" : "contained"}
              color={setting.gateEnabled ? "warning" : "primary"}
              onClick={() => setSigningOpen(true)}
            >
              {setting.gateEnabled ? t("gate.disableButton") : t("gate.enableButton")}
            </Button>
          </Box>

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2} sx={{ color: "text.secondary", fontSize: 13 }}>
            <Typography variant="caption" color="text.secondary">
              {setting.reminderDaysBeforeExpiry != null
                ? t("gate.reminderDays", { days: setting.reminderDaysBeforeExpiry })
                : t("gate.noReminder")}
            </Typography>
            {setting.changedByName && setting.changedAt && (
              <Typography variant="caption" color="text.secondary" sx={{ fontVariantNumeric: "tabular-nums" }}>
                {t("gate.lastChanged", {
                  name: setting.changedByName,
                  date: formatDateTimeWithTimeZone(setting.changedAt)
                })}
              </Typography>
            )}
          </Stack>

          {!setting.gateEnabled && (
            <Box aria-live="polite">
              {impactLoading ? (
                <Typography variant="caption" color="text.secondary">
                  {t("gate.impactLoading")}
                </Typography>
              ) : impactError ? (
                <Alert severity="warning">{impactError}</Alert>
              ) : impact ? (
                <Alert severity={impact.totalRefusedOrders > 0 ? "warning" : "info"}>
                  <Typography variant="subtitle2" sx={{ fontWeight: 700, mb: 0.5 }}>
                    {t("gate.impactTitle")}
                  </Typography>
                  {impact.totalRefusedOrders === 0 ? (
                    <Typography variant="body2">{t("gate.impactNone")}</Typography>
                  ) : (
                    <Stack spacing={1}>
                      <Typography variant="body2">
                        {t("gate.impactSummary", { count: impact.totalRefusedOrders })}
                      </Typography>
                      {impact.analysts.map((analyst) => (
                        <Box key={analyst.analystUserId}>
                          <Typography variant="body2" sx={{ fontWeight: 600 }}>
                            {analyst.analystName}
                          </Typography>
                          {analyst.orders.map((order) => (
                            <Typography key={order.testOrderId} variant="caption" sx={{ display: "block", pl: 1.5 }}>
                              {t("gate.impactOrder", {
                                sample: order.sampleReference,
                                test: order.testCode
                              })}
                              {": "}
                              {order.reasons.map((r) => r.text).join(" ")}
                            </Typography>
                          ))}
                        </Box>
                      ))}
                    </Stack>
                  )}
                </Alert>
              ) : null}
            </Box>
          )}
        </Stack>
      </CardContent>

      <SignatureDialog
        open={signingOpen}
        showComment
        commentRequired
        commentLabel={t("gate.reasonLabel")}
        comment={reason}
        onCommentChange={setReason}
        meaningStatement={nextEnabled ? t("gate.enableMeaning", { lng: "en" }) : t("gate.disableMeaning", { lng: "en" })}
        meaningStatementTranslation={nextEnabled ? t("gate.enableMeaningAr") : t("gate.disableMeaningAr")}
        onCancel={() => {
          setReason("");
          setSigningOpen(false);
        }}
        onConfirm={handleConfirmGate}
      />
    </Card>
  );
}
