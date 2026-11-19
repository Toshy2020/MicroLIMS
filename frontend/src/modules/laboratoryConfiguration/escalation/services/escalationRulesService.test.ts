import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiClient } from "../../../../services/apiClient";
import {
  type EscalationRuleDto,
  escalationRulesService,
  formatTriggerLabel
} from "./escalationRulesService";

vi.mock("../../../../services/apiClient", () => ({
  apiClient: {
    get: vi.fn(),
    post: vi.fn()
  }
}));

function makeRule(over: Partial<EscalationRuleDto> = {}): EscalationRuleDto {
  return {
    id: 1,
    sectionId: 10,
    sectionName: "Microbiology",
    trigger: "ReviewWaiting",
    thresholdWorkingHours: 8,
    notifyRoleType: "SectionHead",
    isActive: true,
    createdByUserId: 5,
    createdByName: "Head One",
    createdAt: "2026-11-15T06:00:00Z",
    retiredAt: null,
    retiredByUserId: null,
    retiredByName: null,
    replacesRuleId: null,
    ...over
  };
}

describe("escalationRulesService", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("lists rules via GET /escalation-rules with sectionId, includeRetired, and AbortSignal", async () => {
    const controller = new AbortController();
    const expected = [makeRule()];
    vi.mocked(apiClient.get).mockResolvedValue({ data: { data: expected } });

    const actual = await escalationRulesService.list({ sectionId: 10, includeRetired: true }, controller.signal);

    expect(apiClient.get).toHaveBeenCalledWith("/escalation-rules", {
      params: { sectionId: 10, includeRetired: true },
      signal: controller.signal
    });
    expect(actual).toEqual(expected);
  });

  it("creates, replaces, and retires rules against the exact controller routes", async () => {
    const created = makeRule({ id: 2 });
    const replaced = makeRule({ id: 3, thresholdWorkingHours: 4, notifyRoleType: "QcManager", replacesRuleId: 2 });
    vi.mocked(apiClient.post)
      .mockResolvedValueOnce({ data: { data: created } })
      .mockResolvedValueOnce({ data: { data: replaced } })
      .mockResolvedValueOnce({ data: { data: { retired: true } } });

    const resCreate = await escalationRulesService.create({
      sectionId: 10,
      trigger: "ReviewWaiting",
      thresholdWorkingHours: 8,
      notifyRoleType: "SectionHead"
    });
    expect(apiClient.post).toHaveBeenNthCalledWith(1, "/escalation-rules", {
      sectionId: 10,
      trigger: "ReviewWaiting",
      thresholdWorkingHours: 8,
      notifyRoleType: "SectionHead"
    });
    expect(resCreate).toEqual(created);

    const resReplace = await escalationRulesService.replace(2, {
      thresholdWorkingHours: 4,
      notifyRoleType: "QcManager"
    });
    expect(apiClient.post).toHaveBeenNthCalledWith(2, "/escalation-rules/2/replace", {
      thresholdWorkingHours: 4,
      notifyRoleType: "QcManager"
    });
    expect(resReplace).toEqual(replaced);

    await escalationRulesService.retire(3);
    expect(apiClient.post).toHaveBeenNthCalledWith(3, "/escalation-rules/3/retire");
  });

  it("formats trigger labels in human-readable form", () => {
    expect(formatTriggerLabel("ReviewWaiting")).toBe("Review waiting");
    expect(formatTriggerLabel("ApprovalWaiting")).toBe("Approval waiting");
    expect(formatTriggerLabel("ReturnedNotCorrected")).toBe("Returned test not corrected");
    expect(formatTriggerLabel("IncubationNotRead")).toBe("Incubation not read");
    expect(formatTriggerLabel("TestingNotStarted")).toBe("Testing not started");
    expect(formatTriggerLabel("DocumentTaskOverdue")).toBe("Document task overdue");
  });
});
