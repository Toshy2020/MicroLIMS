## 9. Notifications, Messages and Discussions

<!-- WF-NOTIF-001 -->
### 9.1 Notifications
#### Purpose
Tell you about conditions that need attention: media lots that are expiring, incubations that are ready, samples waiting for review or approval, and tests returned to you for revision.

#### Who can use it
Every signed-in user. You only receive the notifications that apply to you:

| Notification | Who receives it |
|---|---|
| Review waiting ("{n} sample(s) awaiting review.") | Users with the Reviewer, Section Head, or System Administrator base role type |
| Approval waiting ("{n} sample(s) awaiting approval.") | Users with the Section Head or System Administrator base role type |
| Preparation configurations waiting ("{n} preparation configuration(s) awaiting approval.") | Users with the Section Head or System Administrator base role type |
| Test returned for revision ("Test {code} for sample {ref} was returned for revision.", followed by ": {reason}" when a reason was given) | The analyst assigned to the test |
| Media expiry ("{material} (Lot {lot}) has expired." or "... expires {dd-MMM-yyyy}.") and incubation ready ("{test} ({step}) for {sample or test order} is ready.") | Users who are assigned to the laboratory section concerned |

#### What you see
- The **Notifications** bell in the header, with a number badge for unread notifications.
- Clicking the bell opens a list of your 30 newest notifications. Each item shows the message and the date and time. Unread items are in bold.
- The button **Mark all as read** at the top of the list (disabled when nothing is unread).
- When there is nothing to show: "No notifications" and "You are up to date - nothing needs your attention."
- When the list cannot be loaded: "Notifications could not be loaded". The list is then retried automatically.
- The Analyst dashboard and the laboratory dashboards show some of the same notifications (see Section 8).

[SCREENSHOT SS-091]

#### How to use it
1. Click the **Notifications** bell in the header.
2. Read the list.
3. Click a notification. It is marked as read, and if it belongs to a sample or test, the matching record opens.
4. To mark everything as read at once, click **Mark all as read**.

#### System behavior
- The system works out the notifications from the current data: media lots that expire within 7 days or are already expired, completed incubations ready to read, samples waiting for review or approval, preparation configurations waiting for approval, and tests returned to the analyst in the last 12 hours.
- A new condition can take up to 5 minutes to show up in the list.
- If the same text was already stored for you within the last 12 hours, it is not stored again. Because a waiting count is part of the text, a changed count creates a new notification.
- An expired media lot also produces an e-mail alert when the system is set up to send e-mail.
- A read notification cannot be marked unread again.

> **Note for reviewers:** Alert e-mails are sent to an address built from the username, not to the e-mail address on the user profile, and the alert is marked as sent even when no mail server is set up. (GAP-104)

> **Note for reviewers:** The review-waiting and approval-waiting notifications are based on the review and approval status of each section, which differs from the statuses described in the specification. (GAP-103)

---

<!-- WF-MSG-001 -->
### 9.2 Direct and Group Messages
#### Purpose
Exchange messages with colleagues, one to one or in groups.

#### Who can use it
Every signed-in user. The *Messaging Use* permission has no effect.

#### Before you start
Recipients must be active users. Only active users appear in the recipient list.

#### What you see
Open **Messages** from the account menu in the header (it shows **Messages ({n} unread)** when messages are unread). The screen is titled **Direct & Group Messages** and has two areas:
- **Conversation list** (left): the field **Filter conversations...**, the button **New Message**, and your conversations, each with the participant name, the last message, the time, and an unread badge. Empty: "No conversations yet. Start a new message!" or "No matching conversations found."
- **Chat window** (right): the messages and the box "Write a message... (Press Enter to send)". Before you choose a conversation: "Select a conversation" and "Choose a conversation from the list or start a new message." In an empty conversation: "No messages in this conversation yet." and "Say hello to start the conversation!"

[SCREENSHOT SS-092]

#### How to use it
To start a conversation:
1. Click **New Message**.
2. Choose **Direct Message (1-on-1)** or **Group Conversation**.
3. For a group, type a **Group Title** (for example "e.g. Microbiology Team, Water Testing Group...").
4. In **Select Recipient** (or **Select Participants** for a group), search for colleagues by name ("Search colleagues by name...").
5. Type the **Initial Message**.
6. Click **Send Message** (**Sending...** while processing).

To reply: choose the conversation, type in the message box, and press Enter.

#### System behavior
- If you start a direct message with someone you already have a direct conversation with, the existing conversation is used.
- Every other participant receives a notification: New message from {sender}: "{first 80 characters}...".
- A conversation is marked as read each time its messages are shown in the chat window. The unread count in the account menu is refreshed every 60 seconds while the browser tab is visible.
- Only the participants of a conversation can read it or write in it.
- Sending a message does not need an electronic signature. Messages are not recorded in the audit trail.

#### Validation
- "Please select at least one recipient."
- "Group conversation title is required."
- "Initial message cannot be empty." and "Initial message is required."
- "Message content cannot be empty."

#### Errors
- "Failed to send message."
- "Failed to create conversation."
- "You are not a participant in this conversation."
- "The user directory could not be loaded, so no recipients can be listed. Close and reopen this dialog to try again."

---

<!-- WF-DISC-001 -->
### 9.3 Discussions
#### Purpose
Share questions, decisions, and knowledge with colleagues in posts and comments, sorted into categories.

#### Who can use it
Every signed-in user. The *Discussions Viewing* and *Discussions Creation* permissions have no effect. Authors can always edit and delete their own posts and comments. Marking a post important, editing or deleting other people's posts, and editing other people's comments needs the *Discussions Moderation* permission (by default: System Administrator, Section Head).

#### What you see
- The screen **Discussions & Knowledge Sharing** (open **Discussions** from the account menu) with the button **New Discussion**.
- Filters: the search field "Search discussions by title or content...", the switch **Important Only**, and the category buttons **All Categories**, **Water**, **Equipment**, **Environmental Monitoring (EM)**, **Products**, **Media / Materials**, **Internal Decisions**, **Management Requirements**, **EDA Requirements**, **ISO 17025**, **GMP / Regulatory**, and **Other**.
- Each post shows the author, role, date, category, the number of comments, and the number of attachments. The labels **Important** and **v{n} Edited** can appear. The **Post actions** menu (for the author and for users with the moderation permission) offers **View / Edit**, **Mark Important** / **Unmark Important**, and **Delete Discussion**.
- When nothing is found: "No discussions found" with "Try adjusting your search query or category filters." (or "Be the first to start a conversation in this category.") and the button **Start a Discussion**.
- The post page, with **Back to Discussions**, the full text, the attachments (downloadable), **Edit Post**, and the comments section.

[SCREENSHOT SS-093]

#### How to use it
To create a post:
1. Click **New Discussion**.
2. Enter a **Title** (required).
3. Choose a **Category**.
4. Optionally tick **Mark as Important (Pinned at top)**.
5. Enter the text in **Content / Details** (required).
6. Optionally click **Choose Files** under **Attachments (Documents, Images, Worksheets)**. You can attach up to 5 files of up to 10 MB each. The allowed types are .pdf, .png, .jpg, .jpeg, .gif, .doc, .docx, .xls, .xlsx, .pptx, .txt, and .csv.
7. Click **Publish Discussion** (**Publishing...** while processing).

To comment: open a post, write in "Write a comment or response to this discussion...", and click **Post Comment**. You can **Edit comment** or **Delete comment** on your own comments.

To edit a post: open it, click **Edit Post**, change the fields, and click **Save Changes**.

To delete a post or a comment: use **Delete Discussion** or **Delete comment** and confirm with **Delete Post** or **Delete Comment**.

#### System behavior
- Each saved edit raises the version shown as **v{n} Edited**. Clicking it shows the revision history. If someone else changed the post in the meantime, your edit is refused.
- A deleted post or comment disappears from the screen but is not erased.
- The system checks the content of attached files against their file type.
- Discussions are not part of a sample or test record.

> **Note for reviewers:** Some categories (Internal Decisions, Management Requirements, GMP / Regulatory) suggest that discussions may record decisions. Whether a discussion may be cited as a quality decision has not been confirmed.

#### Validation
- "Discussion title is required." / "Title is required."
- "Discussion content is required." / "Content is required."
- "Comment content cannot be empty."
- Attachment problems (too many files, empty file, too large, wrong type, damaged file) are refused with a message that names the problem.

#### Errors
- "You are not authorized to edit this discussion post." / "You are not authorized to modify this discussion post."
- "You are not authorized to delete this discussion post."
- "You are not authorized to edit this comment."
- "Failed to create discussion." / "Failed to update discussion."
- "Failed to toggle important status." / "Failed to delete discussion post." / "Failed to post comment." / "Failed to save comment." / "Failed to delete comment."
- Confirmations: "Marked as important", "Unmarked as important", "Discussion post deleted.", "Comment added", "Comment updated", "Comment deleted".

<!-- Review log (Sonnet): 18 corrections; invented items removed: the eleven discussion categories (replaced by the real list), "read all" button label (is "Mark all as read"), severity icon and unread dot, "supported formats include", wrong GAP-009 citations (messages/discussions), "moderation authority needed to pin" claim, "real-time" claim, raw notification type names; callouts added: GAP-103, GAP-104, discussion-decision note -->
