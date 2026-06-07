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
                "builtin-sd-comfyui-node-fields",
                "ComfyUI｜节点字段适配",
                "ComfyUI / Stable Diffusion",
                "ComfyUI",
                """
                把需求改写成 ComfyUI 可填字段。

                正向 CLIP 文本编码（Positive CLIP Text Encode）：
                {中文正向提示词：主体、场景、构图、镜头、光线、风格、质量；需要渲染的英文文字按用户原文保留；不要写解释}

                反向 CLIP 文本编码（Negative CLIP Text Encode）：
                {中文反向提示词：低质量、模糊、解剖错误、额外肢体、文字乱码、水印、Logo 伪影、主体漂移、未成年、儿童、少年、幼态、年龄不明确}

                KSampler 参数：
                检查点 / 模型：{待补充}
                画布尺寸：{宽 x 高，待补充}
                采样步数：{待补充，常见 20-40}
                CFG 引导强度：{待补充，按模型调整}
                采样器：{待补充}
                调度器：{待补充}
                随机种子：{随机 / 指定 seed}
                降噪强度：{文生图=1.0；图生图/局部重绘待补充}

                可选节点：
                LoRA: {无 / <lora:name:weight> / 待补充}
                文本反转 / embedding: {无 / embedding:name / 待补充}
                ControlNet: {无 / 类型、预处理器、强度、起止步 / 待补充}
                IP-Adapter / 参考图：{无 / 参考图作用、权重、起止步 / 待补充}
                VAE / 放大：{无 / 待补充}
                """),
            new(
                "builtin-sd-webui-positive-negative",
                "Stable Diffusion｜正负提示词",
                "ComfyUI / Stable Diffusion",
                "SD WebUI",
                """
                生成可粘贴到 Stable Diffusion WebUI / A1111 的字段。

                正向提示词（Prompt）：
                {中文正向提示词，按主体、环境、动作、构图、镜头、光线、风格、质量组织；LoRA 标签只放在正向提示词里}

                反向提示词（Negative prompt）：
                低质量、最差质量、模糊、解剖错误、手部错误、多余手指、多余肢体、变形、面部扭曲、文字乱码、水印、签名、Logo 伪影、未成年、儿童、少年、幼态、年龄不明确、未要求时避免 NSFW

                生成参数（Parameters）：
                采样步数（Steps）：{待补充}
                采样器（Sampler）：{待补充}
                CFG 引导强度（CFG scale）：{待补充}
                尺寸（Size）：{待补充}
                随机种子（Seed）：{随机 / 指定 seed}
                Clip skip：{仅需要时填写}
                高清修复（Hires fix）：{关闭 / 开启，放大倍率与重绘幅度待补充}
                """),
            new(
                "builtin-sd-img2img-controlnet",
                "Stable Diffusion｜图生图 / ControlNet",
                "ComfyUI / Stable Diffusion",
                "图生图",
                """
                生成适合 img2img、inpainting、ControlNet 或 IP-Adapter 的提示词字段。

                任务类型：
                {图生图 / 局部重绘 / ControlNet / IP-Adapter / 仅参考图}

                参考图：
                {参考图作用：保留主体 / 保留姿势 / 保留构图 / 保留风格 / 保留产品外形}

                正向提示词（Positive prompt）：
                {中文正向提示词：描述目标变化，但明确哪些特征必须保持}

                反向提示词（Negative prompt）：
                低质量、模糊、变形、身份漂移、脸部不一致、产品形状错误、文字乱码、水印、未成年、儿童、少年、幼态、年龄不明确

                生成参数（Parameters）：
                重绘幅度（Denoising strength）：{0.25-0.45 小改；0.45-0.65 中等改；0.65+ 大改，按用户需求待补充}
                ControlNet 类型：{canny / depth / openpose / lineart / tile / scribble / 待补充}
                ControlNet 权重：{待补充}
                起止步：{待补充}
                缩放模式：{待补充}
                随机种子（Seed）：{随机 / 指定 seed}
                """),
            new(
                "builtin-sd-lora-embedding",
                "Stable Diffusion｜LoRA / Embedding",
                "ComfyUI / Stable Diffusion",
                "LoRA",
                """
                生成带 LoRA、embedding 或风格触发词的 Stable Diffusion 提示词。

                基础模型族：
                {SD 1.5 / SDXL / SD3 / Flux / 待补充}

                正向提示词（Prompt）：
                {主体、场景、构图、镜头、光线、风格、质量}
                LoRA 标签：{<lora:name:0.6-0.9>，仅在用户提供 LoRA 名称时写}
                触发词：{用户提供的触发词；没有则待补充}
                文本反转 / Embeddings：{embedding:name；没有则无}

                反向提示词（Negative prompt）：
                低质量、最差质量、模糊、解剖错误、额外肢体、文字乱码、水印、身份漂移、风格冲突、未成年、儿童、少年、幼态、年龄不明确

                注意事项：
                不要编造 LoRA 名称、模型族、触发词或 embedding 名称；缺失时列为待补充。
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
                "builtin-jimeng-director-storyboard",
                "即梦｜导演分镜工作台",
                "即梦 / Seedance",
                "导演分镜",
                """
                把创意改写成即梦 / Seedance 可执行导演分镜提示词。

                任务类型：{文生视频 / 图生视频 / 首尾帧 / 视频编辑}
                生成目标：{一句话说明最终视频效果}
                参考素材：{无 / @图片1 / @图片2 / @视频1 / @音频1，并说明每个素材作用}
                主体与场景：{人物、产品、环境、道具、关键识别特征}
                镜头语言：{景别、视角、焦段感、构图、对焦、景深}
                运镜：{推近 / 拉远 / 横移 / 环绕 / 跟拍 / 手持感 / 稳定器 / 一镜到底}
                时间轴：
                0-2s：{开场画面、主体出现、镜头动作、声音}
                2-5s：{主要动作、情绪或卖点、镜头推进}
                5-8s：{变化、转场、视觉高潮或信息揭示}
                8-10s：{收束画面、停留、字幕或品牌记忆点}
                视觉风格：{光线、色彩、质感、画面密度、氛围}
                声音与字幕：{BGM、环境声、音效、对白、字幕位置和语言}
                平台参数：{时长、画幅比例、清晰度、是否保留参考主体一致性}
                负面约束：避免闪烁、身份漂移、产品变形、额外肢体、文字乱码、水印、突然跳切、风格跑偏。
                """),
            new(
                "builtin-jimeng-reference-control",
                "即梦｜多模态引用控制",
                "即梦 / Seedance",
                "引用素材",
                """
                基于参考素材生成即梦 / Seedance 提示词，并明确每个素材的控制作用。

                目标：{最终生成视频或图片的目标}
                @图片1：{保留主体 / 构图 / 风格 / 色彩 / 产品外形 / 角色身份}
                @图片2：{可选，说明承担的作用}
                @视频1：{可选，参考动作 / 运镜 / 节奏 / 转场}
                @音频1：{可选，参考音乐节奏 / 语气 / 环境声}
                主体一致性：保持 {脸部、发型、服装、产品外形、颜色、材质、品牌元素} 不漂移。
                允许变化：{背景 / 镜头角度 / 光线 / 动作 / 表情 / 转场}
                禁止变化：{身份 / 产品结构 / 标志 / 核心颜色 / 关键道具}
                镜头设计：{景别、视角、运动、对焦、节奏}
                画面风格：{写实 / 电影感 / 商业 / 二次元 / 国风 / 科技感 / 其他}
                输出参数：{时长、比例、清晰度、字幕、声音}
                负面约束：避免参考主体变形、无关元素混入、五官漂移、产品结构错误、文字乱码、低清晰度。
                """),
            new(
                "builtin-jimeng-short-drama",
                "即梦｜短剧对白分镜",
                "即梦 / Seedance",
                "短剧对白",
                """
                生成一段适合即梦 / Seedance 的短剧或漫剧对白视频提示词。

                剧情前提：{一句话说明冲突或事件}
                角色 A：{身份、外观、情绪、服装、动作习惯}
                角色 B：{身份、外观、情绪、服装、动作习惯}
                场景：{地点、时间、环境、道具、氛围}
                表演节奏：{停顿、眼神、转身、靠近、沉默、爆发或克制}
                分镜：
                0-2s：{建立场景和角色关系}
                2-5s：{角色 A 动作或台词}
                5-8s：{角色 B 反应或反转}
                8-10s：{情绪落点、结尾画面、字幕}
                对白：
                A：“{台词，待补充}”
                B：“{台词，待补充}”
                镜头：{近景 / 中景 / 过肩 / 双人构图 / 推近 / 跟拍}
                声音：{环境声、BGM、音效、对白语言}
                字幕：{中文字幕 / 中英双语 / 无字幕，位置和样式}
                负面约束：避免口型错位、角色身份变化、表情僵硬、额外人物、台词乱码、突兀跳切。
                """),
            new(
                "builtin-short-drama-guided-project",
                "短剧工作台｜AI手把手带做",
                "短剧工作台",
                "AI带做",
                """
                像导演助理一样，判断我现在做到哪一步，并带我继续做漫剧/短剧。

                当前材料：
                {粘贴一句想法、已有剧情、角色设定、三视图提示词、上一段镜头或生成结果；没有就写“我还没开始”}

                请输出：

                当前阶段：
                {新手开做 / 剧情角色 / 角色三视图 / 当前镜头 / 继续下一段 / 修改返工}

                我已经有的东西：
                {列出已经可用的剧情、角色、外观锚点、镜头、台词或连续性信息}

                现在缺的东西：
                {最多 5 条真正影响下一步的缺失信息}

                你建议我下一步点：
                {新手开做 / 剧情角色 / 三视图 / 运镜 / 下一段}

                下一条可以直接发送的需求：
                {替我写一句不超过 120 字的下一步需求，让我可以直接发送给模型继续}

                说明：
                你要像手把手教新手做漫剧/短剧，不要假设我懂分镜、三视图、镜头语言或视频模型参数。
                """),
            new(
                "builtin-short-drama-starter",
                "短剧工作台｜新手从0开做",
                "短剧工作台",
                "新手开做",
                """
                我想从 0 开始做一条漫剧/短剧，请 AI 手把手带我完成第一轮。

                我现在只有一个想法：
                {例如：一个普通人突然获得改写命运的能力 / 一个 AI 宠物陪残障开发者完成作品 / 待补充}

                请先帮我确定：
                作品形式：{真人短剧 / 漫剧 / 二次元 / Galgame 分支演出 / 产品剧情 / 待补充}
                目标观众：{谁会想看}
                第一眼钩子：{第 1-2 秒必须出现的画面或台词}
                主角：{身份、欲望、弱点、外观方向}
                阻力：{反派、误会、制度、时间压力或内心冲突}
                第一段剧情：{5-10 秒能拍出来的一段}
                需要先画的角色三视图：{主角 / 对手 / 关键配角}
                下一步：{建议我点“剧情角色”“三视图”还是“运镜”}

                要求：
                像教新手一样一步一步来。不要一次生成完整长剧本；先把第一段做扎实，让我能继续点下一步。
                """),
            new(
                "builtin-short-drama-script",
                "短剧工作台｜想法生成剧本",
                "短剧工作台",
                "剧本",
                """
                把一个想法扩成可以继续分镜的漫剧 / 短剧剧本。

                原始想法：
                {粘贴一句话题材、世界观、人物关系或已有剧情}

                请输出：

                一句话梗概：
                {主角、目标、阻力、看点}

                人物表：
                主角：{身份、欲望、弱点、外观方向、情绪弧线}
                对手 / 阻力：{身份或阻力类型、目标、压迫方式}
                关键配角：{功能、与主角关系、推动剧情方式}

                剧本结构：
                开场钩子：{第 1-2 秒的画面或台词}
                第一段：{5-10 秒内能拍出来的剧情动作}
                第二段：{承接第一段的反应或信息揭示}
                第三段：{反转、悬念或下一段钩子}

                正文剧本：
                场景：{地点、时间、氛围}
                动作：{角色怎么动，关系怎么变化}
                对白：{短句，适合字幕和口型}
                情绪节拍：{停顿、迟疑、爆发、沉默或反应}

                下一步分镜需要：
                {列出场景、角色、道具、关键台词、结尾画面}

                要求：
                先服务“分镜和视频生成”，不要写成小说；对白短、画面强、每段都有明确情绪推进。
                """),
            new(
                "builtin-short-drama-storyboard",
                "短剧工作台｜剧本拆分镜",
                "短剧工作台",
                "分镜",
                """
                把剧本拆成可生成的分镜表。

                剧本：
                {粘贴剧本、剧情梗概或上一轮输出}

                请按镜头输出：

                镜号：{01 / 02 / 03}
                时长：{建议 5-10 秒；或每镜 2-4 秒}
                剧情目的：{这个镜头推进什么信息或情绪}
                画面内容：{可视化画面，不写抽象心理}
                角色动作：{眼神、走位、手部动作、表情变化}
                台词 / 字幕：{短句；无对白写无对白}
                景别与机位：{特写 / 近景 / 中景 / 全景 / 俯拍 / 仰拍 / 过肩}
                运镜：{推近 / 拉远 / 横移 / 环绕 / 跟拍 / 拉焦 / 固定镜头}
                声音：{环境声、BGM、音效、对白语气}
                需要资产：{角色三视图 / 场景参考 / 道具参考 / 无}
                下一步：{建议先做三视图 / 直接生成当前镜头 / 继续补剧本}

                分镜原则：
                一个镜头只完成一个主要情绪或信息点；每个镜头都要能转成视频模型提示词。
                """),
            new(
                "builtin-short-drama-qa-revision",
                "短剧工作台｜质检返工",
                "短剧工作台",
                "质检返工",
                """
                检查当前漫剧 / 短剧制作内容能不能继续生产，并给出修正版。

                待质检内容：
                {粘贴剧本、分镜、三视图、镜头提示词、上一段结果或交付包}

                请检查：

                剧本问题：
                {是否太像小说、是否缺可视化动作、对白是否过长、第一段钩子是否弱}

                分镜问题：
                {镜头是否可拍、时长是否合理、每镜是否只推进一个信息点、是否缺景别和机位}

                角色资产问题：
                {是否缺三视图、外观锚点是否不清、服装道具是否会漂移}

                运镜问题：
                {是否只有空泛“电影感”、是否缺推拉摇移、拉焦、构图、声音和字幕}

                连续性问题：
                {上一段结尾、下一段起点、人物站位、光线、场景、情绪是否连续}

                风险等级：
                {低 / 中 / 高，并说明原因}

                修正版：
                {直接给出可继续制作的修正版剧本、分镜或镜头提示词}

                下一步建议：
                {建议点击：剧本 / 分镜 / 三视图 / 运镜 / 下一段 / 交付包}
                """),
            new(
                "builtin-short-drama-package",
                "短剧工作台｜整理制作交付包",
                "短剧工作台",
                "交付包",
                """
                把已有内容整理成短剧制作交付包，方便保存、复制和继续生成。

                已有内容：
                {粘贴剧情、剧本、分镜、三视图、镜头提示词或上一段结果}

                请整理为：

                项目一句话：
                {作品核心}

                角色资产：
                {角色名、身份、外观锚点、服装道具、禁止变化项}

                剧本摘要：
                {当前已完成剧情和下一步冲突}

                分镜表：
                {镜号、时长、画面、动作、台词、声音、资产需求}

                三视图提示词：
                {可复制给图像模型的正面 / 侧面 / 背面 / 表情参考}

                当前视频提示词：
                {可复制给视频模型的 5-10 秒镜头提示词}

                下一段承接：
                {上一段结尾画面、人物站位、情绪、场景、光线、下一段起点}

                待补充清单：
                {最多 5 条}
                """),
            new(
                "builtin-short-drama-one-line-story",
                "短剧工作台｜一句话生成剧情角色",
                "短剧工作台",
                "剧情角色",
                """
                从用户一句话生成短剧剧情种子、角色雏形和第一段钩子，为后续三视图与镜头生成做准备。

                用户一句话：
                {粘贴一句短剧创意，例如“被退婚的女主回到宴会复仇”}

                短剧 Logline：
                {一句话说明主角、欲望、阻碍和爽点 / 泪点 / 悬念}

                类型定位：
                {复仇 / 甜宠 / 悬疑 / 逆袭 / 都市 / 古风 / 科幻 / Galgame 分支演出 / 待补充}

                核心冲突：
                主角想要：{待补充}
                阻碍来自：{反派 / 误会 / 家庭 / 制度 / 时间压力 / 内心弱点 / 待补充}
                冲突爆发原因：{为什么必须现在发生}

                角色雏形：
                主角：{姓名、身份、外貌方向、性格、欲望、弱点、秘密、情绪弧线}
                对手 / 阻力：{姓名或阻力类型、身份、动机、压迫方式}
                关键配角：{助攻、见证者、信息提供者、情绪镜子；没有则待补充}

                第一段钩子：
                {第一个 5-10 秒必须出现的画面、动作或台词，让观众想看下一段}

                角色资产准备：
                需要三视图的角色：{主角 / 对手 / 关键配角}
                必须锁定的外观锚点：{脸型、发型、服装主色、道具、体态、年龄感、画风}

                可连续方向：
                1. {下一段方向 A}
                2. {下一段方向 B}
                3. {下一段方向 C}

                负面约束：
                不要把一句话扩写成完整小说；不要编造真实人物或未授权品牌；角色必须成年 18+，禁止未成年化、幼态化和年龄模糊；不要让角色功能重叠；不要让第一段没有可视化钩子。
                """),
            new(
                "builtin-short-drama-character-turnaround",
                "短剧工作台｜角色三视图资产卡",
                "短剧工作台",
                "角色资产",
                """
                指挥画图 AI 生成短剧角色三视图，并沉淀角色资产卡。

                角色名称：{待补充}
                身份与性格：{待补充}
                成年设定：成年角色，18+，具体年龄待补充；禁止未成年化、幼态化、学生化年龄暗示。
                画风：{真人短剧 / 漫剧 / 二次元 / 国风 / 赛博 / 待补充}

                正面视图提示词：
                {同一角色正面全身设定图，纯色或设定稿背景，清楚展示脸型、五官、发型、服装正面结构、体态比例、主色、标志性道具和材质}

                侧面视图提示词：
                {同一角色侧面全身设定图，保持脸型轮廓、发型体积、鼻梁下颌、服装层次、道具位置和身高比例一致}

                背面视图提示词：
                {同一角色背面全身设定图，展示发型背面、背部服装结构、腰线、鞋履、道具佩戴方式、外套和装饰细节}

                表情 / 手势参考：
                1. {常态表情}
                2. {压抑 / 犹豫 / 愤怒 / 震惊 / 温柔等剧情表情}
                3. {标志性手势或动作}

                角色一致性锁定：
                发色、瞳色、脸型、年龄感、体态比例、服装主色、标志性道具、画风、材质质感不得变化。

                负面约束：
                避免脸部漂移、年龄变小、服装乱换、发型错误、左右不一致、道具消失、额外肢体、手部错误、文字乱码、水印、低清晰度。
                """),
            new(
                "builtin-short-drama-master-shot",
                "短剧工作台｜5-10秒大师运镜",
                "短剧工作台",
                "运镜提示词",
                """
                生成一段 5-10 秒短剧视频提示词，重点是导演级运镜和表演调度。

                项目状态：{短剧名 / 题材 / 画风 / 平台 / 画幅}
                角色资产：{角色名、外貌锚点、服装锚点、道具锚点、禁止变化项}
                剧情目的：{本段要推进的冲突、反转、情绪或信息点}
                上一段结尾：{如果有，写最后一帧人物站位、表情、动作、镜头状态和光线}

                当前镜头提示词：
                时长：5-10 秒
                开场画面：{第一帧构图、主体站位、光线方向、场景层次}
                主体与场景：{角色、道具、背景、空间关系}
                大师级运镜：{景别、视角、焦段感、推拉摇移、环绕、跟拍、拉焦、浅景深、手持或稳定器、镜头节奏}
                表演调度：{眼神、微表情、停顿、转身、呼吸、手部动作、距离变化}
                时间轴：
                0-2s：{开场起势}
                2-5s：{核心动作 / 台词 / 情绪推进}
                5-8s：{反应 / 反转 / 信息揭示}
                8-10s：{钩子、停留、下一段承接点}
                台词 / 字幕：{对白、字幕语言和位置；无台词写无对白}
                声音：{环境声、BGM、音效、对白语气}
                视觉风格：{光线、色彩、质感、平台适配}
                负面约束：避免角色身份漂移、脸部漂移、服装变化、口型错位、额外人物、额外肢体、文字乱码、闪烁、突然跳切、低清晰度、风格跑偏。
                """),
            new(
                "builtin-short-drama-next-segment",
                "短剧工作台｜继续下一段",
                "短剧工作台",
                "连续剧情",
                """
                基于上一段结果，继续生成下一段 5-10 秒短剧镜头。不要重启故事。

                上一段结果：
                {粘贴上一段镜头提示词或生成结果}

                上一段结尾画面：
                {最后一帧人物站位、表情、动作、镜头景别、光线方向、场景状态}

                连续性锁定：
                角色外貌、发型、服装、道具、年龄感、场景、光线方向、画风、画幅、镜头质感必须保持。

                下一段剧情目的：
                {反转 / 追问 / 情绪爆发 / 行动升级 / 悬念收束 / 新信息揭示}

                下一段镜头提示词：
                时长：5-10 秒
                开场画面：承接上一段最后一帧
                大师级运镜：{从上一段镜头状态自然接续，说明推、拉、摇、移、环绕、跟拍、拉焦、景深和节奏变化}
                表演调度：{角色眼神、微表情、动作、距离、停顿、情绪递进}
                时间轴：
                0-2s：{承接上一段结尾}
                2-5s：{本段主要动作或台词}
                5-8s：{反应、反转或信息揭示}
                8-10s：{新的结尾画面和下一段钩子}
                台词 / 字幕：{可选}
                声音：{环境声、BGM、音效}
                负面约束：避免重启故事、角色换脸、服装变化、场景跳变、光线方向混乱、镜头乱动、突然跳切、口型错位、文字乱码。
                """),
            new(
                "builtin-jimeng-viral-promo",
                "即梦｜短视频宣发模板",
                "即梦 / Seedance",
                "宣发",
                """
                生成一条产品、项目或内容宣发用的即梦 / Seedance 短视频提示词。

                宣发对象：{产品 / 软件 / 游戏 / 内容 / 活动}
                核心卖点：{1-3 个真实卖点，不夸大}
                受众：{目标人群、使用场景、情绪需求}
                开场钩子：{第一秒吸引注意的画面或动作}
                展示方式：{产品特写、界面操作、场景使用、前后对比、人物反应}
                镜头节奏：{快速剪辑 / 稳定推进 / 一镜到底 / 节拍卡点}
                画面风格：{干净商业 / 科技感 / 生活方式 / 热梗轻喜剧 / 电影感}
                字幕文案：{短句、少字、清晰，不要大段文字}
                声音：{BGM、按钮声、提示音、环境声或旁白}
                输出参数：{9:16 / 16:9 / 1:1，时长，清晰度}
                合规约束：不要虚假承诺、不要夸大疗效或收益、不要冒充官方背书。
                负面约束：避免廉价感、背景杂乱、产品变形、文字乱码、主体漂移、无关人物抢戏。
                """),
            new(
                "builtin-jimeng-seedream-image",
                "即梦｜Seedream 图片提示词",
                "即梦 / Seedance",
                "文生图",
                """
                生成适合即梦 Seedream 的图片提示词。

                图片目标：{海报 / 商品图 / 角色图 / 场景概念 / 多图融合 / 组图分镜}
                主体：{人物 / 产品 / 角色 / 建筑 / 物体}
                关键识别特征：{脸部、发型、服装、产品外形、材质、颜色、标志、道具}
                参考图：{无 / @图片1 / @图片2，并说明保留主体、风格、构图或色彩}
                构图：{近景 / 半身 / 全身 / 俯拍 / 仰拍 / 居中 / 三分法 / 留白}
                场景：{地点、时间、背景元素、空间层次}
                光线：{自然光、棚拍光、逆光、柔光、霓虹、低调光、高调光}
                风格：{写实摄影 / 商业海报 / 国风 / 二次元 / 3D / 平面设计 / UI 概念}
                文字与版式：{是否需要文字、标题、Logo、排版位置；不需要时写“无文字”}
                输出参数：{比例、清晰度、组图数量}
                负面约束：避免脸部漂移、手部错误、产品结构错误、文字乱码、水印、低清晰度、无关元素。
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
