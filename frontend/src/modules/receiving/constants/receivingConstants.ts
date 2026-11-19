import type { TFunction } from "i18next";
import { i18n } from "../../../i18n";
import { CategoryDefinition } from "../types/receivingTypes";

export function getReceivingCategories(t?: TFunction): CategoryDefinition[] {
  const translate = t ?? i18n.getFixedT("en", "receiving");
  return [
    {
      key: "product",
      label: translate("constants.categories.product.label", { ns: "receiving" }),
      apiCategory: "FinishedProduct",
      backendCategoryName: "FinishedProduct",
      description: translate("constants.categories.product.description", { ns: "receiving" })
    },
    {
      key: "rm",
      label: translate("constants.categories.rm.label", { ns: "receiving" }),
      apiCategory: "RawMaterial",
      backendCategoryName: "RawMaterial",
      description: translate("constants.categories.rm.description", { ns: "receiving" })
    },
    {
      key: "pm",
      label: translate("constants.categories.pm.label", { ns: "receiving" }),
      apiCategory: "PackagingMaterial",
      backendCategoryName: "PackagingMaterial",
      description: translate("constants.categories.pm.description", { ns: "receiving" })
    },
    {
      key: "water",
      label: translate("constants.categories.water.label", { ns: "receiving" }),
      apiCategory: null,
      backendCategoryName: "Water",
      description: translate("constants.categories.water.description", { ns: "receiving" })
    },
    {
      key: "em",
      label: translate("constants.categories.em.label", { ns: "receiving" }),
      apiCategory: null,
      backendCategoryName: "EnvironmentalMonitoring",
      description: translate("constants.categories.em.description", { ns: "receiving" })
    },
    {
      key: "ac",
      label: translate("constants.categories.ac.label", { ns: "receiving" }),
      apiCategory: null,
      backendCategoryName: "AfterCleaning",
      description: translate("constants.categories.ac.description", { ns: "receiving" })
    }
  ];
}

export const RECEIVING_CATEGORIES: CategoryDefinition[] = getReceivingCategories();

export function getSampleStatusOptions(t?: TFunction) {
  const translate = t ?? i18n.getFixedT("en", "receiving");
  return [
    { value: "ALL", label: translate("constants.sampleStatuses.ALL", { ns: "receiving" }) },
    { value: "Received", label: translate("constants.sampleStatuses.Received", { ns: "receiving" }) },
    { value: "InTesting", label: translate("constants.sampleStatuses.InTesting", { ns: "receiving" }) },
    { value: "PendingReview", label: translate("constants.sampleStatuses.PendingReview", { ns: "receiving" }) },
    { value: "UnderReview", label: translate("constants.sampleStatuses.UnderReview", { ns: "receiving" }) },
    { value: "UnderApproval", label: translate("constants.sampleStatuses.UnderApproval", { ns: "receiving" }) },
    { value: "Approved", label: translate("constants.sampleStatuses.Approved", { ns: "receiving" }) },
    { value: "Rejected", label: translate("constants.sampleStatuses.Rejected", { ns: "receiving" }) },
    { value: "RetestRequested", label: translate("constants.sampleStatuses.RetestRequested", { ns: "receiving" }) }
  ];
}

export const SAMPLE_STATUS_OPTIONS = getSampleStatusOptions();

export function getTestStatusOptions(t?: TFunction) {
  const translate = t ?? i18n.getFixedT("en", "receiving");
  return [
    { value: "ALL", label: translate("constants.testStatuses.ALL", { ns: "receiving" }) },
    { value: "Waiting", label: translate("constants.testStatuses.Waiting", { ns: "receiving" }) },
    { value: "InProgress", label: translate("constants.testStatuses.InProgress", { ns: "receiving" }) },
    { value: "ResultEntered", label: translate("constants.testStatuses.ResultEntered", { ns: "receiving" }) },
    { value: "UnderReview", label: translate("constants.testStatuses.UnderReview", { ns: "receiving" }) },
    { value: "Reviewed", label: translate("constants.testStatuses.Reviewed", { ns: "receiving" }) },
    { value: "Approved", label: translate("constants.testStatuses.Approved", { ns: "receiving" }) },
    { value: "Rejected", label: translate("constants.testStatuses.Rejected", { ns: "receiving" }) }
  ];
}

export const TEST_STATUS_OPTIONS = getTestStatusOptions();
