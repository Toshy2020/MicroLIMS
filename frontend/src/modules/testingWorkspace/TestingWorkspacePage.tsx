import { useEffect, useState } from "react";
import { Grid } from "@mui/material";
import { SampleCard } from "./SampleCard";
import { TestWorkflowDialog } from "./FloatingDialogs";
import { WorkspaceService } from "./services/WorkspaceService";
import { SampleCard as SampleCardType, TestOrderSummary } from "./types/workspaceTypes";
import { LoadingSpinner } from "../../components/LoadingSpinner";
import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";

export function TestingWorkspacePage() {
  const [samples, setSamples] = useState<SampleCardType[] | null>(null);
  const [activeTest, setActiveTest] = useState<TestOrderSummary | null>(null);
  const [activeCategory, setActiveCategory] = useState<string | undefined>();

  const load = () => WorkspaceService.getActiveSamples().then(setSamples);

  useEffect(() => { load(); }, []);

  if (!samples) return <LoadingSpinner />;

  const handleTestClick = (sample: SampleCardType, test: TestOrderSummary) => {
    setActiveTest(test);
    setActiveCategory(sample.category);
  };

  const handleClose = () => {
    setActiveTest(null);
    load(); // refresh statuses after a workflow dialog closes
  };

  return (
    <>
      <PageHeader title="Testing Workspace" subtitle="Every active sample, one card each. Click a test to open its workflow." />
      <SectionTitle>Active Samples</SectionTitle>
      <Grid container spacing={2}>
        {samples.map((s) => (
          <Grid item xs={12} sm={6} md={4} key={s.sampleId}>
            <SampleCard sample={s} onTestClick={(test) => handleTestClick(s, test)} />
          </Grid>
        ))}
      </Grid>
      <TestWorkflowDialog open={!!activeTest} test={activeTest} category={activeCategory} onClose={handleClose} />
    </>
  );
}
