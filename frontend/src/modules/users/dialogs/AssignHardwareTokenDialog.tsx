import { useEffect, useState } from "react";
import {
  FormControl,
  FormControlLabel,
  FormLabel,
  IconButton,
  InputAdornment,
  MenuItem,
  Radio,
  RadioGroup,
  TextField,
  Typography
} from "@mui/material";
import Visibility from "@mui/icons-material/Visibility";
import VisibilityOff from "@mui/icons-material/VisibilityOff";
import type { TFunction } from "i18next";
import { Trans } from "react-i18next";
import { toast } from "sonner";
import { Ltr } from "../../../components/Ltr";
import { FormDialog } from "../../../components/lab/FormDialog";
import { useTranslation } from "../../../i18n";
import { getErrorMessage, serverMessage } from "../../../utils/errorMessage";
import { UserRecord, UserService } from "../services/UserService";

export interface AssignHardwareTokenDialogProps {
  open: boolean;
  user: UserRecord | null;
  onClose: () => void;
  onAssigned: () => void;
}

type SeedFormat = "base32" | "hex";
type StepSeconds = 30 | 60;

interface FieldErrors {
  serialNumber?: string;
  seed?: string;
  stepSeconds?: string;
  reason?: string;
}

function validateSeed(rawSeed: string, format: SeedFormat, t: TFunction<"users">): string | null {
  const trimmed = rawSeed.trim();
  if (!trimmed) return t("hardwareToken.validation.seedRequired");
  if (format === "hex") {
    const compact = trimmed.replace(/\s+/g, "");
    if (!/^[0-9a-fA-F]+$/.test(compact) || compact.length % 2 !== 0) {
      return t("hardwareToken.validation.hexEven");
    }
    return null;
  }
  const compact = trimmed.replace(/[\s-]+/g, "").replace(/=+$/, "");
  if (!compact || !/^[A-Z2-7]+$/i.test(compact)) {
    return t("hardwareToken.validation.base32Chars");
  }
  return null;
}

/** Administrator dialog to assign a TOTP hardware token to another user without retaining the seed after submit. */
export function AssignHardwareTokenDialog({ open, user, onClose, onAssigned }: AssignHardwareTokenDialogProps) {
  const { t } = useTranslation("users");
  const [serialNumber, setSerialNumber] = useState("");
  const [seed, setSeed] = useState("");
  const [showSeed, setShowSeed] = useState(false);
  const [seedFormat, setSeedFormat] = useState<SeedFormat>("base32");
  const [stepSeconds, setStepSeconds] = useState<StepSeconds>(30);
  const [reason, setReason] = useState("");
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!open) {
      setSerialNumber("");
      setSeed("");
      setShowSeed(false);
      setSeedFormat("base32");
      setStepSeconds(30);
      setReason("");
      setFieldErrors({});
      setSubmitting(false);
    }
  }, [open, user?.id]);

  const handleSubmit = async () => {
    if (!user || submitting) return;

    const trimmedSerial = serialNumber.trim();
    const trimmedReason = reason.trim();
    const errors: FieldErrors = {};

    if (!trimmedSerial) {
      errors.serialNumber = t("hardwareToken.validation.serialRequired");
    }
    const seedError = validateSeed(seed, seedFormat, t);
    if (seedError) {
      errors.seed = seedError;
    }
    if (stepSeconds !== 30 && stepSeconds !== 60) {
      errors.stepSeconds = t("hardwareToken.validation.stepInvalid");
    }
    if (!trimmedReason) {
      errors.reason = t("hardwareToken.validation.reasonRequired");
    }

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }

    setFieldErrors({});
    setSubmitting(true);
    const seedPayload = seed.trim();
    try {
      await UserService.assignHardwareToken(user.id, {
        serialNumber: trimmedSerial,
        seed: seedPayload,
        seedFormat,
        stepSeconds,
        reason: trimmedReason
      });
      toast.success(t("hardwareToken.success", { username: user.username }));
      onAssigned();
      onClose();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, t("hardwareToken.error")));
    } finally {
      // Clear the seed immediately after every submission attempt, succeeded or failed.
      setSeed("");
      setShowSeed(false);
      setSubmitting(false);
    }
  };

  return (
    <FormDialog
      open={open}
      onClose={onClose}
      onSubmit={handleSubmit}
      title={t("hardwareToken.title")}
      submitLabel={t("hardwareToken.submit")}
      submitting={submitting}
      maxWidth="xs"
    >
      <Typography variant="body2">
        <Trans
          i18nKey="hardwareToken.intro"
          ns="users"
          values={{ fullName: user?.fullName ?? "", username: user?.username ?? "" }}
          components={{ user: <strong />, ltr: <Ltr>{null}</Ltr> }}
        />
      </Typography>
      <TextField
        label={t("hardwareToken.serialNumber")}
        size="small"
        value={serialNumber}
        onChange={(e) => {
          setSerialNumber(e.target.value);
          if (fieldErrors.serialNumber) setFieldErrors((prev) => ({ ...prev, serialNumber: undefined }));
        }}
        error={Boolean(fieldErrors.serialNumber)}
        helperText={fieldErrors.serialNumber}
        disabled={submitting}
        required
        fullWidth
      />
      <FormControl component="fieldset" disabled={submitting}>
        <FormLabel component="legend" sx={{ fontSize: 12 }}>
          {t("hardwareToken.seedFormat")}
        </FormLabel>
        <RadioGroup
          row
          value={seedFormat}
          onChange={(e) => {
            setSeedFormat(e.target.value as SeedFormat);
            if (fieldErrors.seed) setFieldErrors((prev) => ({ ...prev, seed: undefined }));
          }}
        >
          <FormControlLabel value="base32" control={<Radio size="small" />} label={t("hardwareToken.base32")} />
          <FormControlLabel value="hex" control={<Radio size="small" />} label={t("hardwareToken.hex")} />
        </RadioGroup>
      </FormControl>
      <TextField
        label={t("hardwareToken.seed")}
        size="small"
        type={showSeed ? "text" : "password"}
        value={seed}
        onChange={(e) => {
          setSeed(e.target.value);
          if (fieldErrors.seed) setFieldErrors((prev) => ({ ...prev, seed: undefined }));
        }}
        error={Boolean(fieldErrors.seed)}
        helperText={fieldErrors.seed ?? t("hardwareToken.seedHelper")}
        disabled={submitting}
        required
        fullWidth
        slotProps={{
          htmlInput: { autoComplete: "off" },
          input: {
            endAdornment: (
              <InputAdornment position="end">
                <IconButton
                  aria-label={showSeed ? t("hardwareToken.hideSeed") : t("hardwareToken.showSeed")}
                  onClick={() => setShowSeed((prev) => !prev)}
                  edge="end"
                  size="small"
                  disabled={submitting}
                >
                  {showSeed ? <VisibilityOff fontSize="small" /> : <Visibility fontSize="small" />}
                </IconButton>
              </InputAdornment>
            )
          }
        }}
      />
      <TextField
        select
        label={t("hardwareToken.timeStep")}
        size="small"
        value={stepSeconds}
        onChange={(e) => {
          setStepSeconds(Number(e.target.value) as StepSeconds);
          if (fieldErrors.stepSeconds) setFieldErrors((prev) => ({ ...prev, stepSeconds: undefined }));
        }}
        error={Boolean(fieldErrors.stepSeconds)}
        helperText={fieldErrors.stepSeconds ?? t("hardwareToken.timeStepHelper")}
        disabled={submitting}
        fullWidth
      >
        <MenuItem value={30}>{t("hardwareToken.seconds30")}</MenuItem>
        <MenuItem value={60}>{t("hardwareToken.seconds60")}</MenuItem>
      </TextField>
      <TextField
        label={t("fields.reason")}
        size="small"
        multiline
        minRows={2}
        value={reason}
        onChange={(e) => {
          setReason(e.target.value);
          if (fieldErrors.reason) setFieldErrors((prev) => ({ ...prev, reason: undefined }));
        }}
        error={Boolean(fieldErrors.reason)}
        helperText={fieldErrors.reason}
        disabled={submitting}
        required
        fullWidth
        placeholder={t("hardwareToken.reasonPlaceholder")}
      />
    </FormDialog>
  );
}
