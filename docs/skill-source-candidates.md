# Skill 候选来源清单

日期：2026-06-03

本文记录即梦 / Dreamina / Seedance 相关、可供啊拼模板或 Skill 体系研究的开源仓库。当前已在啊拼中整合的 `builtin-jimeng-seedance-director` 优化目标和新增即梦模板为原创干净房间实现：只综合公开功能形态、场景分类和产品需求，不复制候选仓库的代码、README、提示词正文或 Skill 文件。

这些仓库仍然只是研究候选，不是当前发布包的第三方依赖，也不需要进入发布包声明；只有在后续真正导入经过审查的内容时，才更新 `THIRD_PARTY_NOTICES.md`、`docs/license-inventory.md` 和 `docs/open-source-references.md`。

许可证信息优先以 GitHub 仓库许可证元数据为准。GitHub API 无法识别仓库许可证的项目，需要人工复核 LICENSE 文件、README 许可证段落、版权归属和可复制范围后，才能复制任何正文、代码、提示词或 Skill 文件。

## 导入规则

- 优先研究 MIT、MIT-0、Apache-2.0、CC0 等宽松许可证来源。
- 真正导入时，必须保留原仓库名称、URL、作者、许可证、导入文件范围和必要声明。
- 只复制啊拼实际需要的 Skill / 文本资产，不把整个上游项目树提交进 Git。
- 没有明确许可证的仓库只能学习结构，不能复制正文、代码、提示词或 Skill 文件。
- 高星但没有明确许可证的仓库按黑箱处理：只观察它解决了哪些用户问题和大致模块边界，不读取或搬运可版权化表达，不导入文件，不复刻命名、段落或词库。
- GPL API、CLI 或 Skill 项目不进入微软商店版、商业分发链路或内置包。
- 涉及浏览器自动化、账号登录、平台 API、官方 CLI 包装的项目，需要单独审查平台条款、隐私影响和用户授权路径。

## 当前整合落点

- `src/PromptInputMethod.App/Services/OptimizationTargetService.cs`
  - 新增原创内置优化目标 `builtin-jimeng-seedance-director` / `即梦导演 Skill`。
  - 覆盖文生视频、图生视频、首尾帧过渡、产品宣发、短剧对白、Seedream 图片和视频编辑。
  - 输出强调素材引用、主体一致性、镜头设计、时间轴、声音字幕、平台参数和负面约束。
- `src/PromptInputMethod.App/Services/PromptTemplateCatalogService.cs`
  - 新增原创即梦模板：导演分镜工作台、多模态引用控制、短剧对白分镜、短视频宣发模板、Seedream 图片提示词。
- `src/PromptInputMethod.App/CompactPromptWindow.xaml.cs`
  - 升级内置“即梦”模式的本地结构草稿和模型优化规则，使未手动选择优化目标时也使用同一套导演型结构。

以上整合不包含候选仓库的上游文本、代码、脚本、词库或 API 调用实现。

## 优先研究候选

| 仓库 | 已核验许可证元数据 | 方向 | 对啊拼的参考价值 |
| --- | --- | --- | --- |
| [dexhunter/seedance2-skill](https://github.com/dexhunter/seedance2-skill) | MIT | Seedance 2.0 视频提示词 Skill，支持 Claude Code、Cursor、Cline 等 Agent | 多 Agent 兼容写法、中英双 Skill 文件、场景模板拆分、`@` 引用语法。 |
| [Emily2040/seedance-2.0](https://github.com/Emily2040/seedance-2.0) | MIT | 模块化 Seedance 2.0 视频生产 Skill 系统 | Skill OS 化、references 瘦身、多模态视频工作流组织。 |
| [leigegehaha/jimeng-cli-free](https://github.com/leigegehaha/jimeng-cli-free) | Apache-2.0 | 本地即梦 / Dreamina 网页端图片工作流 | 本地 Skill + 浏览器 / 客户端桥接、模型控制、参考图上传、本地下载。涉及平台使用规则，产品化前必须另审。 |
| [rich5000/seedance-prompt-guide](https://github.com/rich5000/seedance-prompt-guide) | MIT | Seedance 2.0 提示词工程指南 Skill | 文生视频、图生视频、一镜到底、产品广告、视频编辑等场景分类。 |
| [woodfantasy/Seedance2.0-ShotDesign-Skills](https://github.com/woodfantasy/Seedance2.0-ShotDesign-Skills) | MIT-0 | Seedance 2.0 电影镜头设计 Skill | 镜头运动字典、导演预设、光照结构、短剧和漫剧模板。 |
| [ppdbxdawj/ai-skills](https://github.com/ppdbxdawj/ai-skills) | MIT | Agent Skill 合集，包含 `seedream-image` | Seedream 图片提示词、角色一致性、海报/PPT/电商布局、组图和分镜工作流。 |
| [iamzhihuix/happy-claude-skills](https://github.com/iamzhihuix/happy-claude-skills) | MIT | Skill 合集，包含 `happy-dreamina` | 用官方 CLI 包装 text-to-image、image-to-image、text-to-video、image-to-video 和任务历史的思路。 |
| [11Yuxuanyang/seedance-viral-forge](https://github.com/11Yuxuanyang/seedance-viral-forge) | MIT | 营销和短视频爆款向提示词 Skill | 带货、商家舞蹈引流、热梗流量、短视频宣发模板。 |
| [cyuanxv/ai-mandrama-skills](https://github.com/cyuanxv/ai-mandrama-skills) | MIT | 中文 AI 漫剧 / 短剧端到端工作流 Skill | 剧本到分镜、配音、字幕、横竖版成片、Dreamina CLI、edge-tts、ffmpeg 编排。 |

## 需要许可证确认

| 仓库 | 当前许可证信号 | 确认前允许用途 | 备注 |
| --- | --- | --- | --- |
| [songguoxs/seedance-prompt-skill](https://github.com/songguoxs/seedance-prompt-skill) | GitHub API 未识别仓库许可证；README 末尾有 `MIT` 许可证段落 | 只学习结构和思路；确认许可证文本和版权范围前不要复制 | 适合研究基础 Seedance prompt Skill 形态、触发词、内置词库和中文视频提示词结构。 |
| [MapleShaw/seedance2.0-prompt-skill](https://github.com/MapleShaw/seedance2.0-prompt-skill) | 未检测到仓库许可证 | 只学习架构 | 相机编码、角色卡、首帧/关键帧模块和长视频流水线较完整。 |
| [yuyou-dev/dreamina-cli-skill](https://github.com/yuyou-dev/dreamina-cli-skill) | 未检测到仓库许可证 | 只学习架构 | `SKILL.md`、`agents/`、`references/`、`scripts/` 结构清楚，有 Python wrapper 和 dry-run JSON 输出思路。 |

## 黑箱排除来源

以下仓库在公开搜索中热度较高或内容形态有参考价值，但许可证元数据未明确或不适合直接进入啊拼。处理方式是只保留“用户需求类型”层面的观察，不复制任何代码、提示词、词库、README 段落、文件结构细节或命名体系。

| 仓库 | 当前许可证信号 | 黑箱处理结论 |
| --- | --- | --- |
| `songguoxs/seedance-prompt-skill` | GitHub API 未识别仓库许可证；README 许可证段落仍需人工复核 | 高星但许可证元数据不明确；只确认“自然语言到 Seedance 提示词”是高频需求，不复制触发词、词库或正文。 |
| `MapleShaw/seedance2.0-prompt-skill` | GitHub API 未识别仓库许可证 | 只确认“长视频、角色卡、关键帧、素材剪辑”是需求方向；啊拼使用原创引用控制和分镜结构。 |
| `yuyou-dev/dreamina-cli-skill` | GitHub API 未识别仓库许可证 | 只确认“CLI wrapper + dry-run + JSON 结果”是工程形态；不复制脚本或 Skill 目录内容。 |
| `liangdabiao/Seedance2-Storyboard-Generator` | GitHub API 未识别许可证 | 可确认“分镜生成器”是高需求方向；啊拼使用原创时间轴/镜头结构实现。 |
| `beshuaxian/higgsfield-seedance2-jineng` | GitHub API 未识别许可证 | 只确认“视频 Skill 包装”需求存在；不复制 Skill 文本。 |
| `op7418/Seedance-Product-Video` | GitHub API 未识别许可证 | 只确认“产品视频”是高频场景；啊拼用原创产品宣发模板。 |
| `AKCodez/higgsfield-claude-skills` | GitHub API 未识别许可证 | 只确认“跨 Agent Skill”需求存在；不使用其文件或段落。 |
| `robonuggets/seedance-skill` | GitHub API 未识别许可证 | 只确认基础 Seedance Skill 形态；不复制表达。 |

## 不进入内置包

| 仓库 | 许可证 | 原因 |
| --- | --- | --- |
| [iptag/jimeng-api](https://github.com/iptag/jimeng-api) | GPL-3.0 | 可本地学习，但不要作为第三方 API 代码或 Skill 内容进入啊拼内置包、微软商店版或商业分发链路。 |

## 研究优先级

1. `dexhunter/seedance2-skill`：多 Agent 兼容和双语 Skill 规范。
2. `Emily2040/seedance-2.0`：Skill OS 化和模块化架构。
3. `leigegehaha/jimeng-cli-free`：本地工程化和网页端桥接，平台条款另审。
4. `ppdbxdawj/ai-skills`：Seedream 图片提示词工作流。
5. `11Yuxuanyang/seedance-viral-forge`：短视频宣发和营销模板。
6. `songguoxs/seedance-prompt-skill`：许可证确认后再考虑复制。
