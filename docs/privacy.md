# Privacy

This document describes the intended data boundaries for Prompt Input Method. It is an engineering statement, not legal advice.

## Local By Default

- The compact window, scene detection, prompt structuring, clipboard import, and OCR context cleanup run locally.
- API keys are stored in Windows Credential Manager under `PromptInputMethod/OpenAICompatibleApiKey`.
- The bundled `appsettings.json` does not contain API keys.
- Local settings are stored under `%APPDATA%\PromptInputMethod\appsettings.json`.

## OCR

- `fire_eye_ocr` is the default local OCR backend and runs through `fire-eye-ocr-worker.exe` with embedded OCR model assets.
- `windows_media_ocr` runs locally through Windows APIs and is used only as a fallback when Fire Eye OCR is unavailable or fails.
- OCR only runs after the user clicks an OCR button.
- `窗口 OCR` captures only the recorded foreground window. It does not silently fall back to full-screen capture.
- `区域 OCR` captures the virtual screen only so the user can drag-select a region.
- OCR can be disabled in settings.

## Model Requests

Model calls are off by default. When enabled, only the prompt text shown in the confirmation dialog is sent to the configured OpenAI-compatible endpoint.

Before sending, the app can locally redact:

- API keys and secret keys.
- Tokens and bearer/session values.
- Email addresses.
- Mainland China phone numbers.
- Chinese ID numbers.
- Bank cards that pass a Luhn check.
- Sensitive URL query parameters such as `key`, `password`, `token`, `session`, and `code`.

Redaction is best-effort and should not be treated as a complete DLP system.

## Images And Multimodal Models

Images are not sent to model providers by default.

Images are sent only when all of these are true:

- Model external requests are enabled.
- Image external requests are enabled.
- The user explicitly clicks `附图给模型` and selects an image.
- The user confirms the model send dialog.

When sent, images are encoded as OpenAI-compatible `image_url` data URLs. The current single-image size limit is 8 MB.

## Logs And Diagnostics

- The app should not log full prompts, API keys, OCR full text, or image bytes.
- OCR scheduler diagnostics may be written to `%APPDATA%\PromptInputMethod\prompt_input_method.ocr.scheduler.v1.json` and contain provider attempt metadata, not full OCR text.
- The settings panel includes `清除历史`, which removes OCR scheduler diagnostics and temporary OCR image files.
- The settings panel includes `清除收藏`, which removes the local prompt favorites file.

## Favorites

- Prompt favorites are stored locally in `%APPDATA%\PromptInputMethod\prompt_input_method.prompt_favorites.v1.json`.
- Favorites are not synced and are not uploaded automatically.
- Favorite contents may contain user business information, so users should review them before sharing logs or release bundles.

## External Services

The app does not choose a model provider for the user. If the user configures a remote endpoint, that provider receives confirmed prompt text and any explicitly attached images. Review that provider's privacy policy before use.
