# Migration From Xiaxia Pet

This project reuses selected ideas and native OCR components from the `xiaxia-pet` reference tree while rebuilding the app shell as a smaller WinUI prompt input method.

## Migrated Or Adapted

- `native/fire-eye-worker` is migrated from the Xiaxia Pet Fire Eye OCR worker and adapted to a standalone stdin/stdout JSON protocol.
- `native/ocr-rs-patched` is migrated from the Xiaxia Pet patched OCR library and adjusted for this workspace, Windows CMake/Ninja discovery, and OpenCL/Vulkan/CPU scheduling.
- `assets/fire_eye` contains the Fire Eye OCR model assets used by the worker.
- OCR result concepts such as provider ID, text, lines, words, bounding boxes, and backend metadata are preserved so the app can route OCR providers consistently.

## Rewritten In This Project

- The WinUI 3 compact window.
- Global hotkey handling.
- Foreground-window reading.
- Clipboard text import and result copy.
- Window and region capture services.
- OCR provider router and scheduler diagnostics.
- Local prompt structuring and scene prompt routing.
- OpenAI-compatible text and multimodal model client.
- Privacy settings, confirmation dialog, and local redaction.

## Intentionally Not Migrated

- Xiaxia Pet desktop pet UI.
- Vue/Tauri toolbar and frontend.
- Search service.
- TTS/reading features.
- Xiaxia favorites system.
- Agent-specific Q&A flows.
- Xia Run, ZeroClaw, and game center features.

## Compatibility Notes

The migrated OCR worker is treated as an optional enhancement. If the worker cannot start, times out, crashes, or returns an error, the app routes to the next available OCR provider instead of crashing.
