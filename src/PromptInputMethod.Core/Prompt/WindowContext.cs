namespace PromptInputMethod.Core.Prompt;

public sealed record WindowContext(string ProcessName, string Title, string ClassName)
{
    public static WindowContext Unknown { get; } = new(string.Empty, string.Empty, string.Empty);
}
