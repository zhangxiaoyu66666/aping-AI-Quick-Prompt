using System.Text.Json;

namespace PromptInputMethod.App.Services;

public sealed class PromptTemplateCatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object _builtInCacheLock = new();
    private readonly object _promptsChatCacheLock = new();
    private readonly Dictionary<string, IReadOnlyList<PromptTemplateCatalogItem>> _sourceCache = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<PromptTemplateCatalogItem>? _builtInCache;
    private IReadOnlyList<PromptTemplateCatalogItem>? _promptsChatCache;

    public IReadOnlyList<PromptTemplateCatalogItem> LoadBuiltIn()
    {
        lock (_builtInCacheLock)
        {
            return _builtInCache ??= LoadStaticBuiltIn();
        }
    }

    public IReadOnlyList<PromptTemplateCatalogItem> LoadBySource(string source)
    {
        if (string.Equals(source, "prompts.chat", StringComparison.OrdinalIgnoreCase))
        {
            return LoadPromptsChatTemplatesCached();
        }

        lock (_builtInCacheLock)
        {
            if (_sourceCache.TryGetValue(source, out var cached))
            {
                return cached;
            }

            var builtIn = _builtInCache ??= LoadStaticBuiltIn();
            var templates = builtIn
                .Where(template => string.Equals(template.Source, source, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            _sourceCache[source] = templates;
            return templates;
        }
    }

    public void PreloadSource(string source)
    {
        _ = LoadBySource(source);
    }

    private IReadOnlyList<PromptTemplateCatalogItem> LoadAllBuiltIn()
    {
        return LoadBuiltIn()
            .Concat(LoadPromptsChatTemplatesCached())
            .ToArray();
    }

    private static IReadOnlyList<PromptTemplateCatalogItem> LoadStaticBuiltIn()
    {
        return
        [
            new(
                "builtin-chatgpt-shortcut-translate",
                "英语翻译/修改",
                "ChatGPT-Shortcut",
                "语言与润色",
                """
                I want you to act as an English translator, spelling corrector and improver. I will speak to you in any language and you will detect the language, translate it, and answer in corrected and improved English while preserving the original meaning.
                """),
            new(
                "builtin-chatgpt-shortcut-writing",
                "写作助理",
                "ChatGPT-Shortcut",
                "写作",
                """
                As a writing improvement assistant, improve the spelling, grammar, clarity, concision, and readability of the text provided. Break down long sentences, reduce repetition, and preserve the original meaning and tone.
                """),
            new(
                "builtin-chatgpt-shortcut-prompt-generator",
                "提示词生成器",
                "ChatGPT-Shortcut",
                "提示词工程",
                """
                I want you to act as a prompt generator. I will give you a topic, role, or goal, and you will create a clear, reusable prompt that defines role, task, context, constraints, output format, and quality criteria.
                """),
            new(
                "builtin-chatgpt-shortcut-prompt-improver",
                "提示词修改器",
                "ChatGPT-Shortcut",
                "提示词工程",
                """
                Please improve the following prompt so it produces stronger results. Keep the user's intent, remove ambiguity, add missing constraints, define the expected output format, and make the instruction directly usable.
                """),
            new(
                "builtin-chatgpt-shortcut-summary",
                "总结内容",
                "ChatGPT-Shortcut",
                "总结",
                """
                Summarize the following text into a concise, easy-to-read version. Capture the main points, remove repetition, keep important constraints and decisions, and avoid adding unsupported facts.
                """),
            new(
                "builtin-sd-anima-quality",
                "SD 高质量画面",
                "SD-Anima-Prompt-Studio",
                "画质",
                """
                masterpiece, best quality, highly detailed, ultra detailed, cinematic lighting, volumetric lighting, sharp focus, intricate details, rich color grading, professional illustration
                """),
            new(
                "builtin-sd-anima-character",
                "Anima 角色基础",
                "SD-Anima-Prompt-Studio",
                "角色",
                """
                anime style, expressive eyes, detailed beautiful face, natural pose, clean silhouette, carefully designed outfit, coherent character identity, polished illustration quality
                """),
            new(
                "builtin-sd-anima-composition",
                "Anima 构图与镜头",
                "SD-Anima-Prompt-Studio",
                "构图",
                """
                rule of thirds, dynamic composition, three-quarter view, cinematic wide shot, shallow depth of field, subject in sharp focus, leading lines, balanced framing
                """),
            new(
                "builtin-sd-anima-background",
                "Anima 背景氛围",
                "SD-Anima-Prompt-Studio",
                "背景",
                """
                atmospheric background, soft natural light, detailed environment, coherent color palette, gentle depth, cinematic mood, beautiful sky, subtle environmental storytelling
                """),
            new(
                "builtin-veo3-single-shot",
                "Veo 3｜电影单镜头",
                "Veo 3",
                "电影镜头",
                """
                Create a single cinematic Veo 3 shot.

                Shot: [shot type, camera angle, lens, framing]
                Subject: [character / object / environment]
                Location: [specific place and time]
                Action: [one clear action or beat]
                Camera movement: [push in / tracking / handheld / static / pan / tilt]
                Lighting: [natural / practical / neon / low key / high key]
                Visual style: [cinematic realism / documentary / commercial / stylized]
                Audio: [ambient sound, music, sound effects]
                Dialogue: [optional spoken line]
                Duration and aspect ratio: [seconds, 16:9 / 9:16 / 1:1]
                Constraints: no extra cuts, no unreadable text, no distorted hands or faces, maintain subject consistency.
                """),
            new(
                "builtin-veo3-dialogue-scene",
                "Veo 3｜对白短剧",
                "Veo 3",
                "对白短剧",
                """
                Generate a short cinematic dialogue scene for Veo 3.

                Scene premise: [what is happening]
                Characters: [name / role / appearance / emotion]
                Location and mood: [place, time, atmosphere]
                Shot plan: [single shot or 2-3 short beats]
                Camera: [framing, lens, movement, focus]
                Performance: [gestures, facial expressions, pacing]
                Dialogue:
                Character A: "[line]"
                Character B: "[line]"
                Sound design: [room tone, ambience, music, effects]
                Lighting and color: [specific look]
                Output: [duration, aspect ratio, language]
                Avoid: melodrama, unclear lip sync, extra characters, unstable identity, distorted anatomy.
                """),
            new(
                "builtin-veo3-timestamp-storyboard",
                "Veo 3｜时间戳分镜",
                "Veo 3",
                "时间戳分镜",
                """
                Create a Veo 3 timestamped shot sequence.

                Format:
                0-2s: [opening framing, subject, environment, camera movement, sound]
                2-5s: [main action, emotional beat, lens and movement]
                5-8s: [transition or reveal, lighting change, sound cue]
                8-10s: [ending frame, final action, hold or fade]

                Global style: [cinematic style, color, lighting]
                Subject consistency: [identity, outfit, props]
                Location continuity: [where it happens]
                Audio: [music, ambience, effects, dialogue if any]
                Constraints: smooth continuity, no sudden identity changes, no extra limbs, no text artifacts, no random cuts unless specified.
                """),
            new(
                "builtin-jimeng-general-video",
                "即梦｜通用短视频",
                "即梦 / Seedance",
                "通用短视频",
                """
                生成一段短视频提示词。

                主题：{视频主题}
                风格：{写实 / 电影感 / 轻商业 / 二次元 / 其他}
                画面主体：{人物 / 产品 / 场景 / 事件}
                画面细节：{主体外观、环境、道具、颜色}
                动态效果：{主体动作、镜头运动、转场方式}
                节奏：{舒缓 / 快节奏 / 稳定推进}
                字幕：{是否需要字幕、字幕内容与样式}
                BGM / 音效：{音乐氛围、环境声、关键音效}
                格式要求：{时长、画幅比例、清晰度、平台}
                负面约束：避免画面闪烁、主体变形、文字乱码、风格跑偏、无关元素。
                """),
            new(
                "builtin-jimeng-product-video",
                "即梦｜电商产品视频",
                "即梦 / Seedance",
                "电商产品",
                """
                生成一段电商产品短视频提示词。

                产品：{产品名称 / 类型}
                卖点：{核心卖点 1-3 个}
                使用场景：{家庭 / 户外 / 办公 / 美妆 / 餐饮 / 其他}
                开场画面：{产品出现方式}
                展示动作：{旋转、推近、拆解、使用演示、前后对比}
                镜头语言：{近景、特写、推拉、环绕、慢动作}
                光线与背景：{干净背景、柔和光、品牌色}
                字幕和文案：{短卖点文字，不要过长}
                BGM / 音效：{轻快、科技、清爽、质感}
                格式要求：{9:16 / 16:9，时长}
                负面约束：避免夸大功效、虚假承诺、复杂背景、文字乱码、产品变形、廉价影楼感。
                """),
            new(
                "builtin-jimeng-keyframe-transition",
                "即梦｜首尾帧过渡",
                "即梦 / Seedance",
                "首尾帧过渡",
                """
                基于首尾帧生成一段自然过渡视频提示词。

                首帧描述：{第一张图的主体、构图、光线、场景}
                尾帧描述：{第二张图的主体、构图、光线、场景}
                过渡目标：{从 A 到 B 的变化}
                主体一致性：保持 {人物 / 产品 / 角色 / 场景} 的身份、比例、颜色和关键特征一致。
                镜头运动：{推近 / 拉远 / 横移 / 环绕 / 平滑变焦 / 稳定转场}
                动态细节：{头发、衣物、光影、背景、产品角度等变化}
                节奏：{自然、平滑、不中断}
                字幕 / BGM：{如需填写}
                格式要求：{时长、画幅比例}
                负面约束：避免主体漂移、身份变化、画面闪烁、突然跳切、文字乱码、额外人物或物体。
                """),
            new(
                "builtin-ai-coding-golden",
                "AI编程｜黄金默认模板",
                "AI编程",
                "通用 Agent",
                """
                请作为代码仓库中的 AI 编程智能体完成任务，但必须遵守最小修改原则。

                【我的需求】
                {用户原始需求}

                【请先判断任务类型】
                你需要先判断这是：根因分析 / Bug 修复 / 新增功能 / UI 修改 / 重构规划 / 代码审查 / 配置构建问题。

                【执行规则】
                1. 先阅读相关代码，不要凭空猜测。
                2. 先说明你准备查看哪些文件，以及为什么。
                3. 如果任务不明确，基于现有代码做最小合理假设，不要扩大范围。
                4. 只修改和任务直接相关的文件。
                5. 不要顺手重构，不要新增依赖。
                6. 不要修改配置、锁文件、构建脚本，除非任务必须。
                7. 不要删除现有功能，不要改变用户已有交互逻辑，除非需求明确要求。
                8. 每次修改后检查 diff，确认没有无关改动。

                【验证规则】
                修改完成后运行相关检查：类型检查、lint、build、单元测试或相关测试；如果无法运行，请说明原因。

                【最终回复格式】
                1. 任务类型判断
                2. 根因 / 实现思路
                3. 修改文件
                4. 修改摘要
                5. 验证结果
                6. 风险点
                7. 未完成项
                """),
            new(
                "builtin-ai-coding-root-cause",
                "AI编程｜只查根因",
                "AI编程",
                "根因分析",
                """
                本轮任务只允许分析，不允许修改任何文件。

                【问题现象】
                {描述 bug 现象}

                【请你做的事】
                1. 阅读相关入口、路由、状态管理、条件渲染、生命周期和服务调用代码。
                2. 找出最可能导致该现象的 3 条代码路径。
                3. 区分必然触发路径、偶发触发路径、只是表象但不是根因的路径。
                4. 不要改代码，不要新建文件，不要运行破坏性命令。
                5. 不要根据猜测下结论，必须引用具体文件、函数、变量、条件判断。

                【输出格式】
                - 结论摘要
                - 证据链
                - 可能根因排序
                - 推荐最小修复方案
                - 需要我确认的问题
                """),
            new(
                "builtin-ai-coding-minimal-bugfix",
                "AI编程｜最小修复 Bug",
                "AI编程",
                "Bug 修复",
                """
                请修复以下 bug，要求最小改动。

                【Bug 描述】
                {用户看到的现象}

                【复现步骤】
                1. {步骤1}
                2. {步骤2}
                3. {步骤3}

                【预期行为】
                {正常应该怎样}

                【实际行为】
                {现在怎样}

                【限制】
                - 先定位根因，再修改。
                - 只改和 bug 直接相关的代码。
                - 不要重构，不要新增依赖，不要改变现有公开 API。
                - 不要改 UI 结构，除非 bug 根因就是 UI 结构。
                - 不要修改数据结构，除非必要，并说明兼容性。

                【验证】
                修改后运行：{typecheck / test / build / lint 命令}

                【最终输出】
                1. 根因是什么
                2. 修改了哪些文件
                3. 为什么这是最小修复
                4. 验证命令和结果
                5. 是否存在边界风险
                """),
            new(
                "builtin-ai-coding-feature-add",
                "AI编程｜新增功能",
                "AI编程",
                "新增功能",
                """
                请在现有项目中新增功能：{功能名称}

                【功能目标】
                {一句话说明功能}

                【用户交互】
                - 入口位置：{在哪个页面 / 按钮 / 菜单}
                - 用户操作：{点击 / 输入 / 拖拽 / 快捷键}
                - 系统反馈：{弹窗 / 状态变化 / 保存 / 错误提示}

                【技术限制】
                - 必须沿用当前项目的技术栈和文件组织方式。
                - 优先复用已有组件、service、store、helper。
                - 不要引入新依赖，不要重写已有模块，不要修改无关页面。
                - 不要改变现有数据格式，除非我明确要求。

                【允许修改】
                - {文件 / 目录}

                【禁止修改】
                - {配置文件 / 锁文件 / 核心模块 / 无关目录}

                【实现要求】
                1. 先阅读相关代码，说明现有实现方式。
                2. 给出最小实现方案，然后按最小方案实现。
                3. 实现后补充必要错误处理。
                4. 不要写假数据，除非明确标注 mock。

                【验收标准】
                - {标准1}
                - {标准2}
                - {标准3}

                【输出】
                - 实现方案
                - 修改文件
                - 核心逻辑说明
                - 验证方式
                """),
            new(
                "builtin-ai-coding-ui-change",
                "AI编程｜UI 修改",
                "AI编程",
                "UI 修改",
                """
                请修改当前 UI，严格按以下要求执行。

                【目标页面 / 组件】
                {页面名 / 组件名 / 文件路径}

                【修改内容】
                1. {修改点1}
                2. {修改点2}
                3. {修改点3}

                【视觉风格】
                - 风格：{Windows 11 Fluent Design / Mica / Acrylic / 简洁浅色 / 深色}
                - 主色：{颜色}
                - 圆角：{8px / 12px / 现有规范}
                - 字体层级：保持现有项目规范

                【布局要求】
                - 保留现有整体布局。
                - 只修改 {目标区域}。
                - 不要移动无关组件，不要删除现有功能入口。
                - 不要引入新的 UI 框架，不要改变现有状态管理逻辑。

                【交互要求】
                - {按钮行为}
                - {输入框行为}
                - {悬浮 / 展开 / 折叠行为}

                【禁止事项】
                - 禁止全局重构样式。
                - 禁止改路由、store、后端接口。
                - 禁止删除旧逻辑。
                - 禁止把组件拆成一堆新文件，除非明显必要。

                【验收】
                - 页面能正常打开。
                - 原有功能不受影响。
                - 新 UI 与截图 / 需求一致。
                - 运行 {构建命令} 通过。
                """),
            new(
                "builtin-ai-coding-review",
                "AI编程｜代码审查",
                "AI编程",
                "代码审查",
                """
                请审查当前未提交代码变更，不要修改文件。

                【审查重点】
                1. 是否改了任务无关代码
                2. 是否引入回归风险
                3. 是否有隐藏的破坏性行为
                4. 是否有类型错误、异步竞态、状态不同步
                5. 是否违反项目结构和命名规范
                6. 是否有性能或安全问题

                【请重点检查】
                - {关键模块}
                - {关键状态}
                - {关键接口}
                - {关键 UI 交互}

                【输出格式】
                按严重程度分组：阻塞问题 / 高风险问题 / 中风险问题 / 可优化项 / 没问题的部分。

                每个问题必须包含：文件路径、具体代码位置、为什么是问题、推荐修复方式。
                """),
            new(
                "builtin-skill-codex-skill",
                "Skill体系｜Codex SKILL.md",
                "Skill体系",
                "Codex Skill",
                """
                请为 Codex 设计一个可落地的 Skill。

                【Skill 目标】
                {这个 Skill 要让 Codex 更擅长完成什么任务}

                【适用触发】
                - 用户明确提到：{关键词}
                - 任务涉及：{文件类型 / 工具 / 工作流}
                - 不适用场景：{不要误触发的情况}

                【目录结构】
                skills/{skill-name}/
                - SKILL.md
                - scripts/（可选：可复用脚本）
                - templates/（可选：模板文件）
                - examples/（可选：输入输出示例）
                - references/（可选：短参考文档）

                【SKILL.md 内容要求】
                - name：{短名称}
                - description：一句话说明能力、触发条件和边界
                - workflow：分步骤写清如何执行
                - inputs：需要用户或仓库提供什么
                - outputs：产物格式
                - tools：需要 shell / browser / documents / spreadsheets / MCP 等工具时写明
                - safety：禁止事项、权限边界、不要改什么
                - verification：完成后如何检查

                【输出格式】
                1. Skill 设计摘要
                2. 建议目录结构
                3. 完整 SKILL.md
                4. 可选脚本 / 模板清单
                5. 验证方式
                """),
            new(
                "builtin-skill-claude-skill",
                "Skill体系｜Claude Skill",
                "Skill体系",
                "Claude Skill",
                """
                请为 Claude Code 设计一个 Skill。

                【Skill 名称】
                {skill-name}

                【能力目标】
                {这个 Skill 解决什么重复工作或专业流程}

                【触发描述】
                用一句清晰描述说明 Claude 什么时候应该使用该 Skill，避免泛化过度。

                【工作流】
                1. 先读取哪些上下文
                2. 如何判断是否适用
                3. 需要执行哪些步骤
                4. 需要调用哪些脚本、模板或外部工具
                5. 如何验证结果

                【边界】
                - 不应该处理的任务
                - 需要用户确认的高风险操作
                - 不允许修改的文件或数据

                【产物】
                输出完整的 Skill 文档，必要时附带 scripts / templates / examples 的文件清单。
                """),
            new(
                "builtin-skill-antigravity-rules",
                "Skill体系｜反重力 Rules",
                "Skill体系",
                "Antigravity Rules",
                """
                请为 Google Antigravity / 反重力设计一套项目 Rules + Skill 工作流。

                【目标】
                {要约束或增强 Agent 的哪类任务}

                【Agent Mode】
                - Planning Mode：{何时必须规划}
                - Fast Mode：{何时允许快速执行}

                【权限策略】
                - 允许直接执行：{安全命令 / 只读操作}
                - 需要 review：{多文件修改 / 依赖 / 构建配置 / 删除 / 移动}
                - 禁止执行：{破坏性命令 / 越权目录 / 凭据操作}

                【Artifacts】
                每个非平凡任务必须产出：
                1. Task List
                2. Implementation Plan
                3. Diff Summary
                4. Verification Walkthrough
                5. Risk Notes

                【Skill 工作流】
                写清触发条件、输入、执行步骤、检查方式和回滚策略。
                """),
            new(
                "builtin-skill-authoring-checklist",
                "Skill体系｜设计检查清单",
                "Skill体系",
                "设计规范",
                """
                请把我的需求整理成一个 Skill 设计检查清单。

                【Skill 基本信息】
                - 名称：
                - 一句话描述：
                - 目标用户：
                - 适用平台：Codex / Claude Code / Antigravity / 通用 Agent

                【触发条件】
                - 明确触发词：
                - 隐式触发场景：
                - 不应触发场景：

                【上下文需求】
                - 必须读取的文件：
                - 可选参考资料：
                - 需要用户补充的信息：

                【执行流程】
                1. 识别任务
                2. 读取上下文
                3. 选择脚本 / 模板
                4. 执行或生成产物
                5. 验证
                6. 汇报

                【安全边界】
                - 禁止修改：
                - 必须确认：
                - 禁止命令：

                【验收】
                - 可复用
                - 可验证
                - 不误触发
                - 不越权
                """),
            new(
                "builtin-skill-female-portrait-director",
                "女性人像提示词导演 Skill",
                "Skill",
                "内置文生图",
                BuildFemalePortraitDirectorSkillText()),
        ];
    }

    private static string BuildFemalePortraitDirectorSkillText()
    {
        var rootPath = Path.Combine(AppContext.BaseDirectory, "Data", "skills", "female-portrait-director");
        var skillPath = Path.Combine(rootPath, "SKILL.md");
        var rootSkill = TryReadText(skillPath);
        var rootSkillSection = string.IsNullOrWhiteSpace(rootSkill)
            ? "SKILL.md 尚未复制到运行目录；请确认应用内容文件已随构建输出。"
            : rootSkill.Trim();

        return $"""
        【内置 Skill 包】
        name: female-portrait-director
        description: 内置文生图 Skill，用于女性人像、写真、服装、电商试衣、古风仙侠、生活方式、影楼精修、参考图保留、提示词优化、图片反推、参数推荐与安全改写。
        Skill 名称：女性人像提示词导演 Skill
        英文名称：Female Portrait Prompt Director Skill
        Skill 文件：{skillPath}
        来源目录：{rootPath}
        来源项目：https://github.com/liyue-aigc/female-portrait-director
        作者：李岳
        版本：FEMALE-PORTRAIT-DIRECTOR-V1.4.1
        授权：MIT License
        输出适配：在啊拼提示词输出框内生成纯文本提示词，遵守 Skill 内容与负面约束，但不要使用 Markdown 代码块或 Markdown 包装最终提示词。
        适用：文生图、女性人像、成人女性肖像、写真、服装搭配、电商试衣、清纯生活照、纯欲曲线、都市时尚、古风仙侠、复古港风、法式慵懒、新中式、运动写真、旅行写真、影楼精修、东方丰腴、参考图保留、提示词优化、图片反推、参数推荐、安全改写
        关键词：女性人像、女角色、美女、写真、肖像、portrait、female portrait、text-to-image、文生图、清纯、纯欲、曲线、都市、时尚、古风、仙侠、电商、服装、试衣、港风、法式、新中式、运动、旅行、影楼、东方丰腴、参考图、反推、参数

        --- ROOT SKILL.md ---
        {rootSkillSection}
        """;
    }

    private static string TryReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private IReadOnlyList<PromptTemplateCatalogItem> LoadPromptsChatTemplatesCached()
    {
        lock (_promptsChatCacheLock)
        {
            return _promptsChatCache ??= LoadPromptsChatTemplates();
        }
    }

    private static IReadOnlyList<PromptTemplateCatalogItem> LoadPromptsChatTemplates()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "prompts-chat-templates.json");
        if (!File.Exists(path))
        {
            return Array.Empty<PromptTemplateCatalogItem>();
        }

        try
        {
            var json = File.ReadAllText(path);
            var items = JsonSerializer.Deserialize<PromptTemplateFileItem[]>(json, JsonOptions) ?? [];
            return items
                .Where(item => !string.IsNullOrWhiteSpace(item.Title)
                    && !string.IsNullOrWhiteSpace(item.Text))
                .Select(item => new PromptTemplateCatalogItem(
                    string.IsNullOrWhiteSpace(item.Id) ? $"prompts-chat-{Guid.NewGuid():N}" : item.Id.Trim(),
                    item.Title.Trim(),
                    string.IsNullOrWhiteSpace(item.Source) ? "prompts.chat" : item.Source.Trim(),
                    string.IsNullOrWhiteSpace(item.Category) ? "通用角色" : item.Category.Trim(),
                    item.Text.Trim()))
                .ToArray();
        }
        catch
        {
            return Array.Empty<PromptTemplateCatalogItem>();
        }
    }

    public IReadOnlyList<PromptTemplateCatalogItem> MergeWithUserTemplates(IEnumerable<PromptFavorite> userTemplates)
    {
        var builtIn = LoadAllBuiltIn();
        var users = userTemplates
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .Select(item => new PromptTemplateCatalogItem(
                item.Id,
                item.Title,
                string.IsNullOrWhiteSpace(item.Source) ? "我的模板" : item.Source,
                string.IsNullOrWhiteSpace(item.Category) ? "未分类" : item.Category,
                item.Text,
                true))
            .ToArray();

        return users.Concat(builtIn).ToArray();
    }

    public IReadOnlyList<PromptTemplateCatalogItem> MergeWithUserTemplatesBySource(
        IEnumerable<PromptFavorite> userTemplates,
        string source)
    {
        var users = userTemplates
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .Select(item => new PromptTemplateCatalogItem(
                item.Id,
                item.Title,
                string.IsNullOrWhiteSpace(item.Source) ? "我的模板" : item.Source,
                string.IsNullOrWhiteSpace(item.Category) ? "未分类" : item.Category,
                item.Text,
                true))
            .Where(template => string.Equals(template.Source, source, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return users.Concat(LoadBySource(source)).ToArray();
    }
}

internal sealed record PromptTemplateFileItem(
    string Id,
    string Title,
    string Source,
    string Category,
    string Text);

public sealed record PromptTemplateCatalogItem(
    string Id,
    string Title,
    string Source,
    string Category,
    string Text,
    bool IsUserTemplate = false)
{
    public string DisplayTitle => $"{Title}  ·  {Source} / {Category}";

    public override string ToString()
    {
        return DisplayTitle;
    }
}
