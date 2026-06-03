# 啊拼 开源项目引用

啊拼 的内置提示词模板、交互灵感和基础运行环境参考了以下开源项目与开源软件。项目内保留原项目名称，用于明确来源与尊重开源署名。

## 本项目许可证

啊拼 / AI Quick Prompt 的原创源码与文档按 GNU General Public License version 3 or later（`GPL-3.0-or-later`）开源。完整许可证见仓库根目录 `LICENSE`，项目声明见 `NOTICE.md`。

下列第三方项目、提示词数据、Skill 包、运行时依赖和模型资产仍保留各自原许可证；本项目采用 GPL 不代表重新授权这些上游材料。

## 内置提示词模板来源

- ChatGPT-Shortcut
  - 用途：通用提示词、写作、翻译、总结、提示词工程类模板数据参考。
  - 本项目仅吸收提示词数据与分类思路，不包含其会员、账户、社区等站点功能。
  - 上游源码整树仅作为本地忽略参考目录使用，不纳入公开仓库。

- SD-Anima-Prompt-Studio
  - 用途：文生图、动漫角色、构图、镜头、画面描述类模板数据参考。
  - 本项目仅吸收提示词数据与分类思路，不包含其网页运行环境。
  - 上游源码整树仅作为本地忽略参考目录使用，不纳入公开仓库。

- prompts.chat
  - 用途：通用 AI 角色提示词库、开发者提示词、结构化提示词与图像提示词数据参考。
  - 来源：https://github.com/f/prompts.chat
  - 授权：`prompts.csv`、`PROMPTS.md` 与用户提交的提示词文本按 CC0 1.0 Universal 置于公共领域；项目源码与站点内容使用 MIT License。
  - 本项目仅内置 prompt 数据并保留 `prompts.chat` 源标签，不包含其站点、CLI、MCP、账号或托管功能。

- Female Portrait Prompt Director Skill / 女性人像提示词导演 Skill
  - 用途：内置文生图 Skill，用于女性人像写真、服装/电商试衣、风格路由、参考图保留、提示词优化、图片反推、参数推荐与安全改写。
  - 来源：https://github.com/liyue-aigc/female-portrait-director
  - 作者：李岳
  - 版本：FEMALE-PORTRAIT-DIRECTOR-V1.4.1
  - 授权：MIT License
  - 本项目将其作为内置 Skill 包复制到 `src/PromptInputMethod.App/Data/skills/female-portrait-director/`，随包保留原 `LICENSE`、`NOTICE.md`、`README.md` 与 `SKILL.md`；运行时仅作为文本工作流上下文使用，不执行外部脚本。
  - 啊拼的提示词输出框会适配为纯文本输出，去除 Markdown 代码块包装，但保留原 Skill 的提示词结构、负面约束与安全边界要求。

## 候选 Skill 研究清单

即梦 / Dreamina / Seedance 相关候选 Skill 统一记录在 `docs/skill-source-candidates.md`。这些仓库目前仅作为研究对象，不是内置模板来源，也不是当前发布包的第三方依赖。

当前内置的 `即梦导演 Skill` 优化目标和即梦模板为啊拼原创干净房间实现，不包含候选仓库的代码、README、提示词正文、Skill 文件、脚本或平台 API 调用实现。

导入原则：

- 优先研究 MIT、MIT-0、Apache-2.0 等宽松许可证项目。
- 对 GitHub API 未识别许可证或没有仓库 LICENSE 的项目，只能学习架构，不能复制正文、代码、提示词或 Skill 文件。
- GPL API/CLI/Skill 项目只能本地学习，不进入啊拼内置包、微软商店分支或商业分发链路。
- 涉及浏览器自动化、账号登录、平台 CLI 或平台 API 调用的项目，需要另行审查服务条款、隐私影响和用户授权路径。

## 运行时与框架

- Microsoft Windows App SDK / WinUI 3
  - 用途：Windows 桌面窗口、Fluent Design 控件、Mica 背景与应用外壳。

- .NET
  - 用途：桌面应用运行时、配置、文件、剪贴板、窗口交互等基础能力。

- Rust
  - 用途：本地 OCR worker 以及相关原生能力。

## 说明

啊拼 不是上述开源项目的再发布版本，也不代表上述项目官方立场。若后续发布安装包或源码，请随包附带 GPL 正文、项目声明、各依赖和引用项目的许可证文本，并按对应许可证要求保留版权与署名信息。
