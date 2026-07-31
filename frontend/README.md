# MicroLIMS Frontend

React 18 + TypeScript + Vite + Material UI.

## Structure

```
src/
├── components/     # Reusable controls: DataTable, FloatingDialog, StatusBadge, etc.
├── layouts/        # MainLayout, LoginLayout, DashboardLayout
├── pages/          # Login, Dashboard, Profile, Reports
├── modules/        # One folder per domain:
│   ├── authentication, dashboard, users, roles
│   ├── receiving, testingWorkspace, review, approval, reports
│   ├── pathogen           # cross-cutting workflow dialog, used from Testing Workspace
│   └── laboratoryConfiguration/   # Section Head's Master Configuration area
│       ├── items, specifications, media, gpt
│       ├── water, environmentalMonitoring, afterCleaning
├── services/       # apiClient.ts — the one axios instance everything uses
├── contexts/       # AuthContext
├── routes/         # PublicRoutes -> AuthenticatedRoutes -> {SystemAdministrator,SectionHead,Reviewer,Analyst}Routes
│                    # + menuConfig.ts (role -> visible menu items)
├── theme/          # Material UI theme + brand design tokens
├── hooks/, utils/, types/, constants/
├── App.tsx
└── main.tsx
```

Each module follows the same shape: `components/`, `dialogs/`,
`services/`, `types/`, `constants/`, `hooks/`.

Navigation flow: **DashboardLayout (MainLayout) → Role → menuConfig.ts
→ Visible Modules**. The Sidebar component reads `getMenuForRole()`
from `routes/menuConfig.ts` rather than hardcoding per-role checks —
add a new page to the menu in one place and it's automatically shown
to the right roles.

"Laboratory Configuration" is one collapsible menu (not six top-level
items) containing Items, Specifications, Media, GPT, Water,
Environmental Monitoring, and After Cleaning — this is the frozen
structure going forward; don't reorganize it further.

## Getting it running

Requires Node 18+.

```bash
cd frontend
npm install
cp .env.example .env   # set VITE_API_BASE_URL to your backend URL
npm run dev
```

Runs at `http://localhost:5173` by default, matching the backend's CORS
config (`Frontend:Origin` in `backend/MicroLIMS.API/appsettings.json`).

## What's implemented vs. stubbed

- **Fully wired:** login flow, auth context/token storage, routing
  guards, dashboard summary, sample receiving, testing workspace (cards
  + floating dialog routing), reports (PDF download).
- **Stubbed (structure + API call in place, UI content to fill in):**
  Water/EM/AfterCleaning workflow dialogs, Review/Approval detail views,
  User/Role/Item/Media admin tables. These call real backend endpoints
  already — they just need their specific form fields and layout built
  out per your UI mockups.

## Guiding principle

**The frontend never implements laboratory rules.** If you find
yourself writing an `if` about test results, specification comparison,
or workflow order in a component — stop, that belongs in the backend's
`Application/Services` or `Application/Workflows` instead. The frontend
only displays what the backend returns and collects input to send back.
