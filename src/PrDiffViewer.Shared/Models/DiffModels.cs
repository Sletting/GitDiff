namespace PrDiffViewer.Shared.Models;

/// <summary>How a file changed between the two trees being compared.</summary>
public enum ChangeKind
{
    Unmodified,
    Added,
    Deleted,
    Modified,
    Renamed,
    Copied,
    TypeChanged,
    Conflicted
}

/// <summary>The role of a single line within a diff.</summary>
public enum DiffLineKind
{
    Context,
    Added,
    Deleted
}

/// <summary>
/// How the two branches are compared.
/// <para><see cref="MergeBase"/> is the Azure DevOps PR semantic (three-dot): show what the
/// source branch introduces relative to the merge-base with the target branch.</para>
/// <para><see cref="Direct"/> is a plain two-dot diff between the tips of target and source.</para>
/// </summary>
public enum DiffMode
{
    MergeBase,
    Direct
}

/// <summary>A reference to a commit, trimmed down for display.</summary>
public sealed class CommitRef
{
    public string Sha { get; set; } = "";
    public string ShortSha { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Author { get; set; } = "";
    public string AuthorEmail { get; set; } = "";
    /// <summary>ISO-8601 commit (author) timestamp.</summary>
    public string WhenIso { get; set; } = "";
}

/// <summary>A branch in the repository.</summary>
public sealed class BranchRef
{
    /// <summary>Friendly name, e.g. "main" or "origin/feature/x".</summary>
    public string Name { get; set; } = "";
    public bool IsRemote { get; set; }
    public bool IsHead { get; set; }
    public string TipSha { get; set; } = "";
    public string TipShortSha { get; set; } = "";
    public string TipSummary { get; set; } = "";
    public string WhenIso { get; set; } = "";
}

/// <summary>Result of listing branches for a repository.</summary>
public sealed class BranchList
{
    public string RepoPath { get; set; } = "";
    public string RepoName { get; set; } = "";
    public string? HeadBranch { get; set; }
    public List<BranchRef> Branches { get; set; } = new();
}

/// <summary>A run of text within a line, flagged if it differs from its counterpart line.</summary>
public sealed class WordSegment
{
    public string Text { get; set; } = "";
    public bool Changed { get; set; }

    public WordSegment() { }
    public WordSegment(string text, bool changed)
    {
        Text = text;
        Changed = changed;
    }
}

/// <summary>A single line in a diff hunk.</summary>
public sealed class DiffLine
{
    public DiffLineKind Kind { get; set; }
    /// <summary>Line number on the "old" (left) side, null for added lines.</summary>
    public int? OldLineNumber { get; set; }
    /// <summary>Line number on the "new" (right) side, null for deleted lines.</summary>
    public int? NewLineNumber { get; set; }
    /// <summary>Raw line content, without the leading +/-/space marker.</summary>
    public string Content { get; set; } = "";
    /// <summary>
    /// Word-level segmentation for modified lines (a delete paired with an add).
    /// Null when the whole line is unchanged context or has no paired counterpart.
    /// </summary>
    public List<WordSegment>? Segments { get; set; }
    /// <summary>True if this line had no trailing newline in the source ("\ No newline at end of file").</summary>
    public bool NoNewlineAtEof { get; set; }
}

/// <summary>A contiguous block of changes within a file diff.</summary>
public sealed class DiffHunk
{
    public int OldStart { get; set; }
    public int OldLines { get; set; }
    public int NewStart { get; set; }
    public int NewLines { get; set; }
    /// <summary>The "@@ ... @@" section heading text that trails the range (often a function signature).</summary>
    public string Header { get; set; } = "";
    public List<DiffLine> Lines { get; set; } = new();
}

/// <summary>The diff for one file.</summary>
public sealed class FileDiff
{
    /// <summary>Display path (the new path, or the old path when deleted).</summary>
    public string Path { get; set; } = "";
    public string OldPath { get; set; } = "";
    public string NewPath { get; set; } = "";
    public ChangeKind ChangeKind { get; set; }
    public bool IsBinary { get; set; }
    public int LinesAdded { get; set; }
    public int LinesDeleted { get; set; }
    /// <summary>Rename/copy similarity percentage (0 when not applicable).</summary>
    public int Similarity { get; set; }
    public List<DiffHunk> Hunks { get; set; } = new();

    public bool IsRename => ChangeKind == ChangeKind.Renamed && !string.Equals(OldPath, NewPath, StringComparison.Ordinal);
}

/// <summary>The complete diff between two branches.</summary>
public sealed class DiffSummary
{
    public string RepoName { get; set; } = "";
    public string RepoPath { get; set; } = "";
    public DiffMode Mode { get; set; }
    public string SourceBranch { get; set; } = "";
    public string TargetBranch { get; set; } = "";
    public CommitRef? SourceTip { get; set; }
    public CommitRef? TargetTip { get; set; }
    public CommitRef? MergeBase { get; set; }
    public int TotalAdded { get; set; }
    public int TotalDeleted { get; set; }
    public List<FileDiff> Files { get; set; } = new();
    /// <summary>Commits reachable from the source branch but not the target (newest first).</summary>
    public List<CommitRef> Commits { get; set; } = new();
}

/// <summary>A slice of a file's content at a particular commit, used to expand collapsed context.</summary>
public sealed class FileLines
{
    public string Path { get; set; } = "";
    /// <summary>1-based line number of the first returned line.</summary>
    public int StartLine { get; set; }
    public List<string> Lines { get; set; } = new();
    /// <summary>Total number of lines in the file (so the client knows where EOF is).</summary>
    public int TotalLines { get; set; }
}

/// <summary>Error payload returned by the API on failure.</summary>
public sealed class ApiError
{
    public string Message { get; set; } = "";
    public string? Detail { get; set; }

    public ApiError() { }
    public ApiError(string message, string? detail = null)
    {
        Message = message;
        Detail = detail;
    }
}
