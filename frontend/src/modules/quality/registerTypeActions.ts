import { CapaActions } from "./capa/CapaActions";
import { ChangeControlActions } from "./changeControl/ChangeControlActions";
import { ComplaintActions } from "./complaint/ComplaintActions";
import { DeviationActions } from "./deviation/DeviationActions";
import { registerQualityTypeActions } from "./typeActions";

// Registers the step forms of the four record types in the record page's actions slot. Imported for its effect by
// the route module that loads the record page (QualityRecordRoute), so the page itself stays free of any one type.
registerQualityTypeActions("Deviation", DeviationActions);
registerQualityTypeActions("Capa", CapaActions);
registerQualityTypeActions("ChangeControl", ChangeControlActions);
registerQualityTypeActions("Complaint", ComplaintActions);
