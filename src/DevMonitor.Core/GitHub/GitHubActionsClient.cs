using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DevMonitor.Core.GitHub;

public sealed class GitHubActionsClient(HttpClient http, ISecretStore secrets)
{
    public const string TokenSecretName = "DevMonitor/GitHub";
    public const string TokenEnvironmentVariable = "DEVMONITOR_GITHUB_TOKEN";

    static readonly Dictionary<string, double> IncludedAmountByPlan = new(StringComparer.OrdinalIgnoreCase)
    {
        ["free"] = 12,
        ["pro"] = 18,
    };

    public async Task<ActionsUsage> FetchAsync(CancellationToken cancellationToken)
    {
        var token = Environment.GetEnvironmentVariable(TokenEnvironmentVariable) is { Length: > 0 } fromEnvironment
            ? fromEnvironment
            : secrets.Get(TokenSecretName)
                ?? throw new InvalidOperationException("No GitHub token is configured.");

        using var user = await GetAsync("user", token, cancellationToken);
        var login = user.RootElement.GetProperty("login").GetString()!;
        var plan = user.RootElement.TryGetProperty("plan", out var planElement)
            && planElement.TryGetProperty("name", out var name)
            && name.GetString() is { Length: > 0 } planName
                ? planName
                : "unknown";

        var now = DateTimeOffset.UtcNow;
        using var usage = await GetAsync(
            $"users/{login}/settings/billing/usage?year={now.Year}&month={now.Month}", token, cancellationToken);

        var skus = new Dictionary<string, SkuUsage>();
        foreach (var item in usage.RootElement.GetProperty("usageItems").EnumerateArray())
        {
            if (item.GetProperty("product").GetString() != "actions" || item.GetProperty("unitType").GetString() != "Minutes")
                continue;
            var sku = item.GetProperty("sku").GetString() ?? "unknown";
            var total = skus.GetValueOrDefault(sku) ?? new SkuUsage(0, 0);
            skus[sku] = new(
                total.Minutes + item.GetProperty("quantity").GetDouble(),
                total.Amount + item.GetProperty("grossAmount").GetDouble());
        }

        return new(
            now,
            plan,
            IncludedAmountByPlan.TryGetValue(plan, out var included) ? included : null,
            skus,
            new DateOnly(now.Year, now.Month, 1).AddMonths(1));
    }

    async Task<JsonDocument> GetAsync(string path, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.UserAgent.ParseAdd("DevMonitor");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("GitHub rejected the token.");
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"GitHub returned HTTP {(int)response.StatusCode} for {path.Split('?')[0]}: {ErrorMessage(body)}");
        return JsonDocument.Parse(body);
    }

    static string ErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.GetProperty("message").GetString() ?? body;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return body;
        }
    }
}
