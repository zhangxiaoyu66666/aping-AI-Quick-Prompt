# Security Policy

## Supported Versions

PromptBar is pre-1.0. Security fixes are applied to the current main development line.

## Reporting A Vulnerability

Please do not open a public issue for a vulnerability that exposes secrets, user prompts, local files, or model provider credentials.

Report privately to:

- Maintainer: XiaYu Studio
- Preferred channel after the repository is public: GitHub Security Advisories.
- If GitHub Security Advisories are not available yet, contact the maintainer privately before opening a public issue.

Include:

- Affected version or commit.
- Steps to reproduce.
- Impact and affected data.
- Whether OCR, clipboard, model calls, mounted Skills, or local storage are involved.

## Security Boundaries

- API keys are intended to be stored in Windows Credential Manager.
- OCR runs locally unless the user explicitly attaches an image for a multimodal model call and enables image external requests.
- Model provider calls can send user text to the configured OpenAI-compatible endpoint when enabled.
- Local redaction can reduce accidental secret exposure, but it is not a formal data-loss-prevention system.
- Mounted Skills are treated as text workflows. They are not a permission grant to run local scripts, delete files, access credentials, or operate outside the workspace.

## Out Of Scope

- Reports that require a malicious local administrator.
- Issues caused by user-provided model endpoints logging submitted prompts.
- Third-party model behavior outside this application's control.
