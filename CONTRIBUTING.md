# Contributing

Thanks for helping improve 啊拼 / AI Quick Prompt. The project is early, Windows-focused, GPL-licensed, and privacy-sensitive, so small focused patches are easiest to review.

## License Of Contributions

By submitting a contribution, you agree that your contribution is licensed under the same license as the project: GNU General Public License version 3 or any later version (`GPL-3.0-or-later`).

Do not contribute code, templates, model assets, images, or copied files unless you have the right to provide them under compatible terms and can keep the required upstream notices.

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
- Update `README.md`, `THIRD_PARTY_NOTICES.md`, or `docs/license-inventory.md` when a change adds a dependency, bundled Skill, template source, model asset, or copied upstream material.

## Code Guidelines

- Follow the existing WinUI 3 layout and service patterns.
- Do not introduce a new UI framework or design system.
- Do not add permanent compatibility shims unless they are required by the current behavior.
- Do not modify vendored upstream projects or copied reference projects unless the PR is specifically about them.
- Keep user data, prompts, API keys, screenshots, and local app settings out of commits.
- Keep GPL headers, SPDX metadata, and third-party notices accurate when adding new source files or assets.

## Good First Areas

- Localization improvements.
- Template and Skill documentation.
- Accessibility and keyboard navigation polish.
- README and setup improvements.
- Tests or small diagnostics for prompt routing and Skill matching.
