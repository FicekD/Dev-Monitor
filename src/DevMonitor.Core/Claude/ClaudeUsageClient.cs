using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DevMonitor.Core.Claude;

public sealed class ClaudeUsageClient(HttpClient http, string credentialsPath)
{
    const string Endpoint = "https://api.anthropic.com/api/oauth/usage";

    public async Task<ClaudeUsage> FetchAsync(CancellationToken cancellationToken) =>
        ClaudeUsage.Parse(await FetchRawAsync(cancellationToken), DateTimeOffset.UtcNow);

    public async Task<string> FetchRawAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ReadAccessToken());
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Claude rejected the access token. Using Claude Code will renew it.");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"The Claude usage request failed with HTTP {(int)response.StatusCode}.");
        return body;
    }

    string ReadAccessToken()
    {
        if (!File.Exists(credentialsPath))
            throw new InvalidOperationException($"Claude Code credentials were not found at {credentialsPath}.");

        using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("claudeAiOauth", out var oauth)
            || !oauth.TryGetProperty("accessToken", out var token)
            || token.GetString() is not { Length: > 0 } accessToken)
            throw new InvalidOperationException("The Claude Code credentials file does not contain an access token.");

        if (oauth.TryGetProperty("expiresAt", out var expires) && expires.ValueKind == JsonValueKind.Number)
        {
            var expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(expires.GetInt64());
            if (expiresAt < DateTimeOffset.UtcNow)
                throw new InvalidOperationException(
                    $"The Claude token expired on {expiresAt.ToLocalTime():g}. Using Claude Code will renew it.");
        }
        return accessToken;
    }
}
