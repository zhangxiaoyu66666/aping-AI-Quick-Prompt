# Third-Party Notices

AI Quick Prompt / 啊拼 includes original project code plus templates, optional local OCR components, built-in Skill packages, and development material derived from or inspired by third-party open-source projects.

This file is a high-level notice. See [docs/license-inventory.md](docs/license-inventory.md) and [docs/open-source-references.md](docs/open-source-references.md) for the working inventory.

## Project License

The original AI Quick Prompt / 啊拼 source code and documentation are licensed under the GNU General Public License, version 3 or any later version (`GPL-3.0-or-later`). See [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md).

Third-party components, prompt datasets, bundled Skill packages, framework/runtime packages, and OCR/model assets remain under their own licenses. Their inclusion or attribution here does not relicense them under GPL.

The public source tree does not track copied upstream project trees, old migration reference trees, or local OCR model files. Those folders may exist locally for maintenance; release packaging follows the license inventory and the completed OCR review in [docs/ocr-model-license-review.md](docs/ocr-model-license-review.md).

## Referenced Open-Source Projects

### ChatGPT-Shortcut

- Purpose: built-in prompt template inspiration and prompt workflow references.
- Repository/source: upstream ChatGPT-Shortcut project.
- License: retain the upstream license and notices for any imported template content.
- Notice: AI Quick Prompt keeps the upstream project name in template source labels to preserve attribution.
  The copied upstream source tree is a local-only ignored reference folder, not part of the public repository.

### SD-Anima-Prompt-Studio

- Purpose: image prompt template inspiration for text-to-image and character prompt flows.
- License: retain the upstream license and notices for any imported template content.
- Notice: AI Quick Prompt keeps the upstream project name in template source labels to preserve attribution.
  The copied upstream source tree is a local-only ignored reference folder, not part of the public repository.

### prompts.chat

- Purpose: built-in role prompt library, developer prompts, structured prompts, and image prompt references.
- Repository/source: https://github.com/f/prompts.chat
- License: prompt content and data (`prompts.csv`, `PROMPTS.md`, user-submitted prompts) are dedicated to the public domain under CC0 1.0 Universal; source code and site-authored content are MIT licensed.
- Notice: AI Quick Prompt keeps the upstream project name in template source labels to preserve attribution and does not include prompts.chat site, CLI, MCP, account, or hosting features.

### Female Portrait Prompt Director Skill / 女性人像提示词导演 Skill

- Purpose: built-in text-to-image Skill workflow for female portrait prompts, fashion/ecommerce try-on prompts, style routing, reference-image preservation, prompt optimization, parameter recommendation, and safety rewriting.
- Repository/source: https://github.com/liyue-aigc/female-portrait-director
- Author: 李岳.
- Version: FEMALE-PORTRAIT-DIRECTOR-V1.4.1.
- License: MIT License.
- Notice: AI Quick Prompt bundles this Skill under `src/PromptInputMethod.App/Data/skills/female-portrait-director/` with its original `LICENSE`, `NOTICE.md`, `README.md`, and `SKILL.md`. The app uses it as text workflow context only and adapts final prompt output to AI Quick Prompt's plain-text prompt boxes.

### Microsoft Windows App SDK / WinUI 3

- Purpose: Windows desktop UI framework.
- Package: `Microsoft.WindowsAppSDK`.
- License: governed by Microsoft package license terms.

### Rust Crates And Native OCR Dependencies

- Purpose: optional Fire Eye OCR worker and OCR inference path.
- Inventory: see `native/Cargo.lock` and `docs/license-inventory.md`.
- Note: OCR code is integrated under `native/fire-eye-worker` and `native/ocr-rs-patched`; the old `reference/xiaxia-pet` tree is not part of the public repository.

### MNN

- Purpose: native inference backend used by the optional OCR worker dependency tree.
- License: Apache-2.0.
- Notice: retain upstream license files under the vendored dependency tree, including `native/ocr-rs-patched/3rd_party/MNN/LICENSE.txt` and bundled third-party license files.

### OCR Model Assets

- Purpose: optional local Fire Eye OCR model assets.
- Source: PaddleOCR / PP-OCRv5 mobile detection and recognition model family.
- License: Apache-2.0 per official PaddlePaddle PP-OCRv5 model cards and PaddleOCR repository.
- Status: local-only ignored source assets, embedded into `fire-eye-ocr-worker.exe` for release builds. The 1.0 license review and asset hashes are recorded in [docs/ocr-model-license-review.md](docs/ocr-model-license-review.md).

## Important Packaging Note

AI Quick Prompt 1.0 may publish release binaries that include the Fire Eye OCR worker and embedded PP-OCRv5 model assets, provided this notice, the license inventory, the OCR model review, and the required Apache-2.0 license files are included. Generated template datasets should keep clear source labels and matching attribution. Copied upstream project trees should remain outside Git unless they are deliberately vendored with their full license files.
