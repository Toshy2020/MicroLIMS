# Third-Party Package Qualification: vite-plugin-pwa

**Status:** Draft - awaiting approval

**Prepared by:** ____________________  **Date:** ____________
**Reviewed / Approved by:** ____________________  **Date:** ____________

## Package Details

- **Package Name:** `vite-plugin-pwa`
- **Version:** `2.0.0` (exact pinned version in `frontend/package.json` and `frontend/package-lock.json`; released 2026-10-03 on the npm registry)
- **Licence:** `MIT`
- **Source:** npm registry (`https://registry.npmjs.org/vite-plugin-pwa/-/vite-plugin-pwa-2.0.0.tgz`)
- **Project Reference:** `frontend/package.json` (`devDependencies`); configured in `frontend/vite.config.ts` and imported dynamically in production by `frontend/src/pwa/registerServiceWorker.ts`

### Transitive Components

- `vite-plugin-pwa` is a build-time devDependency that bundles Workbox (`workbox-build`, `workbox-window`) to generate the static `sw.js` precache manifest and registration helper.

## Intended Use

- Generates a build-time service worker (`dist/sw.js`) with `registerType: "prompt"`, `injectRegister: null`, `workbox.navigateFallback: "/index.html"`, `runtimeCaching: []`, and `globPatterns` restricted to compiled frontend build assets (`index.html`, `assets/*`, `manifest.webmanifest`, `icons/*.png`).
- Enables installing the MicroLIMS frontend as a standalone app shell on bench tablets and prompting the user to reload when a new frontend build is waiting (never reloading automatically and suppressing the prompt while a `BenchDialog` is open).

## Excluded / Not Used For

- **GMP Impact Assessment:** Build-time static asset precaching only. Does not store, cache, intercept, or replay any laboratory record, API request, authentication token, or electronic signature.
- **API Isolation:** `runtimeCaching` is explicitly `[]`. Nothing whose URL contains `/api/` or points to the API origin is precached or routed by the service worker; this is enforced on every build by `frontend/scripts/check-sw.mjs`.
- **Help Screenshots:** Excluded from precaching (`globIgnores: ["**/help/**"]` and copied after `vite build` by `scripts/copy-help-assets.mjs`).

## Verification

- Verified in Vitest and post-build checks:
  - `frontend/src/pwa/UpdatePrompt.test.tsx`: `UpdatePrompt shows "A new version is available." with Reload; does not appear while a bench dialog is open; appears after it closes`; `Reload is never called without a click`; `registerServiceWorker is a no-op in tests`; `manifest has name, short_name, start_url "/", display "standalone", orientation "any", 192/512/maskable icons`
  - `frontend/scripts/check-sw.test.mjs`: `passes when precache list and routes contain only build assets`; `fails if the generated sw.js precache list or routes contain /api/`; `fails if the generated sw.js contains the API origin`; `fails if help screenshots are precached`
  - Production build gate (`npm run build && node scripts/check-sw.mjs`) run locally and in CI (`.github/workflows/ci.yml`).

## Manual Verification Required Before Go-Live (Not Verified Automatically)

- [ ] On the chosen tablet model and browser, install MicroLIMS to the home screen from the Vercel deployment and confirm standalone launch and one prompt-driven update cycle.
