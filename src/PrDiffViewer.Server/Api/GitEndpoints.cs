using PrDiffViewer.Server.Services;
using PrDiffViewer.Shared.Models;

namespace PrDiffViewer.Server.Api;

public static class GitEndpoints
{
    public static RouteGroupBuilder MapGitApi(this IEndpointRouteBuilder app, bool includeErrorDetail)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/branches", (string repo, GitDiffService git) =>
            Guard(() => Results.Ok(git.GetBranches(repo)), includeErrorDetail));

        group.MapGet("/diff", (
            string repo,
            string source,
            string target,
            string? mode,
            int? context,
            GitDiffService git) =>
            Guard(() =>
            {
                var diffMode = string.Equals(mode, "direct", StringComparison.OrdinalIgnoreCase)
                    ? DiffMode.Direct
                    : DiffMode.MergeBase;
                var summary = git.GetDiff(repo, source, target, diffMode, context ?? 3);
                return Results.Ok(summary);
            }, includeErrorDetail));

        group.MapGet("/lines", (
            string repo,
            string commit,
            string path,
            int start,
            int count,
            GitDiffService git) =>
            Guard(() => Results.Ok(git.GetFileLines(repo, commit, path, start, count)), includeErrorDetail));

        return group;
    }

    /// <summary>Runs a git operation, converting expected failures into clean JSON errors.</summary>
    private static IResult Guard(Func<IResult> action, bool includeErrorDetail)
    {
        try
        {
            return action();
        }
        catch (GitServiceException ex)
        {
            return Results.BadRequest(new ApiError(ex.Message));
        }
        catch (Exception ex)
        {
            // Only surface raw exception text in Development to avoid leaking internals.
            return Results.Json(
                new ApiError("An unexpected error occurred reading the repository.",
                    includeErrorDetail ? ex.Message : null),
                statusCode: 500);
        }
    }
}
