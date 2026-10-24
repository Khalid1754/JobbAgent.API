using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace JobbAgent.Api.Services;

public class DocumentException(string message) : AgentException(message);

// Läser ut texten ur ett uppladdat CV (PDF, Word eller vanlig text).
public static class CvReader
{
    public static string ReadText(Stream file, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            var text = extension switch
            {
                ".pdf" => ReadPdf(file),
                ".docx" => ReadDocx(file),
                ".txt" or ".md" => new StreamReader(file, Encoding.UTF8).ReadToEnd(),
                _ => throw new DocumentException("Ladda upp CV:t som PDF, Word (.docx) eller textfil."),
            };
            text = Text.Tidy(text);
            if (text.Length < 50)
                throw new DocumentException("Hittade nästan ingen text i filen. Är det en skannad bild? Klistra in texten i stället.");
            return text;
        }
        catch (Exception e) when (e is not DocumentException)
        {
            throw new DocumentException("Filen kunde inte läsas. Prova att spara om den eller klistra in texten.");
        }
    }

    private static string ReadPdf(Stream file)
    {
        using var buffer = new MemoryStream();
        file.CopyTo(buffer);
        using var pdf = PdfDocument.Open(buffer.ToArray());
        return string.Join("\n\n", pdf.GetPages().Select(p => ContentOrderTextExtractor.GetText(p)));
    }

    // En .docx är en zip-fil; texten ligger i word/document.xml, ett <w:p> per stycke.
    private static string ReadDocx(Stream file)
    {
        using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        var entry = zip.GetEntry("word/document.xml") ?? throw new DocumentException("Word-filen saknar text.");
        using var stream = entry.Open();
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = XDocument.Load(stream).Descendants(w + "p")
            .Select(p => string.Concat(p.Descendants().Select(n =>
                n.Name == w + "t" ? n.Value : n.Name == w + "tab" ? "\t" : n.Name == w + "br" ? "\n" : "")));
        return string.Join("\n", paragraphs);
    }
}

// Hämtar en annons från en webbadress och gör om sidan till ren text.
public partial class AdReader(HttpClient http)
{
    public const int MaxLength = 12000;

    // Platsbanken-länkar läses via API:t i stället, det ger hela annonsen utan menyer och reklam.
    public static string? PlatsbankenId(string url) => PlatsbankenUrl().Match(url) is { Success: true } m ? m.Groups[1].Value : null;

    public async Task<string> ReadAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new DocumentException("Länken måste börja med http:// eller https://.");
        string html;
        try
        {
            using var response = await http.GetAsync(uri, ct);
            if (!response.IsSuccessStatusCode)
                throw new DocumentException($"Sidan svarade {(int)response.StatusCode}. Klistra in annonstexten i stället.");
            html = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new DocumentException("Kunde inte öppna länken. Klistra in annonstexten i stället.");
        }
        var text = HtmlToText(html);
        if (text.Length < 200)
            throw new DocumentException("Sidan innehöll nästan ingen text (den kanske kräver inloggning). Klistra in annonstexten i stället.");
        return text.Length > MaxLength ? text[..MaxLength] : text;
    }

    public static string HtmlToText(string html)
    {
        var text = Hidden().Replace(html, " ");
        text = Block().Replace(text, "\n");
        text = Tag().Replace(text, " ");
        return Text.Tidy(WebUtility.HtmlDecode(text));
    }

    [GeneratedRegex(@"platsbanken/annonser/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PlatsbankenUrl();
    [GeneratedRegex(@"<(script|style|noscript|svg|nav|header|footer|form)\b.*?</\1\s*>|<!--.*?-->", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Hidden();
    [GeneratedRegex(@"<(br|/p|/div|/li|/h\d|/tr|li)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex Block();
    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();
}

public static partial class Text
{
    // Tar bort onödiga mellanslag och tomma rader.
    public static string Tidy(string text)
    {
        var lines = text.Replace("\r", "").Split('\n').Select(l => Spaces().Replace(l, " ").Trim());
        return ManyNewlines().Replace(string.Join("\n", lines), "\n\n").Trim();
    }

    public static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex Spaces();
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyNewlines();
}
