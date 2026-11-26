import { afterEach, describe, expect, it, vi } from "vitest";

const http = vi.hoisted(() => ({
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn()
}));
vi.mock("./apiClient", async (importOriginal) => {
  const actual = await importOriginal<typeof import("./apiClient")>();
  return { ...actual, apiClient: http };
});

import { qualityService } from "./qualityService";

afterEach(() => {
  vi.clearAllMocks();
});

const version = (v: number) => ({ headers: { "If-Match": `"${v}"` } });
const envelope = <T,>(data: T) => ({ data: { success: true, data } });
const password = { password: "pw" };
const reason = { reason: "Because", password: "pw" };

describe("qualityService step calls", () => {
  it("sends each type's step request to its own route with the record version as If-Match", async () => {
    http.post.mockResolvedValue(envelope({}));

    await qualityService.assessDeviation(3, { accept: true, classificationId: 2, ownerUserId: 8, dueDate: null, password: "pw" }, 7);
    await qualityService.rejectDeviation(3, reason, 7);
    await qualityService.signDeviationLabConclusion(3, password, 7);
    await qualityService.approveDeviation(3, { capaRequired: true, comment: null, password: "pw" }, 7);
    await qualityService.returnDeviation(3, { comment: "More detail" }, 7);
    await qualityService.closeDeviation(3, password, 7);
    await qualityService.cancelDeviation(3, reason, 7);
    await qualityService.approveCapaPlan(4, password, 8);
    await qualityService.returnCapaPlan(4, { comment: "Add a due date" }, 8);
    await qualityService.verifyCapaCompletion(4, password, 8);
    await qualityService.signCapaEffectiveness(4, { result: "NotEffective", findings: "Still drifting", newCapaId: 9, password: "pw" }, 8);
    await qualityService.cancelCapa(4, reason, 8);
    await qualityService.signChangeControlAssessment(5, password, 9);
    await qualityService.approveChangeControl(5, password, 9);
    await qualityService.rejectChangeControl(5, reason, 9);
    await qualityService.closeChangeControl(5, { postChangeReview: "Fine", unchangedJustification: null, password: "pw" }, 9);
    await qualityService.cancelChangeControl(5, reason, 9);
    await qualityService.addChangeControlAffected(5, { targetType: "Item", targetId: 12, newRecordCode: null }, 9);
    await qualityService.requestComplaintInvestigation(6, { classificationId: 1, ownerUserId: 8, dueDate: "2026-12-30", whatToTest: "Retained sample" }, 10);
    await qualityService.signComplaintLabReport(6, password, 10);
    await qualityService.concludeComplaint(6, { outcome: "Justified", summary: "Confirmed", password: "pw" }, 10);
    await qualityService.closeComplaint(6, password, 10);
    await qualityService.cancelComplaint(6, reason, 10);

    const posted = http.post.mock.calls.map((call) => [call[0], call[2]]);
    expect(posted).toEqual([
      ["/quality/deviations/3/assess", version(7)],
      ["/quality/deviations/3/reject", version(7)],
      ["/quality/deviations/3/lab-conclusion", version(7)],
      ["/quality/deviations/3/approve", version(7)],
      ["/quality/deviations/3/return", version(7)],
      ["/quality/deviations/3/close", version(7)],
      ["/quality/deviations/3/cancel", version(7)],
      ["/quality/capas/4/approve-plan", version(8)],
      ["/quality/capas/4/return-plan", version(8)],
      ["/quality/capas/4/verify-completion", version(8)],
      ["/quality/capas/4/effectiveness", version(8)],
      ["/quality/capas/4/cancel", version(8)],
      ["/quality/change-controls/5/sign-assessment", version(9)],
      ["/quality/change-controls/5/approve", version(9)],
      ["/quality/change-controls/5/reject", version(9)],
      ["/quality/change-controls/5/close", version(9)],
      ["/quality/change-controls/5/cancel", version(9)],
      ["/quality/change-controls/5/affected", version(9)],
      ["/quality/complaints/6/request-investigation", version(10)],
      ["/quality/complaints/6/lab-report", version(10)],
      ["/quality/complaints/6/conclude", version(10)],
      ["/quality/complaints/6/close", version(10)],
      ["/quality/complaints/6/cancel", version(10)]
    ]);
    expect(http.post.mock.calls[10][1]).toEqual({ result: "NotEffective", findings: "Still drifting", newCapaId: 9, password: "pw" });
  });

  it("saves the plans, investigations and assessments with PUT and the record version", async () => {
    http.put.mockResolvedValue(envelope({}));

    await qualityService.saveDeviationInvestigation(3, { impactAssessment: "a", rootCauseCategoryId: 2, rootCauseDescription: "b", proposedActions: "c" }, 7);
    await qualityService.saveCapaPlan(4, { actions: [], effectivenessCriteria: "c", effectivenessMethod: "m", effectivenessPlannedDate: "2027-01-10" }, 8);
    await qualityService.saveChangeControlAssessment(5, { impactSummary: "s", tasks: [], answers: null }, 9);
    await qualityService.saveComplaintInvestigation(6, { findings: "f", sampleIds: [1, 2] }, 10);

    expect(http.put.mock.calls.map((call) => [call[0], call[2]])).toEqual([
      ["/quality/deviations/3/investigation", version(7)],
      ["/quality/capas/4/plan", version(8)],
      ["/quality/change-controls/5/assessment", version(9)],
      ["/quality/complaints/6/investigation", version(10)]
    ]);
  });

  it("creates CAPAs, change controls and complaints without a record version and returns the new record", async () => {
    http.post.mockResolvedValue(envelope({ id: 21, version: 1, record: { id: 21, number: "CAPA-2026-0021" } }));

    const capa = await qualityService.createCapa({
      title: "t", description: "d", sectionId: 1, sourceRecordId: 3, links: null, ownerUserId: 8
    });
    await qualityService.proposeChangeControl({
      title: "t", description: "d", reason: "r", sectionId: 1, affected: [{ targetType: "Equipment", targetId: 4, newRecordCode: null }], ownerUserId: 8
    });
    await qualityService.receiveComplaint({
      title: "t", source: "Customer", receivedOn: "2026-11-20", externalReference: null, itemId: null, lotText: null, complaintText: "c", sectionId: null
    });

    expect(capa.record.number).toBe("CAPA-2026-0021");
    expect(http.post).toHaveBeenNthCalledWith(1, "/quality/capas", expect.objectContaining({ sourceRecordId: 3, ownerUserId: 8 }));
    expect(http.post).toHaveBeenNthCalledWith(2, "/quality/change-controls", expect.objectContaining({ reason: "r" }));
    expect(http.post).toHaveBeenNthCalledWith(3, "/quality/complaints", expect.objectContaining({ source: "Customer" }));
  });

  it("reads the user directory, the checklist template and the checklist answers", async () => {
    http.get.mockResolvedValue(envelope([]));
    const { signal } = new AbortController();

    await qualityService.listUserDirectory("Quality.View", signal);
    await qualityService.getChecklistTemplate("ChangeControl", 2, signal);
    await qualityService.listChecklistAnswers(5, 1, 100, signal);

    expect(http.get).toHaveBeenNthCalledWith(1, "/users/directory", { params: { permission: "Quality.View" }, signal });
    expect(http.get).toHaveBeenNthCalledWith(2, "/quality/config/templates/ChangeControl", { params: { sectionId: 2 }, signal });
    expect(http.get).toHaveBeenNthCalledWith(3, "/quality/5/checklist-answers", { params: { page: 1, pageSize: 100 }, signal });
  });
});
