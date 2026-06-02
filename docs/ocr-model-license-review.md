# OCR Model License Review

Date: 2026-06-02

Scope: the Fire Eye OCR assets used by `native/fire-eye-worker`:

- `assets/fire_eye/PP-OCRv5_mobile_det.mnn`
- `assets/fire_eye/PP-OCRv5_mobile_det_fp16.mnn`
- `assets/fire_eye/PP-OCRv5_mobile_rec.mnn`
- `assets/fire_eye/PP-OCRv5_mobile_rec_fp16.mnn`
- `assets/fire_eye/ppocr_keys_v5.dict`

## Result

The Fire Eye OCR model family is approved for AI Quick Prompt 1.0 public binary redistribution when the release artifact includes the Apache-2.0 notices named below.

The model assets remain outside Git because they are large binary assets, not because redistribution is blocked. Release binaries may include `fire-eye-ocr-worker.exe`, which embeds these assets through `include_bytes!`.

## Verified Sources

| Component | Upstream source | License conclusion | Release action |
| --- | --- | --- | --- |
| `PP-OCRv5_mobile_det` detection model family | Official PaddlePaddle model card: <https://huggingface.co/PaddlePaddle/PP-OCRv5_mobile_det> | Model card lists `License: apache-2.0`; PaddleOCR repository is Apache-2.0. | Keep PaddleOCR attribution and Apache-2.0 notice. |
| `PP-OCRv5_mobile_rec` recognition model family | Official PaddlePaddle model card: <https://huggingface.co/PaddlePaddle/PP-OCRv5_mobile_rec> | Model card lists `License: apache-2.0`; PaddleOCR repository is Apache-2.0. | Keep PaddleOCR attribution and Apache-2.0 notice. |
| `ppocr_keys_v5.dict` character dictionary | PaddleOCR / PP-OCRv5 character dictionary used with the recognition model; `ocr-rs` documents `ppocr_keys_v5.txt` for PP-OCRv5. | Covered as PaddleOCR model support data under Apache-2.0. | Keep PaddleOCR attribution. |
| MNN inference engine and converted `.mnn` format | MNN repository: <https://github.com/alibaba/MNN> | MNN is Apache-2.0. | Keep MNN license files and notice. |
| Patched `ocr-rs` integration | Local `native/ocr-rs-patched`, upstream `zibo-chen/rust-paddle-ocr` / `ocr-rs` package metadata | `ocr-rs` package metadata is Apache-2.0. | Keep upstream `LICENSE` and document local patch. |

The `.mnn` files are converted deployment artifacts for the PaddleOCR PP-OCRv5 mobile model family. Format conversion and fp16 variants are treated as modified Apache-2.0 artifacts for notice purposes.

## Local Asset Fingerprints

| File | SHA-256 |
| --- | --- |
| `PP-OCRv5_mobile_det_fp16.mnn` | `617B5228B101275594F96EBB6AE7662FD1618BCF8E84B0FFDE1CF3B48E754951` |
| `PP-OCRv5_mobile_det.mnn` | `326F846BB5C903282E116EA089E8796B67921586726CCA9457730436A79684C3` |
| `PP-OCRv5_mobile_rec_fp16.mnn` | `FF03E4204260325EABE9F4EAE0EC8CC6B79B8A97A8E38A5292BA69CF02A689FC` |
| `PP-OCRv5_mobile_rec.mnn` | `C809800B09263A8D18C678C211E470FFC464CBB33DB2E6BDE0244766F3FEB0DB` |
| `ppocr_keys_v5.dict` | `F3FF5ED81AD3C267593FD3F7183528BB12BBAAA3AB05145EA0AC9FFEFFBC6EFE` |

## Apache-2.0 Release Obligations

For 1.0 release artifacts that include the OCR worker or embedded OCR models:

- Include the project GPL license and this repository's `NOTICE.md`.
- Include PaddleOCR attribution and Apache-2.0 notice in `THIRD_PARTY_NOTICES.md`.
- Include `native/ocr-rs-patched/LICENSE`.
- Include `native/ocr-rs-patched/3rd_party/MNN/LICENSE.txt` and the license files under MNN's bundled third-party folders.
- State that the `.mnn` and fp16 files are converted deployment artifacts of the PP-OCRv5 mobile model family.

## Decision

AI Quick Prompt 1.0 may ship the Fire Eye OCR worker with embedded PP-OCRv5 model assets. No code or packaging change should remove the OCR worker solely for license reasons after this review.
