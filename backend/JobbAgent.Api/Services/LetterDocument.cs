using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using JobbAgent.Api.Models;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace JobbAgent.Api.Services;

// Allt som står i det nedladdade brevet: brevhuvud med dina kontaktuppgifter, datum, mottagare och själva texten.
public record LetterContent(string? Name, string ContactLine, string DateLine, string? Company, string Subject, string Body)
{
    public static LetterContent From(Run run, Profile? profile, DateTime today)
    {
        var english = run.Language == "en";
        var date = today.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo(english ? "en-GB" : "sv-SE"));
        var contact = string.Join("  ·  ", new[] { profile?.Email, profile?.Phone, profile?.City, profile?.Link }.Where(v => !string.IsNullOrWhiteSpace(v)));
        return new LetterContent(
            profile?.FullName, contact,
            string.IsNullOrWhiteSpace(profile?.City) ? date : $"{profile.City}, {date}",
            run.Company,
            (english ? "Application: " : "Ansökan: ") + run.Title,
            run.Letter ?? "");
    }

    // Brevets stycken. Tomma rader skiljer stycken åt; enkla radbrytningar (t.ex. i hälsningen) behålls.
    public IEnumerable<string[]> Paragraphs() =>
        Body.Replace("\r", "").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Split('\n', StringSplitOptions.TrimEntries));

    public string FileName(string extension)
    {
        var name = $"Personligt brev - {Company ?? Subject}";
        // Tecken som Windows inte tillåter i filnamn.
        foreach (var c in "\\/:*?\"<>|") name = name.Replace(c.ToString(), "");
        return $"{Text.Truncate(name, 80)}.{extension}";
    }
}

public static class LetterDocument
{
    // ----- Word (.docx) -----
    // En .docx är en zip med några XML-filer. Typsnittet Cambria finns i Word; Caladea i PDF:en har samma mått.

    public static byte[] Docx(LetterContent letter)
    {
        var body = new StringBuilder();
        if (letter.Name is not null) body.Append(Paragraph(Run(letter.Name, bold: true, size: 32), after: 40));
        if (letter.ContactLine.Length > 0) body.Append(Paragraph(Run(letter.ContactLine, size: 19, color: "595959"), after: 360, border: true));
        body.Append(Paragraph(Run(letter.DateLine), after: letter.Company is null ? 240 : 0));
        if (letter.Company is not null) body.Append(Paragraph(Run(letter.Company), after: 240));
        body.Append(Paragraph(Run(letter.Subject, bold: true, size: 24), after: 240));
        foreach (var lines in letter.Paragraphs())
            body.Append(Paragraph(string.Join("<w:r><w:br/></w:r>", lines.Select(l => Run(l))), after: 200));

        var document = $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>{body}<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1418" w:right="1418" w:bottom="1418" w:left="1418" w:header="709" w:footer="709" w:gutter="0"/></w:sectPr></w:body></w:document>
            """;
        const string styles = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Cambria" w:hAnsi="Cambria" w:cs="Cambria"/><w:sz w:val="22"/><w:lang w:val="sv-SE"/></w:rPr></w:rPrDefault><w:pPrDefault><w:pPr><w:spacing w:after="0" w:line="288" w:lineRule="auto"/></w:pPr></w:pPrDefault></w:docDefaults></w:styles>
            """;
        const string contentTypes = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/></Types>
            """;
        const string rels = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>
            """;
        const string documentRels = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>
            """;

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", contentTypes);
            Add(zip, "_rels/.rels", rels);
            Add(zip, "word/_rels/document.xml.rels", documentRels);
            Add(zip, "word/styles.xml", styles);
            Add(zip, "word/document.xml", document);
        }
        return output.ToArray();
    }

    private static string Run(string text, bool bold = false, int? size = null, string? color = null)
    {
        var props = (bold ? "<w:b/>" : "") + (color is null ? "" : $"<w:color w:val=\"{color}\"/>") + (size is null ? "" : $"<w:sz w:val=\"{size}\"/>");
        return $"<w:r>{(props.Length > 0 ? $"<w:rPr>{props}</w:rPr>" : "")}<w:t xml:space=\"preserve\">{SecurityElement.Escape(text)}</w:t></w:r>";
    }

    private static string Paragraph(string runs, int after, bool border = false) =>
        $"<w:p><w:pPr>{(border ? "<w:pBdr><w:bottom w:val=\"single\" w:sz=\"6\" w:space=\"8\" w:color=\"BFBFBF\"/></w:pBdr>" : "")}<w:spacing w:after=\"{after}\"/></w:pPr>{runs}</w:p>";

    private static void Add(ZipArchive zip, string path, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
        writer.Write(content.Trim());
    }

    // ----- PDF -----

    private static readonly Lazy<byte[]> Regular = new(() => Font("Caladea-Regular.ttf"));
    private static readonly Lazy<byte[]> Bold = new(() => Font("Caladea-Bold.ttf"));

    private static byte[] Font(string name)
    {
        using var stream = typeof(LetterDocument).Assembly.GetManifestResourceStream($"JobbAgent.Api.Resources.Fonts.{name}")
            ?? throw new InvalidOperationException($"Typsnittet {name} saknas.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private const double Margin = 70, PageWidth = 595, PageHeight = 842, TextWidth = PageWidth - 2 * Margin;

    public static byte[] Pdf(LetterContent letter)
    {
        var builder = new PdfDocumentBuilder();
        var regular = builder.AddTrueTypeFont(Regular.Value);
        var bold = builder.AddTrueTypeFont(Bold.Value);
        var page = builder.AddPage(PageSize.A4);
        var y = PageHeight - Margin;

        void Line(string text, PdfDocumentBuilder.AddedFont font, double size, double after, (byte, byte, byte)? color = null)
        {
            if (y < Margin + size)
            {
                page = builder.AddPage(PageSize.A4);
                y = PageHeight - Margin;
            }
            if (color is { } c) page.SetTextAndFillColor(c.Item1, c.Item2, c.Item3);
            page.AddText(text, size, new PdfPoint(Margin, y - size), font);
            if (color is not null) page.ResetColor();
            y -= size * 1.4 + after;
        }

        void Wrapped(string text, PdfDocumentBuilder.AddedFont font, double size, double after, (byte, byte, byte)? color = null)
        {
            var lines = Wrap(text, w => Width(page, w, font, size)).ToList();
            for (var i = 0; i < lines.Count; i++) Line(lines[i], font, size, i == lines.Count - 1 ? after : 0, color);
        }

        if (letter.Name is not null) Wrapped(letter.Name, bold, 18, 2);
        if (letter.ContactLine.Length > 0)
        {
            Wrapped(letter.ContactLine, regular, 9.5, 4, (89, 89, 89));
            page.SetStrokeColor(191, 191, 191);
            page.DrawLine(new PdfPoint(Margin, y), new PdfPoint(PageWidth - Margin, y), 0.6);
            page.ResetColor();
            y -= 22;
        }
        Wrapped(letter.DateLine, regular, 11, letter.Company is null ? 14 : 0);
        if (letter.Company is not null) Wrapped(letter.Company, regular, 11, 14);
        Wrapped(letter.Subject, bold, 12, 12);
        foreach (var lines in letter.Paragraphs())
            for (var i = 0; i < lines.Length; i++)
                Wrapped(lines[i], regular, 11, i == lines.Length - 1 ? 9 : 0);

        return builder.Build();
    }

    private static double Width(PdfPageBuilder page, string text, PdfDocumentBuilder.AddedFont font, double size) =>
        text.Length == 0 ? 0 : page.MeasureText(text, size, new PdfPoint(0, 0), font).Last().EndBaseLine.X;

    // Radbryter texten så att varje rad får plats på sidan.
    internal static IEnumerable<string> Wrap(string text, Func<string, double> width)
    {
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && width(candidate) > TextWidth)
            {
                yield return line;
                line = word;
            }
            else
            {
                line = candidate;
            }
        }
        yield return line;
    }
}
