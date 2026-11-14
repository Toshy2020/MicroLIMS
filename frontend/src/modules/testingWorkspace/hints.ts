import type { Hint } from "../../components/FieldHint";

// Field hints for the pathogen session (shared TSB, reset). Wording is taken
// from guide chapter 16. Hints describe meaning and effect only.
const SHARED_TSB = { chapterId: "16-pathogen-testing", heading: "16.2 Broth Enrichment and Shared TSB Steps" };
const RESET = { chapterId: "16-pathogen-testing", heading: "16.7 Supervisor Pathogen Session Reset" };

export const hints = {
  tsbMediaLot: {
    text: "The TSB media lot used for the shared enrichment.",
    guide: SHARED_TSB
  },
  incubator: {
    text: "The incubator that holds the shared TSB during enrichment.",
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
    text: "Required. Why the session is reset. A reset deletes the observations, results and incubations of every test in the session and returns the tests to their starting step. The reset and its reason are written to the audit trail.",
    guide: RESET
  }
} satisfies Record<string, Hint>;
