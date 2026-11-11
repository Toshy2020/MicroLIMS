# C8 — Navigation: Design Specification

**Roadmap package:** C8 (`improvement-roadmap.md`)
**Status:** Approved by the user 2026-11-11
**Date:** 2026-11-11
**Builds on:** C6 (per-user interface state), C3 (saved views), C1 (typed record targets).

## 1. Purpose

Get users to their usual places in one click, show where they are on deep pages, open the laboratory and tab they last used, and always say why an action is unavailable.

## 2. Decisions (confirmed by the user, 2026-11-11)

| # | Decision | Answer |
|---|---|---|
| 1 | Favourites | Pages and saved views; up to 15; kept on the server |
| 2 | Recently opened | The last 20 records the user opened; kept on the server |
| 3 | Unavailable actions | The button stays visible and disabled, with the reason, in the server's own words |
| 4 | Last used | Last laboratory and last tab per page, remembered on the server |

## 3. Out of scope

- Reordering or hiding items of the main menu.
- Pinning individual records as favourites.
- Recording which pages a user visits.
- Gating reasons for every action in the application in one release (5.4 lists the first set).

## 4. Current state (from code, not run)

- The menu is built from `menuConfig.ts` by permission and laboratory membership, two levels deep. No favourites, no recents.
- Ctrl+K searches eight record types. Only past search words are remembered, in the browser.
- `AppBreadcrumbs` exists and derives the trail from the menu, so a page that is not a menu entry (one sample, one item, one document) shows only its parent.
- The laboratory tab comes from `?lab=` with a fixed default order (Microbiology first). Tabs inside pages are not remembered.
- Unavailable actions are handled differently on each screen: some hidden, some disabled without explanation, some refused only after the click.
- The rules that refuse an action live in the services and throw a message when they fail. There is no way to ask "may I, and if not, why?" without trying.

## 5. Design

### 5.1 Favourites

- A star on every page header and on every saved view adds or removes a favourite.
- Favourites appear in a "Favourites" section at the top of the sidebar (under My Work) and at the top of Ctrl+K when the search box is empty.
- Up to 15. Reordered with Move up / Move down in a small "Manage favourites" dialog; no dragging needed.
- A favourite the user can no longer open (permission removed, shared view withdrawn) is shown greyed with "No longer available" and a Remove button; it never produces an error page.
- Stored in the per-user interface state from C6.

### 5.2 Recently opened

- When a user opens a sample, test, item, document, equipment record, stock lot, media lot or solution preparation, it is added to their list. The list keeps the 20 newest, one entry per record.
- Shown in Ctrl+K when the search box is empty ("Recent"), and as a collapsible sidebar section showing the newest 5.
- The server writes the label itself after checking the user may read the record, and checks again when the list is shown: a record the user can no longer read disappears from the list.
- This is a convenience list, not an access log; the audit trail and document access logs are unchanged. A user can clear their list.

### 5.3 Where am I

- Deep pages add their own last crumb: "Items › PARA-500 Paracetamol 500 mg", "Document Library › SOP-QC-014".
- A check in the test suite lists every route with a parameter and fails if one has no crumb.
- The browser tab title follows the same trail.

### 5.4 Why is this unavailable

- One shared component for gated actions: the button stays where it is, disabled, with a short reason shown as text beside it (not only in a tooltip), linked for screen readers.
- **The reason comes from the server.** For each record the screen asks which actions are available; the answer lists every action with allowed / not allowed and the reason. The reason is produced by the same check that would refuse the action, so the two cannot disagree.
- The server check still runs when the action is performed. The list is a display aid only.
- Reasons are plain sentences: "You performed a test on this sample, so you cannot review it.", "All tests must be completed before review.", "Your role does not include approving samples.", "This media lot has not passed growth promotion testing."
- First release covers the actions users ask about most: complete review, return to analyst, approve / reject, void a sample, assign an analyst, take / release a test (C1), release a media lot, approve a document revision. Other screens adopt the component later without server changes of the same size.
- An action that does not belong on a screen at all is not added to it just to be shown disabled.

### 5.5 Last laboratory and tab

- The last selected laboratory, and the last tab on each tabbed page, are stored per user on the server and applied the next time, on any computer.
- An explicit link wins: a URL that names a laboratory or tab is always respected.
- If the remembered laboratory or tab is no longer available to the user, the page default is used.

## 6. Error handling

| Situation | Behaviour |
|---|---|
| Interface-state store unavailable | Favourites and last-used fall back to this browser; nothing blocks |
| Recents cannot be loaded | The section is hidden; search works |
| Actions list cannot be loaded | Buttons stay enabled and the server decides on click, as today; a small notice says reasons are unavailable |
| Favourite or recent no longer accessible | Greyed or removed as described; no error page |

## 7. Testing

- **Backend `[PostgresFact]`:** recents add, de-duplicate, cap at 20, per user only, access re-check on write and on read, clear; each covered action's availability for allowed and for each refusal reason, and **the reason equals the message thrown when the action is attempted** in the same state; availability endpoint never changes data.
- **Frontend (`userEvent`):** star toggles; 16th favourite refused with explanation; reorder by keyboard; unavailable favourite; favourites and recents in the sidebar and in Ctrl+K with an empty query; crumb on each deep page; every parameterised route has a crumb; gated button disabled with visible reason and `aria-describedby`; fallback when the actions call fails; last laboratory and tab restored; URL overrides stored value.
- **Manual:** keyboard-only pass of the sidebar additions; screen reader reads the reason of a disabled action.

## 8. Delivery stages

1. Favourites. 2. Recently opened. 3. Last laboratory and tab. 4. Crumbs on deep pages. 5. Action availability on the server for the first set. 6. Gated-action component on those screens. 7. Documents.

## 9. Open items

1. Approval needed before building: treat the recently-opened list as convenience data outside automatic audit capture (it is not an access log).
2. The exact wording of each reason sentence, drafted during implementation for your review.
