import { Alert } from "@mui/material";

// The messages of the step the user tried to leave, once each (several fields can share a message).
export function StepErrorAlert({ errors }: { errors: Record<string, string> }) {
  const messages = [...new Set(Object.values(errors))];
  if (messages.length === 0) return null;
  return (
    <Alert severity="error" role="alert" sx={{ mb: 2 }}>
      {messages.map((message) => (
        <div key={message}>{message}</div>
      ))}
    </Alert>
  );
}
