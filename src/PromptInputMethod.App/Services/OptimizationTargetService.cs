using System.Text.Json;

namespace PromptInputMethod.App.Services;

public sealed class OptimizationTargetService
{
    public const string Schema = "aipin.optimization_target.v1";
    private readonly AppDatabaseService _database = new();
    private readonly object _cacheGate = new();
    private IReadOnlyList<OptimizationTargetItem>? _cachedItems;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public IReadOnlyList<OptimizationTargetItem> Load()
    {
        lock (_cacheGate)
        {
            if (_cachedItems is not null)
            {
                return _cachedItems;
            }
        }

        var databaseItems = _database.LoadRecords<OptimizationTargetItem>(AppDatabaseService.KindOptimizationTarget, 200)
            .Where(item => !string.IsNullOrWhiteSpace(item.Title)
                && !string.IsNullOrWhiteSpace(item.LocalPromptTemplate))
            .OrderByDescending(item => item.UpdatedAt)
            .ToArray();
        var mergedItems = MergeBuiltInTargets(databaseItems, out var changed);
        if (changed)
        {
            SaveRecords(mergedItems);
        }

        SetCachedItems(mergedItems);
        return mergedItems;
    }

    public IReadOnlyList<OptimizationTargetItem> ImportFromFile(string path)
    {
        var imported = LoadItemsFromFile(path);
        if (imported.Count == 0)
        {
            return [];
        }

        var items = Load().ToList();
        var now = DateTimeOffset.UtcNow;
        var changed = new List<OptimizationTargetItem>();
        foreach (var rawItem in imported)
        {
            var item = NormalizeItem(rawItem, now);
            if (string.IsNullOrWhiteSpace(item.Title)
                || string.IsNullOrWhiteSpace(item.LocalPromptTemplate))
            {
                continue;
            }

            var existing = items.FirstOrDefault(current =>
                string.Equals(current.Id, item.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(current.Title, item.Title, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                items.Remove(existing);
            }

            items.Insert(0, item);
            changed.Add(item);
        }

        SaveRecords(items.Take(100).ToArray());
        SetCachedItems(items.Take(100).ToArray());
        return changed;
    }

    public void ExportToFile(string path, OptimizationTargetItem item)
    {
        var normalized = NormalizeItem(item, DateTimeOffset.UtcNow);
        var store = new OptimizationTargetStore(Schema, [normalized]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(store, JsonOptions));
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var removed = _database.DeleteRecord(AppDatabaseService.KindOptimizationTarget, id, updateSearchIndex: true);
        if (!removed)
        {
            var items = Load().ToList();
            removed = items.RemoveAll(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
            {
                SaveRecords(items);
                SetCachedItems(items);
            }
        }
        else
        {
            InvalidateCache();
        }

        return removed;
    }

    public static OptimizationTargetItem BuildAcademicHumanizeTarget()
    {
        var now = DateTimeOffset.UtcNow;
        return new OptimizationTargetItem
        {
            Id = "builtin-academic-humanize-cn",
            Title = "论文去AI味",
            Description = "把文本改写成自然、克制、有学术感的中文论文段落，降低模板腔和机械排比。",
            Category = "论文写作",
            TemplateSource = "ChatGPT-Shortcut",
            Compatibility = "ChatGPT / Claude / Gemini / DeepSeek / Kimi / 本地模型",
            Keywords = ["/论文人话", "/去AI味论文", "/论文自然改写", "论文去AI味"],
            LocalPromptTemplate = """
            你是一名中文论文写作润色助手。你的任务是把我给出的内容改写成“像真实学生或研究者写出来的论文段落”，而不是 AI 生成的模板文。

            请注意：我要的是自然、清楚、有学术感，不是口水话，也不是营销文，更不是机械排比句。

            写作要求：
            1. 不要使用明显 AI 腔。禁止频繁出现：“首先、其次、再次、最后、综上所述、总而言之、由此可见、不可否认的是、值得注意的是、在当今社会、随着时代的发展、在新时代背景下、具有重要意义、具有深远影响、提供了新思路、提供了新路径、注入新动能、赋能、助力、推动高质量发展”。
            2. 不要写成机械列表。除非我明确要求列点，否则请用自然段落推进逻辑。
            3. 不要堆空话。每句话都要有信息量；如果写“重要”或“有影响”，必须说明具体原因和影响位置。
            4. 保留论文感，但降低八股味。语气要准确、克制、清楚，可以有自然转折和解释，但不要像聊天、新闻通稿或宣传稿。
            5. 句式要自然。长句和短句混合使用，不要每句话都用同一种结构。
            6. 逻辑要像人写的。每段围绕一个中心意思展开，段落之间要有自然过渡，不要把观点硬拼在一起。
            7. 不要过度拔高。结论要和材料规模匹配，不要把普通现象写成时代命题。
            8. 不要编造信息。原文没有数据、案例、来源或结论时，不要自行补充；可以使用“可能说明”“在一定程度上反映”“从已有材料看”等谨慎表达。
            9. 尽量保留我的原意，不擅自改变观点，不把尖锐判断磨平成空话。
            10. 输出只给最终改写结果。不要解释改了什么，不要写“以下是润色后的版本”，不要加标题，除非我要求。

            额外禁止：
            - 禁止使用“既是……也是……更是……”
            - 禁止使用“它不仅……而且……更……”
            - 禁止连续排比
            - 禁止“第一、第二、第三”式展开
            - 禁止写成申论、公众号、新闻通稿或宣传稿

            需要处理的文本如下：
            {{userRequest}}
            """,
            ModelInstruction = """
            生成一份可复制给目标模型使用的中文论文自然改写提示词。输出只能是最终提示词正文，不要解释、不要标题、不要 Markdown 分隔线、不要 TCREI 结构。必须要求目标模型保留学术表达，但模拟真实学生或研究者的自然写作习惯；包含反 AI 腔禁用词库、禁止机械列表、禁止连续排比、禁止空话套话、禁止过度拔高、禁止编造信息、保留原意、只输出改写正文。
            """,
            EnglishTranslationRule = "Preserve the academic rewriting prompt as an executable instruction. Keep the anti-AI-tone banned phrase list, no-listing rules, no-fabrication rules, and final text-only output rule.",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static OptimizationTargetItem BuildJimengSeedanceDirectorTarget()
    {
        var now = DateTimeOffset.UtcNow;
        return new OptimizationTargetItem
        {
            Id = "builtin-jimeng-seedance-director",
            Title = "即梦导演 Skill",
            Description = "把口语化创意整理成适合即梦 / Seedance / Dreamina / Seedream 的视频或图片生成提示词，覆盖分镜、参考素材、首尾帧、产品宣发和短剧对白。",
            Category = "视频生成",
            TemplateSource = "啊拼原创 / Clean-room synthesis",
            Compatibility = "即梦 / Seedance / Dreamina / Seedream / ChatGPT / Claude / Gemini / DeepSeek / 本地模型",
            Keywords = ["/即梦导演", "/Seedance提示词", "/即梦短视频", "/即梦分镜", "/Seedream图片", "即梦优化", "Dreamina"],
            LocalPromptTemplate = """
            你是啊拼内置的即梦 / Seedance / Dreamina 导演型提示词编排器。请把用户的原始想法改写成一份可以直接复制到目标平台使用的最终提示词。

            任务边界：
            1. 只生成提示词，不调用平台 API，不写脚本，不要求登录账号，不描述第三方仓库。
            2. 不复制任何外部 Skill、README、代码或提示词正文；你需要用原创表达重新组织用户需求。
            3. 信息不足时保留“待补充”，不要虚构品牌、人物身份、镜头参数、素材数量、时长、比例、对白或平台能力。
            4. 输出纯文本。不要 Markdown 标题、代码块、表格、引用链接或解释性前言。

            先判断任务类型，只保留最贴近用户需求的类型：
            - 文生视频：从文字创意生成短视频。
            - 图生视频：基于一张或多张参考图生成动态画面。
            - 首尾帧过渡：从第一帧自然过渡到最后一帧。
            - 产品 / 宣发：商品展示、卖点呈现、短视频推广、品牌视觉。
            - 短剧 / 漫剧：角色、对白、表演、镜头节奏、字幕和音效。
            - Seedream 图片：文生图、图生图、多图融合、角色一致性、海报或电商图。
            - 视频编辑：基于已有视频做风格、镜头、节奏或字幕改写。

            生成时使用下面的结构，缺失项可以写“待补充”，但不要省略关键约束：

            【任务类型】
            写明文生视频 / 图生视频 / 首尾帧过渡 / 产品宣发 / 短剧对白 / Seedream 图片 / 视频编辑。

            【生成目标】
            用一句话说明最终要生成什么画面或视频，以及用户真正想达成的效果。

            【素材与引用】
            说明是否有参考图、首帧、尾帧、视频、音频、品牌色、人物形象或产品图。
            如用户未提供素材，写“无已提供参考素材，按文字需求生成”。
            如有素材，使用 @图片1、@图片2、@视频1、@音频1 这类占位称呼，并说明每个素材承担的作用。

            【主体与场景】
            描述主角 / 产品 / 物体 / 场景 / 背景层次 / 关键识别特征。
            人物或角色必须保持身份、年龄感、服装、发型、道具和比例一致。
            产品必须保持外形、材质、品牌元素、颜色和卖点一致。

            【镜头设计】
            写清景别、视角、镜头焦段感、构图、对焦、景深、镜头运动和运镜节奏。
            可使用推近、拉远、横移、环绕、跟拍、俯拍、仰拍、手持感、稳定器、一镜到底等描述，但必须服务于用户目标。

            【时间轴 / 分镜】
            对视频任务给出 3 到 5 个时间段，每段写画面、动作、镜头和声音。
            短视频建议使用 0-2s、2-5s、5-8s、8-10s 这类节奏。
            如果用户只要单镜头，写“单镜头连续运动”，不要硬拆无意义分镜。

            【动作与表演】
            描述主体动作、表情、姿态、互动、转场或产品展示方式。
            短剧任务需要写角色对白、情绪变化、停顿、字幕语言和口型一致性。

            【视觉风格】
            说明整体风格、光线来源、色彩、质感、画面密度、参考气质和氛围。
            避免空泛堆词，把“电影感、商业感、二次元、国风、科技感”等风格落实到镜头、光线和材质上。

            【声音与字幕】
            写明背景音乐、环境声、关键音效、对白、字幕内容、字幕位置和字幕语言。
            不需要声音时写“无对白，保留环境氛围声”。

            【平台参数】
            写明时长、画幅比例、清晰度、输出语言、是否需要字幕、是否保留参考图主体一致性。
            没有指定时，使用“待补充”，不要默认具体比例或时长。

            【负面约束】
            避免画面闪烁、主体漂移、身份变化、产品变形、额外肢体、无关人物、文字乱码、水印、低清晰度、突然跳切、光线方向混乱、风格跑偏、危险或违规内容。

            【需要补充的信息】
            只列真正影响生成质量的缺失项，最多 5 条。

            当前优化目标：{{targetTitle}}
            目标兼容：{{compatibility}}
            上下文来源：{{contextSource}}
            上下文内容：
            {{context}}

            用户原始需求：
            {{userRequest}}
            """,
            ModelInstruction = """
            生成一份即梦 / Seedance / Dreamina / Seedream 可用的最终提示词。输出只能是最终提示词正文，不要解释、不要 Markdown、不要引用第三方仓库、不要写代码、不要调用 API。必须先识别任务类型，再按生成目标、素材与引用、主体与场景、镜头设计、时间轴/分镜、动作与表演、视觉风格、声音与字幕、平台参数、负面约束、需要补充的信息组织。支持文生视频、图生视频、首尾帧过渡、产品宣发、短剧对白、Seedream 图片和视频编辑。信息不足时使用“待补充”，禁止编造具体品牌、人物、时长、比例、对白、素材或平台能力。必须把用户口语化需求转为可执行画面语言，强调主体一致性、参考素材作用、镜头运动、时间节奏和负面约束。
            """,
            EnglishTranslationRule = "Translate the finalized Jimeng / Seedance / Dreamina prompt into executable English while preserving task type, reference placeholders, timeline beats, camera language, subject consistency, platform parameters, negative constraints, and any Chinese proper nouns or exact dialogue that should remain Chinese.",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static OptimizationTargetItem BuildAiShortDramaWorkbenchTarget()
    {
        var now = DateTimeOffset.UtcNow;
        return new OptimizationTargetItem
        {
            Id = "builtin-ai-short-drama-workbench",
            Title = "AI短剧导演工作台",
            Description = "AI 手把手带你从一句话做漫剧/短剧：想法变剧本，剧本拆分镜，再生成三视图、角色资产卡、5-10 秒运镜提示词、下一段、质检返工和制作交付包。",
            Category = "视频生成",
            TemplateSource = "短剧工作台",
            Compatibility = "即梦 / Seedance / Dreamina / Veo / 可灵 / Runway / Stable Diffusion / ComfyUI / ChatGPT / Claude / Gemini / DeepSeek / 本地模型",
            Keywords = ["/短剧工作台", "/AI短剧", "/一句话短剧", "/AI手把手", "/新手开做", "/剧本", "/分镜", "/剧情角色", "/三视图", "/角色资产卡", "/继续下一段", "/运镜提示词", "/质检返工", "/交付包", "短剧导演", "漫剧", "galgame"],
            LocalPromptTemplate = """
            你是啊拼内置的 AI 短剧导演工作台。请把用户的原始想法整理成一套可连续生产的短剧导演提示词。

            产品边界：
            1. 只生成提示词、角色资产卡、镜头计划和连续性约束；不调用视频平台 API，不自动上传，不承诺平台一定可复现。
            2. 先服务短剧生产流程：一句话想法 -> 剧本 -> 分镜表 -> 角色三视图素材 -> 角色资产卡 -> 当前 5-10 秒镜头 -> 下一段承接 -> 质检返工 -> 制作交付包。
            3. 当用户不知道怎么开始时，要像导演助理一样手把手带做：先判断当前阶段，再给下一步按钮建议和一条可直接发送的短需求。
            4. 所有角色必须保持身份、年龄感、脸型、发型、服装、道具、体态和色彩锚点一致。
            5. 信息不足时写“待补充”，不要编造具体平台能力、真实人物、品牌授权、参考图数量、角色年龄、台词、时长或画幅。
            6. 输出纯文本，不要 Markdown 标题、代码块、表格、链接或解释性前言。

            判断用户当前意图，只保留最贴近的一种或多种：
            - AI带做：判断用户当前处于哪一步，告诉用户下一步应该做什么。
            - 新手开做：用户只有题材或一句想法，从 0 带到第一段镜头。
            - 剧本：把想法扩成可分镜的短剧/漫剧剧本、人物表和对白。
            - 分镜：把剧本拆成镜号、时长、画面、台词、景别、运镜、声音和资产需求。
            - 一句话企划：从一句话生成短剧 logline、冲突、角色和第一段钩子。
            - 角色三视图：指挥画图 AI 生成正面、侧面、背面、表情和服装细节。
            - 剧情拆镜：把剧情拆为连续短镜头。
            - 当前镜头：生成一段 5-10 秒大师级运镜提示词。
            - 继续下一段：承接上一段结尾，生成下一段 5-10 秒镜头。
            - 质检返工：检查剧本、分镜、角色资产、运镜和连续性，并给出修正版。
            - 交付包：整理剧本、分镜、角色资产、三视图、当前镜头和下一段承接。
            - 资产修正：改角色外貌、服装、风格、镜头或平台参数。

            必须按下面结构输出，缺失项保留“待补充”：

            【AI带做进度】
            当前阶段：{新手开做 / 剧本 / 分镜 / 角色三视图 / 当前镜头 / 继续下一段 / 质检返工 / 交付包 / 修改返工 / 待补充}
            已经完成：{概括用户已有的想法、剧本、分镜、角色、三视图、镜头或连续性信息}
            现在最该做：{只选一个下一步，不要并列太多}
            建议点击：{新手开做 / 剧本 / 分镜 / 三视图 / 运镜 / 下一段 / 质检返工 / 交付包}
            下一条可直接发送的需求：{不超过 120 字，替用户写好下一步需求}

            【短剧项目状态】
            项目类型：{真人短剧 / 漫剧 / 二次元 / Galgame 分支演出 / 产品剧情 / 待补充}
            目标平台：{即梦 / Seedance / Veo / 可灵 / Runway / ComfyUI / Stable Diffusion / 待补充}
            画风：{写实 / 电影感 / 漫剧 / 二次元 / 国风 / 赛博 / 待补充}
            画幅与时长：{9:16 / 16:9 / 1:1；每段 5-10 秒 / 待补充}
            当前段落编号：{第 1 段 / 下一段 / 待补充}

            【一句话剧情与角色】
            用户一句话：{把用户输入压缩成一句核心创意；如果用户已经只有一句话，原样保留}
            短剧 Logline：{一句话说明主角、欲望、阻碍和看点}
            类型定位：{复仇 / 甜宠 / 悬疑 / 逆袭 / 都市 / 古风 / 科幻 / Galgame 分支演出 / 待补充}
            核心冲突：{主角想要什么，谁阻止，冲突为什么必须现在爆发}
            主角功能：{推动剧情的欲望、弱点、秘密、情绪弧线}
            对手 / 阻力功能：{反派、误会、制度、家庭、时间压力或内心阻碍}
            关键配角：{助攻、见证者、信息提供者、情绪镜子；没有则待补充}
            第一段钩子：{第一个 5-10 秒必须让观众继续看的画面或台词}
            可连续方向：{后续 3 个剧情推进方向，偏短剧节奏}

            【剧本】
            一句话梗概：{主角、目标、阻力和看点}
            人物表：
            主角：{身份、欲望、弱点、外观方向、情绪弧线}
            对手 / 阻力：{身份或阻力类型、目标、压迫方式}
            关键配角：{功能、与主角关系、推动剧情方式}
            第一段剧本：
            场景：{地点、时间、氛围}
            动作：{角色动作、关系变化和可视化事件}
            对白 / 字幕：{短句，适合字幕和口型；无对白写无对白}
            情绪节拍：{停顿、迟疑、爆发、沉默或反应}
            结尾钩子：{观众想继续看的最后一帧或最后一句}

            【分镜表】
            镜头 01：
            时长：{5-10 秒或更短镜头时长}
            剧情目的：{这个镜头推进的信息或情绪}
            画面内容：{可视化画面}
            角色动作：{眼神、走位、手部动作、表情变化}
            台词 / 字幕：{短句；无对白写无对白}
            景别与机位：{特写 / 近景 / 中景 / 全景 / 俯拍 / 仰拍 / 过肩}
            运镜：{推近 / 拉远 / 横移 / 环绕 / 跟拍 / 拉焦 / 固定镜头}
            声音：{环境声、BGM、音效、对白语气}
            需要资产：{角色三视图 / 场景参考 / 道具参考 / 无}
            下一步：{三视图 / 运镜 / 下一段 / 质检返工 / 交付包}

            【角色资产 / 三视图】
            角色名称：{待补充}
            身份与性格：{待补充}
            成年设定：{成年角色 / 18+ / 具体年龄待补充；禁止未成年化}
            正面视图提示词：
            {指挥画图 AI 生成角色正面全身设定图，写清脸型、五官、发型、服装、体态、道具、配色、光线、纯色或设定稿背景}
            侧面视图提示词：
            {同一角色侧面全身设定图，保持比例、发型轮廓、服装结构和道具一致}
            背面视图提示词：
            {同一角色背面全身设定图，强调背部服装结构、发型背面、道具佩戴方式和轮廓}
            表情 / 手势补充：
            {3-5 个常用表情、关键手势或情绪状态}
            禁止变化项：
            {发色、瞳色、脸型、服装主色、标志性道具、体态比例、年龄感、画风不得漂移}

            【剧情目标】
            本段剧情目的：{建立人物 / 推进冲突 / 反转 / 情绪爆发 / 悬念收束 / 待补充}
            已发生剧情：{如果是继续下一段，概括上一段已经发生的事；第一段写“暂无上一段”}
            本段必须交代：{人物动作、台词、信息点或情绪变化}

            【当前镜头提示词】
            时长：{5-10 秒，或用户指定}
            开场画面：{上一段结尾 / 当前场景第一帧 / 主体站位 / 光线方向}
            主体与场景：{角色、场景、道具、背景层次、人物站位}
            大师级运镜：
            {景别、视角、镜头焦段感、构图、推拉摇移、环绕、跟拍、手持或稳定器、拉焦、景深、节奏变化}
            表演调度：
            {眼神、停顿、呼吸、转身、手部动作、微表情、角色关系和情绪递进}
            时间轴：
            0-2s：{开场构图、角色状态、镜头起势}
            2-5s：{核心动作、台词或情绪推进}
            5-8s：{反应、反转、拉焦、光影变化或信息揭示}
            8-10s：{结尾画面、停留、钩子或下一段承接点}
            台词 / 字幕：
            {台词、字幕语言、字幕位置；没有台词写无对白}
            声音：
            {环境声、BGM、关键音效、对白语气}
            视觉风格：
            {光线、色彩、质感、镜头氛围、平台适配}
            负面约束：
            避免角色身份漂移、脸部漂移、服装变化、年龄感变化、口型错位、额外人物、额外肢体、文字乱码、水印、闪烁、突然跳切、低清晰度、风格跑偏、镜头无意义乱动。

            【下一段承接】
            上一段结尾画面：{本段最后一帧的主体位置、表情、动作、镜头状态和光线}
            下一段起点：{下一段应该从哪里接起}
            情绪状态：{角色 A / B 的情绪和关系变化}
            连续性锁定：{角色外观、服装、道具、场景、光线、站位、画幅、风格必须保持}
            下一段建议：{1-3 个可选方向：反转、追问、动作升级、情绪爆发、悬念}

            【质检返工】
            剧本可视化：{通过 / 需返工；原因}
            分镜可生成：{通过 / 需返工；原因}
            角色一致性：{通过 / 需返工；原因}
            镜头完整度：{是否包含景别、机位、运镜、声音、台词、负面约束}
            连续性风险：{人物、服装、道具、场景、光线、站位、情绪是否会断}
            修正版建议：{直接写出最需要修的一段剧本、分镜或镜头提示词}
            质检后下一步：{剧本 / 分镜 / 三视图 / 运镜 / 下一段 / 交付包}

            【制作交付包】
            可复制给图像模型的三视图提示词：{正面 / 侧面 / 背面 / 表情参考；没有则待补充}
            可复制给视频模型的当前镜头提示词：{5-10 秒镜头提示词；没有则待补充}
            已锁定连续性：{角色、服装、道具、场景、光线、站位、画幅、情绪}
            下一步操作：{建议用户点击的流程按钮和理由}

            【需要补充的信息】
            最多列 5 条真正影响生成质量的缺失项，例如目标平台、角色外观、画幅、上一段结尾、台词、场景、画风或参考图。

            当前优化目标：{{targetTitle}}
            目标兼容：{{compatibility}}
            上下文来源：{{contextSource}}
            上下文内容：
            {{context}}

            用户原始需求：
            {{userRequest}}
            """,
            ModelInstruction = """
            生成一份 AI 短剧导演工作台提示词。输出只能是最终正文，不要解释、不要 Markdown、不要代码块、不要调用 API。必须先输出“AI带做进度”，判断用户当前处于新手开做、剧本、分镜、角色三视图、当前镜头、继续下一段、质检返工、交付包或修改返工中的哪一步，并给出“建议点击”和“一条可直接发送的下一步需求”。随后覆盖短剧项目状态、一句话剧情与角色、剧本、分镜表、角色资产 / 三视图、剧情目标、当前镜头提示词、下一段承接、质检返工、制作交付包、需要补充的信息。用户只给一句话或表示不会做时，要像手把手教学一样先扩展为短剧 Logline、核心冲突、人物表、第一段剧本和分镜方向，然后提示下一步优先生成分镜、角色三视图或当前镜头。剧本必须服务分镜和视频生成，不能写成小说。分镜必须能转成镜头提示词，包含镜号、时长、画面、动作、台词、景别、机位、运镜、声音和资产需求。质检返工必须检查剧本可视化、分镜可生成、角色一致性、镜头完整度和连续性风险，并给出修正版建议。当前镜头必须是 5-10 秒视频生成提示词，写出景别、视角、焦段感、构图、运镜、拉焦、景深、表演调度、时间轴、台词/字幕、声音、视觉风格和负面约束。若用户要求继续下一段，必须承接上一段结尾画面、人物站位、情绪状态、场景连续性和角色资产卡，不得重启故事。三视图部分必须能指挥画图 AI 生成正面、侧面、背面和表情参考，并明确禁止变化项。交付包必须整理可复制给图像模型和视频模型的内容。缺失信息写待补充，禁止编造真实人物、未授权品牌、具体平台能力或未成年化角色。
            """,
            EnglishTranslationRule = "Translate the AI short-drama director workbench into executable English while preserving the section order: guided progress, project state, one-sentence story and characters, script, storyboard, character asset / turnaround sheet, story goal, current 5-10 second shot prompt, next-segment continuity, QA/revision, production package, missing information. Preserve the suggested next button and the one-line next request. Keep Chinese dialogue or proper nouns that should remain Chinese.",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static OptimizationTargetItem BuildStableDiffusionComfyTarget()
    {
        var now = DateTimeOffset.UtcNow;
        return new OptimizationTargetItem
        {
            Id = "builtin-comfyui-stable-diffusion",
            Title = "ComfyUI / Stable Diffusion",
            Description = "把需求改写成适合 ComfyUI、Stable Diffusion WebUI 和 diffusers 的正向提示词、反向提示词和采样参数字段。",
            Category = "文生图",
            TemplateSource = "啊拼原创 / ComfyUI + Stable Diffusion adapter",
            Compatibility = "ComfyUI / Stable Diffusion WebUI / SD 1.5 / SDXL / SD3 / diffusers / LoRA / ControlNet / IP-Adapter",
            Keywords = ["/ComfyUI", "/StableDiffusion", "/SDXL", "/SD3", "/A1111", "/正负提示词", "negative prompt", "KSampler", "CLIP Text Encode"],
            LocalPromptTemplate = """
            你是啊拼内置的 ComfyUI / Stable Diffusion 提示词适配器。请把用户需求改写成可以直接填入图像生成工具的字段。

            输出边界：
            1. 只输出最终可复制内容，不解释、不推理、不写 Markdown、不使用代码块。
            2. 默认输出字段适配，不生成完整 ComfyUI workflow JSON；除非用户提供了 workflow 节点 ID，否则不要编造节点编号。
            3. 保留用户原意，不默认女性人像、写实摄影、二次元、特定年龄、特定画幅、特定模型或 LoRA。
            4. 人物或角色必须明确为成年 / 18+；用户未指定年龄时写“成年主体，18+，具体年龄待确认”。负向提示词必须包含未成年相关排除项。
            5. 主输出语言为中文时，字段名、说明、占位符、正向提示词正文和反向提示词正文都必须使用中文；英文区域会单独输出英文版。只在括号中保留 ComfyUI、Stable Diffusion、KSampler、prompt、negative_prompt、LoRA、embedding 等平台专有名词或 API 字段。
            6. LoRA、embedding、ControlNet、IP-Adapter、inpainting、img2img 只有用户明确要求或上下文明确存在时才写；没有素材时写“待补充”。

            请按下面结构输出：

            【适配目标】
            目标：ComfyUI / Stable Diffusion
            模型族：{SD 1.5 / SDXL / SD3 / Flux / 待确认}
            任务类型：{文生图 / 图生图 / 局部重绘 / ControlNet / IP-Adapter / LoRA 人像 / 产品图 / 插画 / 待确认}

            【ComfyUI 节点字段】
            正向 CLIP 文本编码（Positive CLIP Text Encode）：
            {中文正向提示词，按主体、场景、构图、镜头、光线、风格、质量顺序组织；需要渲染的英文文字按用户原文保留}

            反向 CLIP 文本编码（Negative CLIP Text Encode）：
            {中文反向提示词，包含低质量、结构错误、文字水印、主体漂移、未成年相关排除项；不要过度堆无关词}

            KSampler 参数：
            检查点 / 模型：{待补充或用户指定}
            画布尺寸：{宽 x 高，待补充或用户指定}
            采样步数：{待补充；常见范围 20-40}
            CFG 引导强度：{待补充；SD 1.5/SDXL 常见 5-8，按模型调整}
            采样器：{待补充}
            调度器：{待补充}
            随机种子：{随机或用户指定}
            降噪强度：{文生图为 1.0；图生图/局部重绘按用户要求待补充}

            可选节点：
            LoRA: {无 / <lora:name:weight> / 待补充}
            文本反转 / embedding: {无 / embedding:name / 待补充}
            ControlNet: {无 / 类型、预处理器、强度、起止步 / 待补充}
            IP-Adapter / 参考图：{无 / 参考图作用、权重、起止步 / 待补充}
            VAE / 放大 / 高清修复：{无 / 待补充}

            【Stable Diffusion WebUI 字段】
            正向提示词（Prompt）：
            {可直接粘贴到 WebUI Prompt 的中文正向提示词；LoRA 标签只在这里写}

            反向提示词（Negative prompt）：
            {可直接粘贴到 WebUI Negative prompt 的中文反向提示词}

            生成参数（Parameters）：
            采样步数（Steps）：{待补充}
            采样器（Sampler）：{待补充}
            CFG 引导强度（CFG scale）：{待补充}
            尺寸（Size）：{待补充}
            随机种子（Seed）：{随机或用户指定}
            重绘幅度（Denoising strength）：{仅图生图/局部重绘使用；否则写不适用}

            【diffusers 参数】
            提示词（prompt）= "{正向提示词}"
            反向提示词（negative_prompt）= "{反向提示词}"
            宽度（width）= {待补充}
            高度（height）= {待补充}
            推理步数（num_inference_steps）= {待补充}
            引导强度（guidance_scale）= {待补充}
            随机种子（seed）= {随机或用户指定}

            【需要补充的信息】
            最多列 5 条真正影响生成质量的缺失项，例如模型族、尺寸、参考图、LoRA 名称、ControlNet 类型、采样器或是否 img2img。

            当前优化目标：{{targetTitle}}
            目标兼容：{{compatibility}}
            上下文来源：{{contextSource}}
            上下文内容：
            {{context}}

            用户原始需求：
            {{userRequest}}
            """,
            ModelInstruction = """
            生成一份 ComfyUI / Stable Diffusion 可复制字段。输出只能是最终正文，不要解释、不要 Markdown、不要完整 workflow JSON，除非用户提供节点 ID。必须包含：适配目标、ComfyUI 节点字段、Stable Diffusion WebUI 字段、diffusers 参数、需要补充的信息。主输出语言为中文时，AIPIN_PROMPT 必须使用中文字段名、中文占位符、中文说明和中文提示词正文；只在括号中保留必要的平台专有名词或 API 字段，例如 Positive CLIP Text Encode、Negative CLIP Text Encode、KSampler、Prompt、Negative prompt、Parameters、prompt、negative_prompt、width、height、num_inference_steps、guidance_scale、seed。英文提示词由 AIPIN_ENGLISH_PROMPT 单独输出，不要把英文版提前塞进中文提示词框。负向提示词必须包含未成年、18 岁以下、儿童、少年、幼态、文字乱码、水印、解剖错误、低质量等排除项。不得编造模型、LoRA、embedding、ControlNet、参考图、尺寸、seed 或采样器；信息不足写待补充。
            """,
            EnglishTranslationRule = "Keep the ComfyUI / Stable Diffusion adapter field names exactly as written: Positive CLIP Text Encode, Negative CLIP Text Encode, KSampler, Prompt, Negative prompt, Parameters, prompt, negative_prompt, width, height, num_inference_steps, guidance_scale, seed. Translate prompt content into executable English, but preserve exact Chinese text that the user wants rendered in the image.",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static OptimizationTargetItem[] BuildBuiltInTargets()
    {
        return
        [
            BuildAcademicHumanizeTarget(),
            BuildJimengSeedanceDirectorTarget(),
            BuildAiShortDramaWorkbenchTarget(),
            BuildStableDiffusionComfyTarget()
        ];
    }

    private static OptimizationTargetItem[] MergeBuiltInTargets(IReadOnlyList<OptimizationTargetItem> databaseItems, out bool changed)
    {
        changed = false;
        var items = databaseItems.ToList();
        foreach (var builtIn in BuildBuiltInTargets())
        {
            var existingIndex = items.FindIndex(item => string.Equals(item.Id, builtIn.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                if (HasBuiltInTargetChanged(items[existingIndex], builtIn))
                {
                    items[existingIndex] = builtIn with
                    {
                        CreatedAt = items[existingIndex].CreatedAt == default ? builtIn.CreatedAt : items[existingIndex].CreatedAt
                    };
                    changed = true;
                }

                continue;
            }

            items.Insert(0, builtIn);
            changed = true;
        }

        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Title)
                && !string.IsNullOrWhiteSpace(item.LocalPromptTemplate))
            .OrderByDescending(item => item.UpdatedAt)
            .Take(100)
            .ToArray();
    }

    private static bool HasBuiltInTargetChanged(OptimizationTargetItem current, OptimizationTargetItem builtIn)
    {
        return !string.Equals(current.Title, builtIn.Title, StringComparison.Ordinal)
            || !string.Equals(current.Description, builtIn.Description, StringComparison.Ordinal)
            || !string.Equals(current.Category, builtIn.Category, StringComparison.Ordinal)
            || !string.Equals(current.TemplateSource, builtIn.TemplateSource, StringComparison.Ordinal)
            || !string.Equals(current.Compatibility, builtIn.Compatibility, StringComparison.Ordinal)
            || !current.Keywords.SequenceEqual(builtIn.Keywords)
            || !string.Equals(current.LocalPromptTemplate, builtIn.LocalPromptTemplate, StringComparison.Ordinal)
            || !string.Equals(current.ModelInstruction, builtIn.ModelInstruction, StringComparison.Ordinal)
            || !string.Equals(current.EnglishTranslationRule, builtIn.EnglishTranslationRule, StringComparison.Ordinal);
    }

    private static IReadOnlyList<OptimizationTargetItem> LoadItemsFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var store = JsonSerializer.Deserialize<OptimizationTargetStore>(json, JsonOptions);
            if (store?.Items is { Count: > 0 })
            {
                return NormalizeItems(store.Items);
            }

            var item = JsonSerializer.Deserialize<OptimizationTargetItem>(json, JsonOptions);
            if (item is not null && !string.IsNullOrWhiteSpace(item.Title))
            {
                return NormalizeItems([item]);
            }

            var items = JsonSerializer.Deserialize<OptimizationTargetItem[]>(json, JsonOptions) ?? [];
            return NormalizeItems(items);
        }
        catch
        {
            return Array.Empty<OptimizationTargetItem>();
        }
    }

    private static OptimizationTargetItem[] NormalizeItems(IEnumerable<OptimizationTargetItem> items)
    {
        var now = DateTimeOffset.UtcNow;
        return items
            .Select(item => NormalizeItem(item, now))
            .Where(item => !string.IsNullOrWhiteSpace(item.Title)
                && !string.IsNullOrWhiteSpace(item.LocalPromptTemplate))
            .OrderByDescending(item => item.UpdatedAt)
            .ToArray();
    }

    private static OptimizationTargetItem NormalizeItem(OptimizationTargetItem item, DateTimeOffset now)
    {
        var id = string.IsNullOrWhiteSpace(item.Id)
            ? BuildId(item.Title)
            : item.Id.Trim();

        return item with
        {
            Id = id,
            Title = item.Title.Trim(),
            Description = item.Description?.Trim() ?? string.Empty,
            Category = string.IsNullOrWhiteSpace(item.Category) ? "用户目标" : item.Category.Trim(),
            TemplateSource = string.IsNullOrWhiteSpace(item.TemplateSource) ? "ChatGPT-Shortcut" : item.TemplateSource.Trim(),
            Compatibility = string.IsNullOrWhiteSpace(item.Compatibility) ? "ChatGPT / Claude / Gemini / 本地模型" : item.Compatibility.Trim(),
            Keywords = item.Keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).Select(keyword => keyword.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            LocalPromptTemplate = item.LocalPromptTemplate.Trim(),
            ModelInstruction = item.ModelInstruction?.Trim() ?? string.Empty,
            EnglishTranslationRule = item.EnglishTranslationRule?.Trim() ?? string.Empty,
            CreatedAt = item.CreatedAt == default ? now : item.CreatedAt,
            UpdatedAt = now
        };
    }

    private static string BuildId(string title)
    {
        var safe = new string((title ?? "target")
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());
        safe = safe.Trim('-');
        return string.IsNullOrWhiteSpace(safe) ? Guid.NewGuid().ToString("N") : safe;
    }

    private void SaveRecords(IReadOnlyList<OptimizationTargetItem> items)
    {
        _database.ReplaceRecords(
            AppDatabaseService.KindOptimizationTarget,
            items.Select(item => new AppRecordItem(
                AppDatabaseService.KindOptimizationTarget,
                item.Id,
                item.Title,
                item.Category,
                item.TemplateSource,
                string.Join(Environment.NewLine, item.Description, item.Compatibility, string.Join(" ", item.Keywords), item.LocalPromptTemplate),
                JsonSerializer.Serialize(item, JsonOptions),
                item.CreatedAt == default ? DateTimeOffset.UtcNow : item.CreatedAt,
                item.UpdatedAt == default ? DateTimeOffset.UtcNow : item.UpdatedAt)),
            updateSearchIndex: true);
    }

    private void SetCachedItems(IReadOnlyList<OptimizationTargetItem> items)
    {
        lock (_cacheGate)
        {
            _cachedItems = items
                .Where(item => !string.IsNullOrWhiteSpace(item.Title)
                    && !string.IsNullOrWhiteSpace(item.LocalPromptTemplate))
                .OrderByDescending(item => item.UpdatedAt)
                .ToArray();
        }
    }

    private void InvalidateCache()
    {
        lock (_cacheGate)
        {
            _cachedItems = null;
        }
    }

}

public sealed record OptimizationTargetItem
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = "用户目标";
    public string TemplateSource { get; init; } = "ChatGPT-Shortcut";
    public string Compatibility { get; init; } = "ChatGPT / Claude / Gemini / 本地模型";
    public string[] Keywords { get; init; } = [];
    public string LocalPromptTemplate { get; init; } = string.Empty;
    public string ModelInstruction { get; init; } = string.Empty;
    public string EnglishTranslationRule { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }

    public override string ToString()
    {
        return Title;
    }
}

internal sealed record OptimizationTargetStore(string Schema, IReadOnlyList<OptimizationTargetItem> Items);
