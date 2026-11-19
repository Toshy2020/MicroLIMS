import type { TFunction } from "i18next";
import { i18n } from "../../../i18n";
import type { Role as RoleType } from "../../authentication/types/authTypes";

export type { RoleType };

export interface TourStep {
  target: string; /* data-tour value */
  title: string;
  body: string;
}

export function tourStateKey(role: RoleType): string {
  return `tour.${role}.v1`;
}

const TOUR_STEP_TARGETS: Record<RoleType, string[]> = {
  Analyst: ["my-work-queue", "nav-my-work", "nav-receiving", "global-search", "notifications", "help-menu"],
  Reviewer: ["my-work-queue", "nav-my-work", "nav-document-control", "global-search", "notifications", "help-menu"],
  SectionHead: ["my-work-queue", "nav-tracking", "nav-oos-tracking", "global-search", "notifications", "help-menu"],
  QcManager: ["my-work-queue", "nav-items", "nav-reports", "global-search", "notifications", "help-menu"],
  Qa: ["my-work-queue", "nav-audit-reviews", "nav-access-review", "global-search", "notifications", "help-menu"],
  SystemAdministrator: ["my-work-queue", "nav-admin-users", "nav-admin-settings", "global-search", "notifications", "help-menu"]
};

export function getTourSteps(t?: TFunction): Record<RoleType, TourStep[]> {
  const translate = t ?? i18n.getFixedT("en", "help");
  const result = {} as Record<RoleType, TourStep[]>;
  for (const role of Object.keys(TOUR_STEP_TARGETS) as RoleType[]) {
    result[role] = TOUR_STEP_TARGETS[role].map((target, index) => ({
      target,
      title: translate(`tourSteps.${role}.${index}.title`, { ns: "help" }),
      body: translate(`tourSteps.${role}.${index}.body`, { ns: "help" })
    }));
  }
  return result;
}

export const TOUR_STEPS: Record<RoleType, TourStep[]> = getTourSteps();
