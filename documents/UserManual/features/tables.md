# Tables, Saved Views and Export User Guide

> **Draft — not approved.** Written from code on `2026-11-17`. No screenshots were made, and the application was not run to prepare it. Your laboratory's approved SOPs take precedence over this guide.

| Field | Value |
|---|---|
| Title | Tables, Saved Views and Export User Guide |
| Version | 0.1 |
| Status | Draft |
| Author | AI coding agent, draft from code |
| Reviewer | |
| Approver | |
| Effective date | |

## 1. What you can change in a table

Many registers, such as the sample register, the materials and equipment lists, the cryovial and media lists, the document library and the Reports results, let you change:

- which columns show, and in what order;
- the row density (comfortable or compact);
- the filters and the search;
- the sort order;
- the number of rows per page (25, 50, 100 or All).

Some pages do not have these options yet. These are the Items page, the OOS Tracking page, the equipment master list, and the Media GPT and reference strain tables on the Reports page.

## 2. Sort

- Click a column heading to sort by that column.
- Click it again to reverse the order.

## 3. Columns and density

1. Click **Columns**.
2. Tick a column to show it, or untick it to hide it.
3. Move a column up or down with the keyboard controls in the menu.
4. Choose **Reset to default** to show the standard columns again.

Your columns and density stay on this browser. Anyone who uses the same browser sees the same settings. Bench mode always uses comfortable rows.

## 4. Filters, search and the web address

- Filters and the search box change which rows show.
- Changing a filter returns the table to page 1.
- The web address of the page holds the search, the sort, the page, the number of rows per page and the filters. Copy the address to send the same list to a colleague.
- The address does not hold your columns or density.
- A link does not give anyone access to rows they cannot already read. Each person sees only the rows their own access allows.

## 5. Saved views

A saved view keeps the filters, the search, the sort, the rows per page, the columns and the density.

- **Save a view:** click **Views**, then save the current view. Give it a name of up to 60 characters. You may keep up to 20 personal views for each table.
- **Open a view:** choose it from **My views** or from **Shared views**.
- **Change a view:** change the table, then choose **Save changes** for your own view. For a view that someone else owns, choose **Save a copy**.
- **Share with a section:** if you have the sharing permission, tick **Share with section** when you save. Only people in that section can see the view. By default the Section Head and the QC Manager have this permission.
- **Default view:** set a view as the default to open the table with that view. Clear the default to stop this.
- **Delete a view:** delete a view you own. You are asked to confirm first, and a deleted view cannot be recovered.
- **A view that no longer fits:** if a view names a column or a filter that was removed, the view opens with the rest of its settings. A notice says "Not applied:" and lists what was left out. Save the view again to remove the missing parts.

You cannot see a view that was shared with a section you do not belong to.

## 6. Select rows and bulk actions

1. Tick the check boxes to select rows.
2. Choose **Select all N that match** to select every row that matches the filters, not only the rows on this page.
3. Choose **Clear** to remove the selection.

Changing a filter or the search clears the selection.

Some actions appear only if you have the permission for them. On the receiving and testing workspace sample list you can assign one analyst to up to 200 selected samples at once. Any sample that cannot be assigned stays selected, and the system tells you why. The other samples are assigned.

## 7. Export to CSV

1. Choose **Export CSV** to download the rows of the table. In the sample register the button reads **Export selected** when rows are selected.
2. You can export up to 5,000 rows at a time.

You need the export permission. By default this is the Section Head, the Reviewer and the QC Manager.

- Every export is recorded with your name, the time with its time zone, the table, the filters and the rows. The file is made only after the record is saved.
- The file starts with these lines: who exported it, when, the list, the filters, the number of rows and the export number. The column headings and the rows follow.
- A cell that starts with `=`, `+`, `-`, `@` or a tab gets a single quote (') in front of it, so that a spreadsheet does not run it as a formula.
- The file is UTF-8 with a byte order mark, so text such as Arabic is kept.

## 8. Long lists

- When a table has more than 100 rows, only the rows you can see are drawn. Scroll to see the others.
- The column headings stay at the top while you scroll.

## 9. What this draft does not cover

- The options in section 1 are not yet on every page.
- The exact look of the menus was not checked on screen for this draft.
