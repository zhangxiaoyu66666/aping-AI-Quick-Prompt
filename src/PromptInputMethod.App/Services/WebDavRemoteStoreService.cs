using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace PromptInputMethod.App.Services;

public sealed class WebDavRemoteStoreService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly HttpMethod MkColMethod = new("MKCOL");
    private static readonly HttpMethod PropFindMethod = new("PROPFIND");

    private readonly HttpClient _httpClient;

    public WebDavRemoteStoreService()
        : this(new HttpClient())
    {
    }

    public WebDavRemoteStoreService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task TestConnectionAsync(
        WebDavConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(settings, PropFindMethod, string.Empty);
        request.Headers.Add("Depth", "0");
        request.Content = new StringContent("""
            <?xml version="1.0" encoding="utf-8"?>
            <propfind xmlns="DAV:"><prop><resourcetype/></prop></propfind>
            """, Encoding.UTF8, "application/xml");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            await EnsureDirectoryAsync(settings, string.Empty, cancellationToken).ConfigureAwait(false);
            return;
        }

        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task<T?> ReadJsonAsync<T>(
        WebDavConnectionSettings settings,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(settings, HttpMethod.Get, relativePath);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccessAsync(response).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteJsonAsync<T>(
        WebDavConnectionSettings settings,
        string relativePath,
        T value,
        CancellationToken cancellationToken = default)
    {
        await BackupExistingFileAsync(settings, relativePath, cancellationToken).ConfigureAwait(false);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        await PutBytesAsync(settings, relativePath, bytes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> FindConflictFilesAsync(
        WebDavConnectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(settings, PropFindMethod, string.Empty);
        request.Headers.Add("Depth", "infinity");
        request.Content = new StringContent("""
            <?xml version="1.0" encoding="utf-8"?>
            <propfind xmlns="DAV:"><prop><displayname/></prop></propfind>
            """, Encoding.UTF8, "application/xml");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ExtractWebDavPaths(xml)
            .Where(IsConflictPath)
            .Take(12)
            .ToArray();
    }

    private async Task BackupExistingFileAsync(
        WebDavConnectionSettings settings,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var request = CreateRequest(settings, HttpMethod.Get, relativePath);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return;
        }

        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            return;
        }

        var backupPath = $"sync/backups/{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}/{NormalizeRelativePath(relativePath)}";
        await PutBytesAsync(settings, backupPath, bytes, cancellationToken).ConfigureAwait(false);
    }

    private async Task PutBytesAsync(
        WebDavConnectionSettings settings,
        string relativePath,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await EnsureDirectoryAsync(settings, string.Empty, cancellationToken).ConfigureAwait(false);
        await EnsureParentDirectoriesAsync(settings, relativePath, cancellationToken).ConfigureAwait(false);
        using var request = CreateRequest(settings, HttpMethod.Put, relativePath);
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    private async Task EnsureParentDirectoriesAsync(
        WebDavConnectionSettings settings,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var parts = NormalizeRelativePath(relativePath)
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .SkipLast(1)
            .ToArray();
        var current = string.Empty;
        foreach (var part in parts)
        {
            current = string.IsNullOrWhiteSpace(current) ? part : $"{current}/{part}";
            await EnsureDirectoryAsync(settings, current, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnsureDirectoryAsync(
        WebDavConnectionSettings settings,
        string relativePath,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(settings, MkColMethod, relativePath);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.MethodNotAllowed)
        {
            return;
        }

        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    private static HttpRequestMessage CreateRequest(
        WebDavConnectionSettings settings,
        HttpMethod method,
        string relativePath)
    {
        var request = new HttpRequestMessage(method, BuildUri(settings, relativePath));
        if (!string.IsNullOrWhiteSpace(settings.Username) || !string.IsNullOrWhiteSpace(settings.Password))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Username}:{settings.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }

        request.Headers.UserAgent.ParseAdd("Aipin-AI-Quick-Prompt/1.0");
        return request;
    }

    private static Uri BuildUri(WebDavConnectionSettings settings, string relativePath)
    {
        if (!Uri.TryCreate(settings.ServerUrl.Trim(), UriKind.Absolute, out var serverUri)
            || serverUri.Scheme is not ("https" or "http"))
        {
            throw new InvalidOperationException("请输入有效的 WebDAV 服务器地址。");
        }

        var relative = string.Join('/',
            NormalizeRelativePath(settings.RemoteRootPath),
            NormalizeRelativePath(relativePath))
            .Trim('/');
        if (string.IsNullOrWhiteSpace(relative))
        {
            return serverUri;
        }

        var baseText = serverUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? serverUri.AbsoluteUri
            : $"{serverUri.AbsoluteUri}/";
        var encoded = string.Join('/', relative.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        return new Uri(new Uri(baseText), encoded);
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return (relativePath ?? string.Empty)
            .Replace('\\', '/')
            .Trim()
            .Trim('/');
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw new InvalidOperationException($"WebDAV 请求失败：{(int)response.StatusCode} {response.ReasonPhrase} {Trim(text)}");
    }

    private static string Trim(string text)
    {
        var normalized = text.Trim();
        return normalized.Length <= 240 ? normalized : $"{normalized[..240]}...";
    }

    private static IEnumerable<string> ExtractWebDavPaths(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return [];
        }

        try
        {
            XNamespace dav = "DAV:";
            var document = XDocument.Parse(xml);
            return document
                .Descendants(dav + "href")
                .Select(element => WebUtility.UrlDecode(element.Value))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static bool IsConflictPath(string path)
    {
        var name = Path.GetFileName(path);
        return name.Contains("conflict", StringComparison.OrdinalIgnoreCase)
            || name.Contains("conflicted copy", StringComparison.OrdinalIgnoreCase)
            || name.Contains("冲突", StringComparison.OrdinalIgnoreCase)
            || name.Contains("冲突副本", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record WebDavConnectionSettings(
    string ServerUrl,
    string RemoteRootPath,
    string Username,
    string Password);
