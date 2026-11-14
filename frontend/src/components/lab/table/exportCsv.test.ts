import { describe, expect, it } from "vitest";
import { buildCsv, neutralise } from "./exportCsv";

describe("exportCsv", () => {
  it("neutralise prefixes cells starting with = + - @ tab CR and leaves others", () => {
    expect(neutralise("=A1")).toBe("'=A1");
    expect(neutralise("+SUM(A1)")).toBe("'+SUM(A1)");
    expect(neutralise("-2024/15")).toBe("'-2024/15");
    expect(neutralise("@cmd")).toBe("'@cmd");
    expect(neutralise("\tcmd")).toBe("'\tcmd");
    expect(neutralise("\rcmd")).toBe("'\rcmd");
    expect(neutralise("EQ-001")).toBe("EQ-001");
    expect(neutralise("Batch 2024-01")).toBe("Batch 2024-01");
    expect(neutralise("")).toBe("");
  });

  it("quotes, commas, newlines are escaped", () => {
    const csv = buildCsv(
      [],
      ["Code", "Notes"],
      [['EQ-001', 'Line 1, "quoted"\nLine 2']]
    );
    expect(csv).toBe('\uFEFFCode,Notes\r\nEQ-001,"Line 1, ""quoted""\nLine 2"\r\n');
  });

  it("output starts with BOM and uses CRLF", () => {
    const csv = buildCsv(
      [["Exported by", "Ada"]],
      ["Code", "Status"],
      [["EQ-001", "Active"]]
    );
    expect(csv.startsWith("\uFEFF")).toBe(true);
    expect(csv).toBe(
      "\uFEFFExported by,Ada\r\n\r\nCode,Status\r\nEQ-001,Active\r\n"
    );
  });

  it("columns follow visible order", () => {
    const csv = buildCsv(
      [],
      ["Status", "Code"],
      [["Active", "EQ-001"]]
    );
    expect(csv).toBe("\uFEFFStatus,Code\r\nActive,EQ-001\r\n");
  });

  it("Arabic text round-trips", () => {
    const arabic = "عينة رقم ١٢٣ - مختبر الميكروبيولوجي";
    const csv = buildCsv([], ["Code", "Name"], [["SMP-1", arabic]]);
    expect(csv).toBe(`\uFEFFCode,Name\r\nSMP-1,${arabic}\r\n`);
  });
});
