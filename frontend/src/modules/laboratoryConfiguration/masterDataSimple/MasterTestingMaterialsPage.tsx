import { Button, Stack, Tab, Tabs } from "@mui/material";
import { Link as RouterLink, useSearchParams } from "react-router-dom";
import { PageHeader } from "../../../components/PageHeader";
import { useOrganisms } from "../../../hooks/useOrganisms";
import { MaterialMasterPage } from "./MaterialMasterPage";
import { MediaConfigurationPage } from "../media/MediaConfigurationPage";
import { ReferenceOrganismsSection } from "./ReferenceOrganismsSection";
import { WorkingStandardCatalogSection } from "./WorkingStandardCatalogSection";
import type { MaterialMasterCategory } from "./services/MaterialMasterService";

export const MASTER_TESTING_MATERIALS_TABS = ["reagents", "standards", "media", "organisms", "other"] as const;
export type MasterTestingMaterialsTab = (typeof MASTER_TESTING_MATERIALS_TABS)[number];

const TAB_LABELS: Record<MasterTestingMaterialsTab, string> = {
  reagents: "Reagents & Indicators",
  standards: "Reference & Primary Standards",
  media: "Culture Media",
  organisms: "Reference Organisms",
  other: "Other Materials"
};

// Declared once so each tab keeps a stable array (the master page memoizes on it).
const REAGENT_CATEGORIES: MaterialMasterCategory[] = ["Reagent", "Indicator"];
const STANDARD_CATEGORIES: MaterialMasterCategory[] = ["ReferenceStandard", "PrimaryStandard"];
const OTHER_CATEGORIES: MaterialMasterCategory[] = ["Other"];

function ReferenceOrganismsTab() {
  const { options } = useOrganisms();
  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ justifyContent: "flex-end" }}>
        <Button component={RouterLink} to="/laboratory-configuration/organisms" variant="outlined" sx={{ fontWeight: 600, textTransform: "none" }}>
          Species list
        </Button>
      </Stack>
      <ReferenceOrganismsSection species={options} />
    </Stack>
  );
}

// Every material the labs receive into stock, defined in one place: the five groups are the existing
// master screens hosted as tabs. The selected tab lives in the URL (?tab=) so the old pages can redirect to it.
export function MasterTestingMaterialsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const requested = searchParams.get("tab");
  const tab: MasterTestingMaterialsTab = MASTER_TESTING_MATERIALS_TABS.find((t) => t === requested) ?? "reagents";

  return (
    <Stack spacing={2}>
      <PageHeader
        title="Master Testing Materials"
        subtitle="Every material the labs receive into stock. Every lab can see and use every entry; only the owner lab can edit it."
      />
      <Tabs
        value={tab}
        onChange={(_, v: MasterTestingMaterialsTab) => setSearchParams({ tab: v }, { replace: true })}
        variant="scrollable"
        scrollButtons="auto"
      >
        {MASTER_TESTING_MATERIALS_TABS.map((t) => (
          <Tab key={t} value={t} label={TAB_LABELS[t]} />
        ))}
      </Tabs>

      {tab === "reagents" && <MaterialMasterPage categories={REAGENT_CATEGORIES} embedded />}
      {tab === "standards" && (
        <>
          <MaterialMasterPage categories={STANDARD_CATEGORIES} embedded />
          <WorkingStandardCatalogSection />
        </>
      )}
      {tab === "media" && <MediaConfigurationPage embedded />}
      {tab === "organisms" && <ReferenceOrganismsTab />}
      {tab === "other" && <MaterialMasterPage categories={OTHER_CATEGORIES} embedded />}
    </Stack>
  );
}
