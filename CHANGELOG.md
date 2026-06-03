# Changelog

## 1.0.3 - 2026-06-03

- Added ComfyUI and Stable Diffusion prompt output adapters for positive prompts, negative prompts, sampling parameters, and workflow-ready fields.
- Expanded the original clean-room Jimeng / Dreamina / Seedance target and documented candidate Skill repositories under review.
- Reworked optimization target selection into a two-level category and target picker for text, image, video, Agent, and custom workflows.
- Kept cloud sync code out of the GitHub community release and added release checks to prevent OneDrive/WebDAV implementation leakage.
- Bumped app, core, public demo, and MSIX package version metadata to 1.0.3.

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
