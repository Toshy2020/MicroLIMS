import { Card, CardContent, Typography, Stack, Box } from "@mui/material";
import { SampleCard as SampleCardType, TestOrderSummary } from "./types/workspaceTypes";
import { CategoryBadge, StatusBadge } from "../../components/StatusBadge";
import { brandColors } from "../../theme";

interface Props {
  sample: SampleCardType;
  onTestClick: (test: TestOrderSummary) => void;
}

// "Each sample appears as a card... Clicking a test opens its frozen
// workflow." (spec section 6, Testing Workspace) - styled per the
// provided design's card + pill-badge language.
export function SampleCard({ sample, onTestClick }: Props) {
  return (
    <Card variant="outlined" sx={{ borderRadius: 2.5 }}>
      <CardContent>
        <Typography sx={{ fontWeight: 700, fontSize: 15, color: brandColors.pageTitle }}>{sample.itemName}</Typography>
        <Typography sx={{ fontSize: 12, color: "text.secondary", mb: 1 }}>Batch: {sample.batchNumber}</Typography>
        <Stack direction="row" spacing={0.75} sx={{ mb: 1.25 }}>
          {sample.category && <CategoryBadge category={sample.category} />}
          <StatusBadge status={sample.status} />
        </Stack>
        <Stack direction="row" spacing={1} sx={{ flexWrap: "wrap", gap: 0.75 }}>
          {sample.assignedTests.map((test) => (
            <Box
              key={test.testOrderId}
              onClick={() => onTestClick(test)}
              sx={{
                cursor: "pointer", px: 1.25, py: 0.5, borderRadius: 5, fontSize: 12, fontWeight: 600,
                bgcolor: "#f3e8ff", color: brandColors.sectionTitle, "&:hover": { bgcolor: "#e9d5ff" }
              }}
            >
              {test.testCode}
            </Box>
          ))}
        </Stack>
      </CardContent>
    </Card>
  );
}
