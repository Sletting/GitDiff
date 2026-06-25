using LibGit2Sharp;
using PrDiffViewer.Shared.Diff;
using PrDiffViewer.Shared.Models;
using LgChangeKind = LibGit2Sharp.ChangeKind;
using ChangeKind = PrDiffViewer.Shared.Models.ChangeKind;

namespace PrDiffViewer.Server.Services;

/// <summary>Thrown for expected, user-facing git problems (bad path, unknown branch, etc.).</summary>
public sealed class GitServiceException : Exception
{
    public GitServiceException(string message) : base(message) { }
}

/// <summary>
/// Opens a local git repository and produces branch lists and branch-to-branch diffs using
/// LibGit2Sharp. Stateless: a fresh <see cref="Repository"/> is opened (and disposed) per call,
/// since LibGit2Sharp repositories are not thread-safe.
/// </summary>
public sealed class GitDiffService
{
    public BranchList GetBranches(string repoPath)
    {
        using var repo = Open(repoPath);

        var branches = new List<BranchRef>();
        foreach (var b in repo.Branches)
        {
            var tip = b.Tip;
            branches.Add(new BranchRef
            {
                Name = b.FriendlyName,
                IsRemote = b.IsRemote,
                IsHead = b.IsCurrentRepositoryHead,
                TipSha = tip?.Sha ?? "",
                TipShortSha = Short(tip?.Sha),
                TipSummary = tip?.MessageShort ?? "",
                WhenIso = tip is null ? "" : tip.Author.When.UtcDateTime.ToString("o")
            });
        }

        // Local branches first, then remote; alphabetical within each group.
        branches.Sort((x, y) =>
        {
            if (x.IsRemote != y.IsRemote) return x.IsRemote ? 1 : -1;
            return string.CompareOrdinal(x.Name, y.Name);
        });

        return new BranchList
        {
            RepoPath = repo.Info.WorkingDirectory ?? repoPath,
            RepoName = RepoName(repo, repoPath),
            HeadBranch = repo.Info.IsHeadDetached ? null : repo.Head.FriendlyName,
            Branches = branches
        };
    }

    public DiffSummary GetDiff(string repoPath, string sourceBranch, string targetBranch, DiffMode mode, int contextLines)
    {
        if (string.IsNullOrWhiteSpace(sourceBranch) || string.IsNullOrWhiteSpace(targetBranch))
            throw new GitServiceException("Both a source and a target branch must be selected.");

        using var repo = Open(repoPath);

        var sourceCommit = ResolveCommit(repo, sourceBranch)
            ?? throw new GitServiceException($"Could not resolve source branch '{sourceBranch}'.");
        var targetCommit = ResolveCommit(repo, targetBranch)
            ?? throw new GitServiceException($"Could not resolve target branch '{targetBranch}'.");

        var mergeBase = repo.ObjectDatabase.FindMergeBase(targetCommit, sourceCommit);

        // PR semantics: compare what the source introduced relative to the merge base with target.
        var oldCommit = mode == DiffMode.MergeBase ? (mergeBase ?? targetCommit) : targetCommit;
        var newCommit = sourceCommit;

        var compareOptions = new CompareOptions
        {
            ContextLines = Math.Clamp(contextLines, 0, 100),
            InterhunkLines = 0,
            Similarity = SimilarityOptions.Renames
        };

        using var patch = repo.Diff.Compare<Patch>(oldCommit.Tree, newCommit.Tree, compareOptions);

        var files = new List<FileDiff>();
        int totalAdded = 0, totalDeleted = 0;

        foreach (var entry in patch)
        {
            var changeKind = MapChangeKind(entry.Status);
            bool isDelete = changeKind == ChangeKind.Deleted;

            var file = new FileDiff
            {
                OldPath = entry.OldPath ?? "",
                NewPath = entry.Path ?? "",
                Path = isDelete ? (entry.OldPath ?? entry.Path ?? "") : (entry.Path ?? entry.OldPath ?? ""),
                ChangeKind = changeKind,
                IsBinary = entry.IsBinaryComparison,
                LinesAdded = entry.LinesAdded,
                LinesDeleted = entry.LinesDeleted,
                Similarity = 0
            };

            if (!entry.IsBinaryComparison)
            {
                file.Hunks = UnifiedDiffParser.ParseHunks(entry.Patch);
                AnnotateWordDiffs(file.Hunks);
            }

            totalAdded += entry.LinesAdded;
            totalDeleted += entry.LinesDeleted;
            files.Add(file);
        }

        // Sort like a file explorer: by path segments, folders grouped naturally.
        files.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

        var commits = new List<CommitRef>();
        var filter = new CommitFilter
        {
            IncludeReachableFrom = sourceCommit,
            ExcludeReachableFrom = targetCommit,
            SortBy = CommitSortStrategies.Time
        };
        foreach (var c in repo.Commits.QueryBy(filter))
            commits.Add(ToCommitRef(c));

        return new DiffSummary
        {
            RepoName = RepoName(repo, repoPath),
            RepoPath = repo.Info.WorkingDirectory ?? repoPath,
            Mode = mode,
            SourceBranch = sourceBranch,
            TargetBranch = targetBranch,
            SourceTip = ToCommitRef(sourceCommit),
            TargetTip = ToCommitRef(targetCommit),
            MergeBase = mergeBase is null ? null : ToCommitRef(mergeBase),
            TotalAdded = totalAdded,
            TotalDeleted = totalDeleted,
            Files = files,
            Commits = commits
        };
    }

    /// <summary>
    /// Returns a slice of a file's content at the given commit, for expanding collapsed
    /// context between (or around) diff hunks. <paramref name="startLine"/> is 1-based.
    /// </summary>
    public FileLines GetFileLines(string repoPath, string commitish, string path, int startLine, int count)
    {
        using var repo = Open(repoPath);

        var commit = ResolveCommit(repo, commitish)
            ?? throw new GitServiceException($"Could not resolve commit '{commitish}'.");

        if (commit[path]?.Target is not Blob blob)
            throw new GitServiceException($"'{path}' was not found at {commitish}.");

        if (blob.IsBinary)
            throw new GitServiceException("Cannot expand a binary file.");

        var all = SplitLines(blob.GetContentText());

        int from = Math.Max(1, startLine);
        int take = Math.Max(0, count);
        // Cap the requested span to what the file actually has so an "expand all" sentinel
        // (a very large count) never preallocates a huge list.
        int available = Math.Max(0, all.Count - from + 1);
        var slice = new List<string>(Math.Min(take, available));
        for (int i = from; i < from + take && i <= all.Count; i++)
            slice.Add(all[i - 1]);

        return new FileLines
        {
            Path = path,
            StartLine = from,
            Lines = slice,
            TotalLines = all.Count
        };
    }

    private static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        if (text.Length == 0)
            return lines;

        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                int end = i;
                if (end > start && text[end - 1] == '\r') end--;
                lines.Add(text.Substring(start, end - start));
                start = i + 1;
            }
        }
        if (start < text.Length)
        {
            int end = text.Length;
            if (end > start && text[end - 1] == '\r') end--;
            lines.Add(text.Substring(start, end - start));
        }
        return lines;
    }

    private static void AnnotateWordDiffs(List<DiffHunk> hunks)
    {
        foreach (var hunk in hunks)
        {
            foreach (var row in SideBySide.Build(hunk.Lines))
            {
                if (row.IsModifiedPair)
                    WordDiff.Annotate(row.Left!, row.Right!);
            }
        }
    }

    private static Repository Open(string repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath))
            throw new GitServiceException("No repository path was provided.");

        string expanded = repoPath.Trim();
        if (!Directory.Exists(expanded) && !File.Exists(expanded))
            throw new GitServiceException($"Path does not exist: {expanded}");

        string? gitDir = Repository.Discover(expanded);
        if (gitDir is null)
            throw new GitServiceException($"'{expanded}' is not inside a git repository.");

        try
        {
            return new Repository(gitDir);
        }
        catch (Exception ex)
        {
            throw new GitServiceException($"Failed to open repository: {ex.Message}");
        }
    }

    /// <summary>Resolves a branch friendly name, then a remote branch, then any commit-ish/sha.</summary>
    private static Commit? ResolveCommit(Repository repo, string name)
    {
        var branch = repo.Branches[name];
        if (branch?.Tip is not null)
            return branch.Tip;

        // Try common remote prefix if the user passed a bare name.
        var remote = repo.Branches[$"origin/{name}"];
        if (remote?.Tip is not null)
            return remote.Tip;

        return repo.Lookup<Commit>(name);
    }

    private static ChangeKind MapChangeKind(LgChangeKind status) => status switch
    {
        LgChangeKind.Added => ChangeKind.Added,
        LgChangeKind.Deleted => ChangeKind.Deleted,
        LgChangeKind.Modified => ChangeKind.Modified,
        LgChangeKind.Renamed => ChangeKind.Renamed,
        LgChangeKind.Copied => ChangeKind.Copied,
        LgChangeKind.TypeChanged => ChangeKind.TypeChanged,
        LgChangeKind.Conflicted => ChangeKind.Conflicted,
        _ => ChangeKind.Modified
    };

    private static CommitRef ToCommitRef(Commit c) => new()
    {
        Sha = c.Sha,
        ShortSha = Short(c.Sha),
        Summary = c.MessageShort,
        Author = c.Author.Name,
        AuthorEmail = c.Author.Email,
        WhenIso = c.Author.When.UtcDateTime.ToString("o")
    };

    private static string RepoName(Repository repo, string fallback)
    {
        var workdir = repo.Info.WorkingDirectory?.TrimEnd('/', '\\');
        var source = string.IsNullOrEmpty(workdir) ? fallback.TrimEnd('/', '\\') : workdir;
        var name = Path.GetFileName(source);
        return string.IsNullOrEmpty(name) ? source : name;
    }

    private static string Short(string? sha) =>
        string.IsNullOrEmpty(sha) ? "" : sha[..Math.Min(8, sha.Length)];
}
