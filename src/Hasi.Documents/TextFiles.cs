using System.Text;

using Hasi.Core.Documents;

namespace Hasi.Documents;

/// <summary>
/// Plain text: read by its byte order mark, else as strict UTF-8, else as Windows-1250 (the old Romanian code page);
/// always written as UTF-8 with a byte order mark and Windows line ends.
/// </summary>
internal static class TextFiles
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    private static readonly Lazy<Encoding> Windows1250 = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1250);
    });

    public static IReadOnlyList<DocumentParagraph> Read(byte[] bytes) =>
        SplitLines(Decode(bytes)).Select(line => new DocumentParagraph(line)).ToList();

    public static void Write(string path, IReadOnlyList<DocumentParagraph> paragraphs) =>
        File.WriteAllText(path, string.Join("\r\n", paragraphs.Select(p => p.Text)), Utf8WithBom);

    internal static string Decode(byte[] bytes)
    {
        ReadOnlySpan<byte> span = bytes;
        if (span.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF])) return Encoding.UTF8.GetString(span[3..]);
        if (span.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE])) return Encoding.Unicode.GetString(span[2..]);
        if (span.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF])) return Encoding.BigEndianUnicode.GetString(span[2..]);
        try
        {
            return StrictUtf8.GetString(span);
        }
        catch (DecoderFallbackException)
        {
            return Windows1250.Value.GetString(span);
        }
    }

    /// <summary>Splits on \r\n, \n or \r. An empty text has no lines; a final line end does not start a new line.</summary>
    internal static List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] is not ('\r' or '\n')) continue;
            lines.Add(text[start..i]);
            if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
            start = i + 1;
        }

        if (start < text.Length) lines.Add(text[start..]);
        return lines;
    }
}
