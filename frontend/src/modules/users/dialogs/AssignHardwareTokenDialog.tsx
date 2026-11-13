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
import { toast } from "sonner";
import { FormDialog } from "../../../components/lab/FormDialog";
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

function validateSeed(rawSeed: string, format: SeedFormat): string | null {
  const trimmed = rawSeed.trim();
  if (!trimmed) return "Enter the hardware token seed.";
  if (format === "hex") {
    const compact = trimmed.replace(/\s+/g, "");
    if (!/^[0-9a-fA-F]+$/.test(compact) || compact.length % 2 !== 0) {
      return "Hex seeds must have an even number of hexadecimal characters.";
    }
    return null;
  }
  const compact = trimmed.replace(/[\s-]+/g, "").replace(/=+$/, "");
  if (!compact || !/^[A-Z2-7]+$/i.test(compact)) {
    return "Base32 seeds may contain letters A-Z and digits 2-7 only.";
  }
  return null;
}

/** Administrator dialog to assign a TOTP hardware token to another user without retaining the seed after submit. */
export function AssignHardwareTokenDialog({ open, user, onClose, onAssigned }: AssignHardwareTokenDialogProps) {
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
      errors.serialNumber = "Enter the token serial number.";
    }
    const seedError = validateSeed(seed, seedFormat);
    if (seedError) {
      errors.seed = seedError;
    }
    if (stepSeconds !== 30 && stepSeconds !== 60) {
      errors.stepSeconds = "Time step must be 30 or 60 seconds.";
    }
    if (!trimmedReason) {
      errors.reason = "Enter a reason for assigning this token.";
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
      toast.success(`Hardware token assigned to ${user.username}.`);
      onAssigned();
      onClose();
    } catch (err: unknown) {
      toast.error(serverMessage(err) ?? getErrorMessage(err, "Could not assign the hardware token."));
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
      title="Assign hardware token"
      submitLabel="Assign hardware token"
      submitting={submitting}
      maxWidth="xs"
    >
      <Typography variant="body2">
        Assign a hardware token to{" "}
        <strong>
          {user?.fullName} (@{user?.username})
        </strong>
        . At next sign-in the user will enter a code from the token to confirm it.
      </Typography>
      <TextField
        label="Serial number"
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
          Seed format
        </FormLabel>
        <RadioGroup
          row
          value={seedFormat}
          onChange={(e) => {
            setSeedFormat(e.target.value as SeedFormat);
            if (fieldErrors.seed) setFieldErrors((prev) => ({ ...prev, seed: undefined }));
          }}
        >
          <FormControlLabel value="base32" control={<Radio size="small" />} label="Base32" />
          <FormControlLabel value="hex" control={<Radio size="small" />} label="Hex" />
        </RadioGroup>
      </FormControl>
      <TextField
        label="Seed"
        size="small"
        type={showSeed ? "text" : "password"}
        value={seed}
        onChange={(e) => {
          setSeed(e.target.value);
          if (fieldErrors.seed) setFieldErrors((prev) => ({ ...prev, seed: undefined }));
        }}
        error={Boolean(fieldErrors.seed)}
        helperText={fieldErrors.seed ?? "Cleared automatically once submitted."}
        disabled={submitting}
        required
        fullWidth
        slotProps={{
          htmlInput: { autoComplete: "off" },
          input: {
            endAdornment: (
              <InputAdornment position="end">
                <IconButton
                  aria-label={showSeed ? "Hide seed" : "Show seed"}
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
        label="Time step"
        size="small"
        value={stepSeconds}
        onChange={(e) => {
          setStepSeconds(Number(e.target.value) as StepSeconds);
          if (fieldErrors.stepSeconds) setFieldErrors((prev) => ({ ...prev, stepSeconds: undefined }));
        }}
        error={Boolean(fieldErrors.stepSeconds)}
        helperText={fieldErrors.stepSeconds ?? "Match the token specification (30 or 60 seconds)."}
        disabled={submitting}
        fullWidth
      >
        <MenuItem value={30}>30 seconds</MenuItem>
        <MenuItem value={60}>60 seconds</MenuItem>
      </TextField>
      <TextField
        label="Reason"
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
        placeholder="Why a hardware token is being assigned"
      />
    </FormDialog>
  );
}
