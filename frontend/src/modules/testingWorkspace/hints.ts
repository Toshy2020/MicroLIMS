import type { Hint } from "../../components/FieldHint";

// Field hints for the pathogen session (shared TSB, reset). Wording is taken
// from guide chapter 16. Hints describe meaning and effect only.
const SHARED_TSB = { chapterId: "16-pathogen-testing", heading: "16.2 Broth Enrichment and Shared TSB Steps" };
const RESET = { chapterId: "16-pathogen-testing", heading: "16.7 Supervisor Pathogen Session Reset" };

export const hints = {
  tsbMediaLot: {
    text: "The TSB media lot for the shared enrichment. The label says the lot must have passed Growth Promotion Testing and be released.",
    guide: SHARED_TSB
  },
  incubator: {
    text: "The incubator that holds the shared TSB during enrichment. The label says it must be in service.",
    guide: SHARED_TSB
  },
  requiredTemperature: {
    text: "Read-only. The temperature each test requires for its own TSB medium. It is not entered here.",
    guide: SHARED_TSB
  },
  requiredTime: {
    text: "Read-only. The incubation time each test requires. It is not entered here.",
    guide: SHARED_TSB
  },
  reasonForReset: {
    text: "Required. Why the session is reset. A reset permanently deletes the observations, results and incubations of every test, and the reason is written to the audit trail.",
    guide: RESET
  }
} satisfies Record<string, Hint>;
