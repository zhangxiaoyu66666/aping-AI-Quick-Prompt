namespace PromptInputMethod.Core.Prompt;

public sealed class ScenePromptRouter
{
    private const double MinimumSceneConfidence = 0.45;

    public ScenePromptRoute Route(SceneDetectionResult detection)
    {
        var scene = detection.Confidence < MinimumSceneConfidence ? PromptScene.Unknown : detection.Scene;
        return new ScenePromptRoute(scene, GetDisplayName(scene), GetExpectedOutput(scene));
    }

    public static string GetDisplayName(PromptScene scene) => scene switch
    {
        PromptScene.Code => "代码开发",
        PromptScene.ChatModel => "AI 对话",
        PromptScene.Document => "文档写作",
        PromptScene.Mail => "沟通写作",
        PromptScene.BrowserReading => "网页阅读",
        PromptScene.SoftwareOperation => "软件操作",
        _ => "通用输入"
    };

    private static string GetExpectedOutput(PromptScene scene) => scene switch
    {
        PromptScene.Code => "生成包含目标、涉及文件/报错、约束、期望修改和验证方式的开发提示词。",
        PromptScene.ChatModel => "生成可直接发给当前模型的清晰指令，减少寒暄和重复背景。",
        PromptScene.Document => "生成写作、润色、总结或改写提示词，明确语气、结构和交付格式。",
        PromptScene.Mail => "生成包含收件人、目的、语气、关键点和禁忌表达的沟通提示词。",
        PromptScene.BrowserReading => "生成用于总结、提问、对比或提炼行动项的提示词。",
        PromptScene.SoftwareOperation => "生成让模型根据界面或报错指导下一步操作的提示词。",
        _ => "生成通用结构化提示词，避免过度推断。"
    };
}

public sealed record ScenePromptRoute(PromptScene Scene, string DisplayName, string ExpectedOutput);
