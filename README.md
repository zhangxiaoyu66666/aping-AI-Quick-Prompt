# AIPIN / AI Quick Prompt

[English](README.md) | [简体中文](README.zh-CN.md)

AIPIN is a Windows desktop prompt workbench for people who move between ChatGPT, Claude, Gemini, local OpenAI-compatible models, image models, video models, and coding agents. It turns rough intent, OCR text, clipboard context, image references, local templates, and mounted `SKILL.md` workflows into clearer prompts that are easier to reuse.

Current public community version: `1.0.5`

Download: [GitHub Releases](https://github.com/zhangxiaoyu66666/aping-AI-Quick-Prompt/releases)

## 1. Software Features

- Chat-style requirement refinement: start with a rough request, then continue the conversation until the prompt is specific enough to use.
- Chinese and English prompt panes: keep localized Chinese output and English prompt output visible side by side.
- Optimization targets: general LLM, text-to-image, ComfyUI / Stable Diffusion, Jimeng / Seedance, Veo 3, AI coding, Skill workflows, and custom targets.
- Field-level copy buttons: copy positive prompt, negative prompt, parameters, task, constraints, verification, storyboard, timing, and other target-specific fields.
- Skill mounting: mount a folder containing `SKILL.md`; AIPIN can match it to the current request and inject it as high-priority workflow context.
- Template and quick prompt library: reuse prompt templates, common snippets, favorite prompts, and imported JSON packs.
- OCR input: use local Fire Eye OCR first, with Windows Media OCR as a fallback when the worker is unavailable.
- OpenAI-compatible model providers: configure base URL, API key, and model name for local or remote chat-completions-compatible endpoints.
- Privacy-aware defaults: API keys are stored in Windows Credential Manager; OCR is local unless the user explicitly sends image context to a model provider.
- GitHub community edition boundary: this branch does not ship OneDrive / WebDAV cloud sync UI, settings, services, or `PromptInputMethod.Core.Sync`.

## 2. Screenshots

![AIPIN main workbench](docs/screenshots/en/workbench.png)

| Quick prompt editor | Skill management |
| --- | --- |
| ![Quick prompt editor](docs/screenshots/en/quick-prompts-dialog.png) | ![Skill management page](docs/screenshots/en/skill-management.png) |

| Quick prompt library | Optimization targets |
| --- | --- |
| ![Quick prompt library](docs/screenshots/en/quick-prompts-library.png) | ![Optimization target management](docs/screenshots/en/optimization-targets.png) |

## 3. Technology Stack

- Desktop UI: WinUI 3, Windows App SDK, .NET 8, Fluent-style Windows desktop layout.
- Core logic: `PromptInputMethod.Core` for local prompt routing and structuring primitives.
- Local storage: SQLite through `Microsoft.Data.Sqlite`.
- Model calls: OpenAI-compatible chat-completions protocol with configurable providers, model refresh, endpoint validation, streaming output, and deep-thinking display.
- OCR: optional Rust native worker under `native/`, with Fire Eye OCR / PP-OCR related assets reviewed separately.
- Secrets: Windows Credential Manager for API keys and provider credentials.
- Packaging: public demo zip, sideload MSIX, and Microsoft Store `.msixupload` helper scripts.
- Release checks: lightweight .NET release checks for prompt routing, template import/export, Skill matching, provider validation, UI coverage, packaging policy, and GitHub cloud-sync exclusion.

Related docs:

- [Privacy model](docs/privacy.md)
- [License inventory](docs/license-inventory.md)
- [Open-source references](docs/open-source-references.md)
- [Optimization target format](docs/optimization-target-format.md)
- [Microsoft Store submission notes](docs/microsoft-store-submission.md)

## 4. Future Plan

- Windows stability first: tray behavior, global hotkey recovery, OCR capture reliability, compact window consistency, high-DPI layout, and faster template / Skill switching.
- Better prompt workflows: richer field adapters for image, video, coding, academic writing, and user-imported optimization targets.
- Stronger local privacy controls: clearer model egress confirmation, local redaction improvements, and safer audit boundaries.
- Template and Skill ecosystem: stable import/export formats for templates, quick prompts, Skills, language packs, and optimization targets.
- macOS research: split shared core logic before choosing Avalonia, .NET MAUI, SwiftUI, or another shell.
- Cross-platform long-term direction: keep Windows native quality while making prompt data and Skill workflows portable.
- Store/commercial branch separation: cloud or paid features, if developed, stay isolated from the public GPL community branch unless intentionally released.

The longer roadmap lives in [PROMPT_INPUT_METHOD_ROADMAP.md](PROMPT_INPUT_METHOD_ROADMAP.md).

## 5. Author Background And Sponsorship

I am Zhang Xiaoyu (章笑语), the developer of AIPIN. I am a second-level disabled developer, and my story has appeared in CCTV-related coverage. For the past half year, I have also been building an AI narrative game project, exploring how AI can help ordinary people write stories, characters, branching choices, and eventually create their own galgame-style works.

AIPIN is one of the first pieces of that longer road. It starts as a prompt workbench, but what I really want to do is lower the barrier between a rough idea and a finished interactive story, so that people who do not have a full studio, a large budget, or perfect physical conditions can still make something personal, playable, and alive.

This project is open source, but keeping it alive still takes real time and real living pressure: Windows packaging, OCR testing, model-provider compatibility, localization, documentation, issue triage, AI narrative game experiments, and future macOS research all need sustained work. If AIPIN helps your workflow, please consider starring the repository, sharing it, reporting useful issues, or sponsoring me.

- GitHub profile README: [zhangxiaoyu66666](https://github.com/zhangxiaoyu66666)
- Profile README repository: [zhangxiaoyu66666/zhangxiaoyu66666](https://github.com/zhangxiaoyu66666/zhangxiaoyu66666)
- Sponsor page: [github.com/sponsors/zhangxiaoyu66666](https://github.com/sponsors/zhangxiaoyu66666)

My goal is not only to make one prompt tool, but to prove that an independent disabled developer can keep building useful AI software, keep telling stories, and keep moving toward a future where everyone can write their own galgame, half joking, half very serious.

## 6. Build Instructions

Requirements:

- Windows 10 1809 or later.
- Visual Studio 2022 or newer with .NET desktop development, C++ build tools, Windows SDK, MSBuild, and CMake tools if building native OCR.
- .NET 8 SDK.
- Rust stable toolchain if building the optional Fire Eye OCR worker.

Build the WinUI app with Visual Studio MSBuild:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" src\PromptInputMethod.App\PromptInputMethod.App.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /p:OutputPath=bin\CodexVerify\ /m /v:minimal
```

Fallback when `msbuild` is available on `PATH`:

```powershell
msbuild src\PromptInputMethod.App\PromptInputMethod.App.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
```

Build the optional OCR worker:

```powershell
cargo build -p fire-eye-ocr-worker --manifest-path native\Cargo.toml --release
```

Run release checks:

```powershell
dotnet run --project tests\PromptInputMethod.ReleaseChecks\PromptInputMethod.ReleaseChecks.csproj --configuration Release
```

Create a public demo package:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\New-PublicDemoPackage.ps1
```

Create a signed MSIX sideload package:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\New-MsixPackage.ps1
```

`dotnet build` may fail WinUI PRI generation on some machines. Prefer Visual Studio MSBuild for the app project.
