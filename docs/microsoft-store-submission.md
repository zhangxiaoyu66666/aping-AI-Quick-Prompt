# Microsoft Store Submission Guide

This guide is for the `microsoft-store` branch of 啊拼 / AI Quick Prompt.

The public GitHub branch stays GPL and local-first. Any Store-only feature must be testable, documented, and kept out of the public GitHub branch unless intentionally released.

## Official Submission Facts

- Partner Center accepts `.msix`, `.msixupload`, `.msixbundle`, `.appx`, `.appxupload`, and `.appxbundle` package files. Microsoft recommends `.msixupload` / `.appxupload` for Windows 10 and later submissions.
- Microsoft Store-delivered MSIX packages are signed with a trusted certificate for customers, so customers do not need to install the side-loading certificate used by local test packages.
- A manually created `.msixupload` can contain one or more app packages or bundles plus an optional `.appxsym` symbol package.
- Desktop Bridge / Win32 products need a privacy policy URL in Partner Center when they access, collect, or transmit personal information.
- Store listings need accurate metadata, package capabilities that match app functionality, and at least one desktop screenshot. Microsoft recommends several high-quality screenshots.

Official references:

- Upload packages: https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/upload-app-packages
- Create upload package: https://learn.microsoft.com/en-us/windows/msix/package/packaging-uwp-apps
- Screenshots and images: https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/screenshots-and-images
- Store policy: https://learn.microsoft.com/en-us/windows/apps/publish/store-policy-archive/store-policy-7-17

## Partner Center Values To Copy

After reserving the app name in Partner Center, copy these exact values into the packaging command:

| Partner Center field | Script parameter | Notes |
| --- | --- | --- |
| Package/Identity/Name | `-PackageName` | Must match the reserved app package identity. |
| Package/Identity/Publisher | `-Publisher` | Must match Partner Center exactly, for example `CN=...`. |
| Publisher display name | `-PublisherDisplayName` | Use the verified developer or studio name. |
| Store listing product name | `-DisplayName` | Use `AI Quick Prompt` for the package manifest. Use Partner Center Store listing localization for `啊拼`. |

Do not submit a package built with the placeholder identity unless Partner Center generated the same identity for your account.

## Build The Store Upload Package

Run from the repository root on the `microsoft-store` branch:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\New-StoreMsixUpload.ps1 `
  -PackageName "<Partner Center Package/Identity/Name>" `
  -Publisher "<Partner Center Package/Identity/Publisher>" `
  -PublisherDisplayName "<Partner Center publisher display name>" `
  -DisplayName "AI Quick Prompt" `
  -Version "1.0.0.0"
```

If you need a smoke test before reserving the Partner Center identity:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\New-StoreMsixUpload.ps1 -AllowPlaceholderIdentity -SkipNativeBuild
```

The upload artifact is written to:

```text
artifacts/store/AI-Quick-Prompt-Store-<version>-<architecture>.msixupload
```

If you want a locally installable signed MSIX before submission, pass either:

```powershell
-CertificateThumbprint "<thumbprint>"
```

or:

```powershell
-PfxPath "<path-to-pfx>" -PfxPassword "<password>"
```

Do not commit certificates, PFX files, passwords, Partner Center secrets, service credentials, or private package IDs that you do not want public.

## Store Listing Checklist

Use the drafted listing text in:

- `store/listing.zh-CN.md`
- `store/listing.en-US.md`
- `store/certification-notes.md`

Recommended Partner Center fields:

| Field | Suggested value |
| --- | --- |
| Category | Productivity |
| Pricing | Free for first Store submission, unless Store-only paid features are already implemented and testable. |
| Markets | Start with China and English-speaking markets, then expand after first certification pass. |
| Age rating | Complete IARC honestly. Current app has no game content, no gambling, no user-generated public sharing, no built-in adult content. |
| Privacy policy URL | `https://github.com/zhangxiaoyu66666/aping-AI-Quick-Prompt/blob/main/docs/privacy.md` or a dedicated website privacy page. |
| Support URL | GitHub issues page or a dedicated support/contact page. |
| Package family | Windows Desktop only. |
| Capabilities note | `runFullTrust` is used because this is a packaged WinUI desktop app with tray, global hotkey, local OCR worker, clipboard, and window capture workflows. |

## Screenshot Plan

Desktop screenshots must be PNG files, landscape or portrait, no larger than 50 MB, and at least 1366 x 768.

Prepare 4 desktop screenshots:

1. Main prompt workbench with input request, optimization target, and prompt panes.
2. Template / Skill selector showing templates separated from Skill workflows.
3. Model management showing OpenAI-compatible provider configuration without API keys visible.
4. Settings showing privacy, OCR, hotkey, and language options.

Avoid showing:

- API keys or endpoint secrets.
- Private prompt history.
- Third-party copyrighted screenshots.
- Unreleased Store-only features unless they ship in the submitted package and are testable.

## Certification Notes

Put this summary in the Partner Center certification notes, adjusted for the final package:

```text
AI Quick Prompt is a packaged WinUI 3 desktop app for local-first prompt drafting.

No account is required for basic use. The app can run without an API key using local prompt structuring and template workflows. Optional model calls require the user to configure an OpenAI-compatible endpoint and API key. API keys are stored in Windows Credential Manager.

The app declares runFullTrust because it provides desktop workflows: tray entry, global hotkey, local OCR worker process, clipboard integration, screenshot/region OCR, and foreground-window context capture. OCR is local by default. Images and prompt text are sent to a model provider only when the user explicitly sends a request and model egress is enabled.

If Store-only online features are enabled in this branch, provide a demo account or test instructions here.
```

## Final Pre-Submission Checks

Run these before uploading to Partner Center:

```powershell
dotnet run --project tests\PromptInputMethod.ReleaseChecks\PromptInputMethod.ReleaseChecks.csproj --configuration Release
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Invoke-UiScreenshotChecks.ps1
```

```powershell
powershell -ExecutionPolicy Bypass -File scripts\New-StoreMsixUpload.ps1 `
  -PackageName "<Partner Center Package/Identity/Name>" `
  -Publisher "<Partner Center Package/Identity/Publisher>" `
  -PublisherDisplayName "<Partner Center publisher display name>" `
  -DisplayName "AI Quick Prompt" `
  -Version "1.0.0.0"
```

If Partner Center validation reports package identity or publisher errors, fix the packaging command values first. Do not edit random manifest fields by hand.
