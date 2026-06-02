namespace PromptInputMethod.Core.Llm;

public interface ILlmClient
{
    Task<string> CompleteAsync(string prompt, LlmRequestOptions options, CancellationToken cancellationToken = default);
    Task<string> CompleteAsync(LlmRequest request, LlmRequestOptions options, CancellationToken cancellationToken = default);
}
