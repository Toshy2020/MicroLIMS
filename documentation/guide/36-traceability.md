## 36. Traceability

### 36.1 End-to-End Traceability Architecture

#### Purpose
MicroLIMS establishes comprehensive, multi-directional traceability across all laboratory operations, linking sample records, preparation parameters, reagent lots, equipment configurations, test executions, secondary reviews, and final electronic authorizations.

#### Traceability Chain Viewer
Traceability relationships are visualized through the interactive **Traceability Chain** embedded within the Audit Event Details drawer (accessible via the **Inspect Event & Traceability Chain** action in the Audit Trail):
- **Dynamic Chain Resolution**: When inspecting an event, the system identifies the target entity and constructs a contextual relationship graph:
  - **Sample Records**: Resolves the complete testing and review chain: Sample -> Sample Preparation -> Test Orders -> Results -> Reviews -> Electronic Signatures ("Signed").
  - **Media Records**: Resolves the media lifecycle: Media Lot -> Material Stock -> Autoclave Equipment -> Media Evaluation Challenges -> Reference Media.
  - **Material Stock Records**: Resolves inventory usage: Material -> Stock Lots -> Consumed Solution Preparations -> Standard Qualifications.
  - **Reference Cryovials**: Resolves microbiological origin: Cryovial Batch -> Organism Master -> Identity Confirmations -> Storage Location.
  - **Equipment Records**: Resolves instrument qualification: Equipment Inventory -> Calibration Documents -> Operational Status History.
  - **Item Records**: Resolves product configuration: Item -> Specifications -> Assigned Tests -> Item Preparation Configurations -> Controlled Documents.
  - **Other Entities**: Any entity outside these primary categories displays a single **General** node indicating the entity type and record identifier.

#### Visualized Node Types
The chain visualizes nineteen distinct operational node types, including: **Item**, **PreparationConfiguration**, **Sample**, **SamplingPoint**, **Department**, **SamplePreparation**, **TestOrder**, **Result**, **Review**, **ElectronicSignature** (displayed with status **Signed**), **Material**, **Media**, **MediaEvaluation**, **MaterialDocument**, **Organism** (**Master Strain**), **Cryovial**, **Equipment**, **EquipmentDocument**, and **EquipmentStatusHistory**.

---

### 36.2 Sample and Testing Workflow Traceability

#### What you see
[SCREENSHOT SS-361]
When tracing a sample record, the traceability chain displays a structured sequential pipeline:
1. **Sample Node**: Displays the unique Sample Reference (e.g., "SMP-0001"), product category, receiving timestamp, and overall sample lifecycle status.
2. **Sample Preparation Node**: Links the sample to its confirmed preparation record, documenting diluents, neutralizers, preparation technique, preparer identity, and confirmation timestamp.
3. **Test Order Nodes**: Displays individual test orders instantiated for each assigned test code, detailing the assigned analyst (**ASSIGNED TO**), test method, and test execution status.
4. **Result and Reading Nodes**: Links test orders to raw plate counts, qualitative observation logs, or instrumental chromatographic and spectroscopic readings.
5. **Review Nodes**: Displays secondary technical verification events, recording reviewer identity, sign-off status, and scientific comments.
6. **Electronic Signature Nodes**: Confirms cryptographic authentication with signer name, role, timestamp, IP address, and signed meaning.

#### Out-of-Specification and Retest Traceability
When analytical results initiate an investigation or retest:
- **Origin Sample Linkage**: Retest samples record the original sample identifier (`OriginSampleId`), establishing bidirectional navigation between original non-conforming testing and investigation retests.
- **Investigation Grouping**: All related samples, retests, and new sampling events share a common Investigation Group Code (`OosGroupCode`).
- **Controlled Investigation Documents**: Investigation reports, root-cause analyses, and CAPA references attach directly to the investigation group, tracking version supersession and document history.

---

### 36.3 Reagent, Instrument and Standard Traceability

#### Physicochemical Analytical Chain
In analytical chemistry workspaces (HPLC, GC, ICP-OES), each test result links to an unbroken chain of physical inputs:
- **Instrument Qualification**: Results record the specific instrument asset identifier, verifying active operational status and calibration validity.
- **Chromatography Columns**: HPLC and GC runs store the unique chromatography column identifier, matching the USP column designation specified in the method master.
- **Volumetric Solutions**: Instrumental runs record mobile phase and diluent solution preparation codes (e.g., `MP-METH 01/10/2026`). Each solution preparation links back to its master recipe, measured pH, preparer, and specific chemical stock lots consumed.
- **Reference and Working Standards**: System suitability tests and sample calculations link to primary reference standard lots or in-house working standard qualification records (`WSQ-nn/MM/yyyy`).

#### Volumetric Titrant Traceability
- Titration results record the specific volumetric titrant preparation code (e.g., `VS-01/10/2026`), standardization factor, standardization timestamp, and standardization temperature.
- Titrant standardization records capture primary standard lot deductions, standardization replicates, and relative standard deviation calculations.

#### Microbiological Media and Reference Strain Chain
- Test incubations record the specific media lot number, incubator equipment identifier, incubation start time, and reading time.
- Prepared media lots link to the dehydrated media manufacturer batch, autoclave cycle parameters (load, temperature, cycle duration), and growth promotion test evaluation results.
- Growth promotion challenges record the reference cryovial batch, source lyophilized microorganism lot, and master organism catalog entry.

> **Note for reviewers:** Non-sample/media/material/cryovial/equipment/item entities display a single "General" node; no stand-alone traceability page exists. (GAP-108)
