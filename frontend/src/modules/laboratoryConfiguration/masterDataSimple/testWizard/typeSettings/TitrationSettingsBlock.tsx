import { TitrationConfigSection } from "../../TitrationConfigSection";
import type { TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
}

export function TitrationSettingsBlock({ model, set }: Props) {
  return (
    <TitrationConfigSection
      form={model.titration}
      onChange={(nextTitration) => {
        set({
          titration: nextTitration,
          moistureRole:
            nextTitration.type !== "KarlFischer" && model.moistureRole === "Water"
              ? "None"
              : model.moistureRole
        });
      }}
      sectionId={model.sectionId}
      moistureRole={model.moistureRole}
      onMoistureRoleChange={(nextRole) => set({ moistureRole: nextRole })}
    />
  );
}
