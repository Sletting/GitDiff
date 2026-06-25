namespace PrDiffViewer.Client.Diff;

/// <summary>A run of code text with the CSS class that colors it.</summary>
public readonly record struct Tok(string Text, string Css);

/// <summary>
/// A small, language-agnostic tokenizer that approximates the Monaco "vs" light theme well enough
/// to make diffs read like the real Azure DevOps view. It is best-effort: it never throws and
/// always returns the original text in order, so rendering is safe even on inputs it doesn't model.
/// </summary>
public static class SyntaxHighlighter
{
    private const string Keyword = "tok-keyword";
    private const string Str = "tok-string";
    private const string Comment = "tok-comment";
    private const string Number = "tok-number";
    private const string Type = "tok-type";
    private const string Function = "tok-function";
    private const string Plain = "tok-text";

    // Union of keywords across the common C-family + script languages.
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract","as","async","await","base","bool","break","byte","case","catch","char","checked",
        "class","const","continue","decimal","default","delegate","do","double","else","enum","event",
        "explicit","extends","extern","false","finally","fixed","float","for","foreach","from","function",
        "func","get","global","goto","if","implements","implicit","import","in","int","interface","internal",
        "is","let","lock","long","namespace","new","null","object","operator","out","override","package",
        "params","private","protected","public","readonly","record","ref","return","sbyte","sealed","set",
        "short","sizeof","static","string","struct","switch","this","throw","throws","true","try","typeof",
        "uint","ulong","unchecked","unsafe","ushort","using","var","virtual","void","volatile","while",
        "yield","def","elif","except","lambda","pass","raise","with","and","or","not","nil","end","then",
        "elsif","module","require","fn","mut","impl","trait","pub","use","match","where","val","fun",
        "echo","local","export","const","type","interface","enum","of","keyof","readonly","never","unknown","any"
    };

    public static List<Tok> Tokenize(string code, string fileExtension)
    {
        var tokens = new List<Tok>();
        if (string.IsNullOrEmpty(code))
            return tokens;

        var ext = (fileExtension ?? "").TrimStart('.').ToLowerInvariant();
        bool slashComments = IsSlashComment(ext);
        bool hashComments = IsHashComment(ext);
        bool dashComments = ext is "sql" or "lua" or "hs";

        // Whole-line heuristics for block-comment continuation lines (e.g. " * foo").
        var trimmed = code.TrimStart();
        if (slashComments && (trimmed.StartsWith("* ") || trimmed == "*" || trimmed.StartsWith("*/") || trimmed.StartsWith("/*")))
        {
            tokens.Add(new Tok(code, Comment));
            return tokens;
        }

        int i = 0;
        int n = code.Length;
        var plain = new System.Text.StringBuilder();

        void FlushPlain()
        {
            if (plain.Length > 0)
            {
                tokens.Add(new Tok(plain.ToString(), Plain));
                plain.Clear();
            }
        }

        while (i < n)
        {
            char c = code[i];

            // Line comments
            if (slashComments && c == '/' && i + 1 < n && code[i + 1] == '/')
            {
                FlushPlain();
                tokens.Add(new Tok(code[i..], Comment));
                break;
            }
            if (hashComments && c == '#')
            {
                FlushPlain();
                tokens.Add(new Tok(code[i..], Comment));
                break;
            }
            if (dashComments && c == '-' && i + 1 < n && code[i + 1] == '-')
            {
                FlushPlain();
                tokens.Add(new Tok(code[i..], Comment));
                break;
            }

            // Block comment (single-line span)
            if (slashComments && c == '/' && i + 1 < n && code[i + 1] == '*')
            {
                FlushPlain();
                int close = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                int end = close < 0 ? n : close + 2;
                tokens.Add(new Tok(code[i..end], Comment));
                i = end;
                continue;
            }

            // Strings
            if (c is '"' or '\'' or '`')
            {
                FlushPlain();
                int end = ReadString(code, i, c);
                tokens.Add(new Tok(code[i..end], Str));
                i = end;
                continue;
            }

            // Numbers
            if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(code[i + 1])))
            {
                FlushPlain();
                int start = i;
                if (c == '0' && i + 1 < n && (code[i + 1] is 'x' or 'X'))
                {
                    i += 2;
                    while (i < n && Uri.IsHexDigit(code[i])) i++;
                }
                else
                {
                    while (i < n && (char.IsDigit(code[i]) || code[i] is '.' or '_' or 'e' or 'E' or 'f' or 'F' or 'd' or 'D' or 'L' or 'l')) i++;
                }
                tokens.Add(new Tok(code[start..i], Number));
                continue;
            }

            // Identifiers / keywords
            if (char.IsLetter(c) || c == '_' || c == '$' || c == '@')
            {
                FlushPlain();
                int start = i;
                if (c == '@') i++; // C# verbatim/identifier prefix
                while (i < n && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or '$')) i++;
                string word = code[start..i];

                string cls;
                if (Keywords.Contains(word))
                    cls = Keyword;
                else if (NextNonSpaceIs(code, i, '('))
                    cls = Function;
                else if (word.Length > 0 && char.IsUpper(word[0]))
                    cls = Type;
                else
                    cls = Plain;

                tokens.Add(new Tok(word, cls));
                continue;
            }

            // Anything else (punctuation, whitespace) accumulates as plain.
            plain.Append(c);
            i++;
        }

        FlushPlain();
        return tokens;
    }

    private static int ReadString(string code, int start, char quote)
    {
        int i = start + 1;
        int n = code.Length;
        while (i < n)
        {
            if (code[i] == '\\' && i + 1 < n) { i += 2; continue; }
            if (code[i] == quote) return i + 1;
            i++;
        }
        return n; // unterminated on this line
    }

    private static bool NextNonSpaceIs(string code, int from, char target)
    {
        for (int i = from; i < code.Length; i++)
        {
            if (code[i] == ' ' || code[i] == '\t') continue;
            return code[i] == target;
        }
        return false;
    }

    private static bool IsSlashComment(string ext) => ext is
        "cs" or "js" or "jsx" or "ts" or "tsx" or "mjs" or "cjs" or "java" or "c" or "h" or "cpp" or
        "hpp" or "cc" or "cxx" or "go" or "rs" or "php" or "css" or "scss" or "less" or "kt" or "kts" or
        "swift" or "scala" or "dart" or "json" or "jsonc" or "proto" or "groovy" or "gradle";

    private static bool IsHashComment(string ext) => ext is
        "py" or "rb" or "sh" or "bash" or "zsh" or "yaml" or "yml" or "toml" or "pl" or "r" or
        "ps1" or "dockerfile" or "makefile" or "mk" or "ini" or "conf" or "tf";
}
