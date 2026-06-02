# License Inventory

Date: 2026-05-30

Scope: current `Prompt Input Method` workspace, including .NET projects, Rust native OCR worker, patched OCR library, bundled OCR models, copied template/reference projects, and copied native third-party source.

This is an engineering inventory and not legal advice. Public release should include a legal review and final NOTICE/LICENSE bundle.

## Project License

啊拼 / AI Quick Prompt original source code and documentation are released under the GNU General Public License, version 3 or any later version (`GPL-3.0-or-later`).

- Full license text: repository root `LICENSE`.
- Project notice: repository root `NOTICE.md`.
- Third-party notices: repository root `THIRD_PARTY_NOTICES.md`.

Third-party components, copied reference projects, prompt datasets, bundled Skill packages, framework/runtime packages, and model assets remain under their own licenses and must keep their required notices.

## Direct Dependencies

| Component | Source | License Metadata | Usage | Notes |
| --- | --- | --- | --- | --- |
| AI Quick Prompt / 啊拼 original code and docs | This repository | GPL-3.0-or-later | Windows prompt workbench app and documentation | Contributions should be accepted under GPL-3.0-or-later. |
| Microsoft.WindowsAppSDK `1.6.250205002` | NuGet | Microsoft license terms | WinUI 3 app runtime | Review redistribution requirements for self-contained unpackaged app. |
| .NET 8 runtime/ref assemblies | Microsoft | MIT / Microsoft notices | App runtime | Distributed via build output when self-contained components are included. |
| `fire-eye-ocr-worker` | Local Rust crate | GPL-3.0-or-later project code, plus Rust crate dependencies | Optional local OCR worker | Migrated/adapted from Xiaxia Pet reference code. |
| `ocr-rs` patched `2.2.2` | Local patch of `ocr-rs` | Apache-2.0 in package metadata | OCR engine wrapper | Keep upstream license and document local modifications. |
| MNN | Vendored under `native/ocr-rs-patched/3rd_party/MNN` | See `LICENSE.txt` in MNN tree | Native inference backend | Large vendored dependency; must retain MNN license/notice. |
| PaddleOCR model assets | `assets/fire_eye` | Requires source/license confirmation | OCR detection/recognition models and charset | Must confirm model license and attribution before public release. |
| prompts.chat prompt data | `src/PromptInputMethod.App/Data/prompts-chat-templates.json` generated from upstream `prompts.csv` | CC0 1.0 Universal for prompt content/data; MIT for source/site content | Built-in prompt template source labeled `prompts.chat` | Keep upstream project name and link in notices. |
| Female Portrait Prompt Director Skill / 女性人像提示词导演 Skill | `src/PromptInputMethod.App/Data/skills/female-portrait-director` copied from `liyue-aigc/female-portrait-director` | MIT License; copyright `(c) 2026 李岳` | Built-in text-to-image Skill source labeled `Skill` | Keep bundled `LICENSE`, `NOTICE.md`, `README.md`, and source project attribution in notices. |
| ChatGPT-Shortcut copied tree / generated references | `ChatGPT-Shortcut-main/`, generated app template data | Upstream license must be retained | Prompt template inspiration and generated template source labels | Consider replacing copied tree with import script before public push if repository size or licensing review is a concern. |
| SD-Anima-Prompt-Studio copied tree / generated references | `SD-Anima-Prompt-Studio-main/`, generated app template data | Upstream license must be retained | Image prompt template inspiration and generated template source labels | Consider replacing copied tree with import script before public push if repository size or licensing review is a concern. |
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
| Fire Eye detection model | `assets/fire_eye/PP-OCRv5_mobile_det.mnn` | License/source must be confirmed. |
| Fire Eye fp16 detection model | `assets/fire_eye/PP-OCRv5_mobile_det_fp16.mnn` | License/source must be confirmed. |
| Fire Eye recognition model | `assets/fire_eye/PP-OCRv5_mobile_rec.mnn` | License/source must be confirmed. |
| Fire Eye fp16 recognition model | `assets/fire_eye/PP-OCRv5_mobile_rec_fp16.mnn` | License/source must be confirmed. |
| OCR charset | `assets/fire_eye/ppocr_keys_v5.dict` | License/source must be confirmed. |

## Release Requirements

- Keep upstream license files for MNN and `ocr-rs`.
- Keep upstream license and NOTICE files for bundled Skill packages.
- Keep GPL `LICENSE`, `NOTICE.md`, and contribution license text synchronized.
- Include third-party notices for Rust crates in the release artifact.
- Confirm PaddleOCR model redistribution terms before publishing binaries that include `assets/fire_eye`.
- Decide whether copied upstream template/reference trees should be published directly, formally vendored, or replaced by import scripts plus generated app data.
- Document that OpenCL/Vulkan are optional GPU backends and may depend on user-installed drivers/runtime libraries.
- Do not publish API keys, user settings, build logs containing secrets, or generated OCR scheduler diagnostics.
