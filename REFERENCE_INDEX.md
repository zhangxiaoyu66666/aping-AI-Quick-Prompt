# Prompt Input Method 参考代码与依赖索引

本文记录从 `M:\AI_Pet\xiaxia-pet` 复制到本项目的 OCR 和核心依赖副本。天雷爆炸/大爆炸功能不属于 Prompt Input Method 的需求范围，相关文件只作为理解夏语 OCR 链路和隐私边界的参考。所有内容都是写入 `M:\AI_Pet\Prompt Input Method`，源桌宠工程未被修改。

## 1. 已复制的可复用代码

### TypeScript 参考逻辑

位置：`reference/xiaxia-pet/typescript`

- `services/bigBangTokenizer.ts`：只参考 OCR 文本顺序、行合并、空格恢复策略；不移植 Big Bang/大爆炸交互。
- `services/bigBangSelection.ts`：只参考选中文本还原规则；不移植词/短语/整行选择 UI。
- `services/ocrService.ts`：截图选区映射、裁剪、图像增强、OCR 调用编排。
- `services/ocrProvider.ts`：OCR Provider 类型、智能调度、fallback、失败隔离。
- `services/searchService.reference-only.ts`：外部搜索权限边界参考，不建议首版接入。
- `composables/useGlobalOcr.reference-only.ts`：OCR 状态机参考，不复制天雷爆炸功能和 Vue UI。
- `constants/settingsDefaults.reference-only.ts`：`thunderBlastEnabled`、`thunderBlastOcrProvider` 等设置项参考。
- `package.reference-only.json` / `package-lock.reference-only.json`：前端 OCR 依赖版本参考。

### Rust / Native 参考逻辑

位置：`reference/xiaxia-pet/rust`

- `src-tauri-src/selection.rs`：截图、前台窗口捕获、剪贴板、工具栏动作派发。
- `src-tauri-src/mouse_monitor.rs`：全局快捷键监听和鼠标位置记录。
- `src-tauri-src/ocr_native.rs`：Windows Media OCR、Windows AI OCR、火眼 OCR worker 调用。
- `src-tauri-src/companion_run.reference-only.rs`：Xia Run 动作入口参考，不作为本项目功能目标。
- `manifests/src-tauri-Cargo.toml`：原项目 Rust 依赖版本参考。
- `manifests/src-tauri-Cargo.lock`：原项目锁定版本参考。
- `manifests/src-tauri-build.rs`：原项目构建火眼 OCR worker 的逻辑参考。
- `manifests/tauri.conf.reference-only.json`：原项目 bundle resources 和临时截图协议参考。

### 文档与本地化参考

位置：`reference/xiaxia-pet/docs` 和 `reference/xiaxia-pet/i18n`

- `docs/thunder-blast-design.md`：只用于理解夏语现有 OCR/截图/隐私设计；天雷爆炸功能本身留在夏语。
- `docs/open-source-license-inventory.reference-only.md`：开源 license 审计参考。
- `i18n/zh-CN/messages/OcrService.ftl`：OCR service 文案参考。
- `i18n/zh-CN/messages/OcrProvider.ftl`：OCR provider 文案参考。
- `i18n/zh-CN/messages/Backend.reference-only.ftl`：后端 OCR 错误文案参考。
- `i18n/zh-CN/messages/ToolbarApp.reference-only.ftl`：工具栏文案参考。

## 2. 已复制的核心依赖

### 火眼 OCR worker

位置：`native/fire-eye-worker`

这是原项目里的独立 Rust OCR worker。它通过 stdin/stdout JSON 协议提供：

- capabilities
- warmup
- recognize
- recognizeCandidate
- shutdown

新项目后续的 WinUI 3/C# 侧可以通过 `Process` 启动该 worker，然后用 JSON 行协议通信。

### ocr-rs-patched

位置：`native/ocr-rs-patched`

这是原项目使用的 PaddleOCR/MNN OCR 引擎补丁版。`native/Cargo.toml` 已经把 crates.io 的 `ocr-rs` patch 到这个本地目录。

### 火眼 OCR 模型资产

位置：`assets/fire_eye`

包含：

- `PP-OCRv5_mobile_det.mnn`
- `PP-OCRv5_mobile_det_fp16.mnn`
- `PP-OCRv5_mobile_rec.mnn`
- `PP-OCRv5_mobile_rec_fp16.mnn`
- `ppocr_keys_v5.dict`

`native/fire-eye-worker/src/main.rs` 里的 `include_bytes!("../../../assets/fire_eye/...")` 路径在当前目录布局下仍然成立。

## 3. 独立 native workspace

已新增：`native/Cargo.toml`

它只管理本项目复制过来的 native OCR 依赖：

```powershell
cd "M:\AI_Pet\Prompt Input Method\native"
cargo build -p fire-eye-ocr-worker
```

这条构建命令只会在 Prompt Input Method 目录里产生 native 构建产物，不会修改 `M:\AI_Pet\xiaxia-pet`。

## 4. 后续接入顺序

1. WinUI 3 先接 `Windows.Media.Ocr`，让项目最小可用。
2. 用 `reference/xiaxia-pet/rust/src-tauri-src/selection.rs` 改写 C# 截图、剪贴板、前台窗口服务。
3. 参考 `bigBangTokenizer.ts` 和 `bigBangSelection.ts` 中的文本顺序、换行和空格恢复规则，实现 `OcrTextNormalizer` / `OcrContextBuilder`；不要移植大爆炸选择交互。
4. 构建 `native/fire-eye-worker`，用 C# `Process` 接入 JSON 行协议。
5. 按 `ocrProvider.ts` 的思路实现 `OcrProviderRouter`，加入失败隔离和 fallback。
6. 按 `ocr_native.rs` 的思路补 Windows AI OCR 和火眼 OCR worker warmup。

## 5. 不应直接带进新项目的东西

- Vue 组件和 Tauri 窗口本身不迁移。
- 天雷爆炸/大爆炸功能不迁移，留给夏语。
- `useGlobalOcr.reference-only.ts` 只看 OCR 状态机，不作为 WinUI 3 运行时代码。
- `searchService.reference-only.ts` 只看权限边界，第一版不要接网页搜索。
- `tauri.conf.reference-only.json` 只看资源打包思路，不作为新项目配置。

## 6. 源工程保护说明

本次操作只从 `M:\AI_Pet\xiaxia-pet` 读取并复制文件，没有对源工程执行写入、移动、删除、格式化或构建命令。源工程已有的未提交改动保持原样。
