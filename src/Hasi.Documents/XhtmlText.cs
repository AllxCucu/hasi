using System.Text;

using HtmlAgilityPack;

namespace Hasi.Documents;

/// <summary>
/// The text of one (X)HTML content document of an EPUB, as paragraphs. All HtmlAgilityPack code of HASI lives here.
/// Every block element without block children (p, li, td, a div with only text, ...) becomes one paragraph; text
/// between blocks becomes a paragraph of its own. h1–h6 give the heading level. White space is collapsed; scripts,
/// styles and SVG drawings are skipped; images are ignored, with their alt text (often a file name or "image").
/// Nothing is loaded from outside the text given.
/// </summary>
internal static class XhtmlText
{
    /// <param name="Text">The text, white space collapsed, never empty.</param>
    /// <param name="HeadingLevel">0 for a normal paragraph, 1–6 for a heading.</param>
    internal sealed record Block(string Text, int HeadingLevel);

    private static readonly HashSet<string> BlockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "aside", "blockquote", "body", "caption", "center", "dd", "details", "dialog", "dir",
        "div", "dl", "dt", "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6",
        "header", "hgroup", "hr", "html", "legend", "li", "main", "menu", "nav", "ol", "p", "pre", "section",
        "summary", "table", "tbody", "td", "tfoot", "th", "thead", "tr", "ul",
    };

    private static readonly HashSet<string> SkippedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "head", "script", "style", "noscript", "template", "svg", "object", "iframe", "rp",
    };

    public static List<Block> Read(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        var root = document.DocumentNode.Descendants("body").FirstOrDefault() ?? document.DocumentNode;
        var blocks = new List<Block>();
        ReadContainer(root, 0, blocks);
        return blocks;
    }

    /// <summary>Collapses runs of white space into one space, drops soft hyphens and zero-width characters, trims.</summary>
    internal static string Collapse(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '­' or '​' or '‌' or '‍' or '﻿') continue;
            if (char.IsWhiteSpace(c))
            {
                if (result.Length > 0 && result[^1] != ' ') result.Append(' ');
                continue;
            }

            result.Append(c);
        }

        return result.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    private static void ReadContainer(HtmlNode container, int headingLevel, List<Block> blocks)
    {
        var inline = new StringBuilder();
        foreach (var child in container.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Text)
            {
                inline.Append(TextOf((HtmlTextNode)child));
                continue;
            }

            if (child.NodeType != HtmlNodeType.Element || SkippedElements.Contains(child.Name)) continue;

            if (BlockElements.Contains(child.Name) || HasBlocks(child))
            {
                Flush(inline, headingLevel, blocks);
                if (child.Name.Equals("pre", StringComparison.OrdinalIgnoreCase)) ReadPreformatted(child, headingLevel, blocks);
                else ReadContainer(child, HeadingLevelOf(child) ?? headingLevel, blocks);
            }
            else
            {
                AppendElement(child, inline, preformatted: false);
            }
        }

        Flush(inline, headingLevel, blocks);
    }

    /// <summary>Preformatted text (a poem, a listing): each non-empty line becomes a paragraph.</summary>
    private static void ReadPreformatted(HtmlNode pre, int headingLevel, List<Block> blocks)
    {
        var text = new StringBuilder();
        AppendInline(pre, text, preformatted: true);
        foreach (var line in text.ToString().Split('\n')) Flush(new StringBuilder(line), headingLevel, blocks);
    }

    /// <summary>The text inside <paramref name="node"/>; a line break is a space (a new line in preformatted text).</summary>
    private static void AppendInline(HtmlNode node, StringBuilder text, bool preformatted)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Text) text.Append(TextOf((HtmlTextNode)child));
            else if (child.NodeType == HtmlNodeType.Element && !SkippedElements.Contains(child.Name)) AppendElement(child, text, preformatted);
        }
    }

    private static void AppendElement(HtmlNode element, StringBuilder text, bool preformatted)
    {
        if (element.Name.Equals("br", StringComparison.OrdinalIgnoreCase)) text.Append(preformatted ? '\n' : ' ');
        else AppendInline(element, text, preformatted);
    }

    private static string TextOf(HtmlTextNode node) => HtmlEntity.DeEntitize(node.Text) ?? "";

    private static void Flush(StringBuilder inline, int headingLevel, List<Block> blocks)
    {
        var text = Collapse(inline.ToString());
        inline.Clear();
        if (text.Length > 0) blocks.Add(new Block(text, headingLevel));
    }

    private static bool HasBlocks(HtmlNode node) =>
        node.Descendants().Any(d => d.NodeType == HtmlNodeType.Element && BlockElements.Contains(d.Name));

    private static int? HeadingLevelOf(HtmlNode element) =>
        element.Name.Length == 2 && char.ToLowerInvariant(element.Name[0]) == 'h' && element.Name[1] is >= '1' and <= '6'
            ? element.Name[1] - '0'
            : null;
}
