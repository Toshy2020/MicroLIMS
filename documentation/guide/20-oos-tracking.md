## 20. OOS Tracking

*(Draft — pending approval; updated for structured OOS & Excursion investigations)*

MicroLIMS automatically opens a structured OOS investigation when a reviewer signs a section review that has one or more out-of-specification or detected results, blocks sample approval and certificates until the investigation is concluded, controls retests through a signed **Retest Plan**, tracks separate **Excursion** reviews for alert/action limit exceedances, and preserves legacy retest chains under the **Legacy** tab.

### 20.1 OOS Investigation Register
<!-- WF-OOS-001 -->
#### Purpose
View open and overdue investigations, concluded investigations, excursion reviews, and legacy OOS retest chains.

#### Who can use it
Requires the *OOS View* permission (`Oos.View`). Analysts without *OOS Manage* see only investigations containing a test they performed; users limited to specific laboratory sections see only investigations in their readable sections. The **Legacy** tab requires *OOS Manage* (`Oos.Manage`).

#### What you see
- In the sidebar group **QUALITY & COMPLIANCE**, the entry **OOS Tracking** (`/oos-tracking`).
- Title **Out-of-Specification Tracking** and subtitle "Out-of-specification investigations, excursion reviews, and legacy retest chains."
- Five tabs:
  - **Open**: open OOS investigations (`PhaseOne`, `AwaitingRetest`, `PhaseTwo`).
  - **Overdue**: open investigations past their Phase I or total due date.
  - **Concluded**: concluded investigations (`Confirmed`, `Invalidated`, `ExcursionClosed`).
  - **Excursions**: excursion reviews (`Kind = Excursion`).
  - **Legacy**: legacy OOS groups (retest chains with an OOS group code and no structured investigation row), with **Upload Investigation** / **Manage Investigation** document controls (§20.5).
- Filters for **Section**, **Status**, **Opened from**, **Opened to**, and **Clear filters**.
- A paged register table with columns **Investigation #**, **Kind**, **Status**, **Sample**, **Section**, **Test(s)**, **Opened**, **Due Date**, **Conclusion**, and **Actions** (**Open**).

---

### 20.2 Structured OOS Investigation Workflow
<!-- WF-OOS-002 -->
#### Opening & Gates
- **Automatic opening:** Completing a section review with one or more failing (`OutOfSpecification` or `Detected`) results opens a new `OOS{MM}{yy}{seq:D3}` investigation (or joins an existing open investigation) in the same transaction, snapshots the section's checklist template, and computes working-day due dates.
- **Manual opening:** If a reviewed failing section has no investigation, the **Sample Summary** approval card displays an OOS banner with **Open investigation** (requires `Oos.Manage`).
- **Approval & Certificate Gates:** While an OOS investigation is open, **Approve**, **Reject**, and Certificate of Analysis issuance are blocked with the investigation number and link. Direct **Retest Retained Sample** and **New Sample Required** options are hidden whenever an OOS investigation covers the section.

#### Phase I — Laboratory Investigation
1. **Analyst Statements (`Oos.Contribute`):** An analyst who performed an investigated test records an **Analyst statement** (1–4000 characters).
2. **Phase I Checklist & Pre-filled Facts:** Authorized users (`Oos.Contribute`) answer each active Phase I checklist item (**Yes**, **No**, **Not Applicable**) with an optional comment (up to 1000 characters) while reviewing the **Pre-filled Laboratory Facts** panel (equipment calibration status, media/solution lots, reference standards, incubations, and result audit counts).
3. **Phase I Assessment & Signature (`Oos.Manage`):** An independent Section Head (who performed none of the investigated tests) signs **Sign Phase I**:
   - **Laboratory Error Confirmed:** Requires an active assignable cause category (not "No cause found") and description; marks investigated result links as invalidated and moves the investigation to **Awaiting Retest**.
   - **No Laboratory Error Found:** Moves the investigation to **Phase II**.
   - Phase I cannot be signed if the laboratory's Phase I checklist template is empty.

#### Retest Plan (`Oos.Manage`)
- Available in **Awaiting Retest** or **Phase II** after Phase I is signed.
- **Save Draft Plan:** Choose **Source** (**Retained Sample** or **New Sample**), **Number of Retests (1–10)**, **Tests to Retest**, **Assigned Analysts** (one per sample; for **New Sample**, original performers cannot be assigned), **Reportable retest** (required when more than one retest is planned, naming which retest provides the reportable result on the origin Certificate of Analysis), and a **Judging Rule** (1–4000 characters). Amendments (Revision > 1) require an **Amendment Reason** and may only name one of the amendment's own retest samples as reportable.
- **Sign & Create Retest Samples:** An independent Section Head (not in the analyst list and not a performer of any investigated/retest test) signs the plan and creates the linked retest samples, binding the reportable sample. If a planned retest sample later fails at review, it joins the investigation and moves **Awaiting Retest** to **Phase II**. On the origin Certificate of Analysis, the highest signed revision governs and only the named reportable retest result is reported (with the retest plan signer manifestation); the investigation PDF report lists all retest samples across all revisions with their results and marks the reportable sample.

#### Phase II & Signed Conclusion
- **Phase II Entries (`Oos.Contribute` or `Oos.Manage`):** Record **Review Outside the Laboratory**, **Hypotheses & Outcomes**, **Investigation Notes**, and **Phase II Checklist** answers.
- **Sign & Conclude (`Oos.Manage`):** An independent Section Head (who performed no investigated or retest test) signs **Sign & Conclude** once all active Phase II items are answered and all planned retest samples have completed review:
  - **OOS Invalidated (Assignable Cause):** Requires a signed retest plan with created samples and an assignable cause category other than "No cause found"; marks the origin sample's investigated orders superseded and result links invalidated. Origin approval is allowed once all planned retest samples are **Approved**.
  - **OOS Confirmed:** Allowed with any cause category (including "No cause found"); reverses any prior origin invalidation/supersession flags and keeps **Approve** blocked (**Reject** is allowed).
- **Reopen Investigation (`Oos.Manage`):** While the origin section is not yet **Approved** or **Rejected**, an independent Section Head can reopen a concluded investigation with a written reason and electronic signature, preserving the prior conclusion in **Reopening History** and returning to **Phase II**.

#### QA Deviation Requests & Export
- **QA Deviation Requests:** Users with `Oos.RequestDeviation` (QA) can click **Request Deviation** on any investigation; Section Heads (`Oos.Manage`) can **Address Request**. Open requests do not block conclusion.
- **Export PDF:** Downloads a signed investigation report PDF with full signature manifestations (printed name, UTC timestamp with zone, meaning) and logs the export.

---

### 20.3 Excursion Reviews
- **Automatic & Manual Opening:** Action-limit exceedances (and alert-limit exceedances when enabled on the section's Excursion template) open or join an **Excursion** review at sample review unless the sample belongs to a monitoring plan occurrence. Users with `Oos.Contribute` or `Oos.Manage` can also open an excursion review from a control chart point (**Open excursion review**).
- **No Approval Block:** Excursion reviews are tracked separately under the **Excursions** tab and never block sample approval or certificates.
- **Closure (`Oos.Manage`):** Once all active Excursion checklist items are answered, an independent Section Head closes the excursion with cause category, cause description, action taken, and electronic signature (**Sign & Close Excursion**).

---

### 20.4 OOS & Excursion Templates (`/laboratory-configuration/oos-templates`)
- **Checklist Templates (`Oos.Manage`, Configure scope):** Select a **Laboratory Section** and **Investigation Kind** (**Oos** or **Excursion**), set optional **Phase I due (working days)** and **Total due (working days)** targets, toggle **Alert limit opens excursion review** (for Excursion), and add, deactivate, or reorder checklist items (buttons or `Alt+Up` / `Alt+Down`). Templates ship empty and must be configured before Phase I can be signed.
- **Cause Categories (`Oos.Manage` + `MasterData.Manage`):** Add, rename, or deactivate shared cause categories with an audited reason. The fixed **No cause found** category cannot be renamed or deactivated.

---

### 20.5 Legacy Laboratory Investigation Documents
For legacy OOS groups on the **Legacy** tab, authorized users (`Oos.Manage`) can upload, download, and supersede PDF or image (JPG, JPEG, PNG, WEBP, TIFF, up to 25 MB) investigation reports via the **Lab Investigation Documents** dialog.
