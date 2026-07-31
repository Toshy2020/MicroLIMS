import { useState } from "react";
import { Box, Typography, Button, Stack, Alert, RadioGroup, FormControlLabel, Radio } from "@mui/material";
import { PathogenService } from "./services/PathogenService";
import { brandColors } from "../../theme";

interface Props {
  testOrderId: number;
  testCode: string;
}

const isSalmonella = (testCode: string) => testCode.toUpperCase().includes("SALMONELLA");
const salmonellaSteps = ["TSB", "RVS", "XLD_TSI"];

// Universal chain: TSB -> Observation -> Continue -> Detection Media ->
// Growth = Detected / No Growth = Absent.
// Salmonella exception: TSB -> RVS -> XLD+TSI -> Detected/Absent.
// Mirrors PathogenWorkflowEngine exactly - the frontend only collects
// input and displays what the backend decides.
export function PathogenDialog({ testOrderId, testCode }: Props) {
  const salmonella = isSalmonella(testCode);
  const [stepIndex, setStepIndex] = useState(0);
  const [growth, setGrowth] = useState<"yes" | "no" | "">("");
  const [result, setResult] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [chainClosed, setChainClosed] = useState(false);

  const currentStepName = salmonella ? salmonellaSteps[stepIndex] : "Simple";

  const submit = async () => {
    if (!growth) return;
    setError(null);
    try {
      await PathogenService.recordObservation(testOrderId, currentStepName, growth === "yes");

      if (!salmonella || growth === "no" || stepIndex === salmonellaSteps.length - 1) {
        setChainClosed(true);
        const { result } = await PathogenService.interpret(testOrderId);
        setResult(result);
      } else {
        setStepIndex((i) => i + 1);
        setGrowth("");
      }
    } catch (e: any) {
      setError(e?.response?.data?.message ?? "Could not record this observation.");
    }
  };

  if (result) {
    return (
      <Box>
        <Alert severity={result === "Detected" ? "error" : "success"} sx={{ mb: 2 }}>
          Final result: <strong>{result}</strong>
        </Alert>
        <Typography variant="body2" color="text.secondary">
          The pathogen chain is complete. This test order is now ready for review.
        </Typography>
      </Box>
    );
  }

  return (
    <Box>
      {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
      <Typography sx={{ color: brandColors.sectionTitle, fontWeight: 700, mb: 0.5 }}>
        Step: {currentStepName}
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        {salmonella
          ? "Salmonella exception chain: TSB → RVS → XLD+TSI."
          : "Universal chain: TSB → Detection Media."}
      </Typography>
      <RadioGroup value={growth} onChange={(e) => setGrowth(e.target.value as "yes" | "no")}>
        <FormControlLabel value="yes" control={<Radio />} label="Growth observed" />
        <FormControlLabel value="no" control={<Radio />} label="No growth observed" />
      </RadioGroup>
      <Stack direction="row" justifyContent="flex-end" sx={{ mt: 2 }}>
        <Button variant="contained" disabled={!growth || chainClosed} onClick={submit}>
          {salmonella && stepIndex < salmonellaSteps.length - 1 && growth === "yes" ? "Continue to next step" : "Submit"}
        </Button>
      </Stack>
    </Box>
  );
}
