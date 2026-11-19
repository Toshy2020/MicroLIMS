import { IcpTestFields } from "../../testMaster/IcpTestFields";
import type { TestFormModel } from "../testFormModel";

interface Props {
  model: TestFormModel;
  set: (patch: Partial<TestFormModel>) => void;
}

export function IcpSettingsBlock({ model, set }: Props) {
  return (
    <IcpTestFields
      form={model.icp}
      onChange={(nextIcp) => set({ icp: nextIcp })}
    />
  );
}
