# License Inventory

Date: 2026-06-02

Scope: current `Prompt Input Method` workspace, including .NET projects, Rust native OCR worker, patched OCR library, generated template data, bundled Skill packages, local-only OCR models, and vendored native third-party source.

This is an engineering inventory and not legal advice. The AI Quick Prompt 1.0 OCR model and native dependency release review is recorded in `docs/ocr-model-license-review.md`.

## Project License

啊拼 / AI Quick Prompt original source code and documentation are released under the GNU General Public License, version 3 or any later version (`GPL-3.0-or-later`).

- Full license text: repository root `LICENSE`.
- Project notice: repository root `NOTICE.md`.
- Third-party notices: repository root `THIRD_PARTY_NOTICES.md`.

Third-party components, prompt datasets, bundled Skill packages, framework/runtime packages, and model assets remain under their own licenses and must keep their required notices.

## Direct Dependencies

| Component | Source | License Metadata | Usage | Notes |
| --- | --- | --- | --- | --- |
| AI Quick Prompt / 啊拼 original code and docs | This repository | GPL-3.0-or-later | Windows prompt workbench app and documentation | Contributions should be accepted under GPL-3.0-or-later. |
| Microsoft.WindowsAppSDK `1.6.250205002` | NuGet | Microsoft license terms | WinUI 3 app runtime | Review redistribution requirements for self-contained unpackaged app. |
| .NET 8 runtime/ref assemblies | Microsoft | MIT / Microsoft notices | App runtime | Distributed via build output when self-contained components are included. |
| `fire-eye-ocr-worker` | Local Rust crate | GPL-3.0-or-later project code, plus Rust crate dependencies | Optional local OCR worker | OCR functionality is integrated here; old migration reference trees are not tracked in the public repository. |
| `ocr-rs` patched `2.2.2` | Local patch of `ocr-rs` | Apache-2.0 in package metadata | OCR engine wrapper | Keep upstream license and document local modifications. |
| MNN | Vendored under `native/ocr-rs-patched/3rd_party/MNN` | Apache-2.0; see `LICENSE.txt` in MNN tree | Native inference backend | Large vendored dependency; retain MNN and bundled third-party license files. |
| PaddleOCR / PP-OCRv5 model assets | Local-only ignored path `assets/fire_eye`; embedded into `fire-eye-ocr-worker.exe` for release builds | Apache-2.0 per official PaddlePaddle PP-OCRv5 model cards and PaddleOCR repository | OCR detection/recognition models and charset | Source/license review completed for 1.0. Keep attribution, hashes, and conversion notice in `docs/ocr-model-license-review.md`. |
| prompts.chat prompt data | `src/PromptInputMethod.App/Data/prompts-chat-templates.json` generated from upstream `prompts.csv` | CC0 1.0 Universal for prompt content/data; MIT for source/site content | Built-in prompt template source labeled `prompts.chat` | Keep upstream project name and link in notices. |
| Female Portrait Prompt Director Skill / 女性人像提示词导演 Skill | `src/PromptInputMethod.App/Data/skills/female-portrait-director` copied from `liyue-aigc/female-portrait-director` | MIT License; copyright `(c) 2026 李岳` | Built-in text-to-image Skill source labeled `Skill` | Keep bundled `LICENSE`, `NOTICE.md`, `README.md`, and source project attribution in notices. |
| ChatGPT-Shortcut generated references | Generated app template data and source labels; local-only ignored path `ChatGPT-Shortcut-main/` for maintenance | Upstream license must be retained for any imported content | Prompt template inspiration and generated template source labels | Copied upstream source tree is not tracked in the public source tree. Keep attribution and document any regeneration process. |
| SD-Anima-Prompt-Studio generated references | Generated app template data and source labels; local-only ignored path `SD-Anima-Prompt-Studio-main/` for maintenance | Upstream license must be retained for any imported content | Image prompt template inspiration and generated template source labels | Copied upstream source tree is not tracked in the public source tree. Keep attribution and document any regeneration process. |
| Rust crates from crates.io | `native/Cargo.lock` | Mixed permissive licenses | Worker build dependencies | Generate final list with `cargo metadata --locked`. |

## Rust Direct Crates

`fire-eye-ocr-worker` direct crates:

- `base64`.
- `image`.
- `jieba-rs`.
- `ocr-rs` patched local crate.
- `serde`.
- `serde_json`.

`ocr-rs-patched` direct crates:

- Build dependencies: `cmake`, `cc`, `bindgen`.
- Runtime dependencies: `image`, `imageproc`, `ndarray`, `thiserror`, `log`, `rayon`, `env_logger`, `fast_image_resize`.
- Optional dependencies: `tokio`, `futures`.

## Bundled Assets Requiring Review

| Asset | Path | Status |
| --- | --- | --- |
| Fire Eye detection model | Local-only `assets/fire_eye/PP-OCRv5_mobile_det.mnn` | Reviewed as PP-OCRv5 / Apache-2.0; hash recorded in `docs/ocr-model-license-review.md`. |
| Fire Eye fp16 detection model | Local-only `assets/fire_eye/PP-OCRv5_mobile_det_fp16.mnn` | Reviewed as converted PP-OCRv5 / Apache-2.0; hash recorded in `docs/ocr-model-license-review.md`. |
| Fire Eye recognition model | Local-only `assets/fire_eye/PP-OCRv5_mobile_rec.mnn` | Reviewed as PP-OCRv5 / Apache-2.0; hash recorded in `docs/ocr-model-license-review.md`. |
| Fire Eye fp16 recognition model | Local-only `assets/fire_eye/PP-OCRv5_mobile_rec_fp16.mnn` | Reviewed as converted PP-OCRv5 / Apache-2.0; hash recorded in `docs/ocr-model-license-review.md`. |
| OCR charset | Local-only `assets/fire_eye/ppocr_keys_v5.dict` | Reviewed as PaddleOCR PP-OCRv5 support data / Apache-2.0; hash recorded in `docs/ocr-model-license-review.md`. |

## Release Requirements

- Keep upstream license files for MNN and `ocr-rs`.
- Keep upstream license and NOTICE files for bundled Skill packages.
- Keep GPL `LICENSE`, `NOTICE.md`, and contribution license text synchronized.
- Include third-party notices for Rust crates in the release artifact.
- Include `docs/ocr-model-license-review.md` and PaddleOCR / MNN attribution when publishing binaries that include the OCR worker.
- Keep copied upstream template trees and old migration reference trees out of Git; use local ignored folders only for regeneration and review.
- Document that OpenCL/Vulkan are optional GPU backends and may depend on user-installed drivers/runtime libraries.
- Do not publish API keys, user settings, build logs containing secrets, or generated OCR scheduler diagnostics.
