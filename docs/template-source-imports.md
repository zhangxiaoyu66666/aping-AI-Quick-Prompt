# Template Source Imports

AI Quick Prompt keeps built-in template source labels and generated app data in the public repository, but it does not track copied upstream website/source trees.

The following paths are local-only and ignored by Git:

- `ChatGPT-Shortcut-main/`
- `SD-Anima-Prompt-Studio-main/`
- `assets/fire_eye/`

They can exist on a maintainer machine for review, regeneration, OCR smoke tests, or attribution checks. A normal WinUI app build should not require those folders.

## Public Repository Policy

- Keep generated app template data under `src/PromptInputMethod.App/Data/`.
- Keep bundled Skill packages under `src/PromptInputMethod.App/Data/skills/` with their own `LICENSE`, `NOTICE.md`, and `README.md`.
- Keep attribution in `THIRD_PARTY_NOTICES.md`, `docs/license-inventory.md`, and `docs/open-source-references.md`.
- Do not commit copied upstream source trees, downloaded archives, local OCR model assets, credentials, user settings, prompt history, or OCR diagnostics.

## Local Reference Setup

For maintenance, download or clone upstream projects into the ignored local folders:

```text
ChatGPT-Shortcut-main/
SD-Anima-Prompt-Studio-main/
assets/fire_eye/
```

Before importing or regenerating any app data from those folders:

1. Confirm the upstream license and required notices.
2. Keep the original project name in generated source labels.
3. Import only the prompt/template content needed by AI Quick Prompt.
4. Exclude unrelated website, account, membership, community, deployment, and paid-feature code.
5. Update the third-party notices and license inventory in the same change.

## OCR Model Assets

`assets/fire_eye/` is reserved for local OCR model assets and charset files. These files remain ignored until their source, license, and redistribution terms are confirmed. Do not publish binaries that include them without updating the license inventory and release notices.
