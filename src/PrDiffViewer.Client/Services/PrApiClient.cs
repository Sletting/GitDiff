using System.Net.Http.Json;
using PrDiffViewer.Shared.Models;

namespace PrDiffViewer.Client.Services;

/// <summary>Typed wrapper over the server git API. Returns (data, error) so callers can show clean messages.</summary>
public sealed class PrApiClient
{
    private readonly HttpClient _http;

    public PrApiClient(HttpClient http) => _http = http;

    public Task<Result<BranchList>> GetBranchesAsync(string repoPath, CancellationToken ct = default)
        => GetAsync<BranchList>($"api/branches?repo={Esc(repoPath)}", ct);

    public Task<Result<DiffSummary>> GetDiffAsync(
        string repoPath, string source, string target, DiffMode mode, int context = 3, CancellationToken ct = default)
    {
        var modeStr = mode == DiffMode.Direct ? "direct" : "mergeBase";
        var url = $"api/diff?repo={Esc(repoPath)}&source={Esc(source)}&target={Esc(target)}&mode={modeStr}&context={context}";
        return GetAsync<DiffSummary>(url, ct);
    }

    public Task<Result<FileLines>> GetLinesAsync(
        string repoPath, string commit, string path, int start, int count, CancellationToken ct = default)
    {
        var url = $"api/lines?repo={Esc(repoPath)}&commit={Esc(commit)}&path={Esc(path)}&start={start}&count={count}";
        return GetAsync<FileLines>(url, ct);
    }

    private async Task<Result<T>> GetAsync<T>(string url, CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
            {
                var data = await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
                return data is null
                    ? Result<T>.Fail("The server returned an empty response.")
                    : Result<T>.Ok(data);
            }

            // Try to surface the server's structured error message.
            try
            {
                var err = await resp.Content.ReadFromJsonAsync<ApiError>(cancellationToken: ct);
                if (!string.IsNullOrWhiteSpace(err?.Message))
                    return Result<T>.Fail(err!.Message);
            }
            catch
            {
                // fall through to a generic message
            }

            return Result<T>.Fail($"Request failed ({(int)resp.StatusCode} {resp.ReasonPhrase}).");
        }
        catch (Exception ex)
        {
            return Result<T>.Fail($"Could not reach the server: {ex.Message}");
        }
    }

    private static string Esc(string s) => Uri.EscapeDataString(s ?? "");
}

/// <summary>A tiny success-or-error result type.</summary>
public sealed class Result<T>
{
    public T? Value { get; private init; }
    public string? Error { get; private init; }
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new() { Value = value };
    public static Result<T> Fail(string error) => new() { Error = error };
}
