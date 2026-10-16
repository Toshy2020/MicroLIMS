## 6. User Interface

### 6.1 Application Shell and Global Header
The MicroLIMS application shell provides a consistent top header, collapsible navigation sidebar, breadcrumb hierarchy, and page container across every authenticated view:

- **Skip Navigation**: Accessible hidden link labeled "Skip to main content" allowing keyboard and screen-reader users to bypass navigation menus.
- **Navigation Toggle**: Hamburger icon button with accessibility label **Toggle navigation menu**. On desktop screens, it toggles the sidebar between expanded and compact rail modes. On mobile screens, it toggles the overlay drawer.
- **Theme Mode Toggle**: Icon button with tooltip and accessibility label **Switch to dark mode** or **Switch to light mode**. Toggles between light and dark visual themes.
- **Notifications Bell**: Icon button with accessibility label **Notifications** (or **Notifications, {n} unread**). Clicking the bell opens a popover listing recent attention alerts, sample review requests, incubation alerts, and media expiry notices.
- **Account Menu**: Profile icon button with accessibility label **Account menu for {name}**. Clicking the menu reveals:
  - **My Profile**: Opens the read-only user profile page (`/profile`).
  - **Messages** (or **Messages ({n} unread)**): Navigates directly to the messaging workspace (`/messages`).
  - **Discussions**: Navigates to the discussions and knowledge feed (`/discussions`).
  - **Change Password**: Navigates to the credential update form (`/change-password`).
  - **Sign Out**: Terminates the session, revokes refresh tokens, and returns to `/login`.

[SCREENSHOT SS-061]

---

### 6.2 Sidebar Layout and Theme Persistence
- **Collapsible Rail**: The sidebar navigation occupies the left edge of the desktop view (landmark: "Main navigation"). When collapsed, it narrows to a 68-pixel rail displaying top-level group icons. Hovering or expanding restores full text labels.
- **Sidebar Toggle Controls**: An explicit toggle button labeled **Collapse sidebar** or **Expand sidebar** allows manual layout adjustment. The collapsed state is persisted in your browser's local storage (`microlims_sidebar_collapsed`).
- **Dark Mode Support**: Users can freely switch between dark and light appearance modes. The active choice is stored in browser local storage (`microlims-theme-mode`). If no choice has been saved, the application detects and follows your operating system's dark mode preference.

---

### 6.3 Breadcrumbs, Section Pages, and Error Handling
- **Breadcrumb Navigation**: Displayed above the primary card of each screen, showing the structural path (e.g. `Home > Microbiology Laboratory > Workspace`).
- **Section Landing Pages (`SCR-NAV-001`)**: Accessible via `/sections/:slug` (with legacy alias `/groups/:slug`). When a major sidebar category is opened, this landing view presents navigation cards for all subordinate pages that the current user has permission to open:
  - Header: Section name (fallback: "Section not available").
  - Subtitle: "{n} page(s) you can open here".
  - Empty state: If you have no permissions for child screens in that section, the page displays: `"You have no pages in this section."`
- **Page Not Found (`SCR-NAV-002`)**: Navigating to an invalid or obsolete URL triggers the application-level catch-all route (`*`):
  - Heading: **Page not found**.
  - Title: "There is no page at this address".
  - Description: `"{pathname}" does not match any MicroLIMS page. The link may be outdated, or the page may have moved. Use the navigation menu or return to your dashboard.`
  - Action button: **Go to Dashboard** (redirects to `/dashboard`).
- **Per-Page Error Boundaries**: Every primary page view is encapsulated within an isolated error boundary. If a runtime rendering exception occurs on a particular screen, only that page displays a recovery prompt while the global header, sidebar navigation, and user session remain fully operational.

---

<!-- SCR-SIG-001 -->
### 6.4 Shared Electronic Signature Dialog
#### Purpose
Provide a standardized, tamper-evident re-authentication dialog for authorizing sensitive laboratory actions, release decisions, record corrections, and controlled document updates.

#### Who can use it
Any authenticated user executing an operational step that requires an electronic signature. The dialog is invoked by 26 distinct actions across the application.

#### What you see
- **Pinned Light Theme**: The signature dialog automatically renders in a high-contrast light theme (`PinnedLightTheme`), regardless of whether the user has enabled dark mode.
- Title: **Electronic Signature**.
- Signer Information:
  - User Full Name (or username).
  - User Role (read-only snapshot).
- Meaning Statement: A plain-language alert box describing the exact legal and operational meaning of the action being signed (e.g. "I confirm the preparation steps shown above are the steps performed for this sample.", "I confirm that this titrant was standardized according to SOP...", or "I entered these qualitative observations and am recording this test result.").
- Input Fields:
  - **Comment (optional)**: Multi-line text area (2 rows) displayed when the calling module permits signature comments.
  - **Password**: Mandatory password input with autofocus.
- Action Buttons:
  - **Cancel**
  - **Sign** (displays **Signing...** while verifying). The button remains disabled until a password has been typed. Pressing Enter in the password field submits the signature.

[SCREENSHOT SS-062]

#### How to use it
1. Initiate a sensitive laboratory action (e.g., clicking **Sign & Save**, **Sign & Void**, **Submit Technical Review**, or **Submit Release Decision**).
2. The **Electronic Signature** modal appears over the current screen.
3. Review your name, role, and the specific meaning statement displayed in the dialog.
4. If applicable, enter any supplementary notes in the **Comment (optional)** field.
5. In the **Password** field, enter your active login password.
6. Click **Sign** (or press Enter).
7. If verification succeeds, the system records the electronic signature and commits the underlying action in a single atomic transaction.

#### System behavior
- **Atomic Execution**: The signature record and the business action are committed together. If password re-authentication fails, the underlying record is not modified.
- **Signature Audit Attributes**: The system captures the user ID, full name, username, base role type snapshot, meaning string, entity ID, UTC timestamp, optional comment, and client IP address in the immutable electronic signature log.
- **Failure Throttling**: If a user enters five incorrect signature passwords within 15 minutes, further signature attempts are temporarily throttled for that user. This throttle applies strictly to electronic signatures and does not lock the user's primary login account.

#### Errors
- If an incorrect password is submitted, the system displays the server error message (fallback: `"Signature failed."`), clears the password input, and keeps the dialog open for re-entry.
