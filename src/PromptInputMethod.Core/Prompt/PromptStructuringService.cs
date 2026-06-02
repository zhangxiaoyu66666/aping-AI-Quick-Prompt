using System.Text;

namespace PromptInputMethod.Core.Prompt;

public sealed class PromptStructuringService
{
    private readonly ScenePromptRouter _router = new();

    public StructuredPrompt Structure(PromptStructuringRequest request)
    {
        var userRequest = string.IsNullOrWhiteSpace(request.UserRequest) ? "请根据当前场景帮助我整理需求。" : request.UserRequest.Trim();
        var contextText = string.IsNullOrWhiteSpace(request.ContextText) ? "暂无额外上下文。" : request.ContextText.Trim();
        var contextFieldName = FormatContextFieldName(request.ContextFieldName);
        var route = _router.Route(request.Scene);
        var contextSource = FormatContextSource(request.WindowContext, request.ContextText, request.ContextSource);
        var finalPrompt = BuildFinalPrompt(route, userRequest, contextText, contextSource, contextFieldName);

        return new StructuredPrompt(finalPrompt.TrimEnd());
    }

    private static string FormatContextFieldName(string? contextFieldName)
    {
        return string.IsNullOrWhiteSpace(contextFieldName) ? "背景上下文" : contextFieldName.Trim();
    }

    private static string FormatContextSource(WindowContext context, string? contextText, string? contextSource)
    {
        var sources = new List<string>();
        var appName = FormatApplicationName(context.ProcessName);
        if (!string.IsNullOrWhiteSpace(appName))
        {
            sources.Add(appName);
        }

        if (!string.IsNullOrWhiteSpace(contextText))
        {
            sources.Add(string.IsNullOrWhiteSpace(contextSource) ? "用户提供的上下文" : contextSource.Trim());
        }

        sources.Add("手动输入");
        return string.Join(" / ", sources.Distinct());
    }

    private static string? FormatApplicationName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        return processName switch
        {
            "devenv" => "Visual Studio",
            "Code" => "VS Code",
            "rider64" => "Rider",
            "idea64" => "IntelliJ IDEA",
            "webstorm64" => "WebStorm",
            "pycharm64" => "PyCharm",
            "clion64" => "CLion",
            "chrome" => "Chrome",
            "msedge" => "Microsoft Edge",
            "firefox" => "Firefox",
            "brave" => "Brave",
            "WINWORD" => "Word",
            "EXCEL" => "Excel",
            "POWERPNT" => "PowerPoint",
            "notepad" => "记事本",
            "Obsidian" => "Obsidian",
            "Typora" => "Typora",
            "OUTLOOK" => "Outlook",
            "Teams" => "Teams",
            "WeChat" => "微信",
            "QQ" => "QQ",
            "Feishu" or "Lark" => "飞书",
            "WindowsTerminal" or "wt" => "Windows Terminal",
            "cmd" => "命令提示符",
            "powershell" or "pwsh" => "PowerShell",
            _ => null
        };
    }

    private static string BuildFinalPrompt(ScenePromptRoute route, string userRequest, string contextText, string contextSource, string contextFieldName)
    {
        return $"""
【Task】
请根据以下需求完成任务，并输出可直接使用的结果：
{userRequest}

【Context】
当前场景：{route.DisplayName}
识别来源：{contextSource}
{contextFieldName}：{contextText}
输出方向：{route.ExpectedOutput}
必须保留用户原意，不编造事实，不加入与当前需求无关的复杂计划。

【References】
参考当前场景、用户需求、上下文材料和目标平台约束组织提示词。
不要编造未经用户确认的品牌内部细节、人物、数据或功能。

【Evaluate】
最终结果必须可直接复制给目标模型使用，并明确标注【Task】【Context】【References】【Evaluate】【Iterate】五部分。
提示词必须包含：任务目标、必要背景、参考材料或风格依据、评估标准、迭代要求。
如果信息不足，在对应部分写出合理占位或需要用户补充的问题，不要胡编具体事实。

【Iterate】
生成结果前先自查是否遗漏 TCREI 任一部分、是否超出用户需求、是否缺少可执行细节。
如存在信息不足，在末尾附上“需要补充的信息：”，列出最少必要项。
""";
    }
}
