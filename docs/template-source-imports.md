# Template Source Imports

AI Quick Prompt keeps built-in template source labels and generated app data in the public repository, but it does not track copied upstream website/source trees.

The following paths are local-only and ignored by Git:

- `ChatGPT-Shortcut-main/`
- `SD-Anima-Prompt-Studio-main/`
- `reference/xiaxia-pet/`
- `reference/skill-candidates/`
- `assets/fire_eye/`

They can exist on a maintainer machine for review, regeneration, OCR smoke tests, or attribution checks. A normal WinUI app build should not require those folders.

## Public Repository Policy

- Keep generated app template data under `src/PromptInputMethod.App/Data/`.
- Keep bundled Skill packages under `src/PromptInputMethod.App/Data/skills/` with their own `LICENSE`, `NOTICE.md`, and `README.md`.
- Keep attribution in `THIRD_PARTY_NOTICES.md`, `docs/license-inventory.md`, and `docs/open-source-references.md`.
- Do not commit copied upstream source trees, downloaded archives, local OCR model assets, credentials, user settings, prompt history, or OCR diagnostics.
- Do not commit old migration reference trees; OCR code should live in `native/fire-eye-worker` and `native/ocr-rs-patched`.
- Keep Jimeng / Dreamina / Seedance Skill candidates as research references until their licenses and imported file scope are reviewed. The working list is `docs/skill-source-candidates.md`.

## Local Reference Setup

For maintenance, download or clone upstream projects into the ignored local folders:

```text
ChatGPT-Shortcut-main/
SD-Anima-Prompt-Studio-main/
assets/fire_eye/
reference/xiaxia-pet/
reference/skill-candidates/
```

Before importing or regenerating any app data from those folders:

1. Confirm the upstream license and required notices.
2. Keep the original project name in generated source labels.
3. Import only the prompt/template content needed by AI Quick Prompt.
4. Exclude unrelated website, account, membership, community, deployment, and paid-feature code.
5. Update the third-party notices and license inventory in the same change.
6. For Skill candidates without a detected repository license, study structure only and do not copy text, prompts, code, or Skill files.

## OCR Model Assets

`assets/fire_eye/` is reserved for local OCR model assets and charset files. The AI Quick Prompt 1.0 review confirms the PP-OCRv5 / MNN redistribution path in `docs/ocr-model-license-review.md`. The raw asset folder remains ignored because it is a large local cache; release binaries may include `fire-eye-ocr-worker.exe` with embedded reviewed OCR assets when the license inventory and notices ship with the package.
