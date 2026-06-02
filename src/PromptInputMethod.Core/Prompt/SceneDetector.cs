namespace PromptInputMethod.Core.Prompt;

public sealed class SceneDetector
{
    private static readonly HashSet<string> CodeProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Code", "devenv", "rider64", "idea64", "webstorm64", "pycharm64", "clion64", "WindowsTerminal", "wt", "cmd", "powershell", "pwsh"
    };

    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "vivaldi", "opera"
    };

    private static readonly HashSet<string> DocumentProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "WINWORD", "notepad", "Obsidian", "Typora", "Notepad++", "EXCEL", "POWERPNT"
    };

    private static readonly HashSet<string> MailProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "OUTLOOK", "olk", "Teams", "WeChat", "QQ", "Feishu", "Lark"
    };

    public SceneDetectionResult Detect(WindowContext context)
    {
        var process = context.ProcessName ?? string.Empty;
        var title = context.Title ?? string.Empty;
        var haystack = $"{process} {title} {context.ClassName}";

        if (CodeProcesses.Contains(process) || ContainsAny(haystack, "github", "gitlab", "stackoverflow", "exception", "error", "traceback"))
        {
            return new SceneDetectionResult(PromptScene.Code, "代码", 0.85, "进程或标题匹配开发/报错场景");
        }

        if (ContainsAny(haystack, "chatgpt", "claude", "gemini", "kimi", "豆包", "通义", "open webui", "lm studio"))
        {
            return new SceneDetectionResult(PromptScene.ChatModel, "聊天模型", 0.82, "标题匹配聊天模型");
        }

        if (MailProcesses.Contains(process) || ContainsAny(haystack, "mail", "邮箱", "outlook", "飞书", "teams"))
        {
            return new SceneDetectionResult(PromptScene.Mail, "邮件/沟通", 0.78, "进程或标题匹配沟通工具");
        }

        if (DocumentProcesses.Contains(process) || ContainsAny(haystack, "markdown", "document", "文档", "pdf"))
        {
            return new SceneDetectionResult(PromptScene.Document, "文档写作", 0.75, "进程或标题匹配文档工具");
        }

        if (BrowserProcesses.Contains(process))
        {
            return new SceneDetectionResult(PromptScene.BrowserReading, "浏览器阅读", 0.65, "浏览器进程匹配");
        }

        if (ContainsAny(haystack, "setup", "installer", "settings", "设置", "安装", "wizard"))
        {
            return new SceneDetectionResult(PromptScene.SoftwareOperation, "软件操作", 0.65, "标题匹配设置/安装类窗口");
        }

        return new SceneDetectionResult(PromptScene.Unknown, "未知", 0.2, "没有足够信号");
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
}
