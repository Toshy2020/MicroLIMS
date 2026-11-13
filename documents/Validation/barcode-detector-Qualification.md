# Third-Party Package Qualification: barcode-detector

**Status:** Draft - awaiting approval

**Prepared by:** ____________________  **Date:** ____________
**Reviewed / Approved by:** ____________________  **Date:** ____________

## Package Details

- **Package Name:** `barcode-detector`
- **Version:** `3.2.3` (exact pinned version in `frontend/package.json` and `frontend/package-lock.json`)
- **Licence:** `MIT`
- **Source:** npm registry (`https://registry.npmjs.org/barcode-detector/-/barcode-detector-3.2.3.tgz`)
- **Project Reference:** `frontend/package.json` (imported lazily via `import("barcode-detector/ponyfill")` only when native `window.BarcodeDetector` is unavailable or does not report both `data_matrix` and `code_128`)

### Transitive Decoder Component

- **Package Name:** `zxing-wasm`
- **Version:** `3.1.6` (exact resolved version in `frontend/package-lock.json`)
- **Licence:** `MIT`
- **Source:** npm registry (`https://registry.npmjs.org/zxing-wasm/-/zxing-wasm-3.1.6.tgz`)
- **Bundled Binary:** `zxing-wasm/reader/zxing_reader.wasm` is bundled directly into the frontend production build (`dist/assets/`) via dynamic `?url` asset import and served from the application's own origin. Its default jsDelivr CDN URL is overridden via `prepareZXingModule({ overrides: { locateFile } })` before constructing `BarcodeDetector`.

## Intended Use

- Client-side fallback barcode decoding for camera preview frames inside `CameraScanDialog` (`frontend/src/components/labels/CameraScanDialog.tsx`):
  - Data Matrix (`data_matrix`) and Code 128 (`code_128`) only.
- Provides a convenience input string fed directly into the exact same `ScanField` or `GlobalSearchDialog` resolve flow as manual typing or USB keyboard-wedge scanners.

## Excluded / Not Used For

- **GMP Impact Assessment:** Convenience input only — no GMP, usability, workflow, or eligibility decision is taken from the decoded client value. Every decoded value is sent to the backend (`/api/labels/scan-resolve`), which re-resolves the code, verifies record existence and usability/status rules, and returns authoritative server decisions.
- No runtime fetching from external CDNs is active in production; the reader `.wasm` binary is shipped with the built frontend assets on the same origin.
- Not bundled into the main application entry chunk (loaded on demand only when camera scanning is triggered on a browser lacking native `BarcodeDetector` support for `data_matrix` and `code_128`).

## Verification

- **Important scope note:** Automated Vitest tests use fake detectors (`window.BarcodeDetector` mocked on `window` and `barcode-detector/ponyfill` mocked as a package module because WebAssembly decoding cannot execute in jsdom). Actual barcode decoding by the `barcode-detector` / `zxing-wasm` WebAssembly ponyfill is **NOT** covered by automated tests and is verified only by the manual check below.
- Verified in Vitest (`frontend/src/components/labels/CameraScanDialog.test.tsx`, `ScanField.test.tsx`, `GlobalSearchDialog.test.tsx`):
  - `reports the first detected value and stops the camera`
  - `shows the permission-refused message`
  - `shows the no-camera message`
  - `Escape closes and stops the camera` (including focus return to the trigger control)
  - `reports only once when the detector fires repeatedly`
  - `falls back to the bundled decoder when the browser has no BarcodeDetector` (verifies `prepareZXingModule` is called with `overrides.locateFile` returning the local asset URL rather than an external URL)
  - `camera button is hidden without getUserMedia`
  - `a camera-detected code goes through the same checks`
  - `camera-detected code opens the record`
- Verified in production Vite build (`npm run build`): `dist/assets/` contains the bundled `zxing_reader` `.wasm` binary and `prepareZXingModule` overrides the upstream default CDN string in the lazy ponyfill chunk.

## Manual Verification Required Before Go-Live (Not Verified Automatically)

- [ ] Real-camera decoding readback on physical workstations and tablets for all six label types (`Sample`, `MediaLot`, `SolutionPreparation`, `Cryovial`, `MaterialStockLot`, `EquipmentAsset`), including browsers using the bundled `barcode-detector/ponyfill` + `zxing_reader.wasm` fallback on HTTPS origins.
