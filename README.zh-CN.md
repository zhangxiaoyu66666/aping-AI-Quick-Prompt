# 啊拼 / AI Quick Prompt

[English](README.md) | [简体中文](README.zh-CN.md)

啊拼 / AI Quick Prompt 是一个以 GPL 许可证开源的 Windows Fluent 提示词快捷工作台。它可以把粗略需求、剪贴板上下文、OCR 文本、图片参考、本地模板和挂载的 `SKILL.md` 工作流整理成更清晰、可复用、可直接发送给模型的提示词。

项目基于 WinUI 3 与 .NET 8 构建，并集成可选的 Rust 原生 OCR worker。啊拼面向经常在 ChatGPT、Claude、Gemini、本地 OpenAI 兼容模型、文生图模型、视频模型和代码智能体之间切换的 Windows 用户。

## 核心特性

- **聊天式需求优化**：从一句粗略想法开始，通过追问和补充逐步生成更精确的提示词。
- **中文与英文提示词并排输出**：同时保留本地化中文提示词和英文提示词，方便跨模型、跨平台使用。
- **优化目标**：支持通用 LLM、文生图、即梦 / Seedance 风格视频提示词、Veo 3、AI 编程、Skill 体系和自定义目标。
- **Skill 挂载**：挂载包含 `SKILL.md` 的文件夹，让啊拼根据当前需求匹配 Skill，并把它作为高优先级工作流上下文注入模型。
- **模板库**：内置受 `ChatGPT-Shortcut`、`prompts.chat`、`SD-Anima-Prompt-Studio` 启发的模板组，并包含内置的女性人像提示词导演 Skill 和用户自定义模板。
- **常用提示词**：用户可以收藏常用提示词，快速复制、编辑和删除。
- **JSON 导入导出**：模板、常用提示词和语言包可以批量导入导出。
- **OCR 输入**：默认优先使用本地 Fire Eye OCR，Windows Media OCR 仅作为 worker 不可用时的兜底方案。
- **OpenAI 兼容提供商**：可配置 Base URL、API Key 和模型名称，连接本地或远程的 Chat Completions 兼容端点。
- **隐私优先默认值**：API Key 存储在 Windows Credential Manager；OCR 默认本地处理，只有用户明确附加图片并启用图片发送时才会把图片上下文发给模型提供商。

## 当前状态

AI Quick Prompt 1.0 是第一个公开 GPL 正式版本。当前代码库包含：

- WinUI 3 展开窗口和紧凑窗口。
- 全局快捷键呼出置顶提示词窗口。
- Fluent 风格导航、模板面板、模型管理、设置和关于页面。
- 模型不可用时的本地提示词结构化兜底逻辑。
- OpenAI 兼容模型调用、模型刷新和端点验证。
- 内置模板与用户自定义模板。
- AI 编程和 Skill 体系优化目标。
- 本地 OCR 路由和可选 Rust worker 集成。
- 内置中文、英文 UI 资源，以及挂载语言包支持。

1.0 版本已经完成 OCR 模型资产和供应商原生依赖项的公开发布许可审查。Fire Eye OCR 可以随包发布已审查的 PP-OCRv5 模型资产，前提是发布包包含所需的 Apache-2.0 声明。详见 [docs/ocr-model-license-review.md](docs/ocr-model-license-review.md)、[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) 和 [docs/license-inventory.md](docs/license-inventory.md)。

复制的上游源码树、本地 OCR 模型文件和旧迁移参考树不会进入 Git。公开仓库保留生成后的应用模板数据、带有原始声明的内置 Skill 包、集成的原生 OCR 代码和归属文档。需要重新生成或维护本地参考目录时，请阅读 [docs/template-source-imports.md](docs/template-source-imports.md)。

后续 Windows 体验打磨、macOS 适配、跨平台核心拆分、模板 / Skill 数据格式和长期平台计划记录在 [PROMPT_INPUT_METHOD_ROADMAP.md](PROMPT_INPUT_METHOD_ROADMAP.md)。

## 许可证

本仓库中的原创源码和文档以 GNU General Public License version 3 or later (`GPL-3.0-or-later`) 发布。

- 完整许可证文本：[LICENSE](LICENSE)。
- 项目声明：[NOTICE.md](NOTICE.md)。
- 第三方声明：[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
- 当前依赖许可证清单：[docs/license-inventory.md](docs/license-inventory.md)。

第三方组件、提示词数据集、内置 Skill 包、模型资产和框架 / 运行时依赖仍遵循它们各自的许可证。本仓库采用 GPL 并不意味着重新许可这些上游材料。

## 使用流程

典型流程如下：

1. 通过全局快捷键或主窗口打开啊拼。
2. 输入粗略需求，或从图片 / 截图中导入可编辑 OCR 文本。
3. 选择优化目标，例如 `通用 LLM`、`文生图`、`AI编程` 或 `Skill体系`。
4. 可选：挂载 `SKILL.md` 文件夹，或从快速模板下拉菜单中插入模板。
5. 将请求发送给已配置的模型。
6. 如果模型需要更多信息，可以继续聊天补充。
7. 复制、插入、收藏或导出生成的中文和英文提示词。

## Skill 体系

啊拼把 Skill 视为可复用的文本工作流，而不是可执行插件。一个挂载的 Skill 应该位于包含 `SKILL.md` 的文件夹中。

挂载 Skill 后，啊拼会：

- 读取 `SKILL.md` 文件。
- 将该 Skill 作为用户模板保存到 `Skill体系`。
- 从标题、描述、标题段落和流程提示中提取触发词。
- 后续用户需求命中该 Skill 时，把 Skill 作为高优先级上下文注入模型。
- 指示模型按照挂载 Skill 直接完成用户任务，而不是只解释如何编写 Skill。

它适合这些场景：

- 图片提示词系统。
- 代码智能体说明。
- 写作、编辑和润色规范。
- 项目专用操作流程。
- 可复用提示词工程剧本。

## 内置模板来源

啊拼包含受开源提示词项目启发的内置模板组：

- `ChatGPT-Shortcut`：写作、翻译、总结和提示词工程参考。
- `prompts.chat`：角色提示词库、开发者提示词、结构化提示词和图片提示词参考。
- `SD-Anima-Prompt-Studio`：文生图、角色、构图和视觉提示词参考。
- `Female Portrait Prompt Director Skill`：内置文生图 Skill 工作流，用于女性人像提示词、服装 / 电商试穿提示词、风格路由、参考图保留、提示词优化和安全重写。

这些原始项目名称会作为来源标签保留，用于归属说明。啊拼不会包含这些项目中与网站、账号、社区、会员或付费功能相关的无关逻辑。内置 Skill 包保留其原始 LICENSE 和 NOTICE 文件，位置在 `src/PromptInputMethod.App/Data/skills/`。

复制的上游源码树不会进入公开仓库；公开仓库只保留生成后的模板数据和来源标签。

## 隐私模型

啊拼是本地优先的工具，但在用户启用并发送请求时，也会把文本和可选图片发送给配置的模型提供商。

- API Key 存储在 Windows Credential Manager。
- 本地 OCR 本身不会把图片发送给模型提供商。
- 只有用户明确附加图片、并启用图片发送时，图片上下文才会进入多模态模型调用。
- 本地敏感信息脱敏可以减少常见密钥外泄风险，但它不是正式的数据防泄漏系统。
- 用户提示词、OCR 文本和模板可能包含敏感内容，请不要把本地应用数据或含隐私的截图提交到仓库。

更多细节请阅读 [docs/privacy.md](docs/privacy.md)。

## 构建要求

- Windows 10 1809 或更高版本。
- Visual Studio 2022 或更新版本，并安装：
  - .NET 桌面开发。
  - C++ 构建工具。
  - Windows SDK。
  - MSBuild。
  - 如需构建原生 OCR，则需要 CMake 工具。
- .NET 8 SDK。
- 如需构建可选 Fire Eye OCR worker，则需要 Rust stable 工具链。

## 构建

使用 Visual Studio MSBuild 构建 WinUI 应用：

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" src\PromptInputMethod.App\PromptInputMethod.App.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /p:OutputPath=bin\CodexVerify\ /m /v:minimal
```

如果机器上 `msbuild` 已加入 `PATH`，也可以使用：

```powershell
msbuild src\PromptInputMethod.App\PromptInputMethod.App.csproj /t:Build /p:Configuration=Debug /p:Platform=x64 /m /v:minimal
```

构建可选 OCR worker：

```powershell
cargo build -p fire-eye-ocr-worker --manifest-path native\Cargo.toml --release
```

部分机器上 `dotnet build` 可能在 WinUI PRI 生成阶段失败。建议优先使用 Visual Studio MSBuild 构建应用项目。

## 运行

Debug x64：

```powershell
& "src\PromptInputMethod.App\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\PromptInputMethod.App.exe"
```

Release x64：

```powershell
& "src\PromptInputMethod.App\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\PromptInputMethod.App.exe"
```

## 仓库结构

```text
src/PromptInputMethod.App/   WinUI 3 应用、UI、设置、OCR、模型调用和模板
src/PromptInputMethod.Core/  本地提示词结构化与路由基础逻辑
native/                      可选 Rust OCR worker 和修补后的 OCR 依赖树
assets/                      应用图标和 OCR 模型资产
docs/                        隐私、许可证清单、开源引用和维护说明
```

`ChatGPT-Shortcut-main/`、`SD-Anima-Prompt-Studio-main/`、`reference/xiaxia-pet/`、`assets/fire_eye/` 等本地目录会被忽略。它们对维护有帮助，但不是正常构建应用所必需的内容。

## 开源文件

仓库包含：

- GPL 项目许可证。
- 项目声明。
- 第三方声明和许可证清单。
- 贡献指南。
- 安全策略。
- 行为准则。
- 更新日志。
- 面向后续编码助手的 AGENTS 说明。
- GitHub Issue 模板和 Pull Request 模板。
- GitHub Actions Windows 构建工作流。
- 本地上游参考导入说明。
- Windows 打磨、macOS 适配和跨平台规划路线图。
- 1.0 发布就绪说明、OCR 模型许可证审查和轻量级发布检查。

Codex for Open Source 准备说明见 [docs/codex-for-open-source.md](docs/codex-for-open-source.md)。

示例挂载 Skill：[examples/skills/aipin-template-review/SKILL.md](examples/skills/aipin-template-review/SKILL.md)。

## 发布检查

运行轻量级非 GUI 发布检查：

```powershell
dotnet run --project tests\PromptInputMethod.ReleaseChecks\PromptInputMethod.ReleaseChecks.csproj --configuration Release
```

为展开、紧凑、窄版和当前高 DPI 设置捕获布局截图：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\Invoke-UiScreenshotChecks.ps1
```

创建包含声明和已审查 OCR 资产的公开演示包：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\New-PublicDemoPackage.ps1
```

创建已签名 MSIX 侧载包：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\New-MsixPackage.ps1
```

完整 1.0 发布检查清单位于 [docs/release-1.0-readiness.md](docs/release-1.0-readiness.md)。后续产品路线图位于 [PROMPT_INPUT_METHOD_ROADMAP.md](PROMPT_INPUT_METHOD_ROADMAP.md)。

## 参与贡献

小而聚焦的补丁最容易审查。修改代码前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 和 [AGENTS.md](AGENTS.md)。

如果改动涉及 OCR 截图、剪贴板行为、模型请求、API Key 存储、Skill 挂载或本地文件访问，请在 Pull Request 中说明隐私影响。
