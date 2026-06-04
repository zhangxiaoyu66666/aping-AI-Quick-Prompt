# AGENTS.md

## Project Overview

PromptBar is a Windows desktop prompt workbench built with WinUI 3 and .NET 8. It helps users turn rough intent, clipboard context, OCR text, image attachments, and mounted `SKILL.md` workflows into reusable prompts or direct Skill execution results.

## Repository Layout

- `src/PromptInputMethod.App/`: WinUI 3 desktop app, services, localization, settings, OCR routing, and OpenAI-compatible model calls.
- `src/PromptInputMethod.Core/`: local scene detection and prompt structuring primitives.
- `native/`: optional Rust native OCR worker and patched OCR dependencies.
- `assets/`: local OCR model assets and project images.
- `docs/`: privacy, licensing, open-source references, and maintenance notes.
- `reference/`: design and implementation references.

## Working Rules

- Inspect existing code before editing.
- Prefer minimal patches over broad rewrites.
- Preserve the current WinUI 3, service-based structure, localization system, and Fluent visual style.
- Do not introduce new dependencies without an explicit reason.
- Do not modify vendored or copied upstream projects unless the task is specifically about them.
- Do not edit generated `bin/`, `obj/`, `.vs/`, build artifacts, local user settings, or OCR diagnostics.
- Do not commit API keys, local model provider credentials, screenshots containing secrets, or user prompt history.

## Agent Writing Rules

- Do not write changelogs, release notes, README updates, or roadmap entries as a raw task log.
- For release notes, use `更新内容` + `使用说明` as the default structure unless the user explicitly asks for a different format.
- Start release-facing text from the user's point of view: what changed in the app, why it matters, what is safer/faster/easier now, and what the user should notice after installing.
- Group changes by product meaning, not by implementation order. Prefer sections such as `Highlights`, `Changed`, `Fixed`, `Privacy / Packaging`, and `Verification` when a release has more than a few items.
- Keep implementation details only when they explain user impact, compatibility, privacy boundaries, packaging behavior, or future maintenance risk.
- Avoid filler such as "bumped version", "updated files", "added checks", or "documented candidates" unless the sentence explains the practical consequence.
- For open-source attribution or license notes, state the boundary clearly: what is bundled, what is only a research reference, what is original clean-room content, and what is excluded.
- For GitHub community releases, explicitly call out store-only or branch-only features when there is a risk of confusing users, especially cloud sync and proprietary service integrations.
- Do not include local experiments, uncommitted user changes, Microsoft Store-only features, cloud sync work, proprietary service hooks, or branch-only code in GitHub community changelogs, release notes, packages, or release assets unless the user explicitly asks for that scope.
- Before writing release notes or preparing a package, inspect `git status`, the release branch, and the intended commit range. Treat dirty working-tree changes as user work by default and exclude them from release content.
- If the user says they have already edited release text, do not overwrite it. Ask or only make the exact scoped change requested.
- Before publishing a release, reread the changelog as a new user. If it does not answer "should I download this, and what changed for me?", rewrite it.

## Build And Verification

Use Visual Studio MSBuild for the WinUI app:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" src\PromptInputMethod.App\PromptInputMethod.App.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /p:OutputPath=bin\CodexVerify\ /m /v:minimal
```

Fallback for machines with Visual Studio 2022:

```powershell
msbuild src\PromptInputMethod.App\PromptInputMethod.App.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
```

Optional native OCR worker:

```powershell
cargo build -p fire-eye-ocr-worker --manifest-path native\Cargo.toml --release
```

If a command cannot run because the local machine lacks Visual Studio, Windows SDK, .NET 8, Rust, CMake, or native assets, report the missing prerequisite clearly.

## Code Style

- Keep C# nullable warnings clean.
- Keep UI text localizable where practical.
- Use existing services and helpers before adding new abstractions.
- Keep comments sparse and useful; explain platform quirks rather than obvious code.
- Match existing file organization and naming.

## Safety And Privacy

- OCR should remain opt-in and local unless the user explicitly sends image context to a model provider.
- Model text calls must respect the privacy settings and local redaction path.
- API keys belong in Windows Credential Manager, not in repository files.
- Mounted Skills are text workflows; do not execute external scripts or destructive actions from a Skill without explicit implementation and user confirmation.

## Definition Of Done

- The requested behavior is implemented or the root cause is clearly identified.
- Relevant checks have been run or skipped with a stated reason.
- The final response lists changed files and verification results.
- No unrelated user changes are reverted.
