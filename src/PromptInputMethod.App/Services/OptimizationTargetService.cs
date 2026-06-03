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
            4. 人物或角色必须明确为成年 / 18+；用户未指定年龄时写“adult subject, 18+, exact age unspecified”。负向提示词必须包含未成年相关排除项。
            5. 中文需求可以理解，但 Stable Diffusion 正向提示词默认输出英文短语；专有名词、中文 Logo 文案或必须出现的中文文字按用户原文保留。
            6. LoRA、embedding、ControlNet、IP-Adapter、inpainting、img2img 只有用户明确要求或上下文明确存在时才写；没有素材时写“not provided / 待补充”。

            请按下面结构输出：

            【适配目标】
            target: ComfyUI / Stable Diffusion
            model family: {SD 1.5 / SDXL / SD3 / Flux / unknown}
            task: {text-to-image / image-to-image / inpainting / ControlNet / IP-Adapter / LoRA portrait / product / illustration / unknown}

            【ComfyUI 节点字段】
            Positive CLIP Text Encode:
            {英文正向提示词，按主体、场景、构图、镜头、光线、风格、质量顺序组织}

            Negative CLIP Text Encode:
            {英文反向提示词，包含低质量、结构错误、文字水印、主体漂移、未成年相关排除项；不要过度堆无关词}

            KSampler:
            checkpoint/model: {待补充或用户指定}
            width x height: {待补充或用户指定}
            steps: {待补充；常见范围 20-40}
            cfg: {待补充；SD 1.5/SDXL 常见 5-8，按模型调整}
            sampler: {待补充}
            scheduler: {待补充}
            seed: {random 或用户指定}
            denoise: {txt2img 为 1.0；img2img/inpaint 按用户要求待补充}

            Optional nodes:
            LoRA: {无 / <lora:name:weight> / 待补充}
            embedding/textual inversion: {无 / embedding:name / 待补充}
            ControlNet: {无 / 类型、预处理器、强度、起止步 / 待补充}
            IP-Adapter/reference image: {无 / 参考图作用、权重、起止步 / 待补充}
            VAE / upscale / hires fix: {无 / 待补充}

            【Stable Diffusion WebUI 字段】
            Prompt:
            {可直接粘贴到 WebUI Prompt 的正向提示词；LoRA 标签只在这里写}

            Negative prompt:
            {可直接粘贴到 WebUI Negative prompt 的反向提示词}

            Parameters:
            Steps: {待补充}
            Sampler: {待补充}
            CFG scale: {待补充}
            Size: {待补充}
            Seed: {random 或用户指定}
            Denoising strength: {仅 img2img/inpaint 使用；否则写 not applicable}

            【diffusers 参数】
            prompt = "{正向提示词}"
            negative_prompt = "{反向提示词}"
            width = {待补充}
            height = {待补充}
            num_inference_steps = {待补充}
            guidance_scale = {待补充}
            seed = {random 或用户指定}

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
            生成一份 ComfyUI / Stable Diffusion 可复制字段。输出只能是最终正文，不要解释、不要 Markdown、不要完整 workflow JSON，除非用户提供节点 ID。必须包含：适配目标、ComfyUI 节点字段、Stable Diffusion WebUI 字段、diffusers 参数、需要补充的信息。ComfyUI 部分必须输出 Positive CLIP Text Encode、Negative CLIP Text Encode、KSampler 参数和可选节点。WebUI 部分必须输出 Prompt、Negative prompt 和 Parameters。diffusers 部分必须输出 prompt、negative_prompt、width、height、num_inference_steps、guidance_scale、seed。正向提示词默认英文，负向提示词必须包含未成年、18 岁以下、儿童、少年、幼态、文字乱码、水印、解剖错误、低质量等排除项。不得编造模型、LoRA、embedding、ControlNet、参考图、尺寸、seed 或采样器；信息不足写待补充。
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
            BuildStableDiffusionComfyTarget()
        ];
    }

    private static OptimizationTargetItem[] MergeBuiltInTargets(IReadOnlyList<OptimizationTargetItem> databaseItems, out bool changed)
    {
        changed = false;
        var items = databaseItems.ToList();
        foreach (var builtIn in BuildBuiltInTargets())
        {
            if (items.Any(item => string.Equals(item.Id, builtIn.Id, StringComparison.OrdinalIgnoreCase)))
            {
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
