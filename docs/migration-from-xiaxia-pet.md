# OCR Integration Notes

This project integrates selected native OCR ideas into the public codebase under `native/fire-eye-worker` and `native/ocr-rs-patched`. It does not publish a separate `reference/xiaxia-pet` source tree.

## Migrated Or Adapted

- `native/fire-eye-worker` is the integrated Fire Eye OCR worker and uses a standalone stdin/stdout JSON protocol.
- `native/ocr-rs-patched` is the integrated patched OCR library adjusted for this workspace, Windows CMake/Ninja discovery, and OpenCL/Vulkan/CPU scheduling.
- The ignored local-only `assets/fire_eye` path is reserved for Fire Eye OCR model assets used by the worker.
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

## Not Included In The Public Repository

- The old `reference/xiaxia-pet` source tree.
- Desktop pet UI.
- Vue/Tauri toolbar and frontend.
- Search service.
- TTS/reading features.
- Favorites system.
- Agent-specific Q&A flows.
- Xia Run, ZeroClaw, and game center features.

## Compatibility Notes

The migrated OCR worker is treated as an optional enhancement. If the worker cannot start, times out, crashes, or returns an error, the app routes to the next available OCR provider instead of crashing.
