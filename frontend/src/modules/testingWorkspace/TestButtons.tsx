import { Button, Stack } from "@mui/material";

export function TestButtons({ tests, onSelect }: { tests: string[]; onSelect: (test: string) => void }) {
  return (
    <Stack direction="row" spacing={1}>
      {tests.map((t) => <Button key={t} size="small" variant="outlined" onClick={() => onSelect(t)}>{t}</Button>)}
    </Stack>
  );
}
