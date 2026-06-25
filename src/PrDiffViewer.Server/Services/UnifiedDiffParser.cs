using System.Globalization;
using System.Text.RegularExpressions;
using PrDiffViewer.Shared.Models;

namespace PrDiffViewer.Server.Services;

/// <summary>
/// Parses the unified-diff text that LibGit2Sharp produces for a single file into a structured
/// list of hunks and lines, tracking old/new line numbers as it goes.
/// </summary>
public static partial class UnifiedDiffParser
{
    [GeneratedRegex(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@(.*)$")]
    private static partial Regex HunkHeaderRegex();

    public static List<DiffHunk> ParseHunks(string patch)
    {
        var hunks = new List<DiffHunk>();
        if (string.IsNullOrEmpty(patch))
            return hunks;

        var rawLines = patch.Split('\n');
        DiffHunk? current = null;
        int oldLine = 0, newLine = 0;

        foreach (var raw in rawLines)
        {
            // Patch text is LF-delimited; strip a stray trailing CR from the split.
            var line = raw.Length > 0 && raw[^1] == '\r' ? raw[..^1] : raw;

            var header = HunkHeaderRegex().Match(line);
            if (header.Success)
            {
                int oldStart = ParseInt(header.Groups[1].Value);
                int oldCount = header.Groups[2].Success ? ParseInt(header.Groups[2].Value) : 1;
                int newStart = ParseInt(header.Groups[3].Value);
                int newCount = header.Groups[4].Success ? ParseInt(header.Groups[4].Value) : 1;

                current = new DiffHunk
                {
                    OldStart = oldStart,
                    OldLines = oldCount,
                    NewStart = newStart,
                    NewLines = newCount,
                    Header = header.Groups[5].Value.TrimStart()
                };
                hunks.Add(current);
                oldLine = oldStart;
                newLine = newStart;
                continue;
            }

            if (current is null)
                continue; // file/index headers (diff --git, ---, +++, etc.) before the first hunk

            if (line.Length == 0)
                continue;

            char marker = line[0];
            string content = line.Length > 1 ? line[1..] : "";

            switch (marker)
            {
                case ' ':
                    current.Lines.Add(new DiffLine
                    {
                        Kind = DiffLineKind.Context,
                        OldLineNumber = oldLine++,
                        NewLineNumber = newLine++,
                        Content = content
                    });
                    break;
                case '+':
                    current.Lines.Add(new DiffLine
                    {
                        Kind = DiffLineKind.Added,
                        NewLineNumber = newLine++,
                        Content = content
                    });
                    break;
                case '-':
                    current.Lines.Add(new DiffLine
                    {
                        Kind = DiffLineKind.Deleted,
                        OldLineNumber = oldLine++,
                        Content = content
                    });
                    break;
                case '\\':
                    // "\ No newline at end of file" applies to the previously emitted line.
                    if (current.Lines.Count > 0)
                        current.Lines[^1].NoNewlineAtEof = true;
                    break;
                default:
                    // Unknown marker (should not occur in well-formed git patches). Ignore it rather
                    // than emit a synthetic line, which would desync the old/new line counters.
                    break;
            }
        }

        return hunks;
    }

    private static int ParseInt(string s) => int.Parse(s, CultureInfo.InvariantCulture);
}
