## 41. Integrations

### 41.1 External Systems and Instrument Interfaces

#### Status of external integrations
MicroLIMS works on its own. The following are not available in the application:
- **Instrument report import**: there is no screen that reads electronic instrument reports (for example chromatography or ICP reports) into MicroLIMS. Instrument results, system suitability values and calibration values are entered by the analyst in the testing workspaces.
- **ERP / SAP**: MicroLIMS has no connection to SAP or other enterprise resource planning systems.
- **Live instrument data feed**: instruments do not send data to MicroLIMS directly.
- **AI assistant**: MicroLIMS has no AI assistant.

> **Note for reviewers:** Report-reading code exists in the system but nothing in the application uses it. (GAP-031)

> **Note for reviewers:** No SAP or ERP integration was found. (GAP-032)

<!-- Review log (Sonnet): 3 corrections; invented items removed: "item release workflows / material receiving maintained in application" claim, "text extraction/parsing" wording, "bidirectional controller interfaces"; callouts added: GAP-031, GAP-032 -->
