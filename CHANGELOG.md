# Changelog

## 1.0.5 - 2026-06-04

### Highlights

- English prompt field-copy buttons now refresh immediately when switching optimization targets. Agent targets show task / constraints / verification, ComfyUI and Stable Diffusion targets show positive / negative / parameters, and video targets show their matching storyboard or timing fields without requiring an app restart.
- The GitHub source tree now includes Microsoft Store submission reference material: listing copy, certification notes, and the Store `.msixupload` helper script. These files document packaging and submission boundaries without adding Store-only cloud sync code to the community build.

### Community Edition Boundary

- OneDrive, WebDAV, and encrypted cloud-sync implementation files remain excluded from the GitHub community release. The public build keeps the local-first behavior and release checks that prevent cloud-sync UI, settings, services, or `PromptInputMethod.Core.Sync` from shipping here.

### Verification

- App, core, manifest, public demo package, and sideloading MSIX metadata now report version `1.0.5`.

## 1.0.3 - 2026-06-03

### Highlights

- Image-generation workflows now produce copy-ready fields for ComfyUI and Stable Diffusion instead of a generic image prompt blob. Outputs separate positive prompt, negative prompt, sampler settings, and workflow notes so users can paste them into node-based or WebUI pipelines with less cleanup.
- Video prompt work is more explicit for Jimeng, Dreamina, and Seedance. The built-in director target now asks for task type, references, frame continuity, storyboard timing, motion, subtitles, sound, platform parameters, and missing materials instead of flattening everything into a vague video description.
- Optimization targets now use a two-level picker: choose the category first, then the exact target. This keeps text, image, video, Agent, and custom workflows from being mixed into one long list.

### Community Edition Boundary

- The GitHub community release does not include OneDrive or WebDAV sync UI, settings, or service code. Cloud sync remains a Microsoft Store branch architecture topic and is documented separately so the public GPL build stays local-first and clear.
- High-star Jimeng / Seedance candidate repositories without clear licenses remain black-box research references. The built-in Jimeng director target and templates are original clean-room project content.

### Verification

- Release checks now guard the new ComfyUI / Stable Diffusion adapters, Jimeng clean-room boundary, two-level target picker, streaming thinking bubble, animation setting, and GitHub cloud-sync exclusion.
- App and package metadata now report version `1.0.3`.

## 1.0.0 - 2026-06-02

- Added open-source project governance files for public repository readiness.
- Added `Skill体系` support for mounted `SKILL.md` workflows.
- Added automatic mounted Skill matching and direct Skill execution prompts.
- Added AI coding prompt templates for Codex, Claude Code, Antigravity, and related agentic coding tools.
- Added Veo 3 and Jimeng / Seedance video prompt template groups.
- Switched the project license from MIT to GPL-3.0-or-later and refreshed open-source notices.
- Stopped tracking copied upstream template source trees and local OCR model assets for a cleaner public source tree.
- Removed the old Xiaxia Pet reference tree from the public source tree; OCR code lives under `native/`.
- Refined expanded UI toward an 啊拼 / AI Quick Prompt desktop workbench layout with Chinese and English prompt panes.
- Completed the public OCR model and native dependency license review for Fire Eye OCR / PP-OCRv5 assets.
- Added lightweight release checks for prompt routing, generated template data, Skill matching, provider validation, accessibility markers, and packaging policy.
- Added public demo packaging and screenshot-based UI regression scripts for 1.0 release verification.

## 0.1.0

- Initial WinUI 3 prototype with global hotkey, local prompt structuring, clipboard import, local OCR routing, OpenAI-compatible model calls, template management, history, and privacy settings.
