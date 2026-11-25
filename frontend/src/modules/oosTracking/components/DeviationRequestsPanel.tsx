import { useState } from "react";
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from "@mui/material";
import { useTranslation } from "../../../i18n";
import { SectionTitle } from "../../../components/SectionTitle";
import { StatusBadge } from "../../../components/StatusBadge";
import type { OosDeviationRequestDetailDto } from "../../../services/oosInvestigationService";
import { formatDateTimeWithTimeZone } from "../../../utils/formatDate";
import { serverMessage } from "../../../utils/errorMessage";

interface DeviationRequestsPanelProps {
  requests: OosDeviationRequestDetailDto[];
  investigationVersion: number;
  canRequest: boolean;
  canAddress: boolean;
  onRequest: (reason: string, investigationVersion: number) => Promise<string | null | void>;
  onAddress: (requestId: number, comment: string, requestVersion: number) => Promise<string | null | void>;
}

export function DeviationRequestsPanel({
  requests,
  investigationVersion,
  canRequest,
  canAddress,
  onRequest,
  onAddress
}: DeviationRequestsPanelProps) {
  const { t } = useTranslation("oosTracking");
  const [reason, setReason] = useState("");
  const [requestError, setRequestError] = useState<string | null>(null);
  const [submittingRequest, setSubmittingRequest] = useState(false);
  const [comments, setComments] = useState<Record<number, string>>({});
  const [addressErrors, setAddressErrors] = useState<Record<number, string | null>>({});
  const [addressingId, setAddressingId] = useState<number | null>(null);

  const handleRequest = async () => {
    const trimmed = reason.trim();
    if (!trimmed) return;
    setRequestError(null);
    setSubmittingRequest(true);
    try {
      const err = await onRequest(trimmed, investigationVersion);
      if (typeof err === "string") {
        setRequestError(err);
        return;
      }
      setReason("");
    } catch (e: unknown) {
      setRequestError(serverMessage(e) ?? t("investigation.loadError"));
    } finally {
      setSubmittingRequest(false);
    }
  };

  const handleAddress = async (req: OosDeviationRequestDetailDto) => {
    const trimmed = (comments[req.id] ?? "").trim();
    if (!trimmed) return;
    setAddressErrors((prev) => ({ ...prev, [req.id]: null }));
    setAddressingId(req.id);
    try {
      const err = await onAddress(req.id, trimmed, req.version);
      if (typeof err === "string") {
        setAddressErrors((prev) => ({ ...prev, [req.id]: err }));
        return;
      }
      setComments((prev) => ({ ...prev, [req.id]: "" }));
    } catch (e: unknown) {
      setAddressErrors((prev) => ({
        ...prev,
        [req.id]: serverMessage(e) ?? t("investigation.loadError")
      }));
    } finally {
      setAddressingId(null);
    }
  };

  return (
    <Paper variant="outlined" sx={{ p: 2.5, borderRadius: 1.5 }}>
      <Box sx={{ mb: 1.5 }}>
        <SectionTitle>{t("deviations.title")}</SectionTitle>
      </Box>

      <Stack spacing={2}>
        {requests.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            {t("deviations.empty")}
          </Typography>
        ) : (
          requests.map((req) => (
            <Box
              key={req.id}
              sx={{
                p: 1.5,
                border: 1,
                borderColor: "divider",
                borderRadius: 1,
                bgcolor: "background.default"
              }}
            >
              <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 1, mb: 0.5 }}>
                <Typography variant="caption" color="text.secondary">
                  {t("deviations.requestedBy", {
                    name: req.requestedByName,
                    date: formatDateTimeWithTimeZone(req.requestedAtUtc)
                  })}
                </Typography>
                <StatusBadge status={req.status} />
              </Box>
              <Typography variant="body2" sx={{ mb: 1 }}>
                {req.reason}
              </Typography>

              {req.status === "Addressed" && req.addressedComment && (
                <Box sx={{ pt: 1, borderTop: 1, borderColor: "divider" }}>
                  <Typography variant="body2">{req.addressedComment}</Typography>
                  {req.addressedByName && req.addressedAtUtc && (
                    <Typography variant="caption" color="text.secondary">
                      {t("deviations.addressedBy", {
                        name: req.addressedByName,
                        date: formatDateTimeWithTimeZone(req.addressedAtUtc)
                      })}
                    </Typography>
                  )}
                </Box>
              )}

              {canAddress && req.status === "Open" && (
                <Stack spacing={1} sx={{ mt: 1 }}>
                  <Stack direction={{ xs: "column", sm: "row" }} spacing={1}>
                    <TextField
                      size="small"
                      fullWidth
                      label={t("deviations.addressCommentLabel")}
                      value={comments[req.id] ?? ""}
                      onChange={(e) => {
                        setComments((prev) => ({ ...prev, [req.id]: e.target.value }));
                        setAddressErrors((prev) =>
                          prev[req.id] ? { ...prev, [req.id]: null } : prev
                        );
                      }}
                    />
                    <Button
                      variant="outlined"
                      size="small"
                      disabled={!(comments[req.id] ?? "").trim() || addressingId === req.id}
                      onClick={() => void handleAddress(req)}
                      sx={{ whiteSpace: "nowrap" }}
                    >
                      {t("deviations.addressButton")}
                    </Button>
                  </Stack>
                  {addressErrors[req.id] && (
                    <Alert severity="error">{addressErrors[req.id]}</Alert>
                  )}
                </Stack>
              )}
            </Box>
          ))
        )}

        {canRequest && (
          <Stack spacing={1}>
            <TextField
              size="small"
              multiline
              rows={2}
              label={t("deviations.reasonLabel")}
              value={reason}
              onChange={(e) => {
                setReason(e.target.value);
                if (requestError) setRequestError(null);
              }}
            />
            {requestError && <Alert severity="error">{requestError}</Alert>}
            <Box>
              <Button
                variant="contained"
                size="small"
                disabled={!reason.trim() || submittingRequest}
                onClick={() => void handleRequest()}
              >
                {t("deviations.requestButton")}
              </Button>
            </Box>
          </Stack>
        )}
      </Stack>
    </Paper>
  );
}
