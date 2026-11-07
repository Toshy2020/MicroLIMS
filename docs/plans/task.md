# Task Progress Tracker

| Task ID | Description | Status | Evidence / Notes |
|---|---|---|---|
| TASK-01 | HPLC method editor: Cleaning residue mode, item picker, extraction vol, swab area, MACO + unit | Completed | HplcGeneralSection, HplcAnalytesSection, HplcMethodDialog, hplcMethodForm, hplcMethodValidation, HplcMethodTable |
| TASK-02 | ICP test elements editor: Cleaning limit (ppm) column per element, mapping | Completed | IcpTestFields.tsx & icpTestConfig.ts updated with cleaningLimitPpm |
| TASK-03 | After-cleaning part config: AcPanels hide chemistry limit & show caption for HPLC/ICP | Completed | AcPanels.tsx detects isMethodLimit, shows captions, sends no limit payload |
| TASK-04 | HPLC/ICP workspace sample rows: show subjectName under sample reference | Completed | Added subjectName in EligibleSampleTable, SampleAssignmentPanel, HplcSampleEntryPage, IcpEligibleSampleDialog, IcpSampleAssignmentPanel, IcpSampleEntryPage |
| TASK-05 | Component tests & verification (tsc & vitest) | Completed | tsc clean, 308/308 tests pass in vitest across 57 test files |
