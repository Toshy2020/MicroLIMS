import { UvTestFields } from "../../testMaster/UvTestFields";
import type { TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
}

export function UvSettingsBlock({ model, set }: Props) {
  return (
    <UvTestFields
      mode={model.uvCalculationMode}
      onModeChange={(nextMode) => set({ uvCalculationMode: nextMode })}
      specificAbsorbance={model.uvSpecificAbsorbance}
      onSpecificAbsorbanceChange={(nextVal) => set({ uvSpecificAbsorbance: nextVal })}
      totalVolumeMl={model.uvTotalVolumeMl}
      onTotalVolumeMlChange={(nextVal) => set({ uvTotalVolumeMl: nextVal })}
      standardVolumeMl={model.uvStandardVolumeMl}
      onStandardVolumeMlChange={(nextVal) => set({ uvStandardVolumeMl: nextVal })}
      standardEntryId={model.uvStandardEntryId}
      onStandardEntryIdChange={(nextId) => set({ uvStandardEntryId: nextId })}
      sectionId={model.sectionId}
      replicateCount={model.replicateCount}
      onReplicateCountChange={(nextCount) => set({ replicateCount: nextCount })}
    />
  );
}
