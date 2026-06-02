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
