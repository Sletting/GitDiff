using Microsoft.AspNetCore.Components;

namespace PrDiffViewer.Client.Diff;

/// <summary>Inline SVG glyphs (16x16) approximating the Fluent/MDL2 icons ADO uses.</summary>
public static class Icons
{
    public static MarkupString Folder => new(
        """<svg viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M1.5 3.5C1.5 2.95 1.95 2.5 2.5 2.5H6.1L7.6 4H13.5C14.05 4 14.5 4.45 14.5 5V12C14.5 12.55 14.05 13 13.5 13H2.5C1.95 13 1.5 12.55 1.5 12V3.5Z" fill="#DCB67A"/></svg>""");

    public static MarkupString File => new(
        """<svg viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M4 1.5H9.5L13 5V14C13 14.28 12.78 14.5 12.5 14.5H4C3.72 14.5 3.5 14.28 3.5 14V2C3.5 1.72 3.72 1.5 4 1.5Z" fill="#FFFFFF" stroke="#A19F9D"/><path d="M9.5 1.5V5H13" stroke="#A19F9D" fill="none"/></svg>""");

    public static MarkupString Branch => new(
        """<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path d="M11.5 4.5a1.5 1.5 0 1 0-2.05 1.4C9.3 7.2 8.3 7.7 6.9 8c-.5.1-1 .25-1.4.5V5.9a1.5 1.5 0 1 0-1 0v4.2a1.5 1.5 0 1 0 1.06.03c.07-.5.5-.85 1.34-1.06 1.5-.36 3.2-.96 3.5-2.78A1.5 1.5 0 0 0 11.5 4.5ZM5 3.5a.5.5 0 1 1 0 1 .5.5 0 0 1 0-1Zm0 9a.5.5 0 1 1 0-1 .5.5 0 0 1 0 1Zm5-7a.5.5 0 1 1 0-1 .5.5 0 0 1 0 1Z"/></svg>""");

    public static MarkupString Compare => new(
        """<svg viewBox="0 0 16 16" fill="currentColor" xmlns="http://www.w3.org/2000/svg"><path d="M6 2 2.5 5.5 6 9V6.5h6V4.5H6V2Zm4 5v2.5H4v2H10V14l3.5-3.5L10 7Z"/></svg>""");
}
