# Contributing

Thanks for helping improve PromptBar. The project is early, Windows-focused, and privacy-sensitive, so small focused patches are easiest to review.

## Development Setup

Requirements:

- Windows 10 1809 or later.
- Visual Studio 2022 or newer with .NET desktop development, C++ build tools, Windows SDK, CMake, and MSBuild.
- .NET 8 SDK.
- Rust stable toolchain if you are working on the optional native OCR worker.

Build the WinUI app:

```powershell
msbuild src\PromptInputMethod.App\PromptInputMethod.App.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
```

Build the optional OCR worker:

```powershell
cargo build -p fire-eye-ocr-worker --manifest-path native\Cargo.toml --release
```

## Pull Request Guidelines

- Keep pull requests focused on one feature, fix, or documentation change.
- Explain the user-visible behavior change.
- Include screenshots or short screen recordings for UI changes when possible.
- Note whether model calls, OCR, clipboard behavior, or local storage behavior changed.
- Run the relevant build or explain why it could not be run.

## Code Guidelines

- Follow the existing WinUI 3 layout and service patterns.
- Do not introduce a new UI framework or design system.
- Do not add permanent compatibility shims unless they are required by the current behavior.
- Do not modify vendored upstream projects or copied reference projects unless the PR is specifically about them.
- Keep user data, prompts, API keys, screenshots, and local app settings out of commits.

## Good First Areas

- Localization improvements.
- Template and Skill documentation.
- Accessibility and keyboard navigation polish.
- README and setup improvements.
- Tests or small diagnostics for prompt routing and Skill matching.
