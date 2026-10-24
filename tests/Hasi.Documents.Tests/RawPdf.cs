using System.Text;

namespace Hasi.Documents.Tests;

/// <summary>
/// A one-page PDF written byte by byte, for what PdfPig's builder cannot make: a font with its own character table
/// (ToUnicode), a font without a correct one (garbled text), an encryption dictionary.
/// </summary>
internal static class RawPdf
{
    /// <param name="content">The page's content stream (PDF operators, ASCII; octal escapes for other codes).</param>
    /// <param name="font">The font dictionary, without the surrounding &lt;&lt; &gt;&gt;.</param>
    /// <param name="toUnicode">An optional ToUnicode CMap; the font gets "/ToUnicode 6 0 R".</param>
    /// <param name="encrypt">An optional encryption dictionary, without &lt;&lt; &gt;&gt;; the trailer points to it.</param>
    public static byte[] Build(string content, string font, string? toUnicode = null, string? encrypt = null)
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            Stream(content),
            "<< " + font + (toUnicode is null ? "" : " /ToUnicode 6 0 R") + " >>",
            Stream(toUnicode ?? ""),
            "<< " + (encrypt ?? "") + " >>",
        };

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R");
        if (encrypt is not null)
            pdf.Append(" /Encrypt 7 0 R /ID [<0123456789ABCDEF0123456789ABCDEF> <0123456789ABCDEF0123456789ABCDEF>]");
        pdf.Append($" >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(pdf.ToString());
    }

    private static string Stream(string data) => $"<< /Length {data.Length} >>\nstream\n{data}\nendstream";
}
