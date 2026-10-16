## 9. Notifications, Messages and Discussions

<!-- WF-NOTIF-001 -->
### 9.1 In-App Notifications
#### Purpose
Alert laboratory personnel to time-sensitive operational conditions, completed incubations ready for observation, expiring media lots, samples awaiting review or release, and tests returned for revision.

#### Who can use it
Any authenticated user. The items delivered depend on your assigned base role type and laboratory section memberships:
- **ApprovalWaiting** and **PendingPreparationConfigApproval**: Delivered to users with role type `SectionHead` or `SystemAdministrator`.
- **ReviewWaiting**: Delivered to users with role type `Reviewer`, `SectionHead`, or `SystemAdministrator`.
- **TestReturnedForRevision**: Delivered directly to the analyst assigned to the returned test order.
- **MediaExpiry** and **IncubationReady**: Delivered to all users who have active membership in the relevant laboratory section.

#### What you see
- In the global header, the **Notifications** bell icon displays an unread counter badge.
- Clicking the bell opens the notifications panel:
  - Header showing unread count and an action button or link to **read all**.
  - A scrollable list of the 30 newest notifications. Each item includes a severity icon (info, warning, error), notification message, relative timestamp, and an unread indicator dot.
  - Items linked to specific sample records, test orders, or media lots display an indicator to open the underlying record directly.
  - Empty state: `"No notifications"` with subtitle `"You are up to date - nothing needs your attention."`

[SCREENSHOT SS-091]

#### How to use it
1. Click the **Notifications** bell icon in the header.
2. Review the list of notifications.
3. Click an individual notification to navigate directly to the affected record (e.g. clicking an incubation notification opens that sample's test workflow). Clicking marks the notification as read.
4. To dismiss all pending unread indicators at once, click **read all**.

#### System behavior
- **Dynamic Notification Evaluation**: The notification service computes current conditions from active database records:
  - **Media Expiry**: Identifies active media lots expiring within 7 days ("... expires {dd-MMM-yyyy}.") or already expired ("{material} (Lot {lot}) has expired." with error severity).
  - **Incubation Complete**: Identifies test orders whose incubation time window has elapsed ("{testCode} ({step}) for {sample or test order} is ready.").
  - **Review Queue**: Samples whose tests are complete and await technical verification.
  - **Approval Queue**: Samples that have passed technical review and await Section Head release authorization.
  - **Returned Tests**: Tests returned by a reviewer for revision within the past 12 hours.
- **De-Duplication**: If a notification with the exact same text was generated for that user within the preceding 12 hours, the system suppresses duplicate creation.

> **Note for reviewers:** Alert e-mails go to `{username}@microlims.local`, not the e-mail on the profile, and are marked sent even if no mail server is set up. (GAP-104)

> **Note for reviewers:** Review/approval waiting notifications use section sign-off status, not the status described in the specification. (GAP-103)

#### Errors
- If notifications fail to load from the server, the popover displays: `"Notifications could not be loaded"`.

---

<!-- WF-MSG-001 -->
### 9.2 Direct and Group Messages
#### Purpose
Enable real-time communication between laboratory colleagues, allowing analysts, reviewers, and Section Heads to coordinate testing activities and resolve bench-level questions.

#### Who can use it
Any authenticated user. The *Messaging use* permission (`Messages.Use`) is declared in the system catalog but is not enforced in code.

#### Before you start
Colleagues must have an active user account in the system directory to be selected as recipients.

#### What you see
- Page route: `/messages` (accessible from the user account menu).
- Two-pane layout:
  - **Left Pane (Conversations)**:
    - Search field: **Filter conversations...**.
    - Action button: **New Message**.
    - List of existing direct and group threads displaying participant names, last message snippet, timestamp, and unread badges.
    - Empty states: `"No conversations yet. Start a new message!"` or `"No matching conversations found."`
  - **Right Pane (Chat Window)**:
    - Thread header displaying participant names or group title.
    - Message history feed.
    - Message input box: **Write a message... (Press Enter to send)** with send action icon.
    - Empty state when no conversation is selected: `"Select a conversation"` with subtitle `"Choose a conversation from the list or start a new message."`
    - Empty state in an open conversation: `"No messages in this conversation yet."` with subtitle `"Say hello to start the conversation!"`

[SCREENSHOT SS-092]

#### How to use it
- **Starting a New Conversation**:
  1. Click **New Message** to open the dialog.
  2. Choose the conversation type: **Direct Message (1-on-1)** or **Group Conversation**.
  3. If creating a group, enter a **Group Title** (placeholder: "e.g. Microbiology Team, Water Testing Group...").
  4. In the recipient field (**Select Recipient** or **Select Participants**), type to search colleagues by name.
  5. In **Initial Message**, enter your message text.
  6. Click **Send Message** (displays **Sending...**).
- **Replying in an Existing Conversation**:
  1. Select a conversation from the left pane list.
  2. Type your reply in the text area at the bottom of the chat window.
  3. Press Enter (or click the send icon).

#### System behavior
- If you start a direct message with a user with whom you already share a 1-on-1 conversation, the system reuses the existing conversation thread.
- When a new message is posted, each other participant receives an in-app notification: `New message from {sender}: "{first 80 characters}..."`.
- Opening a conversation automatically marks all unread messages in that thread as read. The unread message count in the top header is polled every 60 seconds while the application tab remains visible.

> **Note for reviewers:** Messages are not audited and are not part of the official laboratory record. (GAP-009)

#### Validation
- If no recipient is chosen: `"Please select at least one recipient."`
- If group title is omitted: `"Group conversation title is required."`
- If initial message is blank: `"Initial message cannot be empty."` or `"Initial message is required."`
- If a message text area is submitted empty: `"Message content cannot be empty."`

#### Errors
- If sending fails: `"Failed to send message."`
- If thread creation fails: `"Failed to create conversation."`
- If an unauthorized user attempts to query a conversation: `"You are not a participant in this conversation."`
- If user directory loading fails: `"The user directory could not be loaded, so no recipients can be listed. Close and reopen this dialog to try again."`

---

<!-- WF-DISC-001 -->
### 9.3 Discussions and Knowledge Sharing
#### Purpose
Maintain an open, searchable forum for laboratory teams to discuss analytical methods, equipment issues, technical decisions, safety practices, and operational procedures.

#### Who can use it
Any authenticated user. The *Discussions viewing* (`Discussions.View`) and *Discussions creation* (`Discussions.Create`) permissions are not enforced in code. Moderation actions (marking important, editing or deleting any user's post) require the *Discussions moderation* permission (`Discussions.EditAny`), held by default by Section Head and System Administrator. Authors may always edit or delete their own posts and comments.

#### What you see
- Page route: `/discussions` (accessible from the user account menu).
- Header: **Discussions & Knowledge Sharing** and action button **New Discussion**.
- Filter Controls:
  - Search field: **Search discussions by title or content...**.
  - Toggle switch: **Important Only** (filters to pinned posts).
  - Category chips: **All Categories** plus 11 functional categories:
    - General
    - Testing Methods
    - Equipment & Instruments
    - Media & Reagents
    - Quality & Compliance
    - Safety
    - Troubleshooting
    - Training & SOPs
    - Internal Decisions
    - Management Requirements
    - GMP / Regulatory
- Post Cards:
  - Display author, post title, category chip, creation timestamp, comment count, and attachment count.
  - Indicator chips: **Important** (pinned posts) and **v{n} Edited** (showing version number).
  - Post action menu (**Post actions**): **View / Edit**, **Mark Important** / **Unmark Important**, and **Delete Discussion**.
- Discussion Detail Page (`/discussions/:id`):
  - Full post content and downloadable attachment list.
  - Revision history: clicking **v{n} Edited** displays full previous versions.
  - Comments thread with response input: **Write a comment or response to this discussion...** and button **Post Comment**.

[SCREENSHOT SS-093]

#### How to use it
- **Creating a Discussion Post**:
  1. Click **New Discussion** to open the modal.
  2. Enter a descriptive **Title** (required).
  3. Select a **Category** from the dropdown.
  4. If you have moderation authority and want to pin the topic to the top of the feed, check **Mark as Important (Pinned at top)**.
  5. In **Content / Details**, type the body of your post (required).
  6. To include reference files, click **Choose Files** under **Attachments (Documents, Images, Worksheets)**. You may attach up to 5 files (maximum 10 MB each; supported formats include `.pdf`, `.png`, `.jpg`, `.jpeg`, `.gif`, `.doc`, `.docx`, `.xls`, `.xlsx`, `.pptx`, `.txt`, `.csv`).
  7. Click **Publish Discussion** (displays **Publishing...**).
- **Commenting on a Discussion**:
  1. Click on a discussion card to open its detail page.
  2. Scroll to the comments section and enter your feedback in the text box.
  3. Click **Post Comment**.

#### System behavior
- **Version Tracking**: Whenever a post is edited, the system preserves the previous text and increments the displayed version badge (`v{n} Edited`).
- **Soft Deletion**: Deleting a post or comment marks it as deleted in the database; it is removed from the view but retained in storage.

> **Note for reviewers:** Whether discussions may be cited as official GMP decisions requires confirmation; the code does not link discussions to GxP records. (GAP-009)

#### Validation
- Title is mandatory: `"Discussion title is required."` or `"Title is required."`
- Content is mandatory: `"Discussion content is required."` or `"Content is required."`
- Comments cannot be blank: `"Comment content cannot be empty."`

#### Errors
- If editing an unauthorized post: `"You are not authorized to edit this discussion post."` or `"You are not authorized to modify this discussion post."`
- If deleting an unauthorized post: `"You are not authorized to delete this discussion post."`
- If editing an unauthorized comment: `"You are not authorized to edit this comment."`
- Toast confirmations and errors: `"Marked as important"`, `"Unmarked as important"`, `"Discussion post deleted."`, `"Failed to toggle important status."`, `"Failed to delete discussion post."`, `"Comment added"`, `"Comment updated"`, `"Comment deleted"`.
