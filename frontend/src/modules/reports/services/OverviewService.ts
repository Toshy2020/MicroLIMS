import { OverviewDashboardData } from "../types/reportingTypes";
import { ReportingService } from "./ReportingService";
import { brandColors } from "../../../theme";

/* 
 * MOCK ADAPTER - Overview Dashboard Aggregation Service
 * Consumes ReportingService for live search results where possible.
 * Structured to be seamlessly connected to GET /api/reporting/overview when deployed.
 */
export const OverviewService = {
  async getOverviewData(fromDate?: string, toDate?: string): Promise<OverviewDashboardData> {
    try {
      // Attempt to query real results count from backend
      const res = await ReportingService.searchResults({ fromDate, toDate, page: 1, pageSize: 50 }).catch(() => null);
      const totalCount = res?.totalCount ?? 1284;

      return {
        totalTests: {
          title: "Total Tests",
          value: totalCount.toLocaleString(),
          deltaPercent: 12.5,
          deltaDirection: "up",
          comparisonLabel: "vs previous 30 days"
        },
        approvedResults: {
          title: "Approved Results",
          value: Math.round(totalCount * 0.92).toLocaleString(),
          deltaPercent: 10.3,
          deltaDirection: "up",
          comparisonLabel: "vs previous 30 days"
        },
        pendingReview: {
          title: "Pending Review",
          value: 14,
          deltaPercent: -12.5,
          deltaDirection: "down",
          comparisonLabel: "vs previous 30 days"
        },
        pendingApproval: {
          title: "Pending Approval",
          value: 6,
          deltaPercent: -25.0,
          deltaDirection: "down",
          comparisonLabel: "vs previous 30 days"
        },
        outOfSpec: {
          title: "Out of Spec",
          value: 8,
          deltaPercent: 33.3,
          deltaDirection: "up",
          comparisonLabel: "vs previous 30 days",
          variant: "error",
          tooltip: "Laboratory Quality Metric — Evaluated independently of analyst performance"
        },
        alertActionLevel: {
          title: "Alert / Action Level",
          value: 21,
          deltaPercent: 16.7,
          deltaDirection: "up",
          comparisonLabel: "vs previous 30 days",
          variant: "warning"
        },
        categoryDistribution: [
          { category: "FinishedProduct", label: "Finished Product", count: 540, percentage: 42, color: brandColors.badgeProduct },
          { category: "RawMaterial", label: "Raw Material", count: 321, percentage: 25, color: brandColors.badgeRM },
          { category: "PackagingMaterial", label: "Packaging Material", count: 154, percentage: 12, color: brandColors.badgePM },
          { category: "Water", label: "Water", count: 128, percentage: 10, color: "#0891b2" },
          { category: "EnvironmentalMonitoring", label: "Environmental Monitor", count: 103, percentage: 8, color: "#7c3aed" },
          { category: "AfterCleaning", label: "After Cleaning", count: 38, percentage: 3, color: "#be185d" }
        ],
        testDistribution: [
          { testCode: "TAMC", testName: "TAMC", count: 420 },
          { testCode: "TYMC", testName: "TYMC", count: 265 },
          { testCode: "PATHOGEN_ECOLI", testName: "E. coli", count: 145 },
          { testCode: "PATHOGEN_PAERUG", testName: "P. aeruginosa", count: 128 },
          { testCode: "PATHOGEN_SALM", testName: "Salmonella", count: 102 },
          { testCode: "PATHOGEN_SAUREUS", testName: "S. aureus", count: 95 },
          { testCode: "OTHER", testName: "Others", count: 129 }
        ],
        locationDistribution: [
          { location: "Production", count: 449, percentage: 35, color: "#7b2d8e" },
          { location: "Warehouse", count: 321, percentage: 25, color: "#9b3fa8" },
          { location: "QC Lab", count: 192, percentage: 15, color: "#2563eb" },
          { location: "Utilities", count: 128, percentage: 10, color: "#0891b2" },
          { location: "Other", count: 194, percentage: 15, color: "#9ca3af" }
        ],
        recentReports: [
          {
            id: "REP-2026-00125",
            name: "Monthly Microbiology Results - Jul 2026",
            type: "Microbiology",
            dateGenerated: "15 Aug 2026 10:30",
            generatedBy: "Amal Hamdy",
            status: "Final"
          },
          {
            id: "REP-2026-00124",
            name: "Water Monitoring Report - Jul 2026",
            type: "Water",
            dateGenerated: "14 Aug 2026 16:20",
            generatedBy: "Ahmed Ali",
            status: "Final"
          },
          {
            id: "REP-2026-00123",
            name: "Environmental Monitoring - Jul 2026",
            type: "Environmental",
            dateGenerated: "13 Aug 2026 09:15",
            generatedBy: "Amal Hamdy",
            status: "Final"
          },
          {
            id: "REP-2026-00122",
            name: "After Cleaning Report - Jul 2026",
            type: "After Cleaning",
            dateGenerated: "12 Aug 2026 14:40",
            generatedBy: "Sara Mohamed",
            status: "Final"
          }
        ],
        qualitySignals: {
          outOfSpecCount: 8,
          alertActionCount: 21,
          pendingReviewCount: 14,
          pendingApprovalCount: 6
        }
      };
    } catch {
      // Fallback baseline
      return {
        totalTests: { title: "Total Tests", value: "1,284", deltaPercent: 12.5, deltaDirection: "up", comparisonLabel: "vs previous 30 days" },
        approvedResults: { title: "Approved Results", value: "1,182", deltaPercent: 10.3, deltaDirection: "up", comparisonLabel: "vs previous 30 days" },
        pendingReview: { title: "Pending Review", value: 14, deltaPercent: -12.5, deltaDirection: "down", comparisonLabel: "vs previous 30 days" },
        pendingApproval: { title: "Pending Approval", value: 6, deltaPercent: -25.0, deltaDirection: "down", comparisonLabel: "vs previous 30 days" },
        outOfSpec: { title: "Out of Spec", value: 8, deltaPercent: 33.3, deltaDirection: "up", comparisonLabel: "vs previous 30 days", variant: "error" },
        alertActionLevel: { title: "Alert / Action Level", value: 21, deltaPercent: 16.7, deltaDirection: "up", comparisonLabel: "vs previous 30 days", variant: "warning" },
        categoryDistribution: [],
        testDistribution: [],
        locationDistribution: [],
        recentReports: [],
        qualitySignals: { outOfSpecCount: 8, alertActionCount: 21, pendingReviewCount: 14, pendingApprovalCount: 6 }
      };
    }
  }
};
