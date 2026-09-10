# MicroLIMS Deployment & Operations Guide

This guide explains how to deploy the **MicroLIMS** application online for development/testing using free-tier cloud services, and how to maintain the local development environment.

---

## 1. System Architecture

```
                                GitHub Repository
                                       │
                   ┌───────────────────┴───────────────────┐
                   ▼                                       ▼
     Cloudflare Workers / Pages                 Render (Backend API)
    ┌───────────────────────────┐           ┌───────────────────────────┐
    │ React 18 + Vite + MUI     │           │ ASP.NET Core 8 Web API    │
    │ Client-Side SPA (HTTPS)   │──HTTPS───▶│ Container (Docker Linux)  │
    │ (Workers Static Assets)   │           │                           │
    └───────────────────────────┘           └─────────────┬─────────────┘
                                                          │
                                                          │ ConnectionStrings__Default
                                                          ▼
                                                  Neon PostgreSQL
                                            ┌───────────────────────────┐
                                            │ Serverless PostgreSQL 16+ │
                                            │ SSL-Encrypted Database    │
                                            └───────────────────────────┘
```

| Component | Platform | Service Type | Free Tier Specs |
| :--- | :--- | :--- | :--- |
| **Frontend** | [Cloudflare](https://dash.cloudflare.com) | Workers Static Assets / Pages (SPA) | Unlimited bandwidth & requests, Global CDN, `wrangler.jsonc` |
| **Backend** | [Render](https://render.com) | Web Service (Docker Container) | 512 MB RAM, 0.1 CPU, Auto-sleep after 15m inactivity |
| **Database** | [Neon](https://neon.tech) | Serverless PostgreSQL | 0.5 GB storage, SSL connection, Automated branching |

---

## 2. Local Development vs. Online Deployment

| Setting | Local Development | Online Production / Demo |
| :--- | :--- | :--- |
| **Frontend URL** | `http://localhost:5173` | `https://<your-subdomain>.pages.dev` |
| **API URL** | `http://localhost:5000/api` | `https://<your-render-app>.onrender.com/api` |
| **Database** | Local PostgreSQL (`LIMSV2` on `localhost:5432`) | Cloud PostgreSQL on Neon (`ep-xyz...neon.tech`) |
| **Config Location**| `frontend/.env.local`, `appsettings.Development.json` | Cloudflare Environment Variables, Render Environment Variables |

---

## 3. Step-by-Step Online Deployment

### Step 1: Provision Neon PostgreSQL Database
1. Go to [Neon.tech](https://neon.tech) and sign up / log in with GitHub.
2. Click **Create Project**:
   - **Project name**: `microlims-db` (or any name)
   - **Region**: Choose the region closest to your Render service (e.g. Frankfurt or Oregon).
3. Once created, copy the **Connection string** from the dashboard.
   - Format: `postgresql://user:password@ep-sample-12345.eu-central-1.aws.neon.tech/neondb?sslmode=require`
   - Convert to standard ADO.NET / Npgsql format for ASP.NET Core:
     ```
     Host=ep-sample-12345.eu-central-1.aws.neon.tech;Database=neondb;Username=user;Password=your_password;SSL Mode=Require;Trust Server Certificate=true;
     ```

---

### Step 2: Deploy Backend API on Render
1. Go to [Render.com](https://render.com) and log in with GitHub.
2. Click **New +** → **Web Service**.
3. Select your **MicroLIMS** repository.
4. Fill in the service details:
   - **Name**: `microlims-api` (or your chosen name)
   - **Region**: Same region as Neon if available.
   - **Branch**: `main`
   - **Root Directory**: Leave blank (repo root).
   - **Runtime**: **Docker**
   - **Dockerfile Path**: `backend/Dockerfile`
   - **Instance Type**: **Free**
   - **Health Check Path**: `/health/ready` — see [Health Check Endpoints](#41-health-check-endpoints) before setting this.
5. Scroll down to **Environment Variables** and add the following keys:

| Environment Variable Key | Value Example | Description |
| :--- | :--- | :--- |
| `ConnectionStrings__Default` | `Host=ep-...neon.tech;Database=neondb;...` | Your full Neon PostgreSQL connection string |
| `Jwt__Key` | `A_VERY_LONG_RANDOM_SECRET_KEY_AT_LEAST_32_CHARS_LONG!` | Random secure signing key for JWT tokens |
| `Frontend__Origin` | `http://localhost:5173,https://<your-pages-name>.pages.dev` | Allowed CORS origins (comma-separated) |
| `APPLY_MIGRATIONS` | `true` *(First deployment only)* | Tells the API to run EF Core migrations and initialize tables |

6. Click **Create Web Service**.
7. Once deployment finishes, your API URL will be: `https://microlims-api.onrender.com`.
8. Verify by opening `https://microlims-api.onrender.com/health` in your browser. It should return `{"status":"Healthy",...}`.
9. Also open `https://microlims-api.onrender.com/health/ready`. It should return `{"status":"Healthy","checks":[{"name":"postgresql",...}]}` with HTTP 200. A `503` here means the API is running but cannot serve requests — see [Health Check Endpoints](#41-health-check-endpoints).
10. **After first successful startup**: Edit `APPLY_MIGRATIONS` to `false` in Render Environment Variables so migrations do not run on every regular restart.

---

### Step 3: Deploy Frontend on Cloudflare (Workers Static Assets / Pages)

#### Option A: Deploy via Wrangler CLI (Workers Static Assets)
1. Build the frontend locally or via CI:
   ```bash
   cd frontend
   npm run build
   ```
2. Deploy to Cloudflare using the root `wrangler.jsonc` configuration:
   ```bash
   npx wrangler deploy
   ```
3. Cloudflare will deploy the static assets with SPA routing handled natively via `"not_found_handling": "single-page-application"`.

#### Option B: Deploy via Cloudflare Dashboard (Git-connected Pages / Workers)
1. Go to [Cloudflare Dashboard](https://dash.cloudflare.com/) → **Workers & Pages** → **Create application** → **Connect to Git**.
2. Select your **MicroLIMS** repository.
3. Configure the build settings:
   - **Project name**: `microlims`
   - **Production branch**: `main`
   - **Framework preset**: `Vite`
   - **Root directory**: `frontend`
   - **Build command**: `npm run build`
   - **Build output directory**: `dist`
4. Expand **Environment variables** and add:

| Variable Name | Value |
| :--- | :--- |
| `VITE_API_BASE_URL` | `https://<your-render-api-name>.onrender.com/api` |

5. Click **Save and Deploy**.
6. Cloudflare will build the frontend and provide your public URL (e.g. `https://microlims.toshy2020.workers.dev` or `https://microlims.pages.dev`).
7. **Important**: Copy your Cloudflare URL, go back to **Render** → `microlims-api` → **Environment**, and ensure `Frontend__Origin` contains your Cloudflare URL.

---

## 4. EF Core Migrations & `APPLY_MIGRATIONS`

The backend contains 52 Code-First migrations that manage the database schema.

- **First-time database initialization**:
  Set `APPLY_MIGRATIONS=true` in Render. During container startup, the application runs `db.Database.Migrate()` and initializes default roles and the initial administrator account (`admin` / `ChangeMe123!`).
- **Normal Operation**:
  Set `APPLY_MIGRATIONS=false` in Render. The API will start quickly without checking migration status.
- **Applying New Migrations in the Future**:
  When you add new database features, push your changes to GitHub, temporarily set `APPLY_MIGRATIONS=true` in Render, redeploy, and then set it back to `false`.

> **Important once the health check path is set to `/health/ready`:** deploying code that
> contains a new migration while `APPLY_MIGRATIONS=false` will now **fail the deploy**.
> The container starts, but readiness reports `503 — PostgreSQL schema is behind the
> application`, so Render never routes traffic to it and rolls back to the previous
> version. This is deliberate: the alternative is a live instance throwing errors on
> the first query against a missing column. Set `APPLY_MIGRATIONS=true` for the deploy
> that ships the migration, then set it back to `false`.

---

### 4.1 Health Check Endpoints

The API exposes two probes. They are anonymous, cheap, and expose no configuration,
credentials, SQL or stack traces.

| Endpoint | Question | Fails when |
| :--- | :--- | :--- |
| `/health` | Is the process alive? | Only if the application is not running at all. Runs **no** dependency checks, so a database outage can never make it fail. |
| `/health/ready` | Can this instance serve requests? | PostgreSQL is unreachable, **or** the database schema is behind the deployed code. Returns `503`. |

**Set Render's Health Check Path to `/health/ready`** (Dashboard → your service →
**Settings** → **Health Check Path**). Render uses this path both to gate a new deploy
before shifting traffic to it and to monitor the running service, so an instance that
cannot reach its database is never put into rotation.

Use `/health` for anything that should only ask "is the process up?" — an uptime pinger,
or checking whether a free-tier service has woken from sleep. Do **not** point Render's
health check at `/health`: it returns `200` even when the database is unreachable, which
would let a non-functional instance receive traffic.

Two notes for the free tier and Neon:

- A cold start (service asleep, or Neon scaled to zero) makes the first readiness probe
  slower than later ones, because it opens a real database connection. If Render reports
  a health check timeout on a service that is otherwise fine, this is the usual cause.
- Readiness performs two lightweight read-only operations — a connection test and a read
  of the EF migrations history table. It never writes, and never applies a migration.

---

## 5. Security Checklist & Rules

### What Must NEVER Be Committed to GitHub:
- ❌ **Neon database passwords or production connection strings**
- ❌ **Real JWT secret signing keys**
- ❌ **SMTP / email passwords**
- ❌ **`.env` or `.env.local` files containing production secrets**
- ❌ **`appsettings.Production.json`**

### What to Do If Credentials Were Previously Committed:
- If a password or key was committed to Git history in the past, consider it compromised.
- Change the database password directly in the database provider (e.g. Neon Dashboard or local PostgreSQL).
- Generate a new, strong random string for `Jwt__Key` in Render.

---

## 6. Troubleshooting Common Issues

### Issue 1: Render Web Service Sleeping (Cold Start)
- **Symptom**: The first request after 15+ minutes takes 30–50 seconds to respond.
- **Cause**: Render's free tier suspends idle web services.
- **Resolution**: This is normal for the free tier. Once awake, requests respond immediately. You can test if the API is awake by opening `https://<app>.onrender.com/health`.

### Issue 1b: Deploy Fails the Health Check / Rolls Back
- **Symptom**: Render marks the deploy as failed and keeps the previous version live. `https://<app>.onrender.com/health` returns `200`, but `/health/ready` returns `503`.
- **Cause**: The instance is running but not ready. Open `/health/ready` and read the `description` field:
  - `PostgreSQL schema is behind the application` — the deploy contains a migration that has not been applied. Set `APPLY_MIGRATIONS=true` and redeploy, then set it back to `false`.
  - `PostgreSQL is not reachable` — `ConnectionStrings__Default` is wrong or missing, or the database provider is unreachable (see Issue 4).
- **Resolution**: Fix the cause above. Do **not** work around it by pointing the health check at `/health` — that would let a broken instance take traffic.

### Issue 2: CORS Error in Browser Console (`Access-Control-Allow-Origin`)
- **Symptom**: Login or API requests fail in the browser with a CORS error.
- **Cause**: The Cloudflare Pages URL is not listed in Render's `Frontend__Origin` environment variable.
- **Resolution**: In Render Dashboard → Environment Variables → `Frontend__Origin`, ensure your exact Cloudflare URL (e.g. `https://microlims.pages.dev`, without trailing slash) is included in the comma-separated list.

### Issue 3: Page Reload on Cloudflare Pages / Workers
- **Symptom**: Navigating to a client-side route and refreshing gives a 404 Not Found.
- **Cause**: Single Page Application routes need SPA not-found handling.
- **Resolution**: Handled natively by Cloudflare Workers Static Assets via `"not_found_handling": "single-page-application"` in `wrangler.jsonc`. Do not add a `/* /index.html 200` rule to `_redirects` as that triggers a Cloudflare infinite loop validator error.

### Issue 4: Database Connection Failed
- **Symptom**: Render logs show `Npgsql.NpgsqlException: Connection to ... failed`.
- **Cause**: Incorrect connection string or missing SSL mode parameter.
- **Resolution**: Make sure the connection string ends with `SSL Mode=Require;Trust Server Certificate=true;` (or the format provided by Neon).

---

## 7. How to Update the Application

1. **Local Changes**:
   Make and test your code changes locally.
   ```bash
   # Run frontend locally:
   cd frontend
   npm run dev

   # Run backend locally:
   cd backend/MicroLIMS.API
   dotnet run
   ```
2. **Build Verification**:
   ```bash
   # Frontend build check:
   cd frontend
   npm run build

   # Backend build check:
   cd backend/MicroLIMS.API
   dotnet build
   ```
3. **Deploy**:
   Commit and push your changes to GitHub `main` branch.
   ```bash
   git push origin main
   ```
   - Render will automatically detect the push and redeploy the backend container.
   - Cloudflare Pages will automatically detect the push and rebuild the frontend.
