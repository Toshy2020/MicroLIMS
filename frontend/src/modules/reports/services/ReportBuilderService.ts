import { ReportBuilderCriteria, ReportBuilderOptions, ReportPreviewData, ResultRecordItem, SampleCategory } from "../types/reportingTypes";
import { ReportingService } from "./ReportingService";

/*
 * MOCK ADAPTER - Report Builder Service
 * Assembles dynamic report previews based on user criteria and formats reports.
 * Designed to cleanly delegate to backend PDF/Excel generators when available.
 */
export const ReportBuilderService = {
  async generatePreview(
    criteria: ReportBuilderCriteria,
    options: ReportBuilderOptions,
    currentUserName: string
  ): Promise<ReportPreviewData> {
    try {
      // Query underlying result records using existing ReportingService
      const searchRes = await ReportingService.searchResults({
        category: criteria.category || undefined,
        testCode: criteria.selectedTests.length === 1 ? criteria.selectedTests[0] : undefined,
        fromDate: criteria.fromDate,
        toDate: criteria.toDate,
        resultLevel: criteria.resultLevel || undefined,
        approvalStatus: criteria.approvalStatus || undefined,
        pageSize: 50
      }).catch(() => null);

      let items: ResultRecordItem[] = searchRes?.items ?? [];

      if (items.length === 0) {
        items = getMockPreviewRecords();
      }

      // Group items according to criteria.groupBy
      const groups = groupItems(items, criteria.groupBy);

      const periodFrom = criteria.fromDate ? formatDateStr(criteria.fromDate) : "01-Aug-2026";
      const periodTo = criteria.toDate ? formatDateStr(criteria.toDate) : "15-Aug-2026";

      return {
        reportTitle: criteria.reportType,
        reportPurpose: criteria.reportPurpose,
        reportingPeriod: `Period: ${periodFrom} — ${periodTo}`,
        generatedAt: `Generated on: ${new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" })} ${new Date().toLocaleTimeString("en-GB", { hour: "2-digit", minute: "2-digit" })}`,
        generatedByName: currentUserName || "Amal Hamdy (Analyst)",
        totalRecords: items.length,
        groups
      };
    } catch {
      return {
        reportTitle: criteria.reportType,
        reportPurpose: criteria.reportPurpose,
        reportingPeriod: "Period: 01-Aug-2026 — 15-Aug-2026",
        generatedAt: `Generated on: ${new Date().toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" })} 10:30`,
        generatedByName: currentUserName,
        totalRecords: 0,
        groups: []
      };
    }
  },

  exportCsv(items: ResultRecordItem[], title: string) {
    const headers = [
      "Sample/Reference", "Product/Item", "Category", "Test", "Result", "Unit",
      "Specification", "Result Level", "Status", "Analyst", "Approved By", "Approval Date"
    ];
    const rows = items.map((r) => [
      `"${r.referenceNumber}"`,
      `"${r.subjectName}"`,
      `"${r.category}"`,
      `"${r.testDisplayName || r.testCode}"`,
      `"${r.reportedValue}"`,
      `"${r.unit ?? ""}"`,
      `"${r.specLimit ?? ""}"`,
      `"${r.resultLevel}"`,
      `"${r.approvalStatus}"`,
      `"${r.resultEnteredByName}"`,
      `"${r.approvedByName ?? ""}"`,
      `"${r.approvedAt ?? ""}"`
    ]);

    const csvContent = [headers.join(","), ...rows.map((row) => row.join(","))].join("\n");
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `${title.toLowerCase().replace(/\s+/g, "_")}_${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }
};

function formatDateStr(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString("en-GB", { day: "2-digit", month: "short", year: "numeric" });
  } catch {
    return iso;
  }
}

function groupItems(items: ResultRecordItem[], groupBy: string) {
  if (groupBy === "None") {
    return [{ groupKey: "All", groupTitle: "All Results", items }];
  }

  const map = new Map<string, ResultRecordItem[]>();
  for (const item of items) {
    let key = "Other";
    if (groupBy === "Product") key = item.subjectName;
    else if (groupBy === "Category") key = item.category;
    else if (groupBy === "Test") key = item.testDisplayName || item.testCode;
    else if (groupBy === "Location") key = item.subjectDetail || item.subjectName;
    else if (groupBy === "Date") key = item.resultEnteredAt.slice(0, 10);

    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(item);
  }

  return Array.from(map.entries()).map(([groupKey, groupItems]) => ({
    groupKey,
    groupTitle: `${groupBy}: ${groupKey}`,
    items: groupItems
  }));
}

function getMockPreviewRecords(): ResultRecordItem[] {
  return [
    {
      id: 1,
      sampleId: 101,
      testOrderId: 501,
      sourceTable: "TestOrders",
      sourceId: 501,
      round: 1,
      referenceNumber: "FP0826001",
      category: "FinishedProduct",
      subjectName: "Osteocare Liquid",
      subjectDetail: "Batch B0124",
      batchNumber: "B0124",
      controlNumber: "C-2026-09",
      testCode: "TAMC",
      testDisplayName: "TAMC",
      resultKind: "Quantitative",
      numericValue: 1,
      reportedValue: "<1",
      unit: "CFU/mL",
      isBelowDetectionLimit: true,
      detectionLimit: 1,
      alertLimit: "50",
      actionLimit: "100",
      specLimit: "NMT 100",
      resultLevel: "WithinLimit",
      resultEnteredAt: "2026-08-15T09:30:00Z",
      resultEnteredByUserId: 101,
      resultEnteredByName: "Amal Hamdy",
      sampleStatus: "Approved",
      approvalStatus: "Approved",
      approvedByUserId: 201,
      approvedByName: "Amal Hamdy",
      approvedAt: "2026-08-16T10:00:00Z"
    },
    {
      id: 2,
      sampleId: 101,
      testOrderId: 502,
      sourceTable: "TestOrders",
      sourceId: 502,
      round: 1,
      referenceNumber: "FP0826002",
      category: "FinishedProduct",
      subjectName: "Osteocare Liquid",
      subjectDetail: "Batch B0124",
      batchNumber: "B0124",
      controlNumber: "C-2026-09",
      testCode: "TYMC",
      testDisplayName: "TYMC",
      resultKind: "Quantitative",
      numericValue: 1,
      reportedValue: "<1",
      unit: "CFU/mL",
      isBelowDetectionLimit: true,
      detectionLimit: 1,
      alertLimit: "10",
      actionLimit: "20",
      specLimit: "NMT 100",
      resultLevel: "WithinLimit",
      resultEnteredAt: "2026-08-15T09:30:00Z",
      resultEnteredByUserId: 101,
      resultEnteredByName: "Amal Hamdy",
      sampleStatus: "Approved",
      approvalStatus: "Approved",
      approvedByUserId: 201,
      approvedByName: "Amal Hamdy",
      approvedAt: "2026-08-16T10:00:00Z"
    },
    {
      id: 3,
      sampleId: 102,
      testOrderId: 503,
      sourceTable: "TestOrders",
      sourceId: 503,
      round: 1,
      referenceNumber: "RM0826001",
      category: "RawMaterial",
      subjectName: "Honey",
      subjectDetail: "Lot H-9921",
      batchNumber: "H-9921",
      controlNumber: "C-2026-10",
      testCode: "TYMC",
      testDisplayName: "TYMC",
      resultKind: "Quantitative",
      numericValue: 110,
      reportedValue: "110",
      unit: "CFU/mL",
      isBelowDetectionLimit: false,
      detectionLimit: 1,
      alertLimit: "100",
      actionLimit: "200",
      specLimit: "NMT 1000",
      resultLevel: "AlertLevel",
      resultEnteredAt: "2026-08-14T14:15:00Z",
      resultEnteredByUserId: 102,
      resultEnteredByName: "Ahmed Ali",
      sampleStatus: "Approved",
      approvalStatus: "Approved",
      approvedByUserId: 201,
      approvedByName: "Amal Hamdy",
      approvedAt: "2026-08-15T11:00:00Z"
    },
    {
      id: 4,
      sampleId: 102,
      testOrderId: 504,
      sourceTable: "TestOrders",
      sourceId: 504,
      round: 1,
      referenceNumber: "RM0826001",
      category: "RawMaterial",
      subjectName: "Honey",
      subjectDetail: "Lot H-9921",
      batchNumber: "H-9921",
      controlNumber: "C-2026-10",
      testCode: "TAMC",
      testDisplayName: "TAMC",
      resultKind: "Quantitative",
      numericValue: 650,
      reportedValue: "650",
      unit: "CFU/mL",
      isBelowDetectionLimit: false,
      detectionLimit: 1,
      alertLimit: "100",
      actionLimit: "200",
      specLimit: "NMT 500",
      resultLevel: "OutOfSpecification",
      resultEnteredAt: "2026-08-14T14:15:00Z",
      resultEnteredByUserId: 102,
      resultEnteredByName: "Ahmed Ali",
      sampleStatus: "Approved",
      approvalStatus: "Approved",
      approvedByUserId: 201,
      approvedByName: "Amal Hamdy",
      approvedAt: "2026-08-15T11:00:00Z"
    },
    {
      id: 5,
      sampleId: 103,
      testOrderId: 505,
      sourceTable: "TestOrders",
      sourceId: 505,
      round: 1,
      referenceNumber: "WT0826001",
      category: "Water",
      subjectName: "SWT",
      subjectDetail: "Point W-01",
      batchNumber: null,
      controlNumber: null,
      testCode: "WATER_TAMC",
      testDisplayName: "TAMC-Water",
      resultKind: "Quantitative",
      numericValue: 4,
      reportedValue: "4",
      unit: "CFU/mL",
      isBelowDetectionLimit: false,
      detectionLimit: 1,
      alertLimit: "10",
      actionLimit: "50",
      specLimit: "NMT 100",
      resultLevel: "WithinLimit",
      resultEnteredAt: "2026-08-13T11:00:00Z",
      resultEnteredByUserId: 103,
      resultEnteredByName: "Sara Mohamed",
      sampleStatus: "Approved",
      approvalStatus: "Approved",
      approvedByUserId: 201,
      approvedByName: "Amal Hamdy",
      approvedAt: "2026-08-14T09:00:00Z"
    }
  ];
}
