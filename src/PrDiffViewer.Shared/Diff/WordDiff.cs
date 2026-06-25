using PrDiffViewer.Shared.Models;

namespace PrDiffViewer.Shared.Diff;

/// <summary>
/// Computes word-level (intra-line) differences between a deleted line and the added line it
/// pairs with, so the UI can highlight only the changed runs — the way Azure DevOps does.
/// </summary>
public static class WordDiff
{
    /// <summary>
    /// If the two lines share less than this fraction of characters they are treated as a full
    /// rewrite and left without intra-line highlighting (matching ADO's "too dissimilar" behaviour).
    /// </summary>
    private const double MinCommonRatio = 0.20;

    /// <summary>
    /// Above this line length we skip word-diffing: the LCS matrix is O(n·m) in token count, so a
    /// pathological line (minified JS, long base64) could allocate gigabytes. Such lines fall back
    /// to plain add/remove highlighting.
    /// </summary>
    private const int MaxLineLength = 5000;

    public static void Annotate(DiffLine deleted, DiffLine added)
    {
        if (deleted.Content.Length > MaxLineLength || added.Content.Length > MaxLineLength)
        {
            deleted.Segments = null;
            added.Segments = null;
            return;
        }

        var a = Tokenize(deleted.Content);
        var b = Tokenize(added.Content);

        var (aCommon, bCommon, commonChars) = LongestCommonSubsequence(a, b);

        int maxLen = Math.Max(deleted.Content.Length, added.Content.Length);
        if (maxLen == 0 || (double)commonChars / maxLen < MinCommonRatio)
        {
            // Too dissimilar to be a meaningful in-place edit — show as plain add/remove.
            deleted.Segments = null;
            added.Segments = null;
            return;
        }

        deleted.Segments = ToSegments(a, aCommon);
        added.Segments = ToSegments(b, bCommon);
    }

    /// <summary>Splits text into tokens: runs of word characters, runs of whitespace, and individual symbols.</summary>
    private static List<string> Tokenize(string s)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c))
            {
                int start = i;
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                tokens.Add(s.Substring(start, i - start));
            }
            else if (char.IsLetterOrDigit(c) || c == '_')
            {
                int start = i;
                while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                tokens.Add(s.Substring(start, i - start));
            }
            else
            {
                tokens.Add(s[i].ToString());
                i++;
            }
        }
        return tokens;
    }

    /// <summary>
    /// Standard LCS over the token lists. Returns which tokens of each side are part of the common
    /// subsequence (i.e. unchanged) plus the total number of characters in common.
    /// </summary>
    private static (bool[] aCommon, bool[] bCommon, int commonChars) LongestCommonSubsequence(
        List<string> a, List<string> b)
    {
        int n = a.Count, m = b.Count;
        var dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
        {
            for (int j = m - 1; j >= 0; j--)
            {
                dp[i, j] = a[i] == b[j]
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var aCommon = new bool[n];
        var bCommon = new bool[m];
        int commonChars = 0;
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y])
            {
                aCommon[x] = true;
                bCommon[y] = true;
                // Don't let shared indentation/whitespace prop up the similarity score — only real
                // shared content should keep a line out of the "too dissimilar" (plain) bucket.
                if (!string.IsNullOrWhiteSpace(a[x]))
                    commonChars += a[x].Length;
                x++;
                y++;
            }
            else if (dp[x + 1, y] >= dp[x, y + 1])
            {
                x++;
            }
            else
            {
                y++;
            }
        }

        return (aCommon, bCommon, commonChars);
    }

    /// <summary>Merges adjacent tokens sharing the same changed/unchanged state into display segments.</summary>
    private static List<WordSegment> ToSegments(List<string> tokens, bool[] common)
    {
        var segments = new List<WordSegment>();
        for (int i = 0; i < tokens.Count; i++)
        {
            bool changed = !common[i];
            if (segments.Count > 0 && segments[^1].Changed == changed)
            {
                segments[^1].Text += tokens[i];
            }
            else
            {
                segments.Add(new WordSegment(tokens[i], changed));
            }
        }
        return segments;
    }
}
