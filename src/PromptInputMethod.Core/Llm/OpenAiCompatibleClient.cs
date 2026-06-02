using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;

namespace PromptInputMethod.Core.Llm;

public sealed class OpenAiCompatibleClient : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public OpenAiCompatibleClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<string> CompleteAsync(string prompt, LlmRequestOptions options, CancellationToken cancellationToken = default)
    {
        return await CompleteAsync(LlmRequest.TextOnly(prompt), options, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> CompleteAsync(LlmRequest llmRequest, LlmRequestOptions options, CancellationToken cancellationToken = default)
    {
        var result = await CompleteWithResultAsync(llmRequest, options, cancellationToken).ConfigureAwait(false);
        return result.Content;
    }

    public async Task<LlmCompletionResult> CompleteWithResultAsync(LlmRequest llmRequest, LlmRequestOptions options, CancellationToken cancellationToken = default)
    {
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException("模型配置未启用或缺少 baseUrl/model/API key。");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(options.BaseUrl));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        httpRequest.Content = JsonContent.Create(BuildCompletionPayload(llmRequest, options, stream: false), options: JsonOptions);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"模型请求失败：{(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
        }

        return ParseCompletionBody(body);
    }

    public async Task<LlmCompletionResult> CompleteWithResultStreamingAsync(LlmRequest llmRequest, LlmRequestOptions options, IProgress<LlmStreamUpdate>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!options.IsConfigured)
        {
            throw new InvalidOperationException("模型配置未启用或缺少 baseUrl/model/API key。");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(options.BaseUrl));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        httpRequest.Content = JsonContent.Create(BuildCompletionPayload(llmRequest, options, stream: true), options: JsonOptions);

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"模型请求失败：{(int)response.StatusCode} {response.ReasonPhrase}\n{errorBody}");
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (!mediaType.Contains("event-stream", StringComparison.OrdinalIgnoreCase))
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var result = ParseCompletionBody(body);
            progress?.Report(new LlmStreamUpdate(result.Content, result.ReasoningContent));
            return result;
        }

        var contentBuilder = new System.Text.StringBuilder();
        var reasoningBuilder = new System.Text.StringBuilder();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var data = line["data:".Length..].Trim();
            if (data.Length == 0)
            {
                continue;
            }

            if (string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!TryReadStreamingUpdate(data, out var update))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(update.ContentDelta))
            {
                contentBuilder.Append(update.ContentDelta);
            }

            if (!string.IsNullOrEmpty(update.ReasoningDelta))
            {
                reasoningBuilder.Append(update.ReasoningDelta);
            }

            if (!string.IsNullOrEmpty(update.ContentDelta) || !string.IsNullOrEmpty(update.ReasoningDelta))
            {
                progress?.Report(update);
            }
        }

        var content = contentBuilder.ToString();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("模型流式响应内容为空。");
        }

        var reasoning = reasoningBuilder.ToString();
        return new LlmCompletionResult(content.Trim(), string.IsNullOrWhiteSpace(reasoning) ? null : reasoning.Trim());
    }

    private static IDictionary<string, object?> BuildCompletionPayload(LlmRequest llmRequest, LlmRequestOptions options, bool stream)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = options.Model,
            ["temperature"] = 0.2,
            ["stream"] = stream,
            ["messages"] = new[]
            {
                new { role = "system", content = (object)"你是一个提示词结构化助手。保留用户原意，不编造事实，输出可直接复制给其他大模型的清晰 Prompt。" },
                new { role = "user", content = BuildUserContent(llmRequest) }
            }
        };
        ApplyReasoningOptions(payload, llmRequest.Reasoning);
        return payload;
    }

    private static LlmCompletionResult ParseCompletionBody(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var choices = root.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("模型响应没有 choices。");
        }

        var message = choices[0].GetProperty("message");
        var content = ReadMessageContent(message);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("模型响应内容为空。");
        }

        var reasoningContent = ReadReasoningContent(message);
        return new LlmCompletionResult(content.Trim(), string.IsNullOrWhiteSpace(reasoningContent) ? null : reasoningContent.Trim());
    }

    private static bool TryReadStreamingUpdate(string data, out LlmStreamUpdate update)
    {
        update = new LlmStreamUpdate(null, null);
        try
        {
            using var document = JsonDocument.Parse(data);
            var root = document.RootElement;
            if (!root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return false;
            }

            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var delta))
            {
                update = new LlmStreamUpdate(
                    ReadTextProperty(delta, "content"),
                    ReadTextProperty(delta, "reasoning_content", "reasoning", "reasoning_text", "thinking"));
                return true;
            }

            if (choice.TryGetProperty("message", out var message))
            {
                update = new LlmStreamUpdate(ReadMessageContent(message), ReadReasoningContent(message));
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    public async Task<IReadOnlyList<LlmModelInfo>> ListModelsAsync(string baseUrl, string? apiKey, int timeoutSeconds = 15, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("请先填写 Base URL。");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("请先填写 API Key。");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BuildModelsUri(baseUrl));
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, timeoutCts.Token).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"模型列表请求失败：{(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
        }

        using var document = JsonDocument.Parse(body);
        var models = ReadModels(document.RootElement)
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .DistinctBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (models.Length == 0)
        {
            throw new InvalidOperationException("接口可访问，但没有返回可用模型。请确认 Base URL 指向 OpenAI-compatible /v1 endpoint。");
        }

        return models;
    }

    private static object BuildUserContent(LlmRequest request)
    {
        if (request.Images.Count == 0)
        {
            return request.Prompt;
        }

        var content = new List<object>
        {
            new { type = "text", text = request.Prompt }
        };
        content.AddRange(request.Images.Select(image => new
        {
            type = "image_url",
            image_url = new
            {
                url = image.DataUrl,
                detail = "auto"
            }
        }));
        return content;
    }

    private static void ApplyReasoningOptions(IDictionary<string, object?> payload, LlmReasoningOptions? reasoning)
    {
        if (reasoning is null)
        {
            return;
        }

        switch (reasoning.RequestKind)
        {
            case "reasoning_effort":
                payload.Remove("temperature");
                payload["reasoning_effort"] = string.IsNullOrWhiteSpace(reasoning.Effort) ? "high" : reasoning.Effort;
                break;
            case "deepseek_thinking":
                payload.Remove("temperature");
                payload["reasoning_effort"] = string.IsNullOrWhiteSpace(reasoning.Effort) ? "high" : reasoning.Effort;
                payload["thinking"] = new
                {
                    type = reasoning.IncludeThoughts ? "enabled" : "disabled"
                };
                break;
            case "anthropic_thinking":
                payload.Remove("temperature");
                payload["max_completion_tokens"] = 8192;
                payload["thinking"] = new
                {
                    type = "enabled",
                    budget_tokens = 4096
                };
                break;
        }
    }

    private static string? ReadReasoningContent(JsonElement message)
    {
        foreach (var name in new[] { "reasoning_content", "reasoning", "reasoning_text" })
        {
            if (message.TryGetProperty(name, out var property)
                && TryReadVisibleText(property, out var value))
            {
                return value;
            }
        }

        if (message.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.Array)
        {
            var thinkingParts = content.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object
                    && item.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String
                    && type.GetString() is "thinking" or "reasoning" or "reasoning_content" or "summary")
                .Select(ReadReasoningPartText)
                .Where(HasVisibleText)
                .ToArray();

            if (thinkingParts.Length > 0)
            {
                return string.Join(Environment.NewLine, thinkingParts);
            }
        }

        return null;
    }

    private static string? ReadReasoningPartText(JsonElement item)
    {
        foreach (var name in new[] { "text", "thinking", "content", "summary" })
        {
            if (item.TryGetProperty(name, out var property)
                && TryReadVisibleText(property, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? ReadTextProperty(JsonElement item, params string[] names)
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var property)
                && TryReadVisibleText(property, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool TryReadVisibleText(JsonElement property, out string? value)
    {
        value = null;
        if (property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return HasVisibleText(value);
        }

        if (property.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "text", "content", "summary" })
            {
                if (property.TryGetProperty(name, out var child)
                    && child.ValueKind == JsonValueKind.String
                    && HasVisibleText(child.GetString()))
                {
                    value = child.GetString();
                    return true;
                }
            }
        }

        if (property.ValueKind == JsonValueKind.Array)
        {
            var parts = property.EnumerateArray()
                .Select(ReadReasoningPartText)
                .Where(HasVisibleText)
                .ToArray();
            if (parts.Length > 0)
            {
                value = string.Join(Environment.NewLine, parts);
                return true;
            }
        }

        return false;
    }

    private static bool HasVisibleText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var ch in text)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (!char.IsWhiteSpace(ch) && !char.IsControl(ch) && category != UnicodeCategory.Format)
            {
                return true;
            }
        }

        return false;
    }

    private static string? ReadMessageContent(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content))
        {
            return null;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString();
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var textParts = content.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Where(item => !item.TryGetProperty("type", out var type)
                || type.ValueKind != JsonValueKind.String
                || type.GetString() is "text" or "output_text")
            .Select(item => item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
                ? text.GetString()
                : null)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();

        return textParts.Length == 0 ? null : string.Join(Environment.NewLine, textParts);
    }

    private static Uri BuildChatCompletionsUri(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(trimmed);
        }

        return new Uri($"{trimmed}/chat/completions");
    }

    private static Uri BuildModelsUri(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        if (trimmed.EndsWith("/models", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(trimmed);
        }

        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^"/chat/completions".Length].TrimEnd('/');
        }

        return new Uri($"{trimmed}/models");
    }

    private static IEnumerable<LlmModelInfo> ReadModels(JsonElement root)
    {
        var modelArray = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
                ? data
                : default;

        if (modelArray.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in modelArray.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                yield return new LlmModelInfo(item.GetString() ?? string.Empty, null);
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var idProperty))
            {
                continue;
            }

            var id = idProperty.GetString();
            var ownedBy = item.TryGetProperty("owned_by", out var ownedByProperty)
                ? ownedByProperty.GetString()
                : null;
            yield return new LlmModelInfo(id ?? string.Empty, ownedBy);
        }
    }
}
