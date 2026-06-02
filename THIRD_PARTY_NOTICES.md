# Third-Party Notices

AI Quick Prompt / 啊拼 includes original project code plus references, templates, optional local OCR components, built-in Skill packages, and development material derived from or inspired by third-party open-source projects.

This file is a high-level notice. See [docs/license-inventory.md](docs/license-inventory.md) and [docs/open-source-references.md](docs/open-source-references.md) for the working inventory.

## Referenced Open-Source Projects

### ChatGPT-Shortcut

- Purpose: built-in prompt template inspiration and prompt workflow references.
- Repository/source: upstream ChatGPT-Shortcut project.
- Notice: AI Quick Prompt keeps the upstream project name in template source labels to preserve attribution.

### SD-Anima-Prompt-Studio

- Purpose: image prompt template inspiration for text-to-image and character prompt flows.
- Notice: AI Quick Prompt keeps the upstream project name in template source labels to preserve attribution.

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

### MNN

- Purpose: native inference backend used by the optional OCR worker dependency tree.
- Notice: retain upstream license files under the vendored dependency tree.

### OCR Model Assets

- Purpose: optional local Fire Eye OCR model assets.
- Status: redistribution terms must be confirmed before publishing binaries that include these assets.

## Important Packaging Note

Do not publish release binaries that bundle third-party model assets, vendored native dependencies, or copied upstream project trees until the license inventory is complete and reviewed.
