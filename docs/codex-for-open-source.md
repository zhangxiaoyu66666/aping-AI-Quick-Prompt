# Codex for Open Source Preparation

This note is a practical checklist for making AI Quick Prompt easier to evaluate as an open-source project and easier to maintain with AI coding agents.

The project is released under `GPL-3.0-or-later`; third-party template sources, bundled Skills, runtime packages, and model assets retain their own licenses and notices.

## Project Pitch

AI Quick Prompt is a Windows Fluent prompt launcher and prompt workbench. It helps users turn rough intent, OCR text, screenshots, prompt templates, and mounted `SKILL.md` workflows into structured prompts for general LLMs, image models, video models, coding agents, and local OpenAI-compatible providers.

The project is useful for open source because it explores a desktop-native workflow for prompt engineering rather than another web prompt page:

- Windows-first WinUI 3 shell.
- Local OCR and editable screenshot text.
- OpenAI-compatible model provider support.
- Built-in and user-imported prompt libraries.
- Skill mounting and matching for reusable agent workflows.
- Multilingual JSON language packs.
- Privacy-aware local defaults around OCR, credentials, and model calls.

## What API Credits Would Be Used For

If the project receives OpenAI API credits, the highest-impact uses would be:

- testing prompt optimization quality across general LLM, image, video, AI coding, and Skill workflows;
- evaluating the chat refinement loop that asks users for missing requirements before producing the final prompt;
- building regression fixtures for prompt routing, template application, OCR-to-requirement cleanup, and Skill matching;
- generating multilingual UI copy and documentation drafts for Chinese and English;
- validating OpenAI-compatible request and response handling against current model behavior;
- creating examples and demos that show how desktop OCR, templates, and Skills improve real workflows.

## Current Strengths

- The app is already a real Windows desktop app, not a mockup.
- It has concrete model-provider, template, OCR, localization, and privacy code paths.
- It includes an emerging Skill system that can mount `SKILL.md` workflows and match them to user requests.
- It is useful even when model calls are unavailable because local prompt structuring still works.
- It has a clear target audience: Windows users who regularly move between models, coding agents, and prompt-heavy creative tools.

## Gaps To Close Before Public Push

- Keep the completed OCR model redistribution review in `docs/ocr-model-license-review.md` synchronized with release packages.
- Confirm notices for vendored MNN and patched `ocr-rs` files.
- Add a real security contact before publishing the repository.
- Add screenshots or a short demo video to the README once UI stabilizes.
- Add tests for template import/export, language pack loading, Skill matching, and model endpoint validation.
- Keep large copied upstream trees and local OCR model assets ignored; publish generated app data, bundled Skill notices, and attribution documents instead.
- Keep the GPL project license, `NOTICE.md`, and third-party notice files synchronized before public pushes.

## Suggested Application Summary

AI Quick Prompt is an open-source Windows prompt workbench built with WinUI 3 and .NET 8. It combines local OCR, editable screenshot text, prompt template libraries, OpenAI-compatible providers, multilingual UI packs, and mounted `SKILL.md` workflows into one desktop-native prompt launcher. Codex support would help build regression tests, improve prompt quality, document Skill workflows, and make the project easier for other Windows developers and prompt-tool builders to contribute to.

## Maintenance Checklist

- Keep `README.md` accurate after UI or workflow changes.
- Update `CHANGELOG.md` for user-visible behavior.
- Keep `THIRD_PARTY_NOTICES.md` and `docs/license-inventory.md` current.
- Keep GPL contribution and project-license wording consistent across README, CONTRIBUTING, NOTICE, and release artifacts.
- Run the WinUI MSBuild command before release branches.
- Do not publish binaries with unreviewed third-party model assets. The Fire Eye OCR / PP-OCRv5 assets are reviewed for 1.0 when shipped with the documented Apache-2.0 notices.
- Keep prompt templates attributable to their original open-source project names.
- Keep local-only upstream reference folders and OCR model assets out of Git until their redistribution path is deliberately reviewed.
- Keep example Skills under `examples/skills/` small, reviewable, and free of private user data.
