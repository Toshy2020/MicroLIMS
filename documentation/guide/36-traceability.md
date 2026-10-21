## 36. Traceability

### 36.1 The Traceability Chain

#### Purpose
The traceability chain shows the records linked to an audited record. It is part of the audit event drawer of the Audit Trail (see §35). There is no separate traceability page or menu item.

#### Who can use it
Requires the *View audit trail* permission (by default: Section Head, System Administrator).

#### How to open it
1. Search the Audit Trail (see §35.2).
2. Click **Inspect Event & Traceability Chain** on a row.
3. The chain for the record of that row is shown in the drawer.

#### What the chain shows
The chain is built from the type of record that was audited:

| Audited record | Linked records shown |
|---|---|
| Sample | Item, sampling point and department (when present), the sample, its preparation, test orders, results, reviews and electronic signatures (shown with the status **Signed**) |
| Media lot | The dehydrated material used, the media lot and its evaluations |
| Material stock | The material and its documents |
| Reference cryovial | The organism (shown as **Master Strain**) and the cryovial |
| Equipment inventory | The equipment, its documents and its status history |
| Item | The item, its preparation configuration and the samples received for it |
| Any other record | One **General** node showing the record type and number |

Each node shows its type, a title and a status. The node types that can appear are: Item, Preparation Configuration, Sample, Sampling Point, Department, Sample Preparation, Test Order, Result, Review, Electronic Signature, Material, Media, Media Evaluation, Material Document, Organism, Cryovial, Equipment, Equipment Document and Equipment Status History.

> **Note for reviewers:** Records other than sample, media, material, cryovial, equipment and item show a single "General" node; no stand-alone traceability page exists. (GAP-108)

---

### 36.2 Tracing a Sample

<!-- WF-AUDIT-001 -->
#### What you see
![SS-361: Traceability Chain View](../screenshots/ss-361-traceability-chain.png)
When you open the chain for a sample, the nodes follow the testing and review path:
1. **Sample**: the sample reference (for example "SMP-0001"), category, receiving time and status.
2. **Sample Preparation**: the confirmed preparation record.
3. **Test Order**: one node per test, with the assigned analyst (**ASSIGNED TO**), method and status.
4. **Result**: the result recorded for each test.
5. **Review**: the review of the sample, with reviewer and status.
6. **Electronic Signature**: the signature with the status **Signed**.

#### Retests and investigations
Related samples are linked in other screens, not in the chain itself: a retest sample is linked to the original sample, and all samples of the same investigation share one investigation group. Investigation documents are attached to that group and keep their replacement history (see §20).

#### How to use it
1. Open the chain from an audit row of the sample.
2. Read the nodes in order to follow who tested, reviewed and signed.
3. To see every audit entry for the sample, click **View Complete History for {entityName} #{entityId}** in the drawer (see §35.3).

---

### 36.3 Where Other Links Are Shown

The Audit Trail chain does not show reagent, solution, standard, column or instrument links of physicochemical runs, or the media lot and incubator of an incubation. These links are shown on the screens of the modules concerned:
- Test incubations show the media lot and incubator (see §15 and §16).
- Media lots show the dehydrated material, autoclave data and evaluations (see §22).
- Reference cryovials show the material, organism, identity confirmations and thaw events (see §26).
- Physicochemical runs, solutions and standards show their instruments, columns, solutions and standard lots (see §27 and §28).

<!-- Review log (Sonnet): 14 corrections; invented items removed: chains claimed for media/material/equipment/item (autoclave, reference media, stock lots, solution preparations, standard qualifications, calibration documents, specifications, assigned tests, controlled documents), class/field names (OriginSampleId, OosGroupCode), physicochemical and titrant chain claimed as part of the chain, code format examples, "cryptographic" wording; node list rewritten from the verified list; callouts added: none new (GAP-108 kept) -->
