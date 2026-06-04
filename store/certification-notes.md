# Microsoft Store Certification Notes

Use this as the Partner Center "Notes for certification" field. Adjust it if Store-only cloud features are enabled in the submitted build.

```text
AI Quick Prompt is a packaged WinUI 3 desktop app for local-first prompt drafting.

No account is required for basic use. The app can run without an API key using local prompt structuring and template workflows. Optional model calls require the user to configure an OpenAI-compatible endpoint and API key. API keys are stored in Windows Credential Manager.

The app declares runFullTrust because it provides desktop workflows: tray entry, global hotkey, local OCR worker process, clipboard integration, screenshot/region OCR, and foreground-window context capture. OCR is local by default. Images and prompt text are sent to a model provider only when the user explicitly sends a request and model egress is enabled.

If Store-only cloud features are enabled in this branch, use this test path:
1. Launch the app from Start.
2. Use the default local prompt structuring flow without logging in.
3. Open Settings to verify OCR, privacy, hotkey, and language controls.
4. If cloud login is enabled, use the demo credentials provided below.

Demo account:
Not required for the current local-first package.
```
