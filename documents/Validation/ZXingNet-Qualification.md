# Third-Party Package Qualification: ZXing.Net

**Status:** Draft - awaiting approval

## Package Details

- **Package ID:** `ZXing.Net`
- **Version:** `0.16.11` (exact pinned version in `backend/Directory.Packages.props`)
- **Licence:** `Apache-2.0`
- **Project Reference:** `backend/MicroLIMS.Infrastructure/MicroLIMS.Infrastructure.csproj` (core package only; no `ZXing.Net.Bindings.*` packages)

## Intended Use

- Computing barcode module patterns only via `ZXingBarcodeEncoder` (`IBarcodeEncoder`):
  - Code 128 (`Code128Writer`) -> 1D module boolean array (`bool[]`), without quiet zone.
  - Data Matrix ECC 200 (`DataMatrixWriter`, square shape enforced via `SymbolShapeHint.FORCE_SQUARE`) -> 2D module boolean matrix (`bool[,]`), without quiet zone.

## Excluded / Not Used For

- Not used for PDF or image rendering in production (labels are drawn as vector PDF rectangles by `PdfCanvas`).
- Not used for barcode decoding or scanning in production.

## Verification

- Verified by automated unit round-trip and boundary tests in `backend/MicroLIMS.Tests/UnitTests/BarcodeEncoderTests.cs`:
  - `Code128_round_trips`: encodes payloads (`S:FP1026001`, `EQ:INC-F-ML-F-01-003`), paints modules onto a SkiaSharp bitmap (4 px/module, 10-module white margin), and decodes back to the exact input using `BarcodeReaderGeneric`.
  - `DataMatrix_round_trips`: encodes payloads (`MED:TSA/03/26`, `SP:MP-VIT-C 05/11/2026`, `MAT:STK-2026-00123`, and a 254-character `CRY:` payload), paints modules onto a SkiaSharp bitmap (4 px/module, 10-module white margin), and decodes back to the exact input using `BarcodeReaderGeneric`.
  - `DataMatrix_is_square`: verifies square module dimensions (`rows == cols`).
  - `Code128_has_no_quiet_zone`: verifies the first and last modules are bars (`true`).
  - `Empty_content_throws_ArgumentException`: verifies `null`, empty, and whitespace inputs throw `ArgumentException` for both encoders.
