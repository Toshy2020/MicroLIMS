# MicroLIMS Deployment & Operations Guide

This guide explains how to deploy the **MicroLIMS** application online for development/testing using free-tier cloud services, and how to maintain the local development environment.

---

## 1. System Architecture

```
                                GitHub Repository
                                       │
                   ┌───────────────────┴───────────────────┐
                   ▼                                       ▼
     Cloudflare Pages (Frontend)                Render (Backend API)
    ┌───────────────────────────┐           ┌───────────────────────────┐
    │ React 18 + Vite + MUI     │           │ ASP.NET Core 8 Web API    │
    │ Client-Side SPA (HTTPS)   │──HTTPS───▶│ Container (Docker Linux)  │
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
| **Frontend** | [Cloudflare Pages](https://pages.cloudflare.com) | Static Site / Single Page Application | Unlimited bandwidth & requests, Global CDN |
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
9. **After first successful startup**: Edit `APPLY_MIGRATIONS` to `false` in Render Environment Variables so migrations do not run on every regular restart.

---

### Step 3: Deploy Frontend on Cloudflare Pages
1. Go to [Cloudflare Dashboard](https://dash.cloudflare.com/) → **Workers & Pages** → **Create application** → **Pages** → **Connect to Git**.
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
6. Cloudflare will build the frontend and provide your public URL: `https://microlims.pages.dev`.
7. **Important**: Copy your Cloudflare URL (e.g. `https://microlims.pages.dev`), go back to **Render** → `microlims-api` → **Environment**, and ensure `Frontend__Origin` contains `https://microlims.pages.dev`.

---

## 4. EF Core Migrations & `APPLY_MIGRATIONS`

The backend contains 52 Code-First migrations that manage the database schema.

- **First-time database initialization**:
  Set `APPLY_MIGRATIONS=true` in Render. During container startup, the application runs `db.Database.Migrate()` and initializes default roles and the initial administrator account (`admin` / `ChangeMe123!`).
- **Normal Operation**:
  Set `APPLY_MIGRATIONS=false` in Render. The API will start quickly without checking migration status.
- **Applying New Migrations in the Future**:
  When you add new database features, push your changes to GitHub, temporarily set `APPLY_MIGRATIONS=true` in Render, redeploy, and then set it back to `false`.

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
