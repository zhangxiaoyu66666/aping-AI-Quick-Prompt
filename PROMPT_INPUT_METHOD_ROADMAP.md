# Prompt Input Method 开源项目计划书

目标目录：`M:\AI_Pet\Prompt Input Method`

## 1. 项目定位

Prompt Input Method 是一个 Windows 桌面提示词输入法工具，采用 WinUI 3 开发。它的核心行为是：

1. 用户按全局快捷键唤起轻量悬浮窗。
2. 工具判断用户当前场景，例如正在写代码、问聊天模型、写邮件、看网页、处理报错、整理文档或操作软件。
3. 用户输入一句原始要求，不需要自己整理上下文。
4. 工具结合当前场景、前台窗口、选区、剪贴板和 OCR 上下文，立即生成适合当前场景的提示词。
5. 输出给其他大模型更容易理解和执行的结构化 Prompt。
6. 用户可以一键复制、粘贴到当前窗口，或保留多个候选版本。

这个项目不是夏语桌宠的前端移植版，也不是天雷爆炸/大爆炸功能的复制。天雷爆炸继续留在夏语里，Prompt Input Method 只借鉴其中的 OCR、截图、快捷键、悬浮窗、模型调用和隐私控制经验，用来服务“当前场景判断 + 即时提示词生成”这一条主线；Vue 前端、桌宠、角色系统、设置窗口、聊天窗口、游戏中心等都不复制。

## 2. 第一版产品形态

### 2.1 全局入口

- 默认快捷键：`Ctrl + Shift + Space`。
- 备用快捷键：可由用户自定义；默认不占用夏语天雷爆炸使用的 `Ctrl + Shift + X`。
- 按下快捷键后，在鼠标附近或屏幕中央显示 WinUI 3 精简悬浮窗。
- 精简悬浮窗默认置顶、轻量、可 ESC 关闭。
- 输入完成后支持：
  - `Ctrl + Enter`：结构化。
  - `Enter`：换行或提交，可由设置控制。
  - `Ctrl + C`：复制当前结构化结果。
  - `Ctrl + V` / 按钮：把结果粘贴回前台应用。

### 2.2 UI 分层

Prompt Input Method 有两个形态：快捷键呼出的精简版，以及用户主动展开后的完整工作台。

#### 精简版：快捷键呼出的两框 UI

精简版是默认入口，必须像输入法一样轻。它只服务一件事：用户输入要求，马上得到提示词。

- 顶部只保留产品名、设置按钮、关闭/最小化等基础窗口控件。
- 第一块是“用户需求”输入框。
- 第二块是“优化结果”输出框。
- 场景识别只显示必要的候选按钮，例如文生文、文生图、即梦、Veo 3；不要出现完整侧边栏。
- 主要按钮只有“优化提示词”。
- 辅助按钮只保留复制、收藏当前提示词、翻译英文、导出或插入当前窗口这类高频动作。
- 不显示首页、历史记录、我的模板、常用片段、模型管理、会员、帮助等完整导航。
- 默认不展示大量增强选项；需要时点设置或展开进入完整工作台。

精简版的验收标准：

- 用户按快捷键后 1 秒内能开始输入。
- 首屏只能看到“输入要求”和“结果”两件核心事。
- 不需要理解复杂配置也能完成一次提示词优化。
- 所有按钮都不能抢输入框的视觉主导权。

#### 展开版：完整工作台 UI

展开版是用户主动打开的完整管理界面，适合放更多功能按钮和导航。

- 左侧导航：首页、历史记录、我的模板、常用片段、模型管理、设置中心、帮助与反馈。
- 中间主工作区：用户需求、场景识别、中文优化提示词、英文翻译提示词。
- 右侧面板：目标平台/优化模式、增强选项、模型兼容提示。
- 底部状态栏：场景识别结果、输出语言、模型兼容范围。
- 展开版可以有“清空、粘贴、复制提示词、收藏提示词、插入到当前窗口、保存模板、导出”等完整按钮。
- 展开版负责收藏管理：按场景、目标平台、标签、语言、最近使用排序。

展开版不能替代精简版。快捷键默认永远打开精简版；用户点“展开”或从托盘/开始菜单打开时才进入完整工作台。

### 2.3 当前场景判断

Prompt Input Method 的第一判断不是“用户写了什么”，而是“用户此刻在哪个工作场景里”。场景判断用于决定提示词模板、上下文抓取方式和输出风格。

第一版需要支持这些场景：

- 代码场景：VS Code、Visual Studio、JetBrains、终端、GitHub 页面、报错窗口。
- 聊天模型场景：ChatGPT、Claude、Gemini、豆包、Kimi、通义、Open WebUI、LM Studio 等网页或客户端。
- 文档写作场景：Word、Notepad、Obsidian、Typora、Markdown 编辑器。
- 邮件/沟通场景：Outlook、网页邮箱、微信/QQ/飞书/Teams 输入框。
- 浏览器阅读场景：网页、PDF、文档页面、搜索结果页。
- 软件操作场景：设置页、安装器、报错弹窗、业务系统界面。
- 未知场景：只使用用户输入和可选 OCR，不做过度推断。

场景判断信号按低风险优先：

1. 前台进程名、窗口标题、窗口类名。
2. 当前选区或剪贴板文本。
3. 用户主动触发的 OCR 当前窗口文本。
4. 用户输入的要求。
5. 可选的应用配置规则，例如把某个 exe 固定标记为代码场景或聊天模型场景。

### 2.4 即时提示词生成

用户输入要求后，工具要立即产出提示词，而不是让用户进入复杂编辑流程。

推荐主流程：

```text
快捷键唤起
  -> 记录前台窗口和鼠标位置
  -> 判断当前场景
  -> 用户输入一句要求
  -> 可选读取选区 / OCR 当前窗口 / OCR 截图区域
  -> 选择对应场景模板
  -> 调用大模型生成 Prompt
  -> 用户复制或粘贴到当前目标应用
```

不同场景的输出策略：

- 代码场景：生成包含目标、涉及文件/报错、约束、期望修改、验证方式的开发提示词。
- 聊天模型场景：生成可直接发给当前模型的清晰指令，减少寒暄和重复背景。
- 文档场景：生成写作/润色/总结/改写提示词，明确语气、结构和交付格式。
- 邮件沟通场景：生成收件人、目的、语气、关键点、禁忌表达。
- 浏览器阅读场景：生成总结、提问、对比、提炼行动项的提示词。
- 软件操作场景：生成让模型根据界面/报错指导下一步操作的提示词。

### 2.5 核心输出格式

第一版只做“让其他大模型更懂需求”的结构化，不做复杂 Agent 编排。建议默认输出：

```text
当前场景：

目标应用：

任务目标：

背景上下文：

用户原始需求：

关键约束：

期望输出：

验收标准：

需要模型主动澄清的问题：

可直接发送给模型的最终 Prompt：
```

### 2.6 OCR 辅助输入

第一版保留三种输入来源：

- 手动输入：用户直接写需求。
- 读取剪贴板 / 当前选区：模拟复制，读取文本。
- OCR 当前窗口或截图区域：把屏幕文字提取为上下文。

Prompt Input Method 的 OCR 不是天雷爆炸，也不提供大爆炸式文本块交互。OCR 在这里只负责把当前窗口、选区截图或用户指定区域里的文字提取为提示词上下文。

- 截图只服务于“取上下文”。
- 选区只服务于“限定上下文范围”。
- OCR 文本默认进入可编辑上下文框，而不是进入词/短语/整行点击选择界面。
- 用户确认后，文本上下文才会参与模型提示词生成。

### 2.7 提示词收藏

有些场景会反复使用相似提示词，Prompt Input Method 需要自己的提示词收藏功能。这里不是复制夏语的收藏系统，而是为提示词输入法新建轻量收藏库。

收藏对象：

- 用户原始需求。
- 优化后的中文提示词。
- 英文翻译提示词。
- 当前场景类型，例如代码、文生图、Veo 3、邮件、网页总结。
- 目标平台/模型，例如 ChatGPT、Claude、Midjourney、即梦、Veo 3。
- 用户标签，例如 UI、视频、代码审查、邮件、产品需求。
- 创建时间、最近使用时间、使用次数。

精简版收藏行为：

- 结果生成后显示一个轻量“收藏”按钮。
- 点击后默认收藏当前结果，不弹复杂表单。
- 可选二次点击或右键打开小浮层，补充标签和备注。
- 精简版只显示最近 3-5 条收藏的快速插入入口，避免变成完整管理界面。

展开版收藏行为：

- 左侧“我的模板”或独立“收藏提示词”页面展示收藏库。
- 支持搜索、标签筛选、按场景筛选、按目标平台筛选。
- 支持一键套用收藏：把收藏提示词填入用户需求或直接作为优化结果。
- 支持编辑、删除、复制、导出。
- 支持把某条收藏固定到精简版快捷候选。

隐私要求：

- 收藏默认只存在本地。
- 收藏内容可能包含用户业务信息，不能自动同步或自动上传。
- 如果未来做云同步，必须单独开关和清晰提示。

## 3. 需要从夏语项目借鉴的代码资产

源工程：`M:\AI_Pet\xiaxia-pet`

### 3.1 可以作为实现参考的核心逻辑

这些文件已经复制到新项目里作为参考，但需求不是简单照搬。实现时应只抽取对 Prompt Input Method 有用的系统能力、OCR 能力和隐私边界：

| 源文件 | 参考用途 | 新项目建议位置 |
| --- | --- | --- |
| `src/services/bigBangTokenizer.ts` | 只参考 OCR 文本顺序、行合并、空格恢复策略；不移植大爆炸交互 | `reference-only` |
| `src/services/bigBangSelection.ts` | 只参考选中文本还原规则；不移植词/短语/整行选择 UI | `reference-only` |
| `src/services/ocrService.ts` | 截图选区映射、裁剪、图像增强思路 | `src/PromptInputMethod.Core/Ocr/OcrImagePreprocessor.cs` |
| `src/services/ocrProvider.ts` | OCR Provider 类型、调度、fallback 策略 | `src/PromptInputMethod.Core/Ocr/OcrProviderRouter.cs` |
| `src-tauri/src/selection.rs` | 截图、前台窗口捕获、剪贴板动作、图片动作校验 | `src/PromptInputMethod.Native` 或 C# Win32 服务 |
| `src-tauri/src/mouse_monitor.rs` | 全局快捷键监听与鼠标位置记录 | `src/PromptInputMethod.App/Services/GlobalHotkeyService.cs` |
| `src-tauri/src/ocr_native.rs` | Windows Media OCR、Windows AI OCR、火眼 OCR worker 调用 | `src/PromptInputMethod.Native/OcrNative` |
| `src-tauri/crates/fire-eye-worker` | 火眼 OCR 独立 worker | `native/fire-eye-worker` |
| `src-tauri/crates/ocr-rs-patched` | PaddleOCR/MNN OCR 引擎补丁版 | `native/ocr-rs-patched` |
| `src-tauri/assets/fire_eye` | PP-OCRv5 MNN 模型和字典 | `assets/fire_eye` |

注意：TypeScript 文件不能在 WinUI 3 里原样运行。这里复制到项目是为了离线参考，不代表按功能完整移植。大爆炸/天雷爆炸交互不进入 Prompt Input Method；Rust worker 和模型资产可以作为 OCR 后端更接近原样接入。

### 3.2 不复制的内容

- `src/ToolbarApp.vue`：不复制，WinUI 3 重新实现悬浮窗。
- `src/composables/useGlobalOcr.ts`：不整体复制，只提取状态机和调用顺序。
- `src/SettingsWindow.vue`：不复制，只提取设置项。
- `src/services/searchService.ts`：第一版不做网页搜索。
- 夏语角色、聊天、TTS、收藏、游戏中心、桌宠窗口相关代码全部不复制。
- Tauri 配置、Vue/Vite 构建链不复制。

## 4. 依赖迁移清单

### 4.1 WinUI 3 / C# 依赖

建议采用 .NET 8 + Windows App SDK：

- `Microsoft.WindowsAppSDK`
- `Microsoft.Windows.CsWin32`，生成 Win32 API 绑定。
- `CommunityToolkit.Mvvm`，做 ViewModel 和命令绑定。
- `System.Text.Json`，模型请求和配置。
- `Microsoft.Extensions.Http`，HTTP Client 和重试策略。
- `Microsoft.Extensions.Configuration.Json`，本地配置。

### 4.2 Windows 能力

需要封装以下系统能力：

- 全局快捷键：`RegisterHotKey`，比持续轮询 `GetAsyncKeyState` 更适合独立开源工具。
- 前台窗口：`GetForegroundWindow`、`GetWindowRect`。
- 截图：
  - 第一版可用 `Windows.Graphics.Capture` 或 GDI fallback。
  - 如果要最快复用旧逻辑，可参考 `selection.rs` 的 `xcap` 截图思路。
- 剪贴板：WinUI/Windows Clipboard API，必要时用 Win32 兜底。
- 窗口置顶、无边框、透明、焦点恢复：WinUI 3 + AppWindow / HWND interop。

### 4.3 OCR 依赖

第一版建议按稳定性分层：

1. `Windows.Media.Ocr`：系统内置 OCR，作为最轻依赖兜底。
2. Windows AI OCR：沿用 `ocr_native.rs` 的 worker 思路，作为新系统优先项。
3. 火眼 OCR worker：迁移 `fire-eye-worker`、`ocr-rs-patched`、`assets/fire_eye`，作为高质量本地 OCR。

如果项目目标是开源、容易编译，第一阶段可以先实现 `Windows.Media.Ocr`，把火眼 OCR 放到第二阶段。火眼 OCR 会引入 MNN 源码构建、CMake、Ninja、Vulkan/OpenCL、Paddle 模型资产和 license 审计，首次开源门槛更高。

### 4.4 大模型调用依赖

第一版做 Provider 抽象，不绑定单一平台：

- OpenAI-compatible HTTP endpoint。
- 本地模型 endpoint，例如 Ollama、LM Studio、vLLM、AI00。
- 自定义 base URL、API key、model name。
- 请求超时、取消、重试、流式输出。

配置项建议：

```json
{
  "model": {
    "provider": "openai_compatible",
    "baseUrl": "https://api.example.com/v1",
    "apiKeyStorage": "windows_credential_manager",
    "model": "gpt-4.1-mini",
    "timeoutSeconds": 30
  }
}
```

API key 不写入明文配置，走 Windows Credential Manager。

## 5. 目标工程结构

```text
M:\AI_Pet\Prompt Input Method
  README.md
  PROMPT_INPUT_METHOD_ROADMAP.md
  LICENSE
  src
    PromptInputMethod.App
      App.xaml
      MainWindow.xaml
      CompactPromptWindow.xaml
      ExpandedWorkbenchWindow.xaml
      Services
        GlobalHotkeyService.cs
        ForegroundWindowService.cs
        ClipboardService.cs
        FloatingWindowPlacementService.cs
        WindowModeService.cs
      ViewModels
        CompactPromptViewModel.cs
        ExpandedWorkbenchViewModel.cs
        SettingsViewModel.cs
    PromptInputMethod.Core
      Prompt
        SceneDetector.cs
        ScenePromptRouter.cs
        PromptStructuringService.cs
        PromptTemplates.cs
        StructuredPrompt.cs
      Favorites
        FavoritePrompt.cs
        FavoritePromptRepository.cs
        FavoritePromptSearch.cs
      Llm
        ILlmClient.cs
        OpenAiCompatibleClient.cs
        LlmRequestOptions.cs
      Ocr
        IOcrProvider.cs
        OcrProviderRouter.cs
        OcrImagePreprocessor.cs
        OcrTextNormalizer.cs
        OcrContextBuilder.cs
        OcrModels.cs
      Privacy
        SensitiveTextRedactor.cs
        EgressPolicy.cs
      Storage
        LocalAppDatabase.cs
        JsonSettingsStore.cs
    PromptInputMethod.Native
      NativeMethods.cs
      ScreenCaptureService.cs
      WindowsMediaOcrProvider.cs
      WindowsAiOcrProvider.cs
  native
    fire-eye-worker
    ocr-rs-patched
  assets
    fire_eye
  docs
    migration-from-xiaxia-pet.md
    privacy.md
```

## 6. 实施路线

### 阶段 0：开源项目骨架

目标：项目能打开 WinUI 3 空窗口，能构建。

- 创建 solution：`PromptInputMethod.sln`。
- 创建 WinUI 3 App：`PromptInputMethod.App`。
- 创建类库：`PromptInputMethod.Core`。
- 写入 README、LICENSE、贡献说明。
- 建立 GitHub Actions：Windows build。
- 明确 license：建议 MIT 或 Apache-2.0；如果迁移火眼 OCR / MNN / Paddle 模型，必须补 license inventory。

验收：

- `dotnet build` 通过。
- App 能启动并显示一个空悬浮窗。

### 阶段 1：全局快捷键 + 悬浮窗

目标：按快捷键出现精简版两框输入窗口。

- 用 `RegisterHotKey` 注册 `Ctrl + Shift + Space`。
- 记录当前鼠标位置和前台窗口 HWND。
- 精简悬浮窗显示在鼠标附近或屏幕中央；如果越界则贴近当前屏幕可视区域。
- 精简悬浮窗只展示用户需求框、优化结果框、场景候选和少量高频按钮。
- ESC 关闭，结构化完成后可自动隐藏。
- 窗口默认不进入任务栏，不抢过多焦点，但输入时必须可打字。
- 提供“展开”入口，进入完整工作台，但快捷键再次呼出仍默认回到精简版。

验收：

- 任意应用前台时按快捷键可唤起。
- 呼出的是精简版，不是完整工作台。
- 多显示器下位置正确。
- 关闭后焦点能回到原前台窗口或至少不破坏用户输入流。

### 阶段 2：基础模型结构化

目标：判断当前场景，用户输入一段自然语言后，立即输出适合当前场景的结构化 Prompt。

- 实现 `ILlmClient`。
- 实现 OpenAI-compatible client。
- 实现 `SceneDetector`：
  - 读取前台进程名。
  - 读取窗口标题。
  - 识别浏览器、代码编辑器、文档、聊天模型、邮件、终端、未知场景。
- 实现 `ScenePromptRouter`：
  - 根据场景选择 prompt 模板。
  - 把用户输入、窗口标题、选区/OCR 上下文整理为模型输入。
  - 场景置信度低时退回通用模板。
  - 主界面和输出使用用户可读场景名，不直接暴露进程名、程序集名或完整窗口标题。
- 实现 prompt 模板：
  - 保留用户原意。
  - 明确当前场景和目标应用。
  - 提取目标、上下文、约束、输出格式、验收标准。
  - 不凭空添加事实。
  - 不把用户需求改成过度复杂的 Agent 计划。
- 精简版 UI 显示：
  - 上方“用户需求”输入框。
  - 下方“优化结果”输出框。
  - 中间或输入框下方显示少量场景候选。
  - 主要按钮是“优化提示词”。
  - 结果区只保留复制、收藏、翻译英文、导出/插入这类高频动作。
- 展开版 UI 显示：
  - 左侧导航和历史/模板/收藏/设置。
  - 中间双语结果区。
  - 右侧目标平台和增强选项。

验收：

- 无 OCR 时也能作为纯 Prompt 输入法工作。
- API key 缺失时给出本地错误，不崩溃。
- 结构化结果可复制到剪贴板。
- 结构化结果可一键收藏到本地收藏库。
- 快捷键呼出的精简版不会显示完整工作台侧边栏。

### 阶段 2.5：本地收藏库

目标：用户可以把常用场景的提示词保存下来，下次快速复用。

- 实现 `FavoritePrompt` 数据模型：
  - `id`
  - `title`
  - `sourceRequirement`
  - `optimizedPromptZh`
  - `translatedPromptEn`
  - `scenario`
  - `targetPlatform`
  - `tags`
  - `note`
  - `createdAt`
  - `updatedAt`
  - `lastUsedAt`
  - `useCount`
  - `pinnedInCompact`
- 实现 `FavoritePromptRepository`：
  - 本地保存。
  - 搜索。
  - 更新。
  - 删除。
  - 最近使用排序。
- 精简版：
  - 结果区提供“收藏”按钮。
  - 提供最近/固定收藏的快速插入入口。
- 展开版：
  - 收藏列表。
  - 标签筛选。
  - 场景筛选。
  - 目标平台筛选。
  - 编辑和删除。

验收：

- 收藏不依赖网络。
- 收藏后重启应用仍存在。
- 精简版可以一键收藏当前结果。
- 展开版可以搜索并复用已收藏提示词。

当前验收状态：

- 精简版结果区已提供“收藏当前”。
- 最近 5 条收藏可从结果区下拉载入或删除。
- 收藏存储在 `%APPDATA%\PromptInputMethod\prompt_input_method.prompt_favorites.v1.json`。
- 设置中的“清除收藏”会删除本地收藏文件并刷新列表。

### 阶段 3：剪贴板和当前选区输入

目标：用户不用手动复制大量背景文字。

- 当前安全策略：
  - “导入剪贴板”只读取当前剪贴板文本，不修改剪贴板。
  - “复制结果”只在用户明确点击时，把优化后的提示词写入剪贴板。
  - 暂不提供自动插入当前窗口，避免为了粘贴临时覆盖用户剪贴板。
- 后续再实现“读取当前选区”，但必须先有可靠的多格式剪贴板保护方案：
  - 记录当前剪贴板序列号。
  - 模拟 `Ctrl + C`。
  - 短暂等待。
  - 读取剪贴板文本。
  - 如果失败，提示用户手动复制。
- 实现“从剪贴板导入”。
- 支持把结构化结果复制到剪贴板；自动粘贴回前台应用延后。

验收：

- 在记事本、浏览器输入框、VS Code、聊天网页中可读取选区。
- 不覆盖用户剪贴板，或提供“恢复原剪贴板”选项。
- 当前 MVP 验收优先级：导入剪贴板不得修改剪贴板；复制结果只写入优化后的提示词。

### 阶段 4：Windows Media OCR MVP

目标：实现 Prompt Input Method 自己的 OCR 上下文采集能力，不复制天雷爆炸/大爆炸功能。

- 实现当前窗口截图。
- 实现区域截图：
  - 悬浮窗隐藏。
  - 截图当前屏幕。
  - 进入全屏半透明选区层。
  - 用户拖选区域。
  - 映射到真实截图像素。
- 实现图像预处理：
  - 灰度。
  - 对比度拉伸。
  - gamma/contrast 增强。
- 接 `Windows.Media.Ocr`。
- 返回统一 `OcrResult`：
  - `text`
  - `lines`
  - `words`
  - `bbox`
  - `confidence`
- 当前已先接通文件型图片 OCR：用户选择图片，Windows Media OCR 识别文本后进入上下文框；截图和区域选择继续作为后续子任务。
- 当前窗口 OCR 已接通：只截取快捷键呼出前记录的前台窗口；失败时提示错误，不退回全屏截图。
- 区域 OCR 已接通：隐藏精简窗后截取虚拟屏幕，用户拖选区域后裁剪并本地 OCR。

验收：

- 截图区域 OCR 成功。
- OCR 失败时保留图片，不直接把失败吞掉。
- 不会在捕获前台窗口失败时退回全屏截图。

### 阶段 5：OCR 上下文清洗

目标：把 OCR 文本整理成适合提示词生成的上下文，而不是做大爆炸式文本块选择。

- 实现 `OcrTextNormalizer`：
  - 合并明显属于同一段的 OCR 行。
  - 保留报错、代码、表格、列表的基本换行。
  - 修复英文单词间空格和中英文标点附近的常见 OCR 问题。
  - 去除重复行、明显噪声和空白。
  - 当前已接入：OCR 结果进入上下文框前会清洗空白、相邻重复行、常见标点空格，并合并明显连续段落。
- 实现 `OcrContextBuilder`：
  - 根据当前场景决定 OCR 文本放入“背景上下文”“错误信息”“界面文字”还是“参考材料”。
  - 对超长 OCR 文本做截断和摘要前处理。
  - 保留用户可编辑预览，让用户确认后再发给模型。
  - 当前已接入：OCR 导入会按场景/文本特征归类为背景上下文、错误信息、界面文字或参考材料；超长文本会保留首尾并插入省略提示；上下文框仍作为用户可编辑预览。

验收：

- OCR 文本能作为提示词上下文直接使用。
- 代码、报错、网页正文、软件界面文字能进入不同上下文字段。
- 不出现词/短语/整行点击选择界面。

### 阶段 6：OCR Provider 路由

目标：把夏语的智能 OCR 调度迁过来，但先简化。

- 实现 `IOcrProvider`：
  - `Id`
  - `DisplayName`
  - `IsAvailableAsync`
  - `RecognizeAsync`
- 实现 `OcrProviderRouter`：
  - preferred provider。
  - fallback provider。
  - 失败隔离。
  - 耗时记录。
  - 当前已接入：OCR 调用经路由器执行，支持用户指定 preferred provider、失败后 fallback、单 provider 15 秒超时和耗时/错误记录。
- 第一版候选：
  - `windows_media_ocr`
  - `windows_ai_ocr`
  - `fire_eye_ocr`
- 调度记录落地到本地 JSON：
  - 不沿用 `xiaxia.ocr.smartScheduler.v1` 命名，改成 `prompt_input_method.ocr.scheduler.v1`。
  - 当前已接入：记录写入 `%APPDATA%\PromptInputMethod\prompt_input_method.ocr.scheduler.v1.json`。

验收：

- Provider 失败后自动 fallback。
- 用户可以在设置里指定 OCR 后端。
- 15 秒 OCR 超时保护存在。

### 阶段 7：火眼 OCR worker 迁移

目标：把高质量本地 OCR 迁入新项目。

迁移内容：

- 复制 `src-tauri/crates/fire-eye-worker` 到 `native/fire-eye-worker`。
- 复制 `src-tauri/crates/ocr-rs-patched` 到 `native/ocr-rs-patched`。
- 复制 `src-tauri/assets/fire_eye` 到 `assets/fire_eye`。
- 改 worker 的 Cargo workspace 路径。
- 当前已接入：`native/fire-eye-worker`、`native/ocr-rs-patched`、`assets/fire_eye` 已放入当前项目，`native/Cargo.toml` 已包含 worker workspace。
- 保留模型：
  - `PP-OCRv5_mobile_det.mnn`
  - `PP-OCRv5_mobile_det_fp16.mnn`
  - `PP-OCRv5_mobile_rec.mnn`
  - `PP-OCRv5_mobile_rec_fp16.mnn`
  - `ppocr_keys_v5.dict`
- C# 侧通过 stdin/stdout JSON 协议调用 worker。
- 当前已接入：C# `fire_eye_ocr` provider 会把截图临时编码为 PNG 文件，通过 worker stdin/stdout JSON 调用 `recognize`，解析 `words/lines` 后进入现有 OCR router。

需要注意：

- `ocr-rs-patched` 会构建 MNN，依赖 CMake、Ninja、MSVC。
- OpenCL/Vulkan 加速可能导致用户机器差异，必须有 CPU fallback。当前 worker 默认保留 OpenCL/Vulkan/CPU 候选智能调度；GPU 候选失败时继续 fallback 到 CPU。
- 开源发布前要整理 MNN、PaddleOCR、ocr-rs、模型文件 license。

验收：

- `cargo build -p fire-eye-ocr-worker` 成功。
- C# App 能启动 worker。
- worker 能对一张测试截图返回 words/lines。
- worker 崩溃时 App 不崩溃，自动 fallback 到 Windows OCR。

当前验收状态：

- `cargo build -p fire-eye-ocr-worker` 已通过。
- worker 已能对生成的测试 PNG 返回 `words/lines`。
- App 构建会把 Debug/Release worker exe 复制到输出目录；`fire_eye_ocr` 启动失败、超时或识别失败会由现有 router fallback 到 Windows Media OCR。

### 阶段 8：隐私与外发控制

目标：这个工具可以开源给别人用，不能让用户误以为截图/OCR永远本地。

- 本地 OCR 默认开启。
- 调用大模型前明确显示将发送的文本。
- 图片默认不发送给模型，除非用户启用图片外发并点“附图给模型”。
- 添加本地脱敏：
  - API key。
  - token。
  - 邮箱、手机号、身份证、银行卡。
  - URL query 中的 key/password/session/code。
- 外发策略：
  - 仅发送用户确认的原始需求、选中文本、OCR 上下文。
  - 不自动发送整屏截图。
  - 不自动抓取浏览器 DOM。
- API key 存 Windows Credential Manager。
- 当前已接入：设置中可关闭 OCR、关闭模型外发、开启/关闭模型发送前确认、开启/关闭本地脱敏。
- 当前已接入：模型调用前可显示即将发送的文本；开启脱敏时发送的是脱敏后的 Prompt。
- 当前已接入：图片默认不会发送给模型；启用图片外发并显式附图后，多模态模型调用会以 OpenAI-compatible `image_url` data URL 发送图片。
- 当前已接入：API key 仍只写入 Windows Credential Manager。

验收：

- 首次调用模型前有外发提示。
- 设置页可以关闭 OCR、关闭模型外发、清除历史、清除收藏。
- 日志不打印完整 Prompt、API key、OCR 全文。

当前验收状态：

- 模型调用确认弹窗已接入，默认每次显示即将发送的文本。
- 本地脱敏已覆盖 API key、token、邮箱、手机号、身份证、银行卡和敏感 URL query。
- 设置页已提供 OCR 开关、模型外发开关、清除历史、清除收藏。

### 阶段 9：发布

目标：可给开源用户安装试用。

- 打包 MSIX 或 unpackaged exe。
- 写 README：
  - 项目是什么。
  - 快捷键。
  - 支持哪些模型 Provider。
  - OCR 是否本地。
  - 如何配置 API key。
- 写 `docs/privacy.md`。
- 写 `docs/migration-from-xiaxia-pet.md`，说明哪些代码来自夏语，哪些重写。
- 写 license inventory。

当前已接入：

- 当前配置为 unpackaged exe，Release x64 可生成可试用输出。
- README 已补充项目用途、快捷键、构建运行、模型 Provider、OCR 本地性、API key 配置和多模态说明。
- 已新增 `docs/privacy.md`。
- 已新增 `docs/migration-from-xiaxia-pet.md`。
- 已新增 `docs/license-inventory.md` 初稿。

验收：

- 新机器上能安装启动。
- 没有 API key 时仍能使用剪贴板/OCR/复制功能。
- 有 API key 时可以结构化并粘贴回其他模型窗口。

当前验收状态：

- `cargo build -p fire-eye-ocr-worker --release` 已通过。
- `MSBuild PromptInputMethod.sln /p:Configuration=Release /p:Platform=x64 /restore` 已通过。
- Release x64 输出目录包含 `fire-eye-ocr-worker.exe`，worker `staticCapabilities` 检查通过。
- 尚需在干净新机器上做启动/快捷键/OCR/模型请求实测。
- 尚需在公开发布前完成 MNN、PaddleOCR 模型、ocr-rs 和 Rust crates 的最终法律审查。

## 7. 从夏语迁移时的优先级

### 必须先迁

1. 全局快捷键和悬浮窗。
2. 大模型结构化 Prompt。
3. 剪贴板读取和写回。
4. Windows Media OCR。
5. OCR 上下文清洗和用户确认。
6. 本地提示词收藏。

### 第二批迁

1. Windows AI OCR。
2. 火眼 OCR worker。
3. OCR 智能调度。
4. 前台窗口 OCR。
5. 本地脱敏。

### 暂不迁

1. 夏语桌宠 UI。
2. Vue 工具栏。
3. 搜索服务。
4. 朗读/TTS。
5. 夏语收藏系统。
6. 问指定智能体。
7. Xia Run / ZeroClaw / 游戏中心。

## 8. 关键技术风险

### 8.1 WinUI 3 悬浮窗焦点

全局输入法类工具最容易出问题的是焦点：窗口要能输入，又不能破坏用户当前工作流。需要专门验证浏览器、编辑器、聊天软件、管理员权限窗口。

### 8.2 OCR 构建复杂度

火眼 OCR 质量高，但引入 Rust、MNN、CMake、Ninja、GPU 后端和模型资产。开源首版建议先把它作为可选增强，不要让它阻塞基础版本发布。

### 8.3 大模型外发隐私

Prompt Input Method 的价值在“结构化需求”，但它会处理用户输入、剪贴板、OCR 文本，这些都可能敏感。必须把“本地处理”和“发送给模型”在 UI 上分清楚。

### 8.4 快捷键冲突

`Ctrl + Shift + Space` 可能和输入法或 IDE 冲突。设置页必须允许改快捷键。

## 9. 推荐第一周任务

1. 在 `M:\AI_Pet\Prompt Input Method` 初始化 WinUI 3 solution。
2. 做出快捷键可唤起的精简版两框悬浮窗。
3. 实现前台进程名、窗口标题、窗口类型读取。
4. 实现第一版场景判断：代码、聊天模型、文档、邮件、浏览器、软件操作、未知。
5. 实现 OpenAI-compatible 结构化调用。
6. 实现根据场景选择模板并立即生成提示词。
7. 实现本地提示词收藏：一键收藏、最近收藏、固定到精简版。
8. 实现复制结果和粘贴结果。
9. 实现读取当前选区。
10. 实现 `OcrTextNormalizer` / `OcrContextBuilder` 并加单元测试。
11. 先接 `Windows.Media.Ocr`，确认截图区域 OCR 能跑通。

## 10. 最小可用版本定义

MVP 达标条件：

- 按快捷键能打开悬浮窗。
- 快捷键打开的是精简版两框 UI，不是完整工作台。
- 能判断当前基础场景：代码、聊天模型、文档、邮件、浏览器、软件操作、未知。
- 能输入自然语言需求。
- 能结合当前场景和用户需求调用大模型，立即输出结构化 Prompt。
- 能一键收藏当前优化结果，并在重启后继续使用。
- 能复制或粘贴结构化结果。
- 能读取当前选区作为上下文。
- 能截图 OCR，把屏幕文字加入上下文。
- 所有外发内容都必须由用户确认。

只要这些完成，Prompt Input Method 就已经是一个独立可用的开源小项目；火眼 OCR、Windows AI OCR 和 Provider 智能调度可以作为后续增强逐步补上。天雷爆炸/大爆炸仍然属于夏语，不作为本项目功能目标。
