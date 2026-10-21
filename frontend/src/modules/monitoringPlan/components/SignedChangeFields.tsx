import { Stack, TextField, Typography } from "@mui/material";
import type { SignedChange } from "../types";

// Reason plus the two signatures a skip or a window change needs: the
// person making the change and a second person who can approve plans.
export function SignedChangeFields({ value, onChange }: { value: SignedChange; onChange: (next: SignedChange) => void }) {
  const set = (patch: Partial<SignedChange>) => onChange({ ...value, ...patch });
  return (
    <Stack spacing={1.5}>
      <TextField
        label="Reason" required multiline minRows={2} value={value.reason}
        onChange={(e) => set({ reason: e.target.value })} helperText="At least 5 characters."
      />
      <Typography sx={{ fontSize: 13, fontWeight: 600 }}>Your signature</Typography>
      <TextField
        label="Your password" type="password" required autoComplete="current-password"
        value={value.password} onChange={(e) => set({ password: e.target.value })}
      />
      <Typography sx={{ fontSize: 13, fontWeight: 600 }}>Second signature (a different person who can approve plans)</Typography>
      <TextField
        label="Second signer username" required autoComplete="off"
        value={value.secondSignerUsername} onChange={(e) => set({ secondSignerUsername: e.target.value })}
      />
      <TextField
        label="Second signer password" type="password" required autoComplete="off"
        value={value.secondSignerPassword} onChange={(e) => set({ secondSignerPassword: e.target.value })}
      />
    </Stack>
  );
}

export const EMPTY_SIGNED_CHANGE: SignedChange = { reason: "", password: "", secondSignerUsername: "", secondSignerPassword: "" };

export const isSignedChangeComplete = (v: SignedChange) =>
  v.reason.trim().length >= 5 && !!v.password && !!v.secondSignerUsername.trim() && !!v.secondSignerPassword;
