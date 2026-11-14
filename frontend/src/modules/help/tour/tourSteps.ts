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

export const TOUR_STEPS: Record<RoleType, TourStep[]> = {
  Analyst: [
    {
      target: "my-work-queue",
      title: "Your work queue",
      body: "My Work groups tests assigned to you, unassigned section tests you can take, and reading tasks by urgency."
    },
    {
      target: "nav-my-work",
      title: "My Work in the sidebar",
      body: "Use My Work in the navigation menu to return to your queue from any page and see how many tasks are waiting."
    },
    {
      target: "nav-receiving",
      title: "Sample receiving",
      body: "Register incoming samples for your laboratory section and print barcode labels from Receive Sample."
    },
    {
      target: "global-search",
      title: "Search MicroLIMS (Ctrl+K)",
      body: "Press Ctrl+K or select the search bar to jump to a sample, batch, test order, or controlled document by code."
    },
    {
      target: "notifications",
      title: "Notifications",
      body: "Check alerts when incubations are ready to read or when a test is returned to you for revision."
    },
    {
      target: "help-menu",
      title: "Help and shortcuts",
      body: "Open the user guide chapter for the current page with F1, view keyboard shortcuts with ?, or replay this tour."
    }
  ],
  Reviewer: [
    {
      target: "my-work-queue",
      title: "Technical review queue",
      body: "My Work lists samples waiting for technical verification, document reviews, and reading tasks ordered by urgency."
    },
    {
      target: "nav-my-work",
      title: "My Work in the sidebar",
      body: "Select My Work in the sidebar to return to your review queue and check the live task count."
    },
    {
      target: "nav-document-control",
      title: "Document Control",
      body: "Open Document Control to review and approve controlled documents, inspect the training matrix, and read SOPs."
    },
    {
      target: "global-search",
      title: "Search MicroLIMS (Ctrl+K)",
      body: "Press Ctrl+K or select the search bar to find samples, test orders, or controlled documents by identifier."
    },
    {
      target: "notifications",
      title: "Notifications",
      body: "Check alerts when samples enter technical review or when controlled documents require your attention."
    },
    {
      target: "help-menu",
      title: "Help and guidance",
      body: "Press F1 for the current page's user guide chapter, press ? for keyboard shortcuts, or replay this tour."
    }
  ],
  SectionHead: [
    {
      target: "my-work-queue",
      title: "Approvals and section queue",
      body: "My Work brings together sample approvals, technical reviews, media and solution releases, and unassigned section work."
    },
    {
      target: "nav-tracking",
      title: "Tracking Board",
      body: "Open the Tracking Board to follow every received sample and see its current stage across laboratory sections."
    },
    {
      target: "nav-oos-tracking",
      title: "OOS Tracking",
      body: "Use OOS Tracking to oversee out-of-specification investigations, retest progress, and closure decisions."
    },
    {
      target: "global-search",
      title: "Search MicroLIMS (Ctrl+K)",
      body: "Press Ctrl+K or select the search bar to locate samples, batches, master data records, or controlled documents."
    },
    {
      target: "notifications",
      title: "Notifications",
      body: "See alerts for samples awaiting approval, media expiry warnings, and returned test orders."
    },
    {
      target: "help-menu",
      title: "Help and guidance",
      body: "Open the guide chapter for the current page with F1, check keyboard shortcuts with ?, or replay this tour."
    }
  ],
  QcManager: [
    {
      target: "my-work-queue",
      title: "Pending approvals and tasks",
      body: "My Work shows not-applicable test requests and document reading tasks waiting for your decision, grouped by urgency."
    },
    {
      target: "nav-items",
      title: "Items and specifications",
      body: "Open Items to inspect item specifications and record final decisions on not-applicable test requests."
    },
    {
      target: "nav-reports",
      title: "Reports",
      body: "Use Reports to review laboratory performance indicators, turnaround trends, and compliance summaries."
    },
    {
      target: "global-search",
      title: "Search MicroLIMS (Ctrl+K)",
      body: "Press Ctrl+K or select the search bar to look up samples, items, or controlled documents from any screen."
    },
    {
      target: "notifications",
      title: "Notifications",
      body: "Check alerts for pending approvals, document updates, and discussions."
    },
    {
      target: "help-menu",
      title: "Help and guidance",
      body: "Press F1 to open the user guide for the current page or replay this tour whenever needed."
    }
  ],
  Qa: [
    {
      target: "my-work-queue",
      title: "Quality Assurance queue",
      body: "My Work lists document reviews, approvals, and quality tasks waiting for your action, grouped by urgency."
    },
    {
      target: "nav-audit-reviews",
      title: "Audit Trail Reviews",
      body: "Open Audit Trail Reviews to conduct and sign periodic reviews of the GxP audit trail and record findings."
    },
    {
      target: "nav-access-review",
      title: "Access Review",
      body: "Use Access Review to examine periodic user access reports and sign the quality assurance conclusion."
    },
    {
      target: "global-search",
      title: "Search MicroLIMS (Ctrl+K)",
      body: "Press Ctrl+K or select the search bar to search across samples, controlled documents, and laboratory records."
    },
    {
      target: "notifications",
      title: "Notifications",
      body: "Check alerts for document workflow assignments and discussions."
    },
    {
      target: "help-menu",
      title: "Help and guidance",
      body: "Press F1 to open the guide chapter for the current page or replay this tour from the Help menu."
    }
  ],
  SystemAdministrator: [
    {
      target: "my-work-queue",
      title: "Administrator tasks",
      body: "My Work displays administrative tasks and assigned document reading items waiting for your action."
    },
    {
      target: "nav-admin-users",
      title: "Users",
      body: "Open Users to create accounts, manage laboratory section memberships, and handle lockouts or password resets."
    },
    {
      target: "nav-admin-settings",
      title: "Settings",
      body: "Use Settings to configure password policies, session timeouts, and system operating parameters."
    },
    {
      target: "global-search",
      title: "Search MicroLIMS (Ctrl+K)",
      body: "Press Ctrl+K or select the search bar to find users, controlled documents, or records across the application."
    },
    {
      target: "notifications",
      title: "Notifications",
      body: "Check system notifications and discussion updates that need your attention."
    },
    {
      target: "help-menu",
      title: "Help and guidance",
      body: "Press F1 to open the user guide for the current page or replay this tour from the Help menu."
    }
  ]
};
